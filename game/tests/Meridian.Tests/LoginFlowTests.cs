using System.Net.Sockets;
using Meridian.Protocol;
using Meridian.Server;

namespace Meridian.Tests;

/// <summary>Spielt den Login-Ablauf des Original-Clients gegen einen echten Server auf einem freien Port durch.</summary>
public sealed class LoginFlowTests : IAsyncLifetime
{
    private readonly string _accountsPath = Path.Combine(Path.GetTempPath(), $"meridian-accounts-{Guid.NewGuid():N}.json");
    private readonly CancellationTokenSource _cts = new(TimeSpan.FromSeconds(30));
    private GameServer _server = null!;
    private Task _serverTask = null!;

    public Task InitializeAsync()
    {
        var accounts = AccountStore.Load(_accountsPath);
        accounts.AddOrUpdate("Tester", "geheim");
        _server = new GameServer(new ServerOptions { Port = 0 }, accounts);
        _server.Start();
        _serverTask = _server.RunAsync(_cts.Token);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _cts.Cancel();
        await _serverTask;
        File.Delete(_accountsPath);
    }

    [Fact]
    public async Task ValidLogin_GetsLoginOkMenuAndMessageOnReqGame()
    {
        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", _server.Port);
        var stream = client.GetStream();
        var reader = new FrameReader(stream, Framing.MaxServerPayload);

        Assert.Equal((byte)LoginMessage.GetLogin, (await NextAsync(reader)).Type);

        await SendAsync(stream, new LoginRequest(7, 37, 2, "tester", PasswordDigest.Compute("geheim")).ToPayload());

        var ok = await NextAsync(reader);
        Assert.Equal((byte)LoginMessage.LoginOk, ok.Type);
        Assert.Equal(2, ok.Payload.Length);

        var choice = await NextAsync(reader);
        Assert.Equal((byte)LoginMessage.GetChoice, choice.Type);
        Assert.Equal(1 + 5 * 4, choice.Payload.Length);

        var credits = await NextAsync(reader);
        Assert.Equal((byte)LoginMessage.Credits, credits.Type);

        await SendAsync(stream, new PacketWriter(LoginMessage.ReqGame).U32(0).U32(737 * 3 + 3).Str("host").ToArray());
        var message = await NextAsync(reader);
        Assert.Equal((byte)LoginMessage.Message, message.Type);
        var r = new PacketReader(message.Payload.AsSpan(1));
        Assert.Equal(Session.GameNotAvailableText, r.Str());
        Assert.Equal((byte)LoginAction.Logoff, r.U8());
    }

    [Fact]
    public async Task WrongPassword_ThreeTimes_GetsTooManyLoginsAndDisconnect()
    {
        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", _server.Port);
        var stream = client.GetStream();
        var reader = new FrameReader(stream, Framing.MaxServerPayload);
        await NextAsync(reader);

        byte[] bad = new LoginRequest(7, 37, 2, "Tester", PasswordDigest.Compute("falsch")).ToPayload();
        await SendAsync(stream, bad);
        Assert.Equal((byte)LoginMessage.LoginFailed, (await NextAsync(reader)).Type);
        await SendAsync(stream, bad);
        Assert.Equal((byte)LoginMessage.LoginFailed, (await NextAsync(reader)).Type);
        await SendAsync(stream, bad);
        Assert.Equal((byte)LoginMessage.TooManyLogins, (await NextAsync(reader)).Type);
        Assert.Null(await reader.ReadAsync(_cts.Token));
    }

    private async Task<Frame> NextAsync(FrameReader reader) =>
        await reader.ReadAsync(_cts.Token) ?? throw new Xunit.Sdk.XunitException("Verbindung unerwartet geschlossen");

    // Der Original-Client schreibt im Login-Modus einen beliebigen Wert ins crc-Feld.
    private static Task SendAsync(NetworkStream stream, byte[] payload) =>
        stream.WriteAsync(Framing.Frame(payload, crc: 0xBEEF, epoch: 0)).AsTask();
}
