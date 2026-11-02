using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace Meridian.Server;

public sealed record ServerOptions
{
    public int Port { get; init; } = 5959;
    public TimeSpan MenuTimeout { get; init; } = TimeSpan.FromMinutes(10);
    public int MaxLoginAttempts { get; init; } = 3;
    /// <summary>Im Spiel: Trennung, wenn so lange keine Nachricht kam (Client pingt alle 5 s).</summary>
    public TimeSpan GameTimeout { get; init; } = TimeSpan.FromSeconds(30);
    /// <summary>Weltbeschreibung; ohne Angabe ein einzelner Raum.</summary>
    public WorldDefinition World { get; init; } = WorldDefinition.SingleRoom();
    /// <summary>Fester Zufallsstartwert (Tests); null = zufällig.</summary>
    public int? RandomSeed { get; init; }
    /// <summary>Dialogtexte der NPCs.</summary>
    public DialogFile Dialogs { get; init; } = DialogFile.Empty;
    /// <summary>Höhenraster der Räume für Monster-Wegfindung (Standard: data/rooms neben dem Server).</summary>
    public string? RoomLayoutDirectory { get; init; } = Path.Combine(AppContext.BaseDirectory, "data", "rooms");
    /// <summary>Feste Spielstunde statt der Uhr (Tests, Entwicklung); null = Originaluhr.</summary>
    public int? FixedHour { get; init; }
    /// <summary>Gegenstandskatalog und Beutetabellen.</summary>
    public ItemCatalog Items { get; init; } = ItemCatalog.Empty;
    /// <summary>Händler (Läden, Ankauf, Bank) je NPC-Klasse.</summary>
    public ShopFile Shops { get; init; } = ShopFile.Empty;
    /// <summary>SQLite-Datenbank für Spielstände; null = keine Persistenz.</summary>
    public string? Database { get; init; }
    /// <summary>Automatisches Speichern (Original SavePeriod 180 min; hier kürzer, weil es nur geänderte Charaktere schreibt).</summary>
    public TimeSpan SavePeriod { get; init; } = TimeSpan.FromMinutes(5);
    /// <summary>Grafik (.bgf des Clients) für Spieler.</summary>
    public string PlayerIcon { get; init; } = "bta.bgf";   // KOD player_male_icon_rsc
    /// <summary>Optional: Ressourcen-Ordner eines Original-Clients, um die Raum-Security aus den .roo-Dateien zu lesen.</summary>
    public string? ClientResourceDir { get; init; }
}

/// <summary>Nimmt TCP-Verbindungen an und startet pro Verbindung eine <see cref="Session"/>.</summary>
public sealed class GameServer
{
    private readonly ServerOptions options;
    private readonly AccountStore accounts;
    private readonly ConcurrentDictionary<int, Session> _sessions = new();
    private readonly ConcurrentDictionary<string, Session> _byAccount = new(StringComparer.OrdinalIgnoreCase);
    private TcpListener? _listener;
    private int _nextSessionId;

    public GameServer(ServerOptions options, AccountStore accounts)
    {
        this.options = options;
        this.accounts = accounts;
        var world = options.World;
        if (options.ClientResourceDir is { } dir)
            world = world with { Rooms = world.Rooms.Select(r => WithSecurityFromClient(r, dir)).ToList() };
        World = new World(world, Resources, options.PlayerIcon, options.Dialogs, options.RandomSeed, options.Items, options.Shops)
        {
            FixedHour = options.FixedHour,
        };
        if (options.RoomLayoutDirectory is { } layouts)
            World.LoadGrids(layouts);
        if (options.Database is { } database)
        {
            Store = new GameStore(database);
            var characters = Store.LoadAll();
            World.LoadCharacters(characters);
            Log.Info($"Datenbank {Store.Path}: {characters.Count} Charaktere geladen");
        }
    }

    /// <summary>Spielstand-Speicher, falls Persistenz aktiv ist.</summary>
    public GameStore? Store { get; }

    /// <summary>Speichert den Spielstand (Admin „save game“, Timer, Logoff, Serverende).</summary>
    public void SaveGame(string reason)
    {
        if (Store is null)
            return;
        var characters = World.TakeCharactersToSave();
        try
        {
            Store.Save(characters, DateTime.UtcNow);
            if (characters.Count > 0)
                Log.Info($"Spielstand gespeichert ({reason}): {characters.Count} Charakter(e)");
        }
        catch (Exception ex) when (ex is Microsoft.Data.Sqlite.SqliteException or IOException or UnauthorizedAccessException)
        {
            World.SaveFailed(characters.Keys);
            Log.Warn($"Spielstand konnte nicht gespeichert werden ({reason}): {ex.Message}");
        }
    }

    private static RoomDefinition WithSecurityFromClient(RoomDefinition room, string dir)
    {
        string path = Path.Combine(dir, room.Roo);
        if (!File.Exists(path))
        {
            Log.Warn($"Raum {room.Id}: {path} fehlt – Security bleibt {room.Security}");
            return room;
        }
        return room with { Security = RooFile.ReadSecurity(path) };
    }

    public ServerOptions Options => options;
    public AccountStore Accounts => accounts;
    public ResourceRegistry Resources { get; } = new();
    public World World { get; }

    /// <summary>Tatsächlicher Port (relevant bei Port 0 in Tests).</summary>
    public int Port => ((IPEndPoint)_listener!.LocalEndpoint).Port;

    public void Start()
    {
        _listener = new TcpListener(IPAddress.Any, options.Port);
        _listener.Start();
        Log.Info($"Server lauscht auf Port {Port}");
    }

    public async Task RunAsync(CancellationToken ct)
    {
        if (_listener is null)
            Start();
        _ = Task.Run(() => TickLoopAsync(ct), CancellationToken.None);
        _ = Task.Run(() => SaveLoopAsync(ct), CancellationToken.None);
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var client = await _listener!.AcceptTcpClientAsync(ct);
                var session = new Session(Interlocked.Increment(ref _nextSessionId), client, this);
                _sessions[session.Id] = session;
                _ = Task.Run(async () =>
                {
                    try { await session.RunAsync(ct); }
                    finally { Remove(session); }
                }, CancellationToken.None);
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            _listener!.Stop();
            foreach (var s in _sessions.Values)
                s.Close();
            SaveGame("Serverende");
        }
    }

    /// <summary>Automatisches Speichern (blakserv SYST_SAVE).</summary>
    private async Task SaveLoopAsync(CancellationToken ct)
    {
        if (Store is null || options.SavePeriod <= TimeSpan.Zero)
            return;
        using var timer = new PeriodicTimer(options.SavePeriod);
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
                SaveGame("automatisch");
        }
        catch (OperationCanceledException) { }
    }

    /// <summary>Spieltakt der Welt, 4×/s.</summary>
    private async Task TickLoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(250));
        var last = DateTime.UtcNow;
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                var now = DateTime.UtcNow;
                try { World.Tick(now, (now - last).TotalSeconds); }
                catch (Exception ex) { Log.Warn($"Fehler im Spieltakt: {ex}"); }
                last = now;
            }
        }
        catch (OperationCanceledException) { }
    }

    /// <summary>Meldet einen Account als online. Eine ältere Session desselben Accounts wird getrennt
    /// (entspricht [Inactive] Override=Yes im Original).</summary>
    internal void ClaimAccount(string name, Session session)
    {
        if (_byAccount.TryGetValue(name, out var old) && old != session)
        {
            Log.Info($"Session {old.Id}: Account {name} meldet sich erneut an, alte Session wird getrennt");
            old.Close();
        }
        _byAccount[name] = session;
    }

    private void Remove(Session session)
    {
        _sessions.TryRemove(session.Id, out _);
        if (session.AccountName is { } name)
            _byAccount.TryRemove(new KeyValuePair<string, Session>(name, session));
    }
}
