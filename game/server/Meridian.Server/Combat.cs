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

/// <summary>Monsterart aus data/monsters/monsters.json (erzeugt mit tools/make_monsters.py aus den KOD-Klassen).
/// Lebenspunkte, Treffer, Schaden, Takt, Sicht und Tempo folgen den Formeln aus monster.kod
/// (docs/monsters-original.md).</summary>
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
    /// <summary>Schlüssel in monsters.json (KOD-Klasse in Kleinbuchstaben).</summary>
    public string Key { get; init; } = "";
    /// <summary>Monsterwerte nach monster.kod, wenn nicht ausdrücklich gesetzt (Stufe/Schwierigkeit > 0).</summary>
    public int Offense => OffenseOverride ?? Math.Clamp(3 * Level + 60 * Difficulty, 1, 1500);
    public int Defense => DefenseOverride ?? Math.Clamp(3 * Level + 60 * Difficulty, 1, 1500);
    public int? OffenseOverride { get; init; }
    public int? DefenseOverride { get; init; }
    /// <summary>viSpeed (SPEED_VERY_SLOW 4, SLOW 8, AVERAGE 12, FAST 16, VERY_FAST 20) für den Angriffstakt.</summary>
    public int KodSpeed { get; init; }
    /// <summary>Resistenzen in Prozent je Angriffsart ("bludgeon", "thrust", "slash", "holy", "unholy", "fire", "cold",
    /// "shock", "acid", "weapon_all", "spell_all" …).</summary>
    public IReadOnlyDictionary<string, int> Resistances { get; init; } = new Dictionary<string, int>();
    /// <summary>Magische Angriffsart ("fire", "cold", "shock", "acid") für Spieler-Widerstände; null = Waffe/Klaue.</summary>
    public string? AttackType { get; init; }
    /// <summary>viKarma −100..100 (für karma-aggressive Monster und das Karma des Siegers).</summary>
    public int Karma { get; init; }
    /// <summary>viWimpy: Fluchtschwelle in Prozent der Lebenspunkte (0 = kämpft bis zum Tod).</summary>
    public int Wimpy { get; init; }
    /// <summary>AI_*-Verhalten ohne Präfix, z. B. "fight_aggressive", "fight_karma_aggressive", "nomove".</summary>
    public IReadOnlySet<string> Behavior { get; init; } = new HashSet<string>();
    /// <summary>Gift beim Treffer (poison.kod): Chance 1/n, Verlust lossRate/1000 LP alle 10 s, Dauer.</summary>
    public PoisonAttack? Poison { get; init; }

    /// <summary>Untot (Heiliges Symbol, Letzte Riten wirken).</summary>
    public bool Undead { get; init; }

    public bool Aggressive => Behavior.Contains("fight_aggressive") || Behavior.Contains("fight_hyperaggressive");
    public bool KarmaAggressive => Behavior.Contains("fight_karma_aggressive");
    public bool Immobile => Behavior.Contains("nomove") || SpeedFinePerSecond == 0;

    /// <summary>Resistenz gegen eine Angriffsart (battler.kod): größter positiver plus kleinster negativer Wert aller
    /// passenden Einträge; „weapon_all“/„spell_all“ passen auf jede Waffen- bzw. Zauberart.</summary>
    public int ResistanceTo(string attackType) => ResistanceTo([attackType]);

    /// <summary>battler.kod: über alle passenden Einträge (Angriffsarten des Treffers plus weapon_all/spell_all)
    /// größter positiver plus kleinster negativer Wert.</summary>
    public int ResistanceTo(IReadOnlyCollection<string> attackTypes)
    {
        var keys = new HashSet<string>(attackTypes);
        foreach (string t in attackTypes)
            keys.Add(SpellTypes.Contains(t) ? "spell_all" : "weapon_all");
        int max = 0, min = 0;
        foreach (string k in keys)
            if (Resistances.TryGetValue(k, out int r))
            {
                max = Math.Max(max, r);
                min = Math.Min(min, r);
            }
        return Math.Clamp(max + min, -100, 100);
    }

    private static readonly HashSet<string> SpellTypes = ["fire", "cold", "shock", "acid", "holy", "unholy", "quake", "huntersword"];

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
    /// <summary>Grammatisches Geschlecht für Texte: "m" (der Zombie), "f" (die Mumie), "n" (das Skelett).</summary>
    public string Gender { get; init; } = "m";
    /// <summary>Text beim Anschauen (vrDesc); sonst ein allgemeiner.</summary>
    public string? Description { get; init; }
    /// <summary>Zeichenflags fürs Objekt (OF_BLACK usw.).</summary>
    public uint DrawFlags { get; init; }
    /// <summary>MOB_NOFIGHT / nicht aggressiv: greift nie an (Kühe).</summary>
    public bool Peaceful { get; init; }

    // ------------------------------------------------------------------ Katalog

    private static Dictionary<string, MonsterKind>? _all;

    /// <summary>Alle Arten; beim ersten Zugriff aus data/monsters/monsters.json neben der Programmdatei geladen.</summary>
    public static IReadOnlyDictionary<string, MonsterKind> All =>
        _all ??= Load(Path.Combine(AppContext.BaseDirectory, "data", "monsters", "monsters.json"));

    public static MonsterKind ByKey(string key) =>
        All.TryGetValue(key.ToLowerInvariant(), out var kind) ? kind : throw new InvalidDataException($"Unbekannte Monsterart {key}");

    public static MonsterKind Zombie => ByKey("zombie");
    public static MonsterKind Skeleton => ByKey("skeleton");
    public static MonsterKind Mummy => ByKey("mummy");
    public static MonsterKind MummyNoTreasure => ByKey("mummynotreasure");
    public static MonsterKind BlackMummy => ByKey("blackmummy");
    public static MonsterKind Cow => ByKey("cow");
    public static MonsterKind SpectralMummy => ByKey("spectralmummy");
    public static MonsterKind BatteredSkeleton => ByKey("batteredskeleton");
    public static MonsterKind Thrasher => ByKey("thrasher");

    /// <summary>Lauftempo in Feineinheiten/s je viSpeed (SPEED_VERY_SLOW 4 → 70 … SPEED_VERY_FAST 20 → 215).</summary>
    public static int FinePerSecond(int kodSpeed) => kodSpeed <= 0 ? 0 : kodSpeed switch
    {
        4 => 70, 8 => 105, 12 => 140, 16 => 180, 20 => 215, _ => 35 + kodSpeed * 9,
    };

    public static Dictionary<string, MonsterKind> Load(string path)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
        var result = new Dictionary<string, MonsterKind>();
        foreach (var p in doc.RootElement.GetProperty("monsters").EnumerateObject())
        {
            var e = p.Value;
            int level = e.GetProperty("level").GetInt32(), difficulty = e.GetProperty("difficulty").GetInt32();
            int speed = e.GetProperty("speed").GetInt32();
            string? Str(string name) => e.TryGetProperty(name, out var v) && v.ValueKind == System.Text.Json.JsonValueKind.String ? v.GetString() : null;
            HashSet<string> Set(string name) => e.TryGetProperty(name, out var v) ? v.EnumerateArray().Select(x => x.GetString()!).ToHashSet() : [];
            var behavior = Set("behavior");
            var attributes = Set("attributes");
            var spellAttack = Set("attackSpell").Where(t => t is "fire" or "cold" or "shock" or "acid").ToList();
            PoisonAttack? poison = null;
            if (e.TryGetProperty("special", out var sp) && sp.TryGetProperty("poison", out var po) && po.GetProperty("chance").ValueKind == System.Text.Json.JsonValueKind.Number)
                poison = new PoisonAttack(po.GetProperty("chance").GetInt32(), po.GetProperty("lossRate").GetInt32(),
                    TimeSpan.FromMilliseconds(po.GetProperty("durationMs").GetInt32()));
            // GetMaxHitPoints: Stufe unter 40 = Stufe, sonst Stufe × 120 / 100. Sicht 4 + Schwierigkeit/2 Felder.
            int maxHp = level < 40 ? level : level * 120 / 100;
            result[p.Name] = new MonsterKind(Str("name")!, Str("icon")!, maxHp, 0, 0, 0, TimeSpan.Zero,
                FinePerSecond(speed), (4 + difficulty / 2) * 64)
            {
                Key = p.Name, Level = level, Difficulty = difficulty, KodSpeed = speed,
                Karma = e.GetProperty("karma").GetInt32(), Wimpy = e.GetProperty("wimpy").GetInt32(),
                Treasure = Str("treasure"), AlwaysDrops = Str("alwaysDrops"), Gender = Str("gender") ?? "m",
                Description = Str("description"), Behavior = behavior, Poison = poison,
                Undead = e.TryGetProperty("undead", out var ud) && ud.GetBoolean(),
                DrawFlags = e.TryGetProperty("drawFlags", out var df) ? df.GetUInt32() : 0,
                Resistances = e.GetProperty("resistances").EnumerateObject().ToDictionary(r => r.Name, r => r.Value.GetInt32()),
                AttackType = spellAttack.FirstOrDefault(),
                Peaceful = behavior.Contains("nofight") || attributes.Contains("nofight"),   // MOB_NOFIGHT (Kuh)
            };
        }
        return result;
    }
}

/// <summary>Giftbiss eines Monsters (poison.kod MakePoisoned): Chance 1/<paramref name="OneIn"/> je Treffer.</summary>
public sealed record PoisonAttack(int OneIn, int LossRate, TimeSpan Duration);

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
    /// <summary>Zauberwirkungen: festgehalten (Festhalten, Blitzlicht), blind (Blindheit, Gleißen), gelähmt (Schwächen,
    /// Schwindel), vertrieben vom Heiligen Symbol.</summary>
    public DateTime HeldUntil { get; set; }
    public DateTime BlindUntil { get; set; }
    public DateTime PalsyUntil { get; set; }
    public Avatar? TurnedBy { get; set; }
    public DateTime TurnedUntil { get; set; }
    public bool Alive => DiedAt is null;
}
