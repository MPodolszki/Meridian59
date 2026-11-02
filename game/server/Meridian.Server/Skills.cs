using System.Text.Json;

namespace Meridian.Server;

/// <summary>Fertigkeit aus data/skills/skills.json (kod/object/passive/skill, siehe docs/combat-original.md).</summary>
public sealed record SkillDefinition(string Key, int Skid, string Name, string Icon, int Level)
{
    public int ChanceToIncrease { get; init; } = 20;
    /// <summary>Bezugswert: "agility", "might", "aim", "stamina", "intellect", "mysticism".</summary>
    public string Requisite { get; init; } = "agility";
    /// <summary>Anstrengung je Schlag: 1000 × Wert (PayCosts).</summary>
    public int Exertion { get; init; }
    public string? Description { get; init; }

    public uint ObjectId => Skill.ObjectIdBase + (uint)Skid;

    public int RequisiteStat(Attributes a) => Requisite switch
    {
        "might" => a.Might,
        "aim" => a.Aim,
        "stamina" => a.Stamina,
        "intellect" => a.Intellect,
        "mysticism" => a.Mysticism,
        _ => a.Agility,
    };
}

public static class Skill
{
    public const uint ObjectIdBase = 0x00600000;
    public const int Dodge = 401, Parry = 402, Block = 404, Slash = 421, Punch = 430, Brawling = 450;
}

public sealed class SkillBook
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private sealed record SkillFile(IReadOnlyList<SkillDefinition> Skills)
    {
        public IReadOnlyList<string> StartingSkills { get; init; } = [];
    }

    private readonly Dictionary<int, SkillDefinition> _bySkid;

    public SkillBook(IEnumerable<SkillDefinition> skills, IReadOnlyList<string>? starting = null)
    {
        _bySkid = skills.ToDictionary(s => s.Skid);
        Starting = (starting ?? []).Select(k => _bySkid.Values.FirstOrDefault(s => s.Key == k)).OfType<SkillDefinition>().ToList();
    }

    public IReadOnlyList<SkillDefinition> Starting { get; }
    public IEnumerable<SkillDefinition> All => _bySkid.Values;
    public SkillDefinition? BySkid(int skid) => _bySkid.GetValueOrDefault(skid);

    public static SkillBook Load(string path)
    {
        var file = JsonSerializer.Deserialize<SkillFile>(File.ReadAllText(path), Json);
        return new(file?.Skills ?? [], file?.StartingSkills);
    }

    public static SkillBook Empty { get; } = new([]);
}
