namespace Meridian.Server;

/// <summary>
/// Kampfregeln nach dem Original (docs/combat-original.md, docs/combat.md).
/// </summary>
public static class CombatRules
{
    public const int PlayerMaxHp = 20;
    /// <summary>Faustschlag ohne Waffe: Random(1,4) (stroke/unarmed.kod).</summary>
    public const int PlayerMinDamage = 1;
    public const int PlayerMaxDamage = 4;
    /// <summary>IsOkayAttackTime: ein Schlag pro Sekunde, unabhängig von der Waffe.</summary>
    public static readonly TimeSpan PlayerAttackCooldown = TimeSpan.FromSeconds(1.0);
    /// <summary>Nahkampfreichweite in KOD-Feineinheiten (64 = 1 Feld): Waffenreichweite 2 Felder.</summary>
    public const int MeleeRange = 128;
    /// <summary>EQUAL_CHANCE_HIT (battler.kod): Trefferchance bei gleicher Offensive und Defensive.</summary>
    public const int EqualChanceHit = 55;

    /// <summary>battler.kod TryAttack: bound(Offensive × 55 / Defensive, 10, 95) Prozent.</summary>
    public static int HitChance(int offense, int defense) =>
        Math.Clamp(offense * EqualChanceHit / Math.Max(defense, 1), 10, 95);

    /// <summary>monster.kod Fuzzy(n): n − n/4 + Random(0, n/2).</summary>
    public static int Fuzzy(Random rnd, int n) => n - n / 4 + rnd.Next(0, n * 2 / 4 + 1);

    /// <summary>Resistenz anwenden (battler.kod GetDamageFromResistance): × (100 − Resistenz) / 100.</summary>
    public static int Resist(int damage, int resistance) => damage * (100 - Math.Clamp(resistance, -100, 100)) / 100;
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
    /// <summary>Monsterwerte nach monster.kod, wenn nicht ausdrücklich gesetzt (Stufe/Schwierigkeit > 0).</summary>
    public int Offense => OffenseOverride ?? Math.Clamp(3 * Level + 60 * Difficulty, 1, 1500);
    public int Defense => DefenseOverride ?? Math.Clamp(3 * Level + 60 * Difficulty, 1, 1500);
    public int? OffenseOverride { get; init; }
    public int? DefenseOverride { get; init; }
    /// <summary>viSpeed (SPEED_VERY_SLOW 4, SPEED_AVERAGE 12) für den Angriffstakt.</summary>
    public int KodSpeed { get; init; }
    /// <summary>Resistenzen in Prozent je Angriffsart ("bludgeon", "thrust", "slash", "holy", "unholy", "fire", "cold", "shock", "acid").</summary>
    public IReadOnlyDictionary<string, int> Resistances { get; init; } = new Dictionary<string, int>();

    /// <summary>Schaden eines Treffers: feste Spanne, sonst Fuzzy(Stufe / Random(10,15)).</summary>
    public int RollDamage(Random rnd) =>
        MaxDamage > 0 ? rnd.Next(MinDamage, MaxDamage + 1) : Math.Max(1, CombatRules.Fuzzy(rnd, Level / rnd.Next(10, 16)));

    /// <summary>Angriffstakt: fester Wert, sonst Fuzzy(1000) + 3500 − 70 × (3 × Schwierigkeit + Tempo) ms.</summary>
    public TimeSpan RollAttackTime(Random rnd) => Level > 0
        ? TimeSpan.FromMilliseconds(CombatRules.Fuzzy(rnd, 1000) + 3500 - 70 * (3 * Difficulty + KodSpeed))
        : AttackCooldown;

    /// <summary>Lebenspunkte beim Erschaffen (Fuzzy um den Höchstwert, wenn Stufe gesetzt).</summary>
    public int RollHitPoints(Random rnd) => Level > 0 ? Math.Max(1, CombatRules.Fuzzy(rnd, MaxHp)) : MaxHp;

    /// <summary>viLevel und viDifficulty (bestimmen Anzahl der Beutestücke und Geldmenge).</summary>
    public int Level { get; init; }
    public int Difficulty { get; init; }
    /// <summary>Beutetabelle (viTreasure_type) aus data/items/items.json; null = keine Beute.</summary>
    public string? Treasure { get; init; }
    /// <summary>Gegenstand, der immer fallen gelassen wird (Skelett: Langschwert).</summary>
    public string? AlwaysDrops { get; init; }

    // Zombie: SPEED_VERY_SLOW, Stufe 55, karma-aggressiv. Skelett: SPEED_AVERAGE, Stufe 75, aggressiv.
    // Max. LP = 120 × Stufe / 100 (GetMaxHitPoints); Schaden und Takt aus Stufe/Schwierigkeit/Tempo.
    public static readonly MonsterKind Zombie = new("Zombie", "zombie.bgf", 66, 0, 0, 0, TimeSpan.Zero, 70, 5 * 64)
    {
        Level = 55, Difficulty = 4, KodSpeed = 4, Treasure = "zombie",
        Resistances = new Dictionary<string, int> { ["unholy"] = 50, ["holy"] = -25 },
    };
    public static readonly MonsterKind Skeleton = new("Skelett", "skelbody.bgf", 90, 0, 0, 0, TimeSpan.Zero, 140, 7 * 64)
    {
        Level = 75, Difficulty = 5, KodSpeed = 12, Treasure = "tough", AlwaysDrops = "longsword",
        Resistances = new Dictionary<string, int>
        {
            ["thrust"] = 70, ["shock"] = 70, ["cold"] = 70, ["unholy"] = 70, ["fire"] = -10, ["holy"] = -20, ["bludgeon"] = -20,
        },
    };
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
    public int MaxHp { get; set; } = kind.MaxHp;
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
