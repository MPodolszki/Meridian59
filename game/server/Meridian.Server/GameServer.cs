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
    public RoomOptions Room { get; init; } = new();
    /// <summary>Optional: Ressourcen-Ordner eines Original-Clients, um z. B. die Raum-Security aus der .roo zu lesen.</summary>
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
        var room = options.Room;
        if (options.ClientResourceDir is { } dir)
        {
            uint security = RooFile.ReadSecurity(Path.Combine(dir, room.RooFile));
            Log.Info($"Raum {room.RooFile}: Security {security:X8} aus der Client-Datei gelesen");
            room = room with { Security = security };
        }
        World = new World(room, Resources);
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
        }
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
