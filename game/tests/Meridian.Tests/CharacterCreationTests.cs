using Meridian.Protocol;
using Meridian.Server;

namespace Meridian.Tests;

/// <summary>Charaktererstellung wie system.kod/player.kod: Name, Werte, Gesicht, Startsprüche.</summary>
public sealed class CharacterCreationTests
{
    private static string Data(params string[] parts) => Path.Combine([AppContext.BaseDirectory, "data", .. parts]);

    private readonly ResourceRegistry _res = new();
    private readonly World _world;

    public CharacterCreationTests()
    {
        _world = new World(new WorldDefinition(1, [new RoomDefinition(1, "R", "r.roo") { Teleport = new GridPos(5, 5) }]), _res, "bta.bgf",
            seed: 1, items: ItemCatalog.Load(Data("items", "items.json")))
        {
            Spells = SpellBook.Load(Data("spells", "spells.json")),
        };
    }

    private CharacterCreation.Request Request(string name = "Brokk", int gender = Appearance.Female, int[]? stats = null, uint[]? spells = null) =>
        new(1001, name, "Ein Zwerg aus den Bergen.", gender,
            [_res["phkx.bgf"], _res["ptdb.bgf"], _res["pelx.bgf"], _res["pnmx.bgf"], _res["pmkx.bgf"]],
            0x2C, 3, stats ?? [30, 30, 30, 30, 40, 40], spells ?? [2, 23]);

    [Fact]
    public void ValidRequest_CreatesCharacter_WithChosenLookStatsAndSpells()
    {
        Assert.True(_world.IsFirstTime(1001));
        Assert.True(_world.CreateCharacter(Request(), _ => false));
        Assert.False(_world.IsFirstTime(1001));
        Assert.Equal("Brokk", _world.CharacterName(1001));

        var save = _world.TakeCharactersToSave()[1001];
        Assert.Equal(new Appearance(Appearance.Female, "phkx.bgf", "ptdb.bgf", "pelx.bgf", "pnmx.bgf", "pmkx.bgf", 0x2C, 3), save.Profile!.Appearance);
        Assert.Equal(new Attributes(30, 30, 30, 30, 40, 40), save.Magic!.Attributes);
        Assert.Equal([2, 23], save.Magic.Spells.Keys.Order());
        Assert.Equal(15 + 40 / 5, save.Magic.MaxMana);
        // Startausrüstung plus Reagenzien für je 10 Zauber (Kraut für Heilung, blauer Pilz für Zap)
        Assert.Contains(save.Inventory, i => i.Item == "herbs" && i.Number == 10);
        Assert.Contains(save.Inventory, i => i.Item == "bluemushroom" && i.Number == 10);
    }

    [Theory]
    [InlineData("Al")]                // zu kurz
    [InlineData("Qor")]               // Göttername
    [InlineData("Brökk")]             // unerlaubtes Zeichen
    public void InvalidName_IsRejected(string name) => Assert.False(_world.CreateCharacter(Request(name), _ => false));

    [Fact]
    public void NameOfOtherAccount_IsRejected() => Assert.False(_world.CreateCharacter(Request(), n => n == "Brokk"));

    [Fact]
    public void HackedStats_FallBackToJunkCharacter()
    {
        Assert.True(_world.CreateCharacter(Request(stats: [50, 50, 50, 50, 50, 50]), _ => false));
        Assert.Equal(new Attributes(3, 1, 4, 1, 5, 9), _world.TakeCharactersToSave()[1001].Magic!.Attributes);
    }

    [Fact]
    public void UnknownFaceParts_AreReplacedByAllowedOnes()
    {
        var req = Request() with { Parts = [_res["phax.bgf"], 12345, 0, 0, 0] };   // Männerkopf bei einer Frau
        Assert.True(_world.CreateCharacter(req, _ => false));
        var look = _world.TakeCharactersToSave()[1001].Profile!.Appearance;
        Assert.Equal(("phkx.bgf", "ptcd.bgf", "pekx.bgf"), (look.Head, look.Hair, look.Eyes));
    }

    [Fact]
    public void CharInfo_ListsOfferedSpellsOnly()
    {
        byte[] info = CharacterCreation.CharInfo(s => _res[s], _world.Spells.All.ToList());
        Assert.Equal((byte)GameMessage.CharInfo, info[0]);
        Assert.Equal(14, info[1]);   // 14 Haarfarben
        Assert.DoesNotContain(_world.Spells.All, s => s.Key == "hospice" && CharacterCreation.Offered(s));
    }
}

/// <summary>Erstellung über das Netz mit der ClientSession (wie der Godot-Client).</summary>
public sealed class CharacterCreationFlowTests : IAsyncLifetime
{
    private static string Data(params string[] parts) => Path.Combine([AppContext.BaseDirectory, "data", .. parts]);

    private readonly string _db = Path.Combine(Path.GetTempPath(), $"meridian-create-{Guid.NewGuid():N}.db");
    private readonly CancellationTokenSource _cts = new(TimeSpan.FromSeconds(30));
    private GameServer _server = null!;
    private Task _run = null!;

    public Task InitializeAsync()
    {
        var accounts = AccountStore.Open(_db);
        accounts.AddOrUpdate("Neuling", "pw");
        _server = new GameServer(new ServerOptions
        {
            Port = 0,
            World = new WorldDefinition(1, [new RoomDefinition(1, "R", "r.roo") { Teleport = new GridPos(5, 5) }]),
            Items = ItemCatalog.Load(Data("items", "items.json")),
            Spells = SpellBook.Load(Data("spells", "spells.json")),
            Skills = SkillBook.Load(Data("skills", "skills.json")),
            Database = _db,
        }, accounts);
        _server.Start();
        _run = _server.RunAsync(_cts.Token);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _cts.Cancel();
        await _run;
        foreach (var f in new[] { _db, _db + "-wal", _db + "-shm" })
            File.Delete(f);
    }

    [Fact]
    public async Task NewAccount_CreatesCharacter_AndEntersWorldWithChosenName()
    {
        using var c = new ClientSession { CreateNewCharacters = true };
        var events = new List<ClientEvent>();
        c.Event += events.Add;
        await c.ConnectAsync("127.0.0.1", _server.Port, "Neuling", "pw");
        await WaitForAsync(c, events, e => e is ClientEvent.CharacterCreation);
        var choices = events.OfType<ClientEvent.CharacterCreation>().Single().Choices;
        Assert.Equal(14, choices.HairColors.Length);
        Assert.Contains(choices.Spells, s => s.Name == "Kleine Heilung" && s.Cost == 10);
        Assert.Contains(choices.Skills, s => s.Name == "Faustschlag");

        var f = choices.Female;
        await c.CreateCharacterAsync(choices.CharacterId, "Dis", "Eine Schmiedin.", 2,
            [f.Head, f.Hair[1], f.Eyes[0], f.Noses[0], f.Mouths[0]], choices.HairColors[2], choices.SkinColors[1],
            [30, 30, 30, 30, 40, 40], [choices.Spells.First(s => s.Name == "Kleine Heilung").Id], [430]);
        await WaitForAsync(c, events, e => e is ClientEvent.EnteredRoom);
        await WaitForAsync(c, events, e => e is ClientEvent.ObjectAdded a && a.Object.Id == c.PlayerId);
        var self = events.OfType<ClientEvent.ObjectAdded>().Last(e => e.Object.Id == c.PlayerId).Object;
        Assert.Equal("Dis", c.Resolve(self.NameRsc));
        Assert.Equal("btb.bgf", self.Icon);
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
