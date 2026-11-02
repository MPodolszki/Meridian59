using Meridian.Protocol;
using Meridian.Server;

namespace Meridian.Tests;

/// <summary>Inventar, Geld und Handel mit den Tos-Händlern (echte Daten aus data/items und data/shops).</summary>
public sealed class TradeTests : IAsyncLifetime
{
    // Alle drei Händler in einem Raum, damit kein Raumwechsel nötig ist.
    private static readonly WorldDefinition Market = new(1,
    [
        new RoomDefinition(1, "Markt", "tosinn.roo")
        {
            Teleport = new GridPos(10, 10),
            Npcs =
            [
                new NpcDefinition("TosInnKeeper", 10, 11) { Name = "Paddock" },
                new NpcDefinition("TosApothecary", 11, 10) { Name = "Frisconar" },
                new NpcDefinition("TosBanker", 9, 10) { Name = "Skivlat" },
            ],
        },
    ]);

    private static readonly MonsterKind Piñata = new("Zombie", "zombie.bgf", 1, 1, 1, 0.0, TimeSpan.FromHours(1), 0, 64)
        { Level = 55, Difficulty = 4, Treasure = "zombie" };

    private static string Data(params string[] parts) => Path.Combine([AppContext.BaseDirectory, "data", .. parts]);

    private readonly string _accountsPath = Path.Combine(Path.GetTempPath(), $"meridian-accounts-{Guid.NewGuid():N}.db");
    private readonly CancellationTokenSource _cts = new(TimeSpan.FromSeconds(30));
    private GameServer _server = null!;
    private Task _serverTask = null!;

    public Task InitializeAsync()
    {
        var accounts = AccountStore.Open(_accountsPath);
        accounts.AddOrUpdate("Anna", "pw");
        _server = new GameServer(new ServerOptions
        {
            Port = 0,
            World = Market,
            RandomSeed = 7,
            Items = ItemCatalog.Load(Data("items", "items.json")),
            Shops = ShopFile.Load(Data("shops", "tos.json")),
        }, accounts);
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
    public async Task NewCharacter_StartsWithMaceAndMoney()
    {
        var (anna, events) = await EnterAsync();
        await WaitForAsync(anna, events, _ => Money(anna) == 3500);
        Assert.Contains(anna.Inventory, o => anna.Resolve(o.NameRsc) == "Streitkolben");
    }

    [Fact]
    public async Task Buy_FromInnkeeper_UsesDiscountPrices()
    {
        var (anna, events) = await EnterAsync();
        uint paddock = Npc(anna, events, "Paddock");
        await anna.RequestBuyAsync(paddock);
        await WaitForAsync(anna, events, e => e is ClientEvent.BuyList);
        var list = events.OfType<ClientEvent.BuyList>().Last();

        // GetForSale baut die Liste per cons → umgekehrte Reihenfolge; MERCHANT_DISCOUNT = Wert × 1,4.
        Assert.Equal(["Laibe Käse", "Krug Starkbier"], list.Items.Select(i => anna.Resolve(i.Item.NameRsc)));
        Assert.Equal([112u, 42u], list.Items.Select(i => i.Price));
        Assert.Equal(3u, list.Items[0].Item.Amount);

        var cheese = list.Items[0].Item;
        await anna.BuyItemsAsync(paddock, [new ObjectRef(cheese.Id, 2)]);
        await WaitForAsync(anna, events, _ => Money(anna) == 3500 - 224);
        Assert.Contains(anna.Inventory, o => anna.Resolve(o.NameRsc) == "Laibe Käse" && o.Amount == 2);
    }

    [Fact]
    public async Task Buy_TooExpensive_IsRefused()
    {
        var (anna, events) = await EnterAsync();
        uint paddock = Npc(anna, events, "Paddock");
        await anna.RequestBuyAsync(paddock);
        await WaitForAsync(anna, events, e => e is ClientEvent.BuyList);
        var cheese = events.OfType<ClientEvent.BuyList>().Last().Items[0].Item;

        await anna.BuyItemsAsync(paddock, [new ObjectRef(cheese.Id, 500)]);
        await WaitForAsync(anna, events, e => e is ClientEvent.Chat c && c.Text.Contains("Komm wieder"));
        Assert.Equal(3500, Money(anna));
    }

    [Fact]
    public async Task ConditionalSale_AppearsWhenMentioned()
    {
        var (anna, events) = await EnterAsync();
        uint paddock = Npc(anna, events, "Paddock");
        await anna.SayAsync("Hast du Purpurpilze?");
        await WaitForAsync(anna, events, e => e is ClientEvent.Chat c && c.Text.StartsWith("Paddock: Ja, ich hab 4 Purpurpilze"));
        await anna.RequestBuyAsync(paddock);
        await WaitForAsync(anna, events, e => e is ClientEvent.BuyList);
        Assert.Contains(events.OfType<ClientEvent.BuyList>().Last().Items, i => i.Price == 250 && i.Item.Amount == 4);
    }

    [Fact]
    public async Task Sell_ReagentsToApothecary_ForCounterOffer()
    {
        var (anna, events) = await EnterAsync();
        uint frisconar = Npc(anna, events, "Frisconar");
        await anna.RequestBuyAsync(frisconar);
        await WaitForAsync(anna, events, e => e is ClientEvent.BuyList);
        var herbs = events.OfType<ClientEvent.BuyList>().Last().Items.Single(i => anna.Resolve(i.Item.NameRsc) == "Kräuter");
        Assert.Equal(14u, herbs.Price);
        await anna.BuyItemsAsync(frisconar, [new ObjectRef(herbs.Item.Id, 5)]);
        await WaitForAsync(anna, events, _ => Money(anna) == 3500 - 70);

        var mine = anna.Inventory.Single(o => anna.Resolve(o.NameRsc) == "Kräuter");
        await anna.OfferAsync(frisconar, [new ObjectRef(mine.Id, 3)]);
        // Ankauf: Wert × (100 − 10 × Aufschlag) / 100 = 30 × 0,8
        await WaitForAsync(anna, events, e => e is ClientEvent.CounterOffer c && c.Items.Single().Amount == 24);
        await anna.AcceptOfferAsync();
        await WaitForAsync(anna, events, _ => Money(anna) == 3500 - 70 + 24);
        Assert.Contains(anna.Inventory, o => anna.Resolve(o.NameRsc) == "Kräuter" && o.Amount == 2);
    }

    [Fact]
    public async Task Offer_UnwantedItem_IsRefused()
    {
        var (anna, events) = await EnterAsync();
        uint frisconar = Npc(anna, events, "Frisconar");
        await WaitForAsync(anna, events, _ => Money(anna) == 3500);
        var mace = anna.Inventory.Single(o => anna.Resolve(o.NameRsc) == "Streitkolben");
        await anna.OfferAsync(frisconar, [new ObjectRef(mace.Id, null)]);
        await WaitForAsync(anna, events, e => e is ClientEvent.Chat c && c.Text.Contains("kein Interesse"));
        Assert.DoesNotContain(events, e => e is ClientEvent.CounterOffer);
    }

    [Fact]
    public async Task Bank_DepositBalanceWithdraw()
    {
        var (anna, events) = await EnterAsync();
        await WaitForAsync(anna, events, _ => Money(anna) == 3500);
        await anna.DepositAsync(1000);
        await WaitForAsync(anna, events, e => e is ClientEvent.Chat c && c.Text.Contains("jetzt 1000 Schillinge"));
        Assert.Equal(2500, Money(anna));

        await anna.WithdrawAsync(5000);
        await WaitForAsync(anna, events, e => e is ClientEvent.Chat c && c.Text.Contains("nur 1000 Schillinge auf deinem Konto"));

        await anna.WithdrawAsync(400);
        await WaitForAsync(anna, events, _ => Money(anna) == 2900);
        await anna.BalanceAsync();
        await WaitForAsync(anna, events, e => e is ClientEvent.Chat c && c.Text.Contains("600 Schillinge auf deinem Konto"));
    }

    [Fact]
    public async Task DeadMonster_DropsTreasure_ThatCanBePickedUp()
    {
        var (anna, events) = await EnterAsync();
        await WaitForAsync(anna, events, _ => Money(anna) == 3500);
        var monster = _server.World.SpawnMonster(1, Piñata, new GridPos(10, 9));
        while (!events.Any(e => e is ClientEvent.ObjectAdded { Object.IsGettable: true }))
        {
            await anna.AttackAsync(monster.Id);
            await Task.Delay(300, _cts.Token);
            anna.ProcessEvents();
        }
        var loot = events.OfType<ClientEvent.ObjectAdded>().First(e => e.Object.IsGettable).Object;
        await anna.GetAsync(loot.Id);
        await WaitForAsync(anna, events, e => e is ClientEvent.ObjectRemoved r && r.Id == loot.Id);
        await WaitForAsync(anna, events, e => e is ClientEvent.Chat c && c.Text.StartsWith("Du hast") && c.Text.EndsWith("aufgehoben."));
    }

    [Fact]
    public async Task Drop_PartOfMoney_LiesOnTheFloor()
    {
        var (anna, events) = await EnterAsync();
        await WaitForAsync(anna, events, _ => Money(anna) == 3500);
        var money = anna.Inventory.Single(o => o.Icon == "coin.bgf");
        await anna.DropAsync([new ObjectRef(money.Id, 100)]);
        await WaitForAsync(anna, events, e => e is ClientEvent.ObjectAdded { Object: { IsGettable: true, Amount: 100 } });
        Assert.Equal(3400, Money(anna));
    }

    private static int Money(ClientSession s) => (int)(s.Inventory.FirstOrDefault(o => o.Icon == "coin.bgf")?.Amount ?? 0);

    private static uint Npc(ClientSession s, List<ClientEvent> events, string name) =>
        events.OfType<ClientEvent.ObjectAdded>().First(e => s.Resolve(e.Object.NameRsc).StartsWith(name)).Object.Id;

    private async Task<(ClientSession, List<ClientEvent>)> EnterAsync()
    {
        var anna = new ClientSession();
        var events = new List<ClientEvent>();
        anna.Event += events.Add;
        await anna.ConnectAsync("127.0.0.1", _server.Port, "Anna", "pw");
        await WaitForAsync(anna, events, e => e is ClientEvent.EnteredRoom);
        await WaitForAsync(anna, events, e => e is ClientEvent.ObjectAdded a && a.Object.Id == anna.PlayerId);
        await WaitForAsync(anna, events, e => e is ClientEvent.InventoryChanged);
        return (anna, events);
    }

    private async Task WaitForAsync(ClientSession s, List<ClientEvent> events, Func<ClientEvent?, bool> match)
    {
        while (!events.Any(match) && !match(null))
        {
            _cts.Token.ThrowIfCancellationRequested();
            await Task.Delay(10);
            s.ProcessEvents();
        }
    }
}
