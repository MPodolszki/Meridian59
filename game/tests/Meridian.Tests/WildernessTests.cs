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

    [Fact]
    public async Task DeepWoods_NorthWestCorner_LeadsToTheTempleOfShalille_WherePriestessXianaTeaches()
    {
        var (c, events) = await EnterAsync(534);
        await c.MoveAsync(Room.Fine(10), Room.Fine(8), false);
        await WaitForAsync(c, events, e => e is ClientEvent.EnteredRoom { RoomName: "The Temple of Shal'ille" });
        await WaitForAsync(c, events, e => e is ClientEvent.ObjectAdded o && c.Resolve(o.Object.NameRsc).StartsWith("Priesterin Xiana"));
        var xiana = events.OfType<ClientEvent.ObjectAdded>().First(e => c.Resolve(e.Object.NameRsc).StartsWith("Priesterin Xiana")).Object;
        await c.RequestBuyAsync(xiana.Id);
        await WaitForAsync(c, events, e => e is ClientEvent.BuyList);
        var list = events.OfType<ClientEvent.BuyList>().Last().Items;
        // Neue Charaktere: nur Stufe-1-Sprüche erlernbar (PlayerCanLearn), Preis 250 × 2^1.
        Assert.NotEmpty(list);
        Assert.All(list, i => Assert.Equal(500u, i.Price));
        string name = c.Resolve(list[0].Item.NameRsc);
        await c.BuyItemsAsync(xiana.Id, [new ObjectRef(list[0].Item.Id, null)]);
        await WaitForAsync(c, events, e => e is ClientEvent.Chat ch && ch.Text == $"Du hast {name} gelernt.");
    }

    [Fact]
    public void Temples_AreReachable_FromTheWilderness()
    {
        var world = WorldDefinition.LoadAll(Data("world"));
        var r = world.Rooms.ToDictionary(x => x.KodClass ?? "");
        Assert.Contains(r["OutdoorsC4"].RegionExits, e => e.To == r["Temple"].Id);
        Assert.Contains(r["OutdoorsG9"].EdgeExits, e => e.To == r["TempleKraanan"].Id);
        Assert.Contains(r["OutdoorsI8"].RegionExits, e => e.To == r["TempleQor"].Id && e.Contains(38, 26));
        Assert.Single(r["Badland1"].Npcs, n => n.KodClass == "FarenPriestess");
    }

    [Fact]
    public void Sewers_ConnectBarloqueAndJasper_ThroughTheLupoggKingsLair()
    {
        var world = WorldDefinition.LoadAll(Data("world"));
        var r = world.Rooms.ToDictionary(x => x.KodClass ?? "");
        Assert.Contains(r["BarloqueNorth"].Exits, e => e.To == r["BarlSewer"].Id);
        Assert.Contains(r["JasperWest"].Exits, e => e.To == r["JasSewer1"].Id);
        var lair = r["SewerKingLair"];
        Assert.Contains(lair.EdgeExits, e => e.To == r["BarlSewer3"].Id);
        Assert.Contains(lair.EdgeExits, e => e.To == r["JasSewer3"].Id);
        Assert.Contains(lair.FixedMonsters, m => m.Kind == "lupoggking");
        Assert.Equal(["giantrat", "lupogg"], r["BarlSewer"].Monsters.Select(m => m.Kind));
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
            Shops = ShopFile.Merge(new[] { "tos", "marion", "temples" }.Select(a => ShopFile.Load(Data("shops", a + ".json")))),
            Dialogs = DialogFile.Merge(new[] { "tos", "marion", "temples" }.Select(a => DialogFile.Load(Data("dialog", a + ".json")))),
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
