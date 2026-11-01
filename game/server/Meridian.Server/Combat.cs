namespace Meridian.Server;

/// <summary>
/// Vereinfachte Kampfregeln (siehe docs/combat.md). Werte grob am Original orientiert:
/// Spieler starten mit 20 Lebenspunkten, Zombies sind langsam, Skelette schneller und stärker.
/// </summary>
public static class CombatRules
{
    public const int PlayerMaxHp = 20;
    public const int PlayerMinDamage = 2;          // Zwergenfäuste
    public const int PlayerMaxDamage = 6;
    public const double PlayerHitChance = 0.75;
    public static readonly TimeSpan PlayerAttackCooldown = TimeSpan.FromSeconds(1.2);
    /// <summary>Nahkampfreichweite in KOD-Feineinheiten (64 = 1 Feld).</summary>
    public const int MeleeRange = 96;
    /// <summary>Außerhalb des Kampfes: +1 Lebenspunkt alle 4 s.</summary>
    public static readonly TimeSpan RegenInterval = TimeSpan.FromSeconds(4);
    public static readonly TimeSpan OutOfCombat = TimeSpan.FromSeconds(8);
    public static readonly TimeSpan CorpseTime = TimeSpan.FromSeconds(2);
}

/// <summary>Monsterart mit Werten.</summary>
public sealed record MonsterKind(
    string Name,
    string Icon,
    int MaxHp,
    int MinDamage,
    int MaxDamage,
    double HitChance,
    TimeSpan AttackCooldown,
    int SpeedFinePerSecond,
    int AggroRange)
{
    // Zombie: SPEED_VERY_SLOW, Stufe 55, karma-aggressiv. Skelett: SPEED_AVERAGE, Stufe 75, aggressiv.
    public static readonly MonsterKind Zombie = new("Zombie", "zombie.bgf", 24, 1, 4, 0.6, TimeSpan.FromSeconds(2.4), 70, 5 * 64);
    public static readonly MonsterKind Skeleton = new("Skelett", "skeleton.bgf", 36, 2, 6, 0.65, TimeSpan.FromSeconds(1.8), 140, 7 * 64);
}

/// <summary>Monster in einem Raum.</summary>
public sealed class Monster(uint id, MonsterKind kind, Room room, GridPos home)
{
    /// <summary>Objekt-IDs der Monster beginnen hier (unter 2^28).</summary>
    public const uint ObjectIdBase = 0x00300000;

    public uint Id => id;
    public MonsterKind Kind => kind;
    public Room Room => room;
    public GridPos Home => home;
    public int Hp { get; set; } = kind.MaxHp;
    public ushort Row { get; set; } = Room.Fine(home.Row);
    public ushort Col { get; set; } = Room.Fine(home.Col);
    public ushort Angle { get; set; }
    public Avatar? Target { get; set; }
    public DateTime NextAttack { get; set; }
    public DateTime? DiedAt { get; set; }
    public bool Alive => DiedAt is null;
}
