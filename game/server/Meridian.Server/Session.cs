using System.Net.Sockets;
using Meridian.Protocol;

namespace Meridian.Server;

/// <summary>
/// Eine Client-Verbindung. Implementiert bisher nur den Login-/Synched-Modus
/// (protocol.md 3.1/3.2): AP_GETLOGIN → AP_LOGIN → AP_LOGINOK, AP_GETCHOICE, AP_CREDITS.
/// Auf AP_REQ_GAME antwortet der Server mit einer Meldung und Logoff, da der Game-Modus fehlt.
/// </summary>
public sealed class Session(int id, TcpClient client, GameServer server)
{
    public const string GameNotAvailableText = "Willkommen! Die Spielwelt ist noch im Aufbau.";

    private readonly NetworkStream _stream = client.GetStream();
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private Account? _account;
    private int _failedLogins;
    private SecurityStream? _security;

    public int Id => id;
    public string? AccountName => _account?.Name;

    public async Task RunAsync(CancellationToken serverCt)
    {
        Log.Info($"Session {id}: Verbindung von {client.Client.RemoteEndPoint}");
        using var idle = CancellationTokenSource.CreateLinkedTokenSource(serverCt);
        idle.CancelAfter(server.Options.MenuTimeout);
        var reader = new FrameReader(_stream, Framing.MaxClientPayload);

        try
        {
            await SendAsync(LoginReplies.GetLogin());
            while (await reader.ReadAsync(idle.Token) is { } frame)
            {
                idle.CancelAfter(server.Options.MenuTimeout);
                if (frame.Header.Length == 0)
                    continue;   // Original ignoriert leere Nachrichten
                if (frame.Header.Epoch != 0)
                    throw new ProtocolException($"Epoch {frame.Header.Epoch} im Login-Modus");
                if (!await HandleLoginMessageAsync(frame))
                    break;
            }
        }
        catch (OperationCanceledException) when (!serverCt.IsCancellationRequested)
        {
            Log.Info($"Session {id}: Timeout");
            await TrySendAsync(LoginReplies.Timeout());
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
            Close();
            Log.Info($"Session {id}: getrennt");
        }
    }

    /// <summary>Verarbeitet eine Nachricht. false = Verbindung beenden.</summary>
    private async Task<bool> HandleLoginMessageAsync(Frame frame)
    {
        var type = (LoginMessage)frame.Type;

        // Vor erfolgreichem Login wird alles außer AP_LOGIN ignoriert (synched.c).
        if (_account is null && type != LoginMessage.Login)
            return true;

        switch (type)
        {
            case LoginMessage.Login:
                return await HandleLoginAsync(LoginRequest.Parse(frame.Payload));

            case LoginMessage.ReqMenu:
                await SendMenuAsync();
                return true;

            case LoginMessage.ReqGame:
                Log.Info($"Session {id}: {_account!.Name} möchte ins Spiel (Game-Modus fehlt noch)");
                await SendAsync(LoginReplies.Message(GameNotAvailableText, LoginAction.Logoff));
                return true;

            case LoginMessage.Ping:
                return true;

            default:
                Log.Info($"Session {id}: Nachricht {type} ({frame.Type}) wird noch nicht unterstützt");
                return true;
        }
    }

    private async Task<bool> HandleLoginAsync(LoginRequest login)
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
                await SendAsync(LoginReplies.TooManyLogins());
                return false;
            }
            await SendAsync(LoginReplies.LoginFailed());
            return true;
        }

        _account = account;
        server.ClaimAccount(account.Name, this);
        Log.Info($"Session {id}: {account.Name} angemeldet (Client {login.VersionMajor}.{login.VersionMinor})");
        await SendAsync(LoginReplies.LoginOk(account.Type));
        await SendMenuAsync();
        return true;
    }

    private async Task SendMenuAsync()
    {
        // Neue Seeds für den Security-Hash im Game-Modus (protocol.md 2.4).
        _security = new SecurityStream(SecurityStream.CreateSeeds());
        await SendAsync(LoginReplies.GetChoice(_security.Seeds));
        await SendAsync(LoginReplies.Credits());
    }

    private async Task SendAsync(byte[] payload)
    {
        // Im Login-Modus ist der Secure-Token noch 0 → Type-Byte bleibt unverändert, Epoch = 0.
        byte[] frame = Framing.FrameServerMessage(payload);
        await _sendLock.WaitAsync();
        try { await _stream.WriteAsync(frame); }
        finally { _sendLock.Release(); }
    }

    private async Task TrySendAsync(byte[] payload)
    {
        try { await SendAsync(payload); }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException) { }
    }

    public void Close() => client.Close();
}
