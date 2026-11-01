using System.Net.Sockets;
using System.Threading.Channels;
using Meridian.Protocol;

namespace Meridian.Server;

/// <summary>
/// Eine Client-Verbindung (protocol.md 1.2/3.1).
/// Login-Modus: AP_GETLOGIN → AP_LOGIN → AP_LOGINOK, AP_GETCHOICE, AP_CREDITS → AP_REQ_GAME → AP_GAME.
/// Game-Modus: Charakterwahl, Raum betreten, Ping, Bewegung, Chat, BP_REQ_QUIT zurück ins Menü.
/// Alle ausgehenden Nachrichten laufen in Reihenfolge durch eine Queue, weil der Secure-Token
/// pro gesendeter Nachricht weiterwandert.
/// </summary>
public sealed class Session(int id, TcpClient client, GameServer server)
{
    public const string CharModule = "char.dll";
    public const string InterfaceModule = "merintr.dll";
    public const string Motd = "Willkommen auf dem neuen Meridian-Server!";

    /// <summary>Epoche im Game-Modus. Der Server hat noch keine Garbage Collection, sie bleibt konstant.</summary>
    public const byte Epoch = 1;

    private enum Mode { Synched, Game }

    private readonly NetworkStream _stream = client.GetStream();
    private readonly Channel<Outgoing> _outbox = Channel.CreateUnbounded<Outgoing>(new() { SingleReader = true });
    private readonly HashSet<string> _sentResources = new(StringComparer.Ordinal);
    private readonly GameCodec _codec = new();
    private volatile Mode _mode = Mode.Synched;
    private Account? _account;
    private int _failedLogins;
    private Avatar? _avatar;

    /// <summary>Ausgehende Nachricht. Epoch: 0 im Login-Modus, <see cref="Epoch"/> im Game-Modus.</summary>
    private sealed record Outgoing(byte[] Payload, byte Epoch, byte? NewToken = null);

    public int Id => id;
    public string? AccountName => _account?.Name;

    public async Task RunAsync(CancellationToken serverCt)
    {
        Log.Info($"Session {id}: Verbindung von {client.Client.RemoteEndPoint}");
        using var idle = CancellationTokenSource.CreateLinkedTokenSource(serverCt);
        idle.CancelAfter(server.Options.MenuTimeout);
        var writer = Task.Run(() => WriteLoopAsync(serverCt), CancellationToken.None);
        var reader = new FrameReader(_stream, Framing.MaxClientPayload);

        try
        {
            PostLogin(LoginReplies.GetLogin());
            while (await reader.ReadAsync(idle.Token) is { } frame)
            {
                idle.CancelAfter(_mode == Mode.Game ? server.Options.GameTimeout : server.Options.MenuTimeout);
                if (frame.Header.Length == 0)
                    continue;   // Original ignoriert leere Nachrichten
                bool keepGoing = _mode == Mode.Game ? HandleGameFrame(frame) : HandleSynchedFrame(frame);
                if (!keepGoing)
                    break;
            }
        }
        catch (OperationCanceledException) when (!serverCt.IsCancellationRequested)
        {
            Log.Info($"Session {id}: Timeout");
            if (_mode == Mode.Synched)
                PostLogin(LoginReplies.Timeout());
        }
        catch (OperationCanceledException) { }
        catch (ProtocolException ex)
        {
            // Resync (Beacon-Handshake) ist noch nicht implementiert → Verbindung trennen.
            Log.Warn($"Session {id}: Protokollfehler: {ex.Message}");
        }
        catch (IOException) { }
        catch (ObjectDisposedException) { }
        finally
        {
            LeaveWorld();
            _outbox.Writer.TryComplete();
            await writer;
            Close();
            Log.Info($"Session {id}: getrennt");
        }
    }

    // ---------------------------------------------------------------- Login-Modus

    private bool HandleSynchedFrame(Frame frame)
    {
        if (frame.Header.Epoch != 0)
            throw new ProtocolException($"Epoch {frame.Header.Epoch} im Login-Modus");

        var type = (LoginMessage)frame.Type;

        // Vor erfolgreichem Login wird alles außer AP_LOGIN ignoriert (synched.c).
        if (_account is null && type != LoginMessage.Login)
            return true;

        switch (type)
        {
            case LoginMessage.Login:
                return HandleLogin(LoginRequest.Parse(frame.Payload));

            case LoginMessage.ReqMenu:
                SendMenu();
                return true;

            case LoginMessage.ReqGame:
                EnterGameMode();
                return true;

            case LoginMessage.Ping:
                return true;

            default:
                Log.Info($"Session {id}: Login-Nachricht {type} ({frame.Type}) wird nicht unterstützt");
                return true;
        }
    }

    private bool HandleLogin(LoginRequest login)
    {
        if (_account is not null)
            return true;   // schon angemeldet

        var account = server.Accounts.Verify(login.Username, login.PasswordDigest);
        if (account is null)
        {
            _failedLogins++;
            Log.Info($"Session {id}: Login fehlgeschlagen für '{login.Username}' ({_failedLogins}. Versuch)");
            if (_failedLogins >= server.Options.MaxLoginAttempts)
            {
                PostLogin(LoginReplies.TooManyLogins());
                return false;
            }
            PostLogin(LoginReplies.LoginFailed());
            return true;
        }

        _account = account;
        server.ClaimAccount(account.Name, this);
        Log.Info($"Session {id}: {account.Name} angemeldet (Client {login.VersionMajor}.{login.VersionMinor})");
        PostLogin(LoginReplies.LoginOk(account.Type));
        SendMenu();
        return true;
    }

    private void SendMenu()
    {
        // Neue Seeds für den Security-Hash im Game-Modus (protocol.md 2.4).
        _codec.Security = new SecurityStream(SecurityStream.CreateSeeds());
        PostLogin(LoginReplies.GetChoice(_codec.Security.Seeds));
        PostLogin(LoginReplies.Credits());
    }

    private void EnterGameMode()
    {
        Log.Info($"Session {id}: {_account!.Name} betritt den Game-Modus");
        PostLogin(new PacketWriter(LoginMessage.Game).ToArray());
        _mode = Mode.Game;
        // Der Client lädt beim Eintritt in STATE_GAME seine Ressourcen neu → dynamische neu senden.
        lock (_sentResources)
            _sentResources.Clear();
        EnsureResource(CharModule);
        PostGame(GameReplies.LoadModule(server.Resources[CharModule]));
    }

    // ---------------------------------------------------------------- Game-Modus

    private bool HandleGameFrame(Frame frame)
    {
        if (!_codec.Verify(frame))
        {
            // Original: LogSpoofs/HangupSpoofs sind standardmäßig an.
            Log.Warn($"Session {id}: Security-Hash falsch (Nachricht {frame.Type}) – trenne");
            return false;
        }
        if (frame.Header.Epoch != Epoch)
            return true;   // veraltete Epoche: still verwerfen (game.c)

        var type = (GameMessage)frame.Type;
        var r = new PacketReader(frame.Payload.AsSpan(1));
        switch (type)
        {
            case GameMessage.Ping:
                SendEchoPing();
                break;

            case GameMessage.SendCharacters:
                PostGame(GameReplies.Characters([new CharacterEntry(_account!.CharacterId, _account.Name, false)], Motd));
                break;

            case GameMessage.UseCharacter:
                UseCharacter(r.U32() & 0x0FFFFFFF);
                break;

            case GameMessage.SendPlayer:
                if (_avatar is not null)
                    SendPlayer();
                break;

            case GameMessage.SendRoomContents:
                if (_avatar is not null)
                    SendRoomContents();
                break;

            case GameMessage.ReqInventory:
                if (_avatar is not null)
                    PostGame(GameReplies.Inventory([]));
                break;

            case GameMessage.ReqMove when _avatar is not null:
            {
                ushort row = r.U16(), col = r.U16();
                byte speed = r.U8();
                switch (server.World.Move(_avatar, row, col, speed))
                {
                    case MoveResult.ChangedRoom changed:
                        Log.Info($"Session {id}: {_avatar.Name} → {changed.Room.Definition.Name}");
                        SendPlayer();
                        SendRoomContents();
                        break;
                    case MoveResult.Blocked blocked:
                        // Position zurücksetzen: Spieler an alter Stelle neu melden.
                        SendText(blocked.Message ?? "Hier geht es nicht weiter.");
                        PostGame(GameReplies.Move(_avatar.Id, _avatar.Row, _avatar.Col, 0));
                        break;
                }
                break;
            }

            case GameMessage.ReqTurn when _avatar is not null:
                r.U32();   // eigene ID
                server.World.Turn(_avatar, r.U16());
                break;

            case GameMessage.SayTo when _avatar is not null:
            {
                var sayType = (SayType)r.U8();
                string text = r.Str();
                if (text.Length > 0)
                {
                    Log.Info($"Session {id}: {_avatar.Name} sagt: {text}");
                    server.World.Say(_avatar, sayType, text);
                }
                break;
            }

            case GameMessage.ReqQuit:
                LeaveWorld();
                PostGame(GameReplies.Quit());
                _mode = Mode.Synched;
                SendMenu();
                break;

            default:
                Log.Info($"Session {id}: Game-Nachricht {type} ({frame.Type}) wird noch nicht unterstützt");
                break;
        }
        return true;
    }

    private void UseCharacter(uint characterId)
    {
        if (characterId != _account!.CharacterId || _avatar is not null)
            return;

        Log.Info($"Session {id}: {_account.Name} betritt die Welt");
        _avatar = new Avatar(characterId, _account.Name, this);
        EnsureResource(InterfaceModule);
        PostGame(GameReplies.LoadModule(server.Resources[InterfaceModule]));

        server.World.Enter(_avatar);
        SendPlayer();
        SendRoomContents();
        PostGame(GameReplies.Inventory([]));
        SendEchoPing();
    }

    private void SendPlayer()
    {
        var room = _avatar!.Room.Definition;
        string icon = server.World.PlayerIcon;
        EnsureResource(room.Roo);
        EnsureResource(room.Name);
        EnsureResource(icon);
        EnsureResource(_avatar.Name);
        var res = server.Resources;
        byte light = (byte)Math.Clamp(room.BaseLight ?? 200, 0, 255);
        PostGame(GameReplies.Player(new PlayerInfo(
            _avatar.Id, res[icon], res[_avatar.Name],
            _avatar.Room.ObjectId, res[room.Roo], res[room.Name], room.Security,
            light, PlayerLight: 0)));
        if (room.Music is { } music)
        {
            EnsureResource(music);
            PostGame(GameReplies.PlayMusic(res[music]));
        }
    }

    private void SendRoomContents()
    {
        var present = server.World.Snapshot(_avatar!.Room);
        foreach (var a in present)
            EnsureResource(a.Name);
        PostGame(GameReplies.RoomContents(_avatar.Room.ObjectId, present.Select(server.World.ToRoomObject).ToList()));
    }

    /// <summary>Systemtext im Chatfenster (BP_MESSAGE mit dynamischer Format-Ressource).</summary>
    private void SendText(string text)
    {
        EnsureResource(text);
        PostGame(GameReplies.Message(server.Resources[text]));
    }

    private void SendEchoPing()
    {
        var (payload, newToken) = _codec.CreateEchoPing();
        _outbox.Writer.TryWrite(new Outgoing(payload, Epoch, newToken));
    }

    private void LeaveWorld()
    {
        if (_avatar is null)
            return;
        server.World.Leave(_avatar);
        _avatar = null;
    }

    // ---------------------------------------------------------------- Senden

    /// <summary>Stellt sicher, dass der Client die String-Ressource kennt (BP_CHANGE_RESOURCE).
    /// Wird auch von anderen Sessions (über World) aufgerufen.</summary>
    public void EnsureResource(string value)
    {
        lock (_sentResources)
        {
            if (_mode != Mode.Game || !_sentResources.Add(value))
                return;
            PostGame(GameReplies.ChangeResource(server.Resources[value], value));
        }
    }

    /// <summary>Reiht eine Game-Nachricht ein. Threadsicher, wird auch von <see cref="World"/> benutzt.</summary>
    public void Post(byte[] payload) => _outbox.Writer.TryWrite(new Outgoing(payload, Epoch));

    private void PostGame(byte[] payload) => Post(payload);

    private void PostLogin(byte[] payload) => _outbox.Writer.TryWrite(new Outgoing(payload, 0));

    private async Task WriteLoopAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var msg in _outbox.Reader.ReadAllAsync(ct))
            {
                // Auch AP_*-Nachrichten werden verschleiert, sobald ein Token aktiv ist (protocol.md 2.5).
                byte[] frame = _codec.FrameServerMessage(msg.Payload, msg.Epoch);
                if (msg.NewToken is { } token)
                    _codec.Token.Reset(token);   // erst NACH der Echo-Ping-Nachricht (game.c)
                await _stream.WriteAsync(frame, ct);
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or OperationCanceledException)
        {
            Close();
        }
    }

    public void Close() => client.Close();
}
