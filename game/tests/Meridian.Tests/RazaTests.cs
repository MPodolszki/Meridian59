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
        Assert.Contains(world.Rooms.Single(r => r.Id == 1018).Portal!.Destinations, d => d.Room == 202);
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
    public async Task Portal_WarnsFirst_ThenLeadsToAHometownInn_WithStartingGear()
    {
        var (c, events) = await EnterAsync(1018);
        Assert.Equal("The Grand Museum of Raza", events.OfType<ClientEvent.EnteredRoom>().Last().RoomName);
        await c.MoveAsync(Room.Fine(2), Room.Fine(11), false);   // aufs Portal
        await WaitForAsync(c, events, e => e is ClientEvent.Chat ch && ch.Text.StartsWith("Achtung"));
        await c.MoveAsync(Room.Fine(4), Room.Fine(11), false);   // wieder herunter
        await c.MoveAsync(Room.Fine(2), Room.Fine(11), false);   // und erneut hinein
        // SetRandomHomeroom über die vorhandenen Heimatstädte: Gasthaus von Marion oder Cor Noth.
        await WaitForAsync(c, events, e => e is ClientEvent.EnteredRoom { RoomName: "The Limping Toad Inn and Tavern" or "Cibilo Creek Inn" or "Yonder Inn of Jasper" });
        // AddRealWorldObjects: 1000 Schillinge dazu (Start 2500), ein zweiter Streitkolben, 5 Äpfel.
        await WaitForAsync(c, events, _ => c.Inventory.Count(o => c.Resolve(o.NameRsc) == "Streitkolben") == 2
            && c.Inventory.Any(o => c.Resolve(o.NameRsc) is "Apfel" or "Äpfel" && o.Amount == 5)
            && c.Inventory.Any(o => o.Amount == 3500));
    }

    [Fact]
    public async Task Mausoleum_LeversLiftTheDoor_OneSlamsShut_BothOpenIt()
    {
        var (c, events) = await EnterAsync(1016);
        var added = events.OfType<ClientEvent.ObjectAdded>().Select(e => e.Object).ToList();
        // FirstUserEntered: Endbereich mit vier Mumien und einer Schattenmumie, eine Mumie in der Grube.
        Assert.Equal(1, added.Count(o => c.Resolve(o.NameRsc) == "Schattenmumie"));
        Assert.True(added.Count(o => c.Resolve(o.NameRsc) == "Mumie") >= 5);
        var levers = added.Where(o => c.Resolve(o.NameRsc) == "Hebel").OrderBy(o => o.Col).ToList();
        Assert.Equal(2, levers.Count);
        Assert.All(levers, l => Assert.Equal((ushort)1, l.Group));

        await c.ActivateAsync(levers[0].Id);
        await WaitForAsync(c, events, e => e is ClientEvent.Chat { Text: "Der Hebel ist nicht nah genug, um ihn umzulegen." });

        await c.MoveAsync(Room.Fine(27), Room.Fine(5), false);
        await c.ActivateAsync(levers[0].Id);
        await WaitForAsync(c, events, e => e is ClientEvent.SectorMoved { Sector: 3, Animation: 5, Height: 128 });
        await WaitForAsync(c, events, e => e is ClientEvent.ObjectChanged { Object.Group: 2 });
        // Nach 2 s schlägt die Tür zu und der Hebel springt zurück.
        await WaitForAsync(c, events, e => e is ClientEvent.SectorMoved { Sector: 3, Height: 84 });
        await WaitForAsync(c, events, _ => events.OfType<ClientEvent.ObjectChanged>().LastOrDefault()?.Object.Group == 1);

        // Beide Hebel schnell hintereinander: Tür ganz offen.
        await c.ActivateAsync(levers[0].Id);
        await c.MoveAsync(Room.Fine(27), Room.Fine(8), false);
        await c.ActivateAsync(levers[1].Id);
        await WaitForAsync(c, events, e => e is ClientEvent.SectorMoved { Sector: 3, Height: 172 });
    }

    [Fact]
    public void Marion_ExitsLeadIntoExistingRooms_AndCryptsHaveMonsters()
    {
        var world = WorldDefinition.LoadAll(Data("world"));
        var ids = world.Rooms.Select(r => r.Id).ToHashSet();
        foreach (var room in world.Rooms.Where(r => r.Id is >= 200 and <= 205 or >= 2600 and <= 2602))
            Assert.All(room.Exits, e => Assert.Contains(e.To, ids));
        Assert.Equal("spectralmummy", world.Rooms.Single(r => r.Id == 2600).Monsters.Single().Kind);
        Assert.Equal(35, world.Rooms.Single(r => r.Id == 2601).Generators.Count);
        Assert.Equal(MonsterKind.Thrasher, MonsterKind.ByKey(world.Rooms.Single(r => r.Id == 2602).Monsters.Single().Kind));
    }

    [Fact]
    public async Task Marion_ElderSellsOrcTeeth_WhenAsked()
    {
        var (c, events) = await EnterAsync(200);
        Assert.Equal("Marion", events.OfType<ClientEvent.EnteredRoom>().Last().RoomName);
        var elder = events.OfType<ClientEvent.ObjectAdded>().First(e => c.Resolve(e.Object.NameRsc).StartsWith("Ran er'Hoth")).Object;
        await c.MoveAsync(Room.Fine(79), Room.Fine(37), false);
        await c.SayAsync("Hast du Orkzähne?");
        await WaitForAsync(c, events, e => e is ClientEvent.Chat ch && ch.Text.Contains("Sammler"));
    }

    [Fact]
    public async Task CorNoth_WeaponsMaster_TeachesSkills()
    {
        var (c, events) = await EnterAsync(154);
        var rook = events.OfType<ClientEvent.ObjectAdded>().First(e => c.Resolve(e.Object.NameRsc).StartsWith("Rook")).Object;
        await c.RequestBuyAsync(rook.Id);
        await WaitForAsync(c, events, e => e is ClientEvent.BuyList);
        var list = events.OfType<ClientEvent.BuyList>().Last().Items;
        Assert.Contains(list, i => c.Resolve(i.Item.NameRsc) == "Langschwert");
        // Stufe 1 ist immer erlernbar (PlayerCanLearn), Preis 250 × 2^1.
        var mace = list.Single(i => c.Resolve(i.Item.NameRsc) == "Streitkolbenkampf");
        Assert.Equal(500u, mace.Price);
        Assert.DoesNotContain(list, i => c.Resolve(i.Item.NameRsc) == "Faustschlag");   // schon bekannt
        await c.BuyItemsAsync(rook.Id, [new ObjectRef(mace.Item.Id, null)]);
        await WaitForAsync(c, events, e => e is ClientEvent.Chat { Text: "Du hast Streitkolbenkampf gelernt." });
    }

    [Fact]
    public void Jasper_TownExitsLeadIntoJasper_ExceptSewerAndGuildHalls()
    {
        var world = WorldDefinition.LoadAll(Data("world"));
        var ids = world.Rooms.Select(r => r.Id).ToHashSet();
        Assert.Equal(23, world.Rooms.Count(r => r.Id is 350 or 382 or >= 351 and <= 376));
        int[] notYet = [377, 704, 710];   // Kanalisation, Gildenhallen
        foreach (var room in world.Rooms.Where(r => r.Id is 350 or 382 or >= 351 and <= 376))
            Assert.All(room.Exits.Where(e => !notYet.Contains(e.To)), e => Assert.Contains(e.To, ids));
    }

    [Fact]
    public async Task Jasper_BankerKeepsTheTosAccount()
    {
        var (c, events) = await EnterAsync(376);
        await c.MoveAsync(Room.Fine(7), Room.Fine(5), false);
        await c.DepositAsync(100);
        await WaitForAsync(c, events, e => e is ClientEvent.Chat ch && ch.Text.Contains("100 Schillinge"));
    }

    [Fact]
    public void Clothing_ColorsLikeEncodeTwoColorXlat()
    {
        var items = ItemCatalog.Load(Data("items", "items.json"));
        Assert.True(items.TryGet("shirtorange", out var orange));
        Assert.True(items.TryGet("pantsblack", out var black));
        Assert.True(items.TryGet("pantsgray", out var gray));
        Assert.Equal(0x87 + 11 * 4 + 1, orange.ClothingTranslation(1));   // XLAT_TO_ORANGE, Haut 1
        Assert.Equal(124 + 2, black.ClothingTranslation(2));              // XLAT_TO_BLACK: Sonderfall
        Assert.Equal(0x2B, gray.ClothingTranslation(3));                  // PT_GRAY_TO_KGRAY fest
        Assert.Equal("bfe.bgf", gray.LegsMale);
    }

    [Fact]
    public async Task Pub_SellsClothing_ThatCanBeWorn()
    {
        var (c, events) = await EnterAsync(1017);
        var eric = events.OfType<ClientEvent.ObjectAdded>().First(e => c.Resolve(e.Object.NameRsc).StartsWith("Eric")).Object;
        await c.RequestBuyAsync(eric.Id);
        await WaitForAsync(c, events, e => e is ClientEvent.BuyList);
        var shirt = events.OfType<ClientEvent.BuyList>().Last().Items.First(i => c.Resolve(i.Item.NameRsc) == "Hemd (orange)");
        await c.BuyItemsAsync(eric.Id, [new ObjectRef(shirt.Item.Id, null)]);
        await WaitForAsync(c, events, _ => c.Inventory.Any(o => c.Resolve(o.NameRsc) == "Hemd (orange)"));
        await c.UseAsync(c.Inventory.First(o => c.Resolve(o.NameRsc) == "Hemd (orange)").Id);
        await WaitForAsync(c, events, e => e is ClientEvent.Chat ch && ch.Text.StartsWith("Du ziehst") && ch.Text.EndsWith("Hemd (orange) an."));
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
            Shops = ShopFile.Merge(new[] { "raza", "marion", "cor", "jasper" }.Select(a => ShopFile.Load(Data("shops", a + ".json")))),
            Dialogs = DialogFile.Merge(new[] { "raza", "marion", "cor", "jasper" }.Select(a => DialogFile.Load(Data("dialog", a + ".json")))),
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
