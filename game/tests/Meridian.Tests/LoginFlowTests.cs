using System.Net.Sockets;
using Meridian.Protocol;
using Meridian.Server;

namespace Meridian.Tests;

/// <summary>Spielt Login- und Game-Ablauf wie der Original-Client gegen einen echten Server auf einem freien Port durch.</summary>
public sealed class LoginFlowTests : IAsyncLifetime
{
    private readonly string _accountsPath = Path.Combine(Path.GetTempPath(), $"meridian-accounts-{Guid.NewGuid():N}.json");
    private readonly CancellationTokenSource _cts = new(TimeSpan.FromSeconds(30));
    private GameServer _server = null!;
    private Task _serverTask = null!;
    private readonly List<TcpClient> _clients = [];

    public Task InitializeAsync()
    {
        var accounts = AccountStore.Load(_accountsPath);
        accounts.AddOrUpdate("Tester", "geheim");
        accounts.AddOrUpdate("Zweiter", "geheim");
        _server = new GameServer(new ServerOptions { Port = 0 }, accounts);
        _server.Start();
        _serverTask = _server.RunAsync(_cts.Token);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        foreach (var c in _clients)
            c.Dispose();
        _cts.Cancel();
        await _serverTask;
        File.Delete(_accountsPath);
    }

    [Fact]
    public async Task ValidLogin_GetsLoginOkAndMenu()
    {
        var c = await ConnectAsync();
        await ExpectAsync(c, (byte)LoginMessage.GetLogin);
        await c.SendAsync(Login("tester", "geheim"));

        var ok = await ExpectAsync(c, (byte)LoginMessage.LoginOk);
        Assert.Equal(2, ok.Payload.Length);
        var choice = await ExpectAsync(c, (byte)LoginMessage.GetChoice);
        Assert.Equal(1 + 5 * 4, choice.Payload.Length);
        await ExpectAsync(c, (byte)LoginMessage.Credits);
    }

    [Fact]
    public async Task WrongPassword_ThreeTimes_GetsTooManyLoginsAndDisconnect()
    {
        var c = await ConnectAsync();
        await ExpectAsync(c, (byte)LoginMessage.GetLogin);

        byte[] bad = Login("Tester", "falsch");
        await c.SendAsync(bad);
        await ExpectAsync(c, (byte)LoginMessage.LoginFailed);
        await c.SendAsync(bad);
        await ExpectAsync(c, (byte)LoginMessage.LoginFailed);
        await c.SendAsync(bad);
        await ExpectAsync(c, (byte)LoginMessage.TooManyLogins);
        Assert.Null(await c.ReceiveAsync(_cts.Token));
    }

    [Fact]
    public async Task EnterWorld_GetsCharactersPlayerAndRoomContents()
    {
        var c = await LoginAsync("Tester");
        await c.SendAsync(new PacketWriter(LoginMessage.ReqGame).U32(0).U32(737 * 3 + 3).Str("host").ToArray());

        await ExpectAsync(c, (byte)LoginMessage.Game);
        Assert.True(c.GameMode);
        var charModule = await ExpectAsync(c, (byte)GameMessage.ChangeResource);
        Assert.Equal(Session.CharModule, new PacketReader(charModule.Payload.AsSpan(5)).Str());
        await ExpectAsync(c, (byte)GameMessage.LoadModule);

        await c.SendAsync([(byte)GameMessage.SendCharacters]);
        var chars = await ExpectAsync(c, (byte)GameMessage.Characters);
        var r = new PacketReader(chars.Payload.AsSpan(1));
        Assert.Equal(1, r.U16());
        uint charId = r.U32();
        Assert.Equal("Tester", r.Str());

        await c.SendAsync(new PacketWriter((byte)GameMessage.UseCharacter).U32(charId).ToArray());
        var player = await ExpectAsync(c, (byte)GameMessage.Player);
        Assert.Equal(1 + 54, player.Payload.Length);   // Client prüft exakt 54 Byte hinter dem Type
        Assert.Equal(charId, new PacketReader(player.Payload.AsSpan(1)).U32());
        var contents = await ExpectAsync(c, (byte)GameMessage.RoomContents);
        r = new PacketReader(contents.Payload.AsSpan(1));
        Assert.Equal(_server.World.StartRoom.ObjectId, r.U32());
        Assert.Equal(1, r.U16());
        Assert.Equal(charId, r.U32());
        await ExpectAsync(c, (byte)GameMessage.Inventory);
        await ExpectAsync(c, (byte)GameMessage.EchoPing);

        // Ab jetzt ist der Sliding-Token aktiv: Ping/Echo muss weiterhin korrekt entschleiert werden.
        for (int i = 0; i < 3; i++)
        {
            await c.SendAsync([(byte)GameMessage.Ping]);
            await ExpectAsync(c, (byte)GameMessage.EchoPing);
        }

        // Zurück ins Menü: AP_*-Nachrichten sind jetzt ebenfalls verschleiert.
        await c.SendAsync([(byte)GameMessage.ReqQuit]);
        await ExpectAsync(c, (byte)GameMessage.Quit);
        Assert.False(c.GameMode);
        await ExpectAsync(c, (byte)LoginMessage.GetChoice);
        await ExpectAsync(c, (byte)LoginMessage.Credits);
    }

    [Fact]
    public async Task TwoPlayers_SeeEachOther_MoveAndChat()
    {
        var a = await EnterWorldAsync("Tester");
        var b = await EnterWorldAsync("Zweiter");

        // A erfährt, dass B den Raum betritt.
        var create = await ExpectAsync(a, (byte)GameMessage.Create);
        uint bId = new PacketReader(create.Payload.AsSpan(1)).U32();

        // B bewegt sich → A bekommt BP_MOVE.
        await b.SendAsync(new PacketWriter((byte)GameMessage.ReqMove).U16(400).U16(500).U8(18).U32(_server.World.StartRoom.ObjectId).ToArray());
        var move = await ExpectAsync(a, (byte)GameMessage.Move);
        var r = new PacketReader(move.Payload.AsSpan(1));
        Assert.Equal(bId, r.U32());
        Assert.Equal(400, r.U16());
        Assert.Equal(500, r.U16());

        // B sagt etwas → beide bekommen BP_SAID.
        await b.SendAsync(new PacketWriter((byte)GameMessage.SayTo).U8((byte)SayType.Normal).Str("Hallo").ToArray());
        foreach (var c in new[] { a, b })
        {
            var said = await ExpectAsync(c, (byte)GameMessage.Said);
            r = new PacketReader(said.Payload.AsSpan(1));
            Assert.Equal(bId, r.U32());
            r.U32(); r.U8(); r.U32();
            Assert.Equal("Zweiter: Hallo", r.Str());
        }

        // B verlässt das Spiel → A bekommt BP_REMOVE.
        await b.SendAsync([(byte)GameMessage.ReqQuit]);
        var remove = await ExpectAsync(a, (byte)GameMessage.Remove);
        Assert.Equal(bId, new PacketReader(remove.Payload.AsSpan(1)).U32());
    }

    [Fact]
    public async Task WrongSecurityHash_Disconnects()
    {
        var tcp = await ConnectTcpAsync();
        var c = new ClientConnection(tcp.GetStream());
        await ExpectAsync(c, (byte)LoginMessage.GetLogin);
        await c.SendAsync(Login("Tester", "geheim"));
        await ExpectAsync(c, (byte)LoginMessage.LoginOk);
        await ExpectAsync(c, (byte)LoginMessage.GetChoice);
        await ExpectAsync(c, (byte)LoginMessage.Credits);
        await c.SendAsync(new PacketWriter(LoginMessage.ReqGame).U32(0).U32(0).Str("h").ToArray());
        await ExpectAsync(c, (byte)LoginMessage.Game);

        // Gefälschte Nachricht mit falschem Hash direkt auf den Socket.
        await tcp.GetStream().WriteAsync(Framing.Frame([(byte)GameMessage.Ping], crc: 0x1234, epoch: Session.Epoch));
        while (await c.ReceiveAsync(_cts.Token) is not null) { }
    }

    // ---------------------------------------------------------------- Hilfen

    private async Task<TcpClient> ConnectTcpAsync()
    {
        var tcp = new TcpClient();
        _clients.Add(tcp);
        await tcp.ConnectAsync("127.0.0.1", _server.Port);
        return tcp;
    }

    private async Task<ClientConnection> ConnectAsync() => new((await ConnectTcpAsync()).GetStream());

    private async Task<ClientConnection> LoginAsync(string name)
    {
        var c = await ConnectAsync();
        await ExpectAsync(c, (byte)LoginMessage.GetLogin);
        await c.SendAsync(Login(name, "geheim"));
        await ExpectAsync(c, (byte)LoginMessage.LoginOk);
        await ExpectAsync(c, (byte)LoginMessage.GetChoice);
        await ExpectAsync(c, (byte)LoginMessage.Credits);
        return c;
    }

    private async Task<ClientConnection> EnterWorldAsync(string name)
    {
        var c = await LoginAsync(name);
        await c.SendAsync(new PacketWriter(LoginMessage.ReqGame).U32(0).U32(0).Str("h").ToArray());
        await ExpectAsync(c, (byte)LoginMessage.Game);   // erst danach rechnet der Client den Security-Hash
        // Wie das char-Modul erst nach BP_LOAD_MODULE senden: sonst Epoch 0 → Server verwirft still.
        await ExpectAsync(c, (byte)GameMessage.LoadModule);
        await c.SendAsync([(byte)GameMessage.SendCharacters]);
        var chars = await ExpectAsync(c, (byte)GameMessage.Characters);
        uint id = new PacketReader(chars.Payload.AsSpan(3)).U32();
        await c.SendAsync(new PacketWriter((byte)GameMessage.UseCharacter).U32(id).ToArray());
        await ExpectAsync(c, (byte)GameMessage.EchoPing);
        return c;
    }

    private static byte[] Login(string name, string password) =>
        new LoginRequest(7, 37, 2, name, PasswordDigest.Compute(password)).ToPayload();

    /// <summary>Liest bis zur erwarteten Nachricht; BP_CHANGE_RESOURCE und andere werden übersprungen,
    /// außer sie sind selbst erwartet.</summary>
    private async Task<Frame> ExpectAsync(ClientConnection c, byte type)
    {
        while (true)
        {
            var frame = await c.ReceiveAsync(_cts.Token)
                ?? throw new Xunit.Sdk.XunitException($"Verbindung geschlossen, erwartet war {type}");
            if (frame.Type == type)
                return frame;
        }
    }
}
