using System.Text.Json;

namespace Meridian.Server;

/// <summary>Spruch aus data/spells/spells.json (Werte aus kod/object/passive/spell, Texte eigene Übersetzung).</summary>
public sealed record SpellDefinition(string Key, int Sid, string Name, string Icon, int School, int Level, int Mana)
{
    /// <summary>viSpellExertion (Standard 2): Anstrengung 10000 × Wert beim Zaubern.</summary>
    public int Exertion { get; init; } = 2;
    /// <summary>viCast_time in ms (0 = sofort); tatsächlich viCast_time × (150 − Spruchstärke) / 100.</summary>
    public int CastTime { get; init; }
    /// <summary>viChance_To_Increase (Standard 20).</summary>
    public int ChanceToIncrease { get; init; } = 20;
    /// <summary>Anzahl Ziele (GetNumSpellTargets): 0 = keins, 1 = ein Ziel.</summary>
    public int Targets { get; init; }
    public IReadOnlyList<SpellReagent> Reagents { get; init; } = [];
    /// <summary>Wirkung: "heal", "hospice", "createfood", "touchattack", persönliche Verzauberungen "bless",
    /// "superstrength", "haste", "resist".</summary>
    public string Effect { get; init; } = "";
    public int MinDamage { get; init; } = 3;
    public int MaxDamage { get; init; } = 6;
    /// <summary>Angriffsart der Berührungszauber für Resistenzen ("shock", "holy", "acid", "cold", "fire").</summary>
    public string? AttackType { get; init; }
    public string? Description { get; init; }
    public string? CastText { get; init; }
    public string? Unnecessary { get; init; }
    public string? OffText { get; init; }
    /// <summary>Verzauberungsdauer GetDuration: Random(d/2, d) mit d = Basis + Stärke × je Punkt (Sekunden).</summary>
    public int DurationBase { get; init; }
    public int DurationPerPower { get; init; }
    /// <summary>Widerstand (resist.kod GetResistanceStrength): Stärke/2 + Basis, gegen <see cref="AttackType"/>.</summary>
    public int ResistBase { get; init; } = 1;
    public string? HitText { get; init; }

    /// <summary>Objekt-ID des Spruchs im Protokoll (BP_SPELLS, BP_REQ_CAST).</summary>
    public uint ObjectId => Spell.ObjectIdBase + (uint)Sid;

    /// <summary>spell.kod GetValue: 250 × 2^Stufe.</summary>
    public int Price => 250 * (1 << Level);
}

public sealed record SpellReagent(string Item, int Number);

/// <summary>Spruchbuch: alle bekannten Sprüche und die Startsprüche neuer Charaktere.</summary>
public sealed class SpellBook
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private sealed record SpellFile(IReadOnlyList<SpellDefinition> Spells)
    {
        public IReadOnlyList<string> StartingSpells { get; init; } = [];
    }

    private readonly Dictionary<string, SpellDefinition> _byKey;
    private readonly Dictionary<int, SpellDefinition> _bySid;

    public SpellBook(IEnumerable<SpellDefinition> spells, IReadOnlyList<string>? starting = null)
    {
        _byKey = spells.ToDictionary(s => s.Key, StringComparer.OrdinalIgnoreCase);
        _bySid = _byKey.Values.ToDictionary(s => s.Sid);
        Starting = (starting ?? []).Where(_byKey.ContainsKey).Select(k => _byKey[k]).ToList();
    }

    public IReadOnlyList<SpellDefinition> Starting { get; }
    public IEnumerable<SpellDefinition> All => _byKey.Values;

    public SpellDefinition? this[string key] => _byKey.GetValueOrDefault(key);
    public SpellDefinition? BySid(int sid) => _bySid.GetValueOrDefault(sid);
    public SpellDefinition? ByObjectId(uint id) =>
        id > Spell.ObjectIdBase && id - Spell.ObjectIdBase < 1000 ? BySid((int)(id - Spell.ObjectIdBase)) : null;

    public static SpellBook Load(string path)
    {
        var file = JsonSerializer.Deserialize<SpellFile>(File.ReadAllText(path), Json);
        return new(file?.Spells ?? [], file?.StartingSpells);
    }

    public static SpellBook Empty { get; } = new([]);
}

public static class Spell
{
    /// <summary>Objekt-IDs der Sprüche: fester Offset plus SID (unter 2^28).</summary>
    public const uint ObjectIdBase = 0x00500000;
}

/// <summary>Charakterwerte (player.kod piMight … piAim), je 1..50, zusammen höchstens 200.</summary>
public sealed record Attributes(int Might, int Intellect, int Stamina, int Agility, int Mysticism, int Aim)
{
    /// <summary>Startwerte, solange es keine Charaktererstellung gibt: ausgewogen, 200 Punkte wie im Original.</summary>
    public static Attributes Default { get; } = new(35, 30, 35, 30, 40, 30);
}

/// <summary>Zauberregeln aus spell.kod und player.kod (siehe docs/magic.md).</summary>
public static class MagicRules
{
    public const int SpellPowerMin = 1;
    public const int SpellPowerMax = 99;
    /// <summary>BASE_REGEN_TIME (player.kod).</summary>
    public const int BaseManaRegenMs = 150000;
    public const int CastsPerPercentBonus = 7;
    public const int SoftcapPenalty = 4;
    public const int PointsSlope = 7;
    public const int MinNeededToAdvance = 75;
    public const int MaxLearnPoints = 16;
    /// <summary>vlLevelPoints (system.kod): Lernpunkte je höchster Stufe einer Schule.</summary>
    private static readonly int[] LevelPoints = [1, 2, 4, 6, 8, 10];

    public static int LevelLearnPoints(int level) => level <= 0 ? 0 : LevelPoints[Math.Min(level, LevelPoints.Length) - 1];

    /// <summary>GetDivisionReq: Shal'ille/Qor/Faren → Mystik, Kraanan → Ausdauer, Riija/Jala → Intellekt.</summary>
    public static int RequisiteStat(SpellDefinition s, Attributes a) => s.School switch
    {
        3 => a.Stamina,
        5 or 6 => a.Intellect,
        _ => a.Mysticism,
    };

    /// <summary>GetInitialMaxMana: 15 + Mystik/5.</summary>
    public static int InitialMaxMana(Attributes a) => 15 + a.Mysticism / 5;

    /// <summary>CalculateManaTime: Zeit bis zum nächsten Manapunkt.</summary>
    public static TimeSpan ManaTime(Attributes a, int vigor, int maxMana)
    {
        long ms = BaseManaRegenMs + (25 - a.Mysticism) * 1000L;
        ms = ms * 200 / Math.Max(vigor, 1);
        ms /= Math.Max(maxMana, 1);
        return TimeSpan.FromMilliseconds(Math.Clamp(ms, 1000, 60000));
    }

    /// <summary>InitialAbility: Random(Req/2, Req)/2, begrenzt 1..25.</summary>
    public static int InitialAbility(SpellDefinition s, Attributes a, Random rnd)
    {
        int req = RequisiteStat(s, a);
        return Math.Clamp(rnd.Next(req / 2, req + 1) / 2, 1, 25);
    }

    /// <summary>GetManaCost: über Spruchstärke 40 und 80 je 1 weniger, mindestens 1.</summary>
    public static int ManaCost(SpellDefinition s, int power)
    {
        int cost = s.Mana;
        if (cost == 0)
            return 0;
        if (power > 40) cost--;
        if (power > 80) cost--;
        return Math.Max(cost, 1);
    }

    /// <summary>SuccessChance: ((100 − Req) × Stärke / 100) + Req, begrenzt 5..95.</summary>
    public static int SuccessChance(SpellDefinition s, Attributes a, int power)
    {
        int req = RequisiteStat(s, a);
        return Math.Clamp((100 - req) * power / 100 + req, 5, 95);
    }

    /// <summary>GetTranceTime: viCast_time × (150 − Stärke) / 100.</summary>
    public static TimeSpan TranceTime(SpellDefinition s, int power) =>
        s.CastTime == 0 ? TimeSpan.Zero : TimeSpan.FromMilliseconds(s.CastTime * (150 - power) / 100);
}

/// <summary>Aktive persönliche Verzauberung: Zustand aus InitializeEnchantmentState (Segen: Stärke, Superstärke:
/// Stärkebonus, Eile: Stärke, Widerstand: Prozent) und Ende.</summary>
public sealed record Enchantment(int State, DateTime Until);
