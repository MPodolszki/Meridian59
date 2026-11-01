using System.Collections.Concurrent;
using System.Net.Sockets;

namespace Meridian.Protocol;

/// <summary>Ein Objekt im aktuellen Raum aus Client-Sicht. Koordinaten in KOD-Feinkoordinaten.</summary>
public sealed class ClientObject(uint id)
{
    public uint Id => id;
    public uint NameRsc { get; set; }
    public uint IconRsc { get; set; }
    public ushort Row { get; set; }
    public ushort Col { get; set; }
    public ushort Angle { get; set; }
}

/// <summary>Ereignisse für die Oberfläche. Werden im Thread von <see cref="ClientSession.ProcessEvents"/> ausgelöst.</summary>
public abstract record ClientEvent
{
    public sealed record Status(string Text) : ClientEvent;
    public sealed record LoginFailed(string Reason) : ClientEvent;
    /// <summary>Raum betreten; RoomFile ist der Dateiname aus BP_PLAYER (z. B. "tos.roo").</summary>
    public sealed record EnteredRoom(uint PlayerId, string RoomName, string RoomFile = "") : ClientEvent;
    public sealed record ObjectAdded(ClientObject Object) : ClientEvent;
    public sealed record ObjectMoved(ClientObject Object) : ClientEvent;
    public sealed record ObjectRemoved(uint Id) : ClientEvent;
    public sealed record Chat(string Text) : ClientEvent;
    public sealed record Disconnected(string Reason) : ClientEvent;
}

/// <summary>
/// Hochsprachlicher Client für das Meridian-Protokoll ohne Abhängigkeit von Godot:
/// führt den Login-Ablauf wie der Original-Client durch, wählt den ersten Charakter,
/// hält den Raum-Zustand und pingt alle 5 s. Netzwerk läuft im Hintergrund; Ereignisse
/// werden gesammelt und erst in <see cref="ProcessEvents"/> (z. B. aus Godots _Process) ausgeliefert.
/// </summary>
public sealed class ClientSession : IDisposable
{
    public static readonly TimeSpan PingInterval = TimeSpan.FromSeconds(5);

    private readonly ConcurrentQueue<ClientEvent> _events = new();
    private readonly Dictionary<uint, string> _resources = new();
    private readonly Dictionary<uint, ClientObject> _objects = new();
    private readonly CancellationTokenSource _cts = new();
    private TcpClient? _tcp;
    private ClientConnection? _conn;
    private string _username = "";
    private string _password = "";
    private bool _charactersRequested;

    public uint PlayerId { get; private set; }
    public uint RoomId { get; private set; }
    public bool InWorld => PlayerId != 0;

    public event Action<ClientEvent>? Event;

    /// <summary>Text einer Ressource (per BP_CHANGE_RESOURCE gelernt), sonst "#id".</summary>
    public string Resolve(uint rsc)
    {
        lock (_resources)
            return _resources.TryGetValue(rsc, out var s) ? s : $"#{rsc}";
    }

    public async Task ConnectAsync(string host, int port, string username, string password)
    {
        _username = username;
        _password = password;
        _tcp = new TcpClient { NoDelay = true };
        Enqueue(new ClientEvent.Status($"Verbinde mit {host}:{port} …"));
        await _tcp.ConnectAsync(host, port, _cts.Token);
        _conn = new ClientConnection(_tcp.GetStream());
        _ = Task.Run(ReceiveLoopAsync);
        _ = Task.Run(PingLoopAsync);
    }

    /// <summary>Liefert gesammelte Ereignisse im aufrufenden Thread aus.</summary>
    public void ProcessEvents()
    {
        while (_events.TryDequeue(out var e))
            Event?.Invoke(e);
    }

    public Task SayAsync(string text) =>
        SendGameAsync(new PacketWriter((byte)GameMessage.SayTo).U8((byte)SayType.Normal).Str(text).ToArray());

    public Task MoveAsync(ushort row, ushort col, bool running) =>
        SendGameAsync(new PacketWriter((byte)GameMessage.ReqMove)
            .U16(row).U16(col).U8(running ? (byte)36 : (byte)18).U32(RoomId).ToArray());

    public Task TurnAsync(ushort angle) =>
        SendGameAsync(new PacketWriter((byte)GameMessage.ReqTurn).U32(PlayerId).U16(angle).ToArray());

    /// <summary>„Gehen/Öffnen“ (Leertaste im Original): Ausgang oder Tür auf dem aktuellen Feld.</summary>
    public Task GoAsync() => SendGameAsync([(byte)GameMessage.ReqGo]);

    public Task QuitAsync() => SendGameAsync([(byte)GameMessage.ReqQuit]);

    private async Task SendGameAsync(byte[] payload)
    {
        if (_conn is { GameMode: true } && InWorld)
            await _conn.SendAsync(payload, _cts.Token);
    }

    private async Task ReceiveLoopAsync()
    {
        string reason = "Verbindung geschlossen";
        try
        {
            while (await _conn!.ReceiveAsync(_cts.Token) is { } frame)
            {
                if (_conn.GameMode || frame.Type == (byte)GameMessage.Quit)
                    await HandleGameAsync(frame);
                else
                    await HandleLoginAsync(frame);
            }
        }
        catch (OperationCanceledException) { reason = "Getrennt"; }
        catch (Exception ex) when (ex is IOException or ProtocolException or SocketException or ObjectDisposedException)
        {
            reason = ex.Message;
        }
        PlayerId = 0;
        Enqueue(new ClientEvent.Disconnected(reason));
    }

    private async Task PingLoopAsync()
    {
        using var timer = new PeriodicTimer(PingInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(_cts.Token))
                if (_conn is { GameMode: true })
                    await _conn.SendAsync([(byte)GameMessage.Ping], _cts.Token);
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException or ObjectDisposedException) { }
    }

    private async Task HandleLoginAsync(Frame frame)
    {
        var r = new PacketReader(frame.Payload.AsSpan(1));
        switch ((LoginMessage)frame.Type)
        {
            case LoginMessage.GetLogin:
                await _conn!.SendAsync(new LoginRequest(7, 37, 0, _username, PasswordDigest.Compute(_password)).ToPayload());
                break;
            case LoginMessage.LoginOk:
                Enqueue(new ClientEvent.Status("Angemeldet"));
                break;
            case LoginMessage.LoginFailed:
                Enqueue(new ClientEvent.LoginFailed("Name oder Passwort falsch"));
                break;
            case LoginMessage.TooManyLogins:
                Enqueue(new ClientEvent.LoginFailed("Zu viele Fehlversuche"));
                break;
            case LoginMessage.AccountUsed:
                Enqueue(new ClientEvent.LoginFailed("Account wird bereits benutzt"));
                break;
            case LoginMessage.Message:
                string text = r.Str();
                var action = (LoginAction)r.U8();
                if (action == LoginAction.Logoff)
                    Enqueue(new ClientEvent.LoginFailed(text));
                else
                    Enqueue(new ClientEvent.Status(text));
                break;
            case LoginMessage.GetChoice:
                // Der Original-Client geht nach dem Menü sofort ins Spiel (login.c EnterGame).
                _charactersRequested = false;
                await _conn!.SendAsync(new PacketWriter(LoginMessage.ReqGame).U32(0).U32(737 * 3 + 3).Str(Environment.MachineName).ToArray());
                break;
            case LoginMessage.Game:
                Enqueue(new ClientEvent.Status("Betrete das Spiel …"));
                break;
            case LoginMessage.NoCharacters:
                Enqueue(new ClientEvent.LoginFailed("Dieser Account hat keine Charaktere"));
                break;
        }
    }

    private async Task HandleGameAsync(Frame frame)
    {
        var r = new PacketReader(frame.Payload.AsSpan(1));
        switch ((GameMessage)frame.Type)
        {
            case GameMessage.ChangeResource:
            {
                uint id = r.U32();
                string value = r.Str();
                lock (_resources)
                    _resources[id] = value;
                break;
            }
            case GameMessage.LoadModule:
                // Erstes Modul nach AP_GAME ist das Charakter-Modul; es fordert die Liste an (char.c).
                if (!InWorld && !_charactersRequested)
                {
                    _charactersRequested = true;
                    await _conn!.SendAsync([(byte)GameMessage.SendCharacters]);
                }
                break;
            case GameMessage.Characters:
            {
                if (r.U16() == 0)
                {
                    Enqueue(new ClientEvent.LoginFailed("Keine Charaktere vorhanden"));
                    break;
                }
                uint id = r.U32();
                Enqueue(new ClientEvent.Status($"Wähle Charakter {r.Str()}"));
                await _conn!.SendAsync(new PacketWriter((byte)GameMessage.UseCharacter).U32(id).ToArray());
                break;
            }
            case GameMessage.Player:
            {
                PlayerId = r.U32();
                r.U32(); r.U32();               // Icon, Name
                RoomId = r.U32();
                uint roomFile = r.U32();
                uint roomName = r.U32();
                ClearRoom();
                Enqueue(new ClientEvent.EnteredRoom(PlayerId, Resolve(roomName), Resolve(roomFile)));
                break;
            }
            case GameMessage.RoomContents:
            {
                r.U32();
                int n = r.U16();
                for (int i = 0; i < n; i++)
                    AddObject(ReadRoomObject(ref r));
                break;
            }
            case GameMessage.Create:
                AddObject(ReadRoomObject(ref r));
                break;
            case GameMessage.Remove:
            {
                uint id = r.U32() & 0x0FFFFFFF;
                if (_objects.Remove(id))
                    Enqueue(new ClientEvent.ObjectRemoved(id));
                break;
            }
            case GameMessage.Move:
            {
                uint id = r.U32() & 0x0FFFFFFF;
                if (_objects.TryGetValue(id, out var o))
                {
                    o.Row = r.U16();
                    o.Col = r.U16();
                    Enqueue(new ClientEvent.ObjectMoved(o));
                }
                break;
            }
            case GameMessage.Turn:
            {
                uint id = r.U32() & 0x0FFFFFFF;
                if (_objects.TryGetValue(id, out var o))
                {
                    o.Angle = r.U16();
                    Enqueue(new ClientEvent.ObjectMoved(o));
                }
                break;
            }
            case GameMessage.Said:
            {
                r.U32(); r.U32(); r.U8();
                Enqueue(new ClientEvent.Chat(FormatMessage(ref r)));
                break;
            }
            case GameMessage.Message:
            case GameMessage.SysMessage:
                Enqueue(new ClientEvent.Chat(FormatMessage(ref r)));
                break;
            case GameMessage.Quit:
                ClearRoom();
                PlayerId = 0;
                Enqueue(new ClientEvent.Status("Zurück im Menü"));
                break;
        }
    }

    /// <summary>Format-Ressource mit Parametern (protocol.md 4.8); unterstützt %q, %d/%i, %s (ohne Rekursion).</summary>
    private string FormatMessage(ref PacketReader r)
    {
        string fmt = Resolve(r.U32());
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < fmt.Length; i++)
        {
            if (fmt[i] != '%' || i + 1 >= fmt.Length || r.Remaining == 0 && fmt[i + 1] != '%')
            {
                sb.Append(fmt[i]);
                continue;
            }
            char spec = fmt[++i];
            switch (spec)
            {
                case 'q': sb.Append(r.Str()); break;
                case 'd': case 'i': sb.Append((int)r.U32()); break;
                case 's': sb.Append(Resolve(r.U32())); break;
                case '%': sb.Append('%'); break;
                default: sb.Append('%').Append(spec); break;
            }
        }
        return sb.ToString();
    }

    private static ClientObject ReadRoomObject(ref PacketReader r)
    {
        uint rawId = r.U32();
        var o = new ClientObject(rawId & 0x0FFFFFFF);
        if (rawId >> 28 == 1)
            r.U32();                            // amount
        o.IconRsc = r.U32();
        o.NameRsc = r.U32();
        r.U32(); r.U32();                       // flags, rarity
        if (r.U16() != 0) { r.U8(); r.U16(); }  // dlight
        SkipAnimation(ref r);
        SkipOverlays(ref r);
        o.Row = r.U16();
        o.Col = r.U16();
        o.Angle = r.U16();
        SkipAnimation(ref r);
        SkipOverlays(ref r);
        return o;
    }

    private static void SkipOverlays(ref PacketReader r)
    {
        int n = r.U8();
        for (int i = 0; i < n; i++)
        {
            r.U32(); r.U8();                    // Icon, Hotspot
            SkipAnimation(ref r);
        }
    }

    /// <summary>Optionales Präfix (9 = Palette, 10 = Effekt) plus Animation (protocol.md 4.2).</summary>
    private static void SkipAnimation(ref PacketReader r)
    {
        byte type = r.U8();
        if (type is 9 or 10)
        {
            r.U8();
            type = r.U8();
        }
        switch ((AnimationType)type)
        {
            case AnimationType.None: r.U16(); break;
            case AnimationType.Cycle: r.U32(); r.U16(); r.U16(); break;
            case AnimationType.Once: r.U32(); r.U16(); r.U16(); r.U16(); break;
            default: throw new ProtocolException($"Unbekannter Animationstyp {type}");
        }
    }

    private void AddObject(ClientObject o)
    {
        _objects[o.Id] = o;
        Enqueue(new ClientEvent.ObjectAdded(o));
    }

    private void ClearRoom()
    {
        foreach (uint id in _objects.Keys)
            Enqueue(new ClientEvent.ObjectRemoved(id));
        _objects.Clear();
    }

    private void Enqueue(ClientEvent e) => _events.Enqueue(e);

    public void Dispose()
    {
        _cts.Cancel();
        _tcp?.Dispose();
    }
}
