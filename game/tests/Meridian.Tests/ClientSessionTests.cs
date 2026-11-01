using Meridian.Protocol;
using Meridian.Server;

namespace Meridian.Tests;

/// <summary>Der Godot-unabhängige Client (ClientSession) gegen den neuen Server.</summary>
public sealed class ClientSessionTests : IAsyncLifetime
{
    private readonly string _accountsPath = Path.Combine(Path.GetTempPath(), $"meridian-accounts-{Guid.NewGuid():N}.json");
    private readonly CancellationTokenSource _cts = new(TimeSpan.FromSeconds(30));
    private GameServer _server = null!;
    private Task _serverTask = null!;

    public Task InitializeAsync()
    {
        var accounts = AccountStore.Load(_accountsPath);
        accounts.AddOrUpdate("Anna", "pw");
        accounts.AddOrUpdate("Bert", "pw");
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
    public async Task TwoClients_EnterWorld_SeeEachOther_AndChat()
    {
        using var anna = new ClientSession();
        using var bert = new ClientSession();
        var annaEvents = Collect(anna);
        var bertEvents = Collect(bert);

        await anna.ConnectAsync("127.0.0.1", _server.Port, "Anna", "pw");
        await WaitForAsync(anna, annaEvents, e => e is ClientEvent.EnteredRoom);
        Assert.Equal("Gasthaus", annaEvents.OfType<ClientEvent.EnteredRoom>().Single().RoomName);

        await bert.ConnectAsync("127.0.0.1", _server.Port, "Bert", "pw");
        await WaitForAsync(bert, bertEvents, e => e is ClientEvent.ObjectAdded { Object.Id: var id } && id != bert.PlayerId && id != 0);
        await WaitForAsync(anna, annaEvents, e => e is ClientEvent.ObjectAdded a && a.Object.Id == bert.PlayerId);

        await bert.MoveAsync(1000, 1100, running: false);
        await WaitForAsync(anna, annaEvents, e => e is ClientEvent.ObjectMoved { Object: { Row: 1000, Col: 1100 } });

        await anna.SayAsync("Hallo Bert");
        await WaitForAsync(bert, bertEvents, e => e is ClientEvent.Chat { Text: "Anna: Hallo Bert" });

        uint bertId = bert.PlayerId;
        bert.Dispose();
        await WaitForAsync(anna, annaEvents, e => e is ClientEvent.ObjectRemoved r && r.Id == bertId);
    }

    [Fact]
    public async Task WrongPassword_ReportsLoginFailed()
    {
        using var c = new ClientSession();
        var events = Collect(c);
        await c.ConnectAsync("127.0.0.1", _server.Port, "Anna", "falsch");
        await WaitForAsync(c, events, e => e is ClientEvent.LoginFailed);
    }

    private static List<ClientEvent> Collect(ClientSession s)
    {
        var list = new List<ClientEvent>();
        s.Event += list.Add;
        return list;
    }

    private async Task WaitForAsync(ClientSession s, List<ClientEvent> events, Func<ClientEvent, bool> match)
    {
        while (!events.Any(match))
        {
            _cts.Token.ThrowIfCancellationRequested();
            await Task.Delay(10);
            s.ProcessEvents();
        }
    }
}
