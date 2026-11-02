namespace Meridian.Server;

/// <summary>
/// Vereinfachte Kampfregeln (siehe docs/combat.md). Werte grob am Original orientiert:
/// Spieler starten mit 20 Lebenspunkten, Zombies sind langsam, Skelette schneller und stärker.
/// </summary>
public static class CombatRules
{
    public const int PlayerMaxHp = 20;
    /// <summary>Faustschlag ohne Waffe: Random(1,4) (stroke/unarmed.kod).</summary>
    public const int PlayerMinDamage = 1;
    public const int PlayerMaxDamage = 4;
    public const double PlayerHitChance = 0.75;
    public static readonly TimeSpan PlayerAttackCooldown = TimeSpan.FromSeconds(1.2);
    /// <summary>Nahkampfreichweite in KOD-Feineinheiten (64 = 1 Feld).</summary>
    public const int MeleeRange = 96;
    /// <summary>Außerhalb des Kampfes: +1 Lebenspunkt alle 4 s.</summary>
    public static readonly TimeSpan RegenInterval = TimeSpan.FromSeconds(4);
    public static readonly TimeSpan OutOfCombat = TimeSpan.FromSeconds(8);
    public static readonly TimeSpan CorpseTime = TimeSpan.FromSeconds(2);

    // Ausdauer (player.kod): Start 100, max. 200, Erholung im Stand bis 80.
    public const int StartVigor = 100;
    public const int MaxVigor = 200;
    public const int VigorRestThreshold = 80;
    /// <summary>Ab so viel angesammelter Anstrengung wird Ausdauer abgezogen (MIN_VIGOR_CHANGE).</summary>
    public const int MinVigorChange = 20000;
    /// <summary>EXERTION_PER_MOVE (user.kod): Anstrengung pro Bewegung = 2 × (Tempo·5/6)².</summary>
    public const int ExertionPerMove = 2;
    /// <summary>Anstrengung pro Schlag: 1000 × viSkillExertion (Standard 2).</summary>
    public const int ExertionPerAttack = 2000;
    /// <summary>Ausdauer, die man für einen Schlag mindestens braucht (HasVigor).</summary>
    public const int AttackVigor = 2;
    /// <summary>Annahme für das noch fehlende Attribut Stamina (1..50).</summary>
    public const int Stamina = 25;
    /// <summary>FOOD_USE_RATE: Magen leert sich um 0,12 pro Sekunde.</summary>
    public const double StomachPerSecond = 12 / 100.0;

    /// <summary>player.kod CalculateHealthTime: Zeit bis zum nächsten Lebenspunkt, schneller mit mehr Ausdauer.</summary>
    public static TimeSpan HealthTime(int vigor, int maxHp)
    {
        int ms = (200 - vigor) * (200 - vigor) / 6 + 1000;
        ms = (125 - Stamina) * ms / 100;
        ms = ms * 100 / Math.Clamp(maxHp, 40, 100);
        return TimeSpan.FromMilliseconds(ms);
    }

    /// <summary>player.kod GetRestTime: Zeit bis zum nächsten Ausdauerpunkt beim Ausruhen.</summary>
    public static TimeSpan RestTime => TimeSpan.FromMilliseconds(1000 + 30 * (51 - Stamina));

    /// <summary>weapon.kod GetBaseDamage: Grundschaden nach Waffentyp plus Qualität.</summary>
    public static (int Min, int Max) WeaponDamage(ItemKind weapon)
    {
        var (min, max) = weapon.WeaponType switch
        {
            "bludgeon" => (4, 8),   // WEAPON_DAMAGE_MID
            "thrust" => (3, 8),     // WEAPON_DAMAGE_LOW
            "slash" => (5, 11),     // WEAPON_DAMAGE_HIGH
            _ => (PlayerMinDamage, PlayerMaxDamage),
        };
        int bonus = weapon.WeaponQuality switch { "low" => -1, "high" => 1, _ => 0 };
        return (min + bonus, max + bonus);
    }
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
    /// <summary>viLevel und viDifficulty (bestimmen Anzahl der Beutestücke und Geldmenge).</summary>
    public int Level { get; init; }
    public int Difficulty { get; init; }
    /// <summary>Beutetabelle (viTreasure_type) aus data/items/items.json; null = keine Beute.</summary>
    public string? Treasure { get; init; }
    /// <summary>Gegenstand, der immer fallen gelassen wird (Skelett: Langschwert).</summary>
    public string? AlwaysDrops { get; init; }

    // Zombie: SPEED_VERY_SLOW, Stufe 55, karma-aggressiv. Skelett: SPEED_AVERAGE, Stufe 75, aggressiv.
    public static readonly MonsterKind Zombie = new("Zombie", "zombie.bgf", 24, 1, 4, 0.6, TimeSpan.FromSeconds(2.4), 70, 5 * 64)
        { Level = 55, Difficulty = 4, Treasure = "zombie" };
    public static readonly MonsterKind Skeleton = new("Skelett", "skeleton.bgf", 36, 2, 6, 0.65, TimeSpan.FromSeconds(1.8), 140, 7 * 64)
        { Level = 75, Difficulty = 5, Treasure = "tough", AlwaysDrops = "longsword" };
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
    /// <summary>Aktueller A*-Weg (Felder) und sein Zielfeld.</summary>
    public List<GridPos>? Path { get; set; }
    public GridPos? PathGoal { get; set; }
    public DateTime NextAttack { get; set; }
    public DateTime? DiedAt { get; set; }
    public bool Alive => DiedAt is null;
}
