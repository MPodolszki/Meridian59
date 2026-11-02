using System.Text.Json;

namespace Meridian.Server;

/// <summary>
/// Gegenstandsart aus data/items/items.json. Werte (Wert, Mengen-Gegenstand, Typ) aus den KOD-Klassen,
/// Namen und Beschreibungen eigener Text.
/// </summary>
public sealed record ItemKind(string Key, string Name, string Icon, int Value)
{
    /// <summary>Plural für Mengen-Gegenstände (KOD vrName_plural); sonst wie Name.</summary>
    public string? Plural { get; init; }
    /// <summary>Unbestimmter Artikel im Akkusativ ("einen", "eine", "ein"); leer für Mengen-Gegenstände.</summary>
    public string Article { get; init; } = "";
    public string? KodClass { get; init; }
    /// <summary>KOD NumberItem: stapelbar, wird mit Menge übertragen.</summary>
    public bool Number { get; init; }
    /// <summary>ITEMTYPE_*: "money", "food", "reagent", "gem", "potion", "weapon", "armor", "shield" …</summary>
    public IReadOnlyList<string> Types { get; init; } = [];
    public string? Description { get; init; }

    public bool Is(string type) => Types.Contains(type);
    public bool IsMoney => Is("money");

    /// <summary>Name mit Menge bzw. Artikel für Texte: „3 Laibe Käse“, „eine Fackel“.</summary>
    public string Describe(int number) =>
        Number ? $"{number} {(number == 1 ? Name : Plural ?? Name)}" : Article.Length > 0 ? $"{Article} {Name}" : Name;
}

public sealed class ItemCatalog
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    private readonly Dictionary<string, ItemKind> _kinds;

    public ItemCatalog(IEnumerable<ItemKind> kinds)
    {
        _kinds = kinds.ToDictionary(k => k.Key, StringComparer.OrdinalIgnoreCase);
        if (!_kinds.ContainsKey(MoneyKey))
            _kinds[MoneyKey] = new ItemKind(MoneyKey, "Schilling", "coin.bgf", 1) { Plural = "Schillinge", Number = true, Types = ["money"] };
    }

    public const string MoneyKey = "money";

    public ItemKind Money => _kinds[MoneyKey];
    public ItemKind this[string key] => _kinds.TryGetValue(key, out var k) ? k : throw new KeyNotFoundException($"Unbekannter Gegenstand '{key}'");
    public bool TryGet(string key, out ItemKind kind) => _kinds.TryGetValue(key, out kind!);
    public IEnumerable<ItemKind> Kinds => _kinds.Values;

    /// <summary>Beutetabellen (KOD TreasureType plTreasure) nach Namen, z. B. "zombie", "tough".</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<TreasureEntry>> Treasure { get; init; } = new Dictionary<string, IReadOnlyList<TreasureEntry>>();

    private sealed record ItemFile(IReadOnlyList<ItemKind> Items)
    {
        public Dictionary<string, IReadOnlyList<TreasureEntry>> Treasure { get; init; } = new();
    }

    public static ItemCatalog Load(string path)
    {
        var file = JsonSerializer.Deserialize<ItemFile>(File.ReadAllText(path), Json);
        return new(file?.Items ?? []) { Treasure = file?.Treasure ?? new() };
    }

    public static ItemCatalog Empty { get; } = new([]);
}

/// <summary>Ein Gegenstand (Instanz). Mengen-Gegenstände tragen ihre Anzahl selbst (KOD piNumber).</summary>
public sealed class Item(uint id, ItemKind kind, int number = 1)
{
    /// <summary>Objekt-IDs der Gegenstände beginnen hier (unter 2^28).</summary>
    public const uint ObjectIdBase = 0x00400000;

    public uint Id => id;
    public ItemKind Kind => kind;
    public int Number { get; set; } = kind.Number ? number : 1;

    /// <summary>Wert wie KOD GetValue: Mengen-Gegenstände Anzahl × Wert.</summary>
    public int Value => Kind.Number ? Number * Kind.Value : Kind.Value;

    /// <summary>Anzeigename für das Protokoll: Plural ab 2 Stück.</summary>
    public string DisplayName => Kind.Number && Number != 1 ? Kind.Plural ?? Kind.Name : Kind.Name;

    public string Describe() => Kind.Describe(Number);
}

/// <summary>Gegenstand auf dem Boden eines Raums.</summary>
public sealed record FloorItem(Item Item, ushort Row, ushort Col);

/// <summary>Was ein Charakter besitzt. Bleibt erhalten, solange der Server läuft (noch keine Persistenz).</summary>
public sealed class CharacterState
{
    public List<Item> Inventory { get; } = new();
    /// <summary>Kontostand je Bank (KOD Bank-Objekte, Tos = 1).</summary>
    public Dictionary<int, int> Bank { get; } = new();
    public bool Initialized { get; set; }
    /// <summary>Letzte Position beim Ausloggen (null = noch nie in der Welt) und Lebenspunkte.</summary>
    public int? RoomId { get; set; }
    public ushort Row { get; set; }
    public ushort Col { get; set; }
    public ushort Angle { get; set; }
    public int? Hp { get; set; }

    public Item? Find(ItemKind kind) => Inventory.FirstOrDefault(i => i.Kind == kind);
    public Item? Find(uint id) => Inventory.FirstOrDefault(i => i.Id == id);
}
