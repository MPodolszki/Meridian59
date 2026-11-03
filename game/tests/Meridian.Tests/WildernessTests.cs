using Meridian.Protocol;
using Meridian.Server;

namespace Meridian.Tests;

/// <summary>Wildnis (tools/make_wilderness.py): Verbindungen zwischen den Städten, Zonen-Übergänge, Monster.</summary>
public sealed class WildernessTests : IAsyncDisposable
{
    private static string Data(params string[] parts) => Path.Combine([AppContext.BaseDirectory, "data", .. parts]);

    private readonly string _db = Path.Combine(Path.GetTempPath(), $"meridian-wild-{Guid.NewGuid():N}.db");
    private readonly CancellationTokenSource _cts = new(TimeSpan.FromSeconds(30));
    private GameServer? _server;
    private Task? _run;

    [Fact]
    public void Towns_AreConnectedThroughTheWilderness()
    {
        var world = WorldDefinition.LoadAll(Data("world"));
        var rooms = world.Rooms.ToDictionary(r => r.Id);
        var seen = new HashSet<int> { 50 };
        var todo = new Stack<int>([50]);
        while (todo.Count > 0)
        {
            var r = rooms[todo.Pop()];
            foreach (int to in r.Exits.Select(e => e.To).Concat(r.EdgeExits.Select(e => e.To)).Concat(r.RegionExits.Select(e => e.To)))
                if (rooms.ContainsKey(to) && seen.Add(to))
                    todo.Push(to);
        }
        // Von Tos aus zu Fuß: Marion (200), Cor Noth, Jasper, Barloque und die Wildnis dazwischen.
        Assert.Contains(200, seen);
        foreach (string town in new[] { "CorNoth", "Jasper", "BarloqueNorth", "OutdoorsC4", "OutdoorsI9", "IceCave1" })
            Assert.Contains(world.Rooms.Single(r => r.KodClass == town).Id, seen);
        var c4 = world.Rooms.Single(r => r.KodClass == "OutdoorsC4");
        Assert.Equal(["spiderbaby", "ent"], c4.Monsters.Select(m => m.Kind));
        Assert.Equal(80000, c4.GenTime);
    }

    [Fact]
    public async Task Marion_ZoneAtTheEastEdge_LeadsIntoTheDeepWoods()
    {
        var (c, events) = await EnterAsync(200);
        await c.MoveAsync(Room.Fine(30), Room.Fine(68), false);
        await WaitForAsync(c, events, e => e is ClientEvent.EnteredRoom { RoomName: "Deep Woods of Ileria" });
    }

    [Fact]
    public async Task WildernessRoom_SpawnsItsMonsters()
    {
        var (c, events) = await EnterAsync(534);
        await WaitForAsync(c, events, e => e is ClientEvent.ObjectAdded o
            && c.Resolve(o.Object.NameRsc) is "Spinnenjunges" or "lebender Baum");
    }

    private async Task<(ClientSession, List<ClientEvent>)> EnterAsync(int startRoom)
    {
        var accounts = AccountStore.Open(_db);
        accounts.AddOrUpdate("Wanderer", "pw");
        var world = WorldDefinition.LoadAll(Data("world")) with { StartRoom = startRoom };
        _server = new GameServer(new ServerOptions
        {
            Port = 0,
            World = world,
            RandomSeed = 1,
            Items = ItemCatalog.Load(Data("items", "items.json")),
            Spells = SpellBook.Load(Data("spells", "spells.json")),
            Skills = SkillBook.Load(Data("skills", "skills.json")),
            Database = _db,
        }, accounts);
        _server.Start();
        _run = _server.RunAsync(_cts.Token);
        var c = new ClientSession();
        var events = new List<ClientEvent>();
        c.Event += events.Add;
        await c.ConnectAsync("127.0.0.1", _server.Port, "Wanderer", "pw");
        await WaitForAsync(c, events, e => e is ClientEvent.EnteredRoom);
        return (c, events);
    }

    private async Task WaitForAsync(ClientSession s, List<ClientEvent> events, Func<ClientEvent?, bool> match)
    {
        while (!events.ToList().Any(match) && !match(null))
        {
            _cts.Token.ThrowIfCancellationRequested();
            await Task.Delay(10);
            s.ProcessEvents();
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        if (_run is not null)
            await _run;
        foreach (var f in new[] { _db, _db + "-wal", _db + "-shm" })
            File.Delete(f);
    }
}
