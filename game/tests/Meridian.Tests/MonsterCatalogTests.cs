using Meridian.Server;

namespace Meridian.Tests;

/// <summary>Monsterarten aus data/monsters/monsters.json mit den Formeln aus monster.kod.</summary>
public sealed class MonsterCatalogTests
{
    [Fact]
    public void Catalog_HasAllCombatMonstersOfTheOriginal()
    {
        Assert.True(MonsterKind.All.Count >= 60);
        foreach (string key in new[] { "giantrat", "spider", "orc", "troll", "ent", "groundworm", "lich", "yeti", "zombie", "cow" })
            Assert.True(MonsterKind.All.ContainsKey(key), key);
    }

    [Fact]
    public void Ghost_ShrugsOffOrdinaryWeapons_ButNotEnchantedOnes()
    {
        var ghost = MonsterKind.ByKey("ghost");
        Assert.Equal(90, ghost.ResistanceTo(["slash", "nonmagic"]));     // gewöhnliche Waffe
        Assert.Equal(-50, ghost.ResistanceTo(["slash", "magic"]));       // Waffe verzaubern
        Assert.Equal(40, ghost.ResistanceTo(["slash", "nonmagic", "holy"]));   // Heilige Waffe: 90 + (−50)
    }

    [Fact]
    public void Values_FollowMonsterKod()
    {
        var rat = MonsterKind.ByKey("giantrat");       // Stufe 30, Schwierigkeit 1, SPEED_FAST
        Assert.Equal(30, rat.MaxHp);                   // unter Stufe 40: Stufe
        Assert.Equal(150, rat.Offense);                // 3 × 30 + 60 × 1
        Assert.Equal(4 * 64, rat.AggroRange);          // Sicht 4 + 1/2 Felder
        Assert.Equal(180, rat.SpeedFinePerSecond);
        Assert.True(rat.Aggressive);
        Assert.Equal("rat", rat.Treasure);
        Assert.Equal(90, MonsterKind.Skeleton.MaxHp);  // 75 × 120 / 100
        Assert.True(MonsterKind.Cow.Peaceful);
        Assert.True(MonsterKind.ByKey("ent").Immobile);
        Assert.True(MonsterKind.Zombie.KarmaAggressive);
        Assert.NotNull(MonsterKind.ByKey("spider").Poison);
    }

    [Fact]
    public void Resistance_TakesLargestPlusSmallest()
    {
        var spider = MonsterKind.ByKey("spider");      // spell_all 50
        Assert.Equal(50, spider.ResistanceTo("fire"));
        Assert.Equal(0, spider.ResistanceTo("slash"));
        var rat = MonsterKind.ByKey("giantrat");       // Feuer −20, stumpf −10, Hieb +20
        Assert.Equal(-20, rat.ResistanceTo("fire"));
        Assert.Equal(20, rat.ResistanceTo("slash"));
    }
}
