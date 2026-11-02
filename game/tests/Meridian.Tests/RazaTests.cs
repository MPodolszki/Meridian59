using Meridian.Protocol;
using Meridian.Server;

namespace Meridian.Tests;

/// <summary>Raza (Neulingsinsel, newb1–newb8): Gebietsdateien, Start, Kampfverbot, Portal aufs Festland.</summary>
public sealed class RazaTests : IAsyncDisposable
{
    private static string Data(params string[] parts) => Path.Combine([AppContext.BaseDirectory, "data", .. parts]);

    private readonly string _db = Path.Combine(Path.GetTempPath(), $"meridian-raza-{Guid.NewGuid():N}.db");
    private readonly CancellationTokenSource _cts = new(TimeSpan.FromSeconds(30));
    private GameServer? _server;
    private Task? _run;

    [Fact]
    public void AllAreas_LoadTogether_NewCharactersStartInRaza_DeathsOnTheMainlandStayInTos()
    {
        var world = WorldDefinition.LoadAll(Data("world"));
        Assert.Equal(1011, world.StartRoom);
        Assert.Equal(50, world.RespawnRoom);
        var ids = world.Rooms.Select(r => r.Id).ToHashSet();
        Assert.Subset(ids, Enumerable.Range(1011, 8).ToHashSet());
        // Alle Ausgänge in Raza führen in vorhandene Räume, das Portal ins Gasthaus von Tos.
        foreach (var room in world.Rooms.Where(r => r.Id is >= 1011 and <= 1018))
        {
            Assert.All(room.Exits, e => Assert.Contains(e.To, ids));
            Assert.Equal(1011, room.RespawnRoom);
        }
        Assert.Equal(52, world.Rooms.Single(r => r.Id == 1018).Portal!.ToRoom);
        Assert.Equal(3, world.Rooms.Single(r => r.Id == 1012).FixedMonsters.Count);
    }

    [Fact]
    public async Task Village_HasCows_ThatCannotBeAttacked()
    {
        var (c, events) = await EnterAsync(1012);
        var cow = events.OfType<ClientEvent.ObjectAdded>().First(e => c.Resolve(e.Object.NameRsc) == "Kuh").Object;
        await c.AttackAsync(cow.Id);
        await WaitForAsync(c, events, e => e is ClientEvent.Chat { Text: "Hier ist kein Kampf erlaubt." });
        await c.LookAsync(cow.Id);
        await WaitForAsync(c, events, e => e is ClientEvent.Chat ch && ch.Text.StartsWith("Eine Zwergkuh"));
    }

    [Fact]
    public async Task Portal_WarnsFirst_ThenLeadsToTheInnOfTos_WithStartingGear()
    {
        var (c, events) = await EnterAsync(1018);
        Assert.Equal("The Grand Museum of Raza", events.OfType<ClientEvent.EnteredRoom>().Last().RoomName);
        await c.MoveAsync(Room.Fine(2), Room.Fine(11), false);   // aufs Portal
        await WaitForAsync(c, events, e => e is ClientEvent.Chat ch && ch.Text.StartsWith("Achtung"));
        await c.MoveAsync(Room.Fine(4), Room.Fine(11), false);   // wieder herunter
        await c.MoveAsync(Room.Fine(2), Room.Fine(11), false);   // und erneut hinein
        await WaitForAsync(c, events, e => e is ClientEvent.EnteredRoom { RoomName: "Familiars" });
        // AddRealWorldObjects: 1000 Schillinge dazu (Start 2500), ein zweiter Streitkolben, 5 Äpfel.
        await WaitForAsync(c, events, _ => c.Inventory.Count(o => c.Resolve(o.NameRsc) == "Streitkolben") == 2
            && c.Inventory.Any(o => c.Resolve(o.NameRsc) is "Apfel" or "Äpfel" && o.Amount == 5)
            && c.Inventory.Any(o => o.Amount == 3500));
    }

    private async Task<(ClientSession, List<ClientEvent>)> EnterAsync(int startRoom)
    {
        var accounts = AccountStore.Open(_db);
        accounts.AddOrUpdate("Neuling", "pw");
        var world = WorldDefinition.LoadAll(Data("world")) with { StartRoom = startRoom };
        _server = new GameServer(new ServerOptions
        {
            Port = 0,
            World = world,
            RandomSeed = 1,
            Items = ItemCatalog.Load(Data("items", "items.json")),
            Spells = SpellBook.Load(Data("spells", "spells.json")),
            Skills = SkillBook.Load(Data("skills", "skills.json")),
            Shops = ShopFile.Load(Data("shops", "raza.json")),
            Dialogs = DialogFile.Load(Data("dialog", "raza.json")),
            Database = _db,
        }, accounts);
        _server.Start();
        _run = _server.RunAsync(_cts.Token);
        var c = new ClientSession();
        var events = new List<ClientEvent>();
        c.Event += events.Add;
        await c.ConnectAsync("127.0.0.1", _server.Port, "Neuling", "pw");
        await WaitForAsync(c, events, e => e is ClientEvent.EnteredRoom);
        await WaitForAsync(c, events, e => e is ClientEvent.InventoryChanged);
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
