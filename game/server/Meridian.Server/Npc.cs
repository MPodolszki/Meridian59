using System.Text.Json;

namespace Meridian.Server;

/// <summary>Dialogtexte je NPC-Klasse (data/dialog/*.json, eigener Inhalt).</summary>
public sealed record NpcDialog(string Title, string Description, string Greeting, IReadOnlyList<NpcTopic> Topics);

public sealed record NpcTopic(IReadOnlyList<string> Keywords, string Reply);

public sealed record DialogFile(IReadOnlyDictionary<string, NpcDialog> Npcs)
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public static DialogFile Load(string path) =>
        JsonSerializer.Deserialize<DialogFile>(File.ReadAllText(path), Json) ?? new DialogFile(new Dictionary<string, NpcDialog>());

    public static DialogFile Empty { get; } = new(new Dictionary<string, NpcDialog>());

    /// <summary>Dialoge mehrerer Gebiete zusammenführen (NPC-Klassen sind eindeutig).</summary>
    public static DialogFile Merge(IEnumerable<DialogFile> files) =>
        new(files.SelectMany(f => f.Npcs).ToDictionary(kv => kv.Key, kv => kv.Value));
}

/// <summary>Nicht-Spieler-Figur in einem Raum.</summary>
public sealed class Npc(uint id, NpcDefinition definition, NpcDialog? dialog)
{
    /// <summary>Objekt-IDs der NPCs beginnen hier (unter 2^28, getrennt von Räumen und Charakteren).</summary>
    public const uint ObjectIdBase = 0x00200000;

    /// <summary>Begrüßungswörter, auf die jeder NPC mit seiner Begrüßung antwortet.</summary>
    private static readonly string[] Greetings = ["hallo", "hi", "grüß", "gruss", "moin", "servus", "hello", "guten tag"];

    public uint Id => id;
    /// <summary>Bildgruppe (ANIMATE_NONE): Hebel 1 = oben, 2 = unten (gstlever.kod piState).</summary>
    public ushort Group { get; set; } = 1;
    public NpcDefinition Definition => definition;
    public string Name => definition.Name ?? definition.KodClass;
    public string DisplayName => dialog is null ? Name : $"{Name} ({dialog.Title})";
    public ushort Row => Room.Fine(definition.Row, definition.FineRow);
    public ushort Col => Room.Fine(definition.Col, definition.FineCol);
    public ushort Angle => (ushort)definition.Angle;

    public string Describe() => definition.Description ?? dialog?.Description ?? $"Du siehst {Name}.";

    /// <summary>Händler-Rolle (Laden, Ankauf, Bank), falls in data/shops definiert.</summary>
    public Merchant? Merchant { get; set; }

    /// <summary>Antwort auf Gesagtes, oder null wenn der NPC sich nicht angesprochen fühlt.
    /// Themen-Stichwörter haben Vorrang; Begrüßung oder Nennung des Namens ergibt die Begrüßung.</summary>
    public string? Reply(string text)
    {
        if (dialog is null)
            return null;
        string lower = text.ToLowerInvariant();
        foreach (var topic in dialog.Topics)
            if (topic.Keywords.Any(k => lower.Contains(k, StringComparison.Ordinal)))
                return topic.Reply;
        if (lower.Contains(Name.ToLowerInvariant(), StringComparison.Ordinal) || Greetings.Any(g => lower.Contains(g, StringComparison.Ordinal)))
            return dialog.Greeting.Length > 0 ? dialog.Greeting : null;   // Portal, Schilder: stumm
        return null;
    }
}
