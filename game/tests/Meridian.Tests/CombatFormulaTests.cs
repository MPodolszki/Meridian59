using Meridian.Server;

namespace Meridian.Tests;

/// <summary>Kampfformeln nach dem Original (docs/combat-original.md).</summary>
public sealed class CombatFormulaTests
{
    [Theory]
    [InlineData(405, 405, 55)]     // gleiche Werte → EQUAL_CHANCE_HIT
    [InlineData(180, 405, 24)]
    [InlineData(10, 1000, 10)]     // Untergrenze
    [InlineData(1000, 100, 95)]    // Obergrenze
    public void HitChance_FollowsBattlerTryAttack(int offense, int defense, int chance) =>
        Assert.Equal(chance, CombatRules.HitChance(offense, defense));

    [Fact]
    public void MonsterValues_FromLevelAndDifficulty()
    {
        Assert.Equal(405, MonsterKind.Zombie.Offense);
        Assert.Equal(525, MonsterKind.Skeleton.Defense);
        var rnd = new Random(1);
        for (int i = 0; i < 200; i++)
        {
            Assert.InRange(MonsterKind.Zombie.RollHitPoints(rnd), 50, 83);           // Fuzzy(66)
            Assert.InRange(MonsterKind.Zombie.RollDamage(rnd), 2, 6);                // Fuzzy(55 / 10..15)
            Assert.InRange(MonsterKind.Skeleton.RollDamage(rnd), 3, 9);
            Assert.InRange(MonsterKind.Zombie.RollAttackTime(rnd).TotalMilliseconds, 3130, 3630);
            Assert.InRange(MonsterKind.Skeleton.RollAttackTime(rnd).TotalMilliseconds, 2360, 2860);
        }
    }

    [Theory]
    [InlineData(10, 70, 3)]        // Skelett gegen Stich
    [InlineData(10, -20, 12)]      // Skelett gegen Wucht
    [InlineData(10, 0, 10)]
    public void Resistance_ScalesDamage(int damage, int resistance, int expected) =>
        Assert.Equal(expected, CombatRules.Resist(damage, resistance));
}
