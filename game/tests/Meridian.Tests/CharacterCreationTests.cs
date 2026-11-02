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
