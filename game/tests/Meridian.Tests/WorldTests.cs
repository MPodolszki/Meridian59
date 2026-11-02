using Meridian.Protocol;
using Meridian.Server;

namespace Meridian.Tests;

/// <summary>Mehrere Räume: Ausgänge und verschlossene Türen (Feld-Koordinaten wie KOD plExits).</summary>
public sealed class WorldTests : IAsyncLifetime
{
    private static readonly WorldDefinition TwoRooms = new(50,
    [
        new RoomDefinition(50, "Straßen", "tos.roo")
        {
            Teleport = new GridPos(10, 10),
            Exits = [new ExitDefinition(12, 18, To: 52, ToRow: 4, ToCol: 10), new ExitDefinition(57, 3, To: 999, ToRow: 1, ToCol: 1)],
            LockedDoors = [new LockedDoorDefinition(50, 10) { Message = "Verschlossen." }],
        },
        new RoomDefinition(52, "Gasthaus", "tosinn.roo") { Teleport = new GridPos(4, 10), Music = "inn.mid" },
    ]);

    private readonly string _accountsPath = Path.Combine(Path.GetTempPath(), $"meridian-accounts-{Guid.NewGuid():N}.db");
    private readonly CancellationTokenSource _cts = new(TimeSpan.FromSeconds(30));
    private GameServer _server = null!;
    private Task _serverTask = null!;

    public Task InitializeAsync()
    {
        var accounts = AccountStore.Open(_accountsPath);
        accounts.AddOrUpdate("Anna", "pw");
        accounts.AddOrUpdate("Bert", "pw");
        _server = new GameServer(new ServerOptions { Port = 0, World = TwoRooms }, accounts);
        _server.Start();
        _serverTask = _server.RunAsync(_cts.Token);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _cts.Cancel();
        await _serverTask;
        foreach (var f in new[] { _accountsPath, _accountsPath + "-wal", _accountsPath + "-shm" })
            File.Delete(f);
    }

    [Fact]
    public async Task Exit_ChangesRoom_AndOthersSeeLeaving()
    {
        using var anna = new ClientSession();
        using var bert = new ClientSession();
        var annaEvents = Collect(anna);
        var bertEvents = Collect(bert);

        await anna.ConnectAsync("127.0.0.1", _server.Port, "Anna", "pw");
        await WaitForAsync(anna, annaEvents, e => e is ClientEvent.EnteredRoom { RoomName: "Straßen" });
        await bert.ConnectAsync("127.0.0.1", _server.Port, "Bert", "pw");
        await WaitForAsync(bert, bertEvents, e => e is ClientEvent.EnteredRoom);
        await WaitForAsync(anna, annaEvents, e => e is ClientEvent.ObjectAdded a && a.Object.Id == bert.PlayerId);

        // Bert läuft auf das Ausgangsfeld (12,18): noch kein Raumwechsel, erst „Gehen“ löst aus.
        uint bertId = bert.PlayerId;
        await bert.MoveAsync(Room.Fine(12, 10), Room.Fine(18, 50), running: false);
        await WaitForAsync(anna, annaEvents, e => e is ClientEvent.ObjectMoved { Object.Row: 12 * 64 + 10 });
        Assert.DoesNotContain(bertEvents, e => e is ClientEvent.EnteredRoom { RoomName: "Gasthaus" });
        await bert.GoAsync();
        await WaitForAsync(bert, bertEvents, e => e is ClientEvent.EnteredRoom { RoomName: "Gasthaus" });
        await WaitForAsync(bert, bertEvents, e => e is ClientEvent.ObjectAdded { Object: { Row: 4 * 64 + 32, Col: 10 * 64 + 32 } });
        Assert.Equal(_server.World.Rooms.Single(r => r.Definition.Id == 52).ObjectId, bert.RoomId);
        await WaitForAsync(anna, annaEvents, e => e is ClientEvent.ObjectRemoved r && r.Id == bertId);
    }

    [Fact]
    public async Task LockedDoor_And_UnknownTarget_AreBlocked()
    {
        using var anna = new ClientSession();
        var events = Collect(anna);
        await anna.ConnectAsync("127.0.0.1", _server.Port, "Anna", "pw");
        await WaitForAsync(anna, events, e => e is ClientEvent.EnteredRoom);

        await anna.GoAsync();   // am Ankunftspunkt ist kein Ausgang
        await WaitForAsync(anna, events, e => e is ClientEvent.Chat { Text: "Hier ist kein Durchgang." });

        await anna.MoveAsync(Room.Fine(50), Room.Fine(10), running: false);
        await anna.GoAsync();
        await WaitForAsync(anna, events, e => e is ClientEvent.Chat { Text: "Verschlossen." });

        await anna.MoveAsync(Room.Fine(57), Room.Fine(3), running: false);
        await anna.GoAsync();
        await WaitForAsync(anna, events, e => e is ClientEvent.Chat { Text: "Dieser Weg ist noch nicht begehbar." });
    }

    [Fact]
    public void TosJson_IsConsistent()
    {
        var world = WorldDefinition.Load(Path.Combine(AppContext.BaseDirectory, "data", "world", "tos.json"));
        Assert.Equal(50, world.StartRoom);
        Assert.Equal(18, world.Rooms.Count);
        _ = new World(world, new ResourceRegistry(), "blank.bgf");

        // Alle Ausgänge zwischen Tos-Räumen zeigen auf existierende Räume mit Zielfeld ≥ 1.
        var ids = world.Rooms.Select(r => r.Id).ToHashSet();
        foreach (var exit in world.Rooms.SelectMany(r => r.Exits).Where(e => ids.Contains(e.To)))
            Assert.True(exit.ToRow >= 1 && exit.ToCol >= 1);
    }

    [Theory]
    [InlineData(100, "NONE", 100)]
    [InlineData(100, "180", 2148)]
    [InlineData(3000, "180", 952)]
    [InlineData(100, "ANGLE_SOUTH", 100)]
    public void RotateAngle_FollowsKod(int angle, string rotate, int expected) =>
        Assert.Equal(expected, World.RotateAngle((ushort)angle, rotate));

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
