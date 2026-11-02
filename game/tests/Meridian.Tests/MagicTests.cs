using Meridian.Protocol;
using Meridian.Server;

namespace Meridian.Tests;

/// <summary>Magie: Startsprüche, Mana, Zaubern mit Kosten/Trance, Lernregeln (echte Daten aus data/spells).</summary>
public sealed class MagicTests : IAsyncLifetime
{
    private static readonly WorldDefinition Shop = new(1,
    [
        new RoomDefinition(1, "Apotheke", "tosapoth.roo")
        {
            Teleport = new GridPos(10, 10),
            Npcs = [new NpcDefinition("TosApothecary", 11, 10) { Name = "Frisconar" }],
        },
    ]);

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
            World = Shop,
            RandomSeed = 3,
            Items = ItemCatalog.Load(Data("items", "items.json")),
            Shops = ShopFile.Load(Data("shops", "tos.json")),
            Spells = SpellBook.Load(Data("spells", "spells.json")),
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
    public async Task NewCharacter_KnowsStartingSpells_WithFullMana()
    {
        var (anna, events) = await EnterAsync();
        await WaitForAsync(anna, events, _ => anna.KnownSpells.Count == 3 && anna.KnownSpells.All(s => s.Ability is >= 1 and <= 25));
        // Mana: 15 + Mystik 40 / 5 = 23
        await WaitForAsync(anna, events, e => e is ClientEvent.ManaChanged { Mana: 23, MaxMana: 23 });
        Assert.Contains(anna.KnownSpells, s => anna.Resolve(s.Spell.NameRsc) == "Kleine Heilung" && s.Targets == 1 && s.School == 1);
        // Reagenzien für je 10 Zauber: Kräuter 1 (Heilung) + 2 (Nahrung) je Zauber
        Assert.Contains(anna.Inventory, o => anna.Resolve(o.NameRsc) == "Kräuter" && o.Amount == 30);
    }

    [Fact]
    public async Task MinorHeal_OnHealthyTarget_IsRefused()
    {
        var (anna, events) = await EnterAsync();
        await WaitForAsync(anna, events, _ => anna.KnownSpells.Count == 3);
        var heal = anna.KnownSpells.Single(s => s.Targets == 1);
        await anna.CastAsync(heal.Spell.Id, [anna.PlayerId]);
        await WaitForAsync(anna, events, e => e is ClientEvent.Chat { Text: "Du bist kerngesund." });
    }

    [Fact]
    public async Task CreateFood_TakesTrance_ThenSucceedsOrFails()
    {
        var (anna, events) = await EnterAsync();
        await WaitForAsync(anna, events, e => e is ClientEvent.ManaChanged { Mana: 23 });
        await WaitForAsync(anna, events, _ => anna.KnownSpells.Count == 3);
        var food = anna.KnownSpells.Single(s => anna.Resolve(s.Spell.NameRsc) == "Nahrung erschaffen");
        await anna.CastAsync(food.Spell.Id, []);
        await WaitForAsync(anna, events, e => e is ClientEvent.Chat { Text: "Du konzentrierst dich ganz auf Nahrung erschaffen." });
        await WaitForAsync(anna, events, e => e is ClientEvent.Chat c && (c.Text.EndsWith("erscheint.") || c.Text.Contains("misslungen")));
        bool success = events.Any(e => e is ClientEvent.Chat c && c.Text.EndsWith("erscheint."));
        // Erfolg kostet 10 Mana und Reagenzien, Misserfolg die Hälfte und keine.
        await WaitForAsync(anna, events, e => e is ClientEvent.ManaChanged { Mana: var m } && m == (success ? 13 : 18));
        if (success)
            Assert.Contains(anna.Inventory, o => anna.Resolve(o.NameRsc) == "Kräuter" && o.Amount == 28);
    }

    [Fact]
    public async Task Zap_ChargesTheHands()
    {
        var (anna, events) = await EnterAsync();
        await WaitForAsync(anna, events, e => e is ClientEvent.ManaChanged { Mana: 23 });
        await WaitForAsync(anna, events, _ => anna.KnownSpells.Count == 3);
        var zap = anna.KnownSpells.Single(s => anna.Resolve(s.Spell.NameRsc) == "Zap");
        await anna.CastAsync(zap.Spell.Id, []);
        await WaitForAsync(anna, events, e => e is ClientEvent.Chat c && (c.Text.StartsWith("Funken springen") || c.Text.Contains("misslungen")));
    }

    [Fact]
    public async Task Hospice_IsNotOffered_WithoutBasis()
    {
        var (anna, events) = await EnterAsync();
        uint frisconar = events.OfType<ClientEvent.ObjectAdded>().First(e => anna.Resolve(e.Object.NameRsc).StartsWith("Frisconar")).Object.Id;
        await anna.RequestBuyAsync(frisconar);
        await WaitForAsync(anna, events, e => e is ClientEvent.BuyList);
        Assert.DoesNotContain(events.OfType<ClientEvent.BuyList>().Last().Items, i => anna.Resolve(i.Item.NameRsc) == "Hospiz");
    }

    [Fact]
    public void LearnRules_FollowPlayerCanLearn()
    {
        var book = new SpellBook(
        [
            new SpellDefinition("a1", 101, "A1", "x.bgf", 1, 1, 3),
            new SpellDefinition("a2", 102, "A2", "x.bgf", 1, 2, 5),
            new SpellDefinition("b2", 103, "B2", "x.bgf", 1, 2, 5),
            new SpellDefinition("c3", 104, "C3", "x.bgf", 1, 3, 10),
        ]);
        var world = new World(new WorldDefinition(1, [new RoomDefinition(1, "R", "r.roo")]), new ResourceRegistry(), "bta.bgf", seed: 1)
            { Spells = book };
        var state = new CharacterState();

        Assert.Null(world.CannotLearn(state, book["a1"]!));                       // Stufe 1 immer
        Assert.NotNull(world.CannotLearn(state, book["c3"]!));                    // keine Stufe-2-Grundlage
        state.Spells[102] = 90;
        state.Spells[103] = 90;
        Assert.Null(world.CannotLearn(state, book["c3"]!));                       // 180 ≥ benötigte Punkte
        state.Spells[102] = 20;
        state.Spells[103] = 20;
        Assert.NotNull(world.CannotLearn(state, book["c3"]!));                    // zu wenig geübt
        Assert.Equal("Diesen Spruch kennst du bereits.", world.CannotLearn(state, book["a2"]!));
    }

    private async Task<(ClientSession, List<ClientEvent>)> EnterAsync()
    {
        var anna = new ClientSession();
        var events = new List<ClientEvent>();
        anna.Event += events.Add;
        await anna.ConnectAsync("127.0.0.1", _server.Port, "Anna", "pw");
        await WaitForAsync(anna, events, e => e is ClientEvent.EnteredRoom);
        await WaitForAsync(anna, events, e => e is ClientEvent.InventoryChanged);
        // Der Godot-Client fordert die Sprüche wie der Original-Client an (BP_SEND_SPELLS).
        await anna.RequestSpellsAsync();
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
