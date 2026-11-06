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

    /// <summary>Ausrüstungsplatz: "weapon", "shield" (auch Fackel), "armor", Kleidung "shirt" (ITEM_USE_SHIRT) bzw.
    /// "legs" (ITEM_USE_LEGS); null = nicht ausrüstbar.</summary>
    public string? Slot { get; init; }
    /// <summary>Waffen (weapon.kod): "bludgeon", "thrust", "slash"; Qualität "low", "normal", "high", "nerudite".</summary>
    public string? WeaponType { get; init; }
    public string WeaponQuality { get; init; } = "normal";
    /// <summary>Waffenfertigkeit (viProficiency_needed, SKID), z. B. 452 Streitkolbenkampf.</summary>
    public int Proficiency { get; init; }
    /// <summary>viDefense_base: Rüstung verändert die Defensive (Kette −50), Schilde erhöhen das Blocken.</summary>
    public int DefenseBonus { get; init; }
    /// <summary>Höchste Schadensminderung (defmod viDamage_base).</summary>
    public int DamageReduce { get; init; }
    /// <summary>Waffe in der Hand der Spielerfigur (KOD player_*_overlay_icon_rsc, z. B. maceov.bgf).</summary>
    public string? Overlay { get; init; }
    /// <summary>Waffe in der Ego-Ansicht (KOD vrWeapon_window_overlay, z. B. povmace.bgf).</summary>
    public string? WindowOverlay { get; init; }
    /// <summary>Kleidung: Farbe XLAT_TO_* (EncodeTwoColorXLAT mit dem Hautton) oder feste Übersetzung PT_* (Hose C).</summary>
    public int? Color { get; init; }
    public byte? Translation { get; init; }
    /// <summary>Beine des Spielers mit dieser Hose/diesem Rock (pants.kod *_male/female_player_rsc).</summary>
    public string? LegsMale { get; init; }
    public string? LegsFemale { get; init; }

    /// <summary>Farbübersetzung für einen Hautton (util.kod EncodeTwoColorXLAT: 0x87 + 11 × Farbe + Haut, Schwarz 124 + Haut).</summary>
    public byte ClothingTranslation(byte skin) => Translation ?? Color switch
    {
        null => 0,
        0x0C => (byte)(124 + skin),
        { } c => (byte)(0x87 + 11 * c + skin),
    };

    /// <summary>Lichtquelle, wenn benutzt (Fackel).</summary>
    public bool Light { get; init; }
    /// <summary>Spruchgegenstand (spelitem.kod): Trank, Schriftrolle oder Zauberstab mit diesem Spruch (Schlüssel aus
    /// spells.json), Spruchstärke zwischen PowerMin und PowerMax, Ladungen (Stäbe) zwischen ChargesMin und ChargesMax.</summary>
    public string? Spell { get; init; }
    /// <summary>Angriffsbonus beim Tragen (Panzerhandschuhe piAttack_bonus).</summary>
    public int AttackBonus { get; init; }
    /// <summary>Widerstandsring (resring.kod): Art und Wert, solange er getragen wird.</summary>
    public string? ResistType { get; init; }
    public int ResistValue { get; init; }
    /// <summary>Berserkerring (besring.kod): Treffer − Random(15,25)×10, Schaden + Random(1,4); jeder Schlag kostet eine Ladung.</summary>
    public bool Berserk { get; init; }
    /// <summary>Ring der Unsichtbarkeit (ringinv.kod): unsichtbar, solange getragen; jede Minute eine Ladung.</summary>
    public bool Invisible { get; init; }
    /// <summary>Instrument (instrum.kod viInstrument_strength): Laute 20, edle/wahre Laute 30, Jala-Halskette 5.</summary>
    public int InstrumentLevel { get; init; }
    /// <summary>item.kod GetBaseSpellModifier: Zu-/Abschlag auf die Spruchstärke, solange benutzt (Plattenrüstung −30,
    /// Robe +10, Waffen nach Typ/Qualität); SpellModifierSchools ersetzt ihn je Schule (Fackel: Faren +10).</summary>
    public int SpellModifier { get; init; }
    public IReadOnlyDictionary<int, int>? SpellModifierSchools { get; init; }
    /// <summary>Jüngerrobe (robedisc.kod): Schule der Robe; Sprüche dieser Schule + 4 × höchste gelernte Stufe (bis 20),
    /// andere + 5.</summary>
    public int DiscipleSchool { get; init; }
    /// <summary>Verflucht (IsCursed, Ring der Lethargie): lässt sich nicht ablegen, fallen lassen oder weitergeben, bis
    /// „Fluch lösen“ ihn entfernt; jeder Versuch kostet TortureExertion Anstrengung.</summary>
    public bool Cursed { get; init; }
    public int TortureExertion { get; init; }
    /// <summary>Senkt die Ausdauerschwelle, unter der man sich erholt (lethring.kod piVigorRestThresholdChange).</summary>
    public int VigorRestDrop { get; init; }
    public string? UseText { get; init; }
    public string? TortureText { get; init; }
    public string? UncurseText { get; init; }
    /// <summary>Beim Erzeugen wird zufällig eine dieser Arten (Jüngerrobe: Schule nach Random(1, 6)).</summary>
    public IReadOnlyList<string>? Variants { get; init; }
    /// <summary>Sonderstab (specwand/*.kod): "fire", "heal", "vampire", "jolt"; Wirkung DamageMin..DamageMax.</summary>
    public string? Special { get; init; }
    public int DamageMin { get; init; }
    public int DamageMax { get; init; }
    /// <summary>Fernwaffe (ranged.kod): Reichweite in Feldern, Trefferbonus (viHit_roll_modifier), Zusatzschaden
    /// (viWeaponDamage); belegt beide Hände (viUse_Amount = 2).</summary>
    public int Range { get; init; }
    public int HitBonus { get; init; }
    public int WeaponDamage { get; init; }
    public bool TwoHanded { get; init; }
    /// <summary>Munition (ammo.kod, Platz "quiver"): Angriffsarten des Pfeils; Bündelgröße beim Erzeugen (piNumber).</summary>
    public IReadOnlyList<string>? AttackTypes { get; init; }
    public int DefaultNumber { get; init; } = 1;
    public bool IsRanged => WeaponType == "ranged";
    public int PowerMin { get; init; } = 30;
    public int PowerMax { get; init; } = 60;
    public int ChargesMin { get; init; } = 1;
    public int ChargesMax { get; init; } = 1;
    /// <summary>Essen (food.kod): Nährwert (→ Ausdauer) und Sättigung; Getränk für die Meldungen.</summary>
    public int Nutrition { get; init; }
    public int Filling { get; init; }
    public bool Beverage { get; init; }

    public bool IsFood => Is("food") && Filling > 0;

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
    /// <summary>Ausgerüstet/benutzt (KOD poOwner-Liste plUsing).</summary>
    public bool Used { get; set; }
    /// <summary>Restladungen eines Zauberstabs (piHits); null = noch nicht ausgewürfelt.</summary>
    public int? Charges { get; set; }
    /// <summary>Waffenweihe (enchwp/holywp/unholywp.kod, WA_ENCHANTED/WA_ATTACKSPELLTYPE): "magic", "holy" oder
    /// "unholy" bis zum Ablauf; wird noch nicht gespeichert.</summary>
    public (string Type, DateTime Until)? WeaponEnchant { get; set; }

    /// <summary>Wert wie KOD GetValue: Mengen-Gegenstände Anzahl × Wert.</summary>
    public int Value => Kind.Number ? Number * Kind.Value : Kind.Value;

    /// <summary>Anzeigename für das Protokoll: Plural ab 2 Stück.</summary>
    public string DisplayName => Kind.Number && Number != 1 ? Kind.Plural ?? Kind.Name : Kind.Name;

    public string Describe() => Kind.Describe(Number);

    public bool IsRangedWeapon => Kind.IsRanged;
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
    /// <summary>Charakterwerte und Magie (player.kod): Karma −100..100, Mana, bekannte Sprüche (SID → Fähigkeit %),
    /// Zauber je Schule seit der letzten Verbesserung (piSchoolCast).</summary>
    public Attributes Attributes { get; set; } = Attributes.Default;
    public int Karma { get; set; }
    /// <summary>Verschmolzene Manaknoten (player.kod piNodeList, Bitmaske NODE_*).</summary>
    public int Nodes { get; set; }
    /// <summary>Heimatstadt (user.kod piHomeroom): Raum des Heimatgasthauses; negativ = in der Ahnenhalle fest gewählt.</summary>
    public int? Hometown { get; set; }
    public int Mana { get; set; }
    public int MaxMana { get; set; }
    public Dictionary<int, int> Spells { get; } = new();
    public Dictionary<int, int> SchoolCasts { get; } = new();
    /// <summary>Persönliche Verzauberungen (SID → Zustand und Ende), player.kod plEnchantments.</summary>
    public Dictionary<int, Enchantment> Enchantments { get; } = new();
    /// <summary>Fertigkeiten (SKID → Fähigkeit %), Schläge je Waffenfertigkeit (Übungszähler), Verbesserungen
    /// im laufenden Zeitfenster (Advancement Points) und dessen Ende.</summary>
    public Dictionary<int, int> Skills { get; } = new();
    public Dictionary<int, int> Swings { get; } = new();
    public int AdvancementPoints { get; set; }
    public DateTime AdvancementReset { get; set; }
    public bool MagicInitialized { get; set; }
    /// <summary>Charaktererstellung: gewählter Name, Beschreibung, Aussehen; Created = Erstellung abgeschlossen.</summary>
    public string? Name { get; set; }
    public string Description { get; set; } = "";
    public Appearance Appearance { get; set; } = Appearance.Default;
    public bool Created { get; set; }
    /// <summary>Ausdauer (KOD piVigor, 0..200, Start 100) und Rest-Anstrengung in 1/10000 Punkten (piExertion).</summary>
    public int Vigor { get; set; } = CombatRules.StartVigor;
    public int Exertion { get; set; }
    /// <summary>Mageninhalt 0..100 (piStomach), leert sich mit der Zeit.</summary>
    public double Stomach { get; set; }
    public DateTime StomachUpdated { get; set; } = DateTime.UtcNow;
    /// <summary>Letzte Position beim Ausloggen (null = noch nie in der Welt) und Lebenspunkte.</summary>
    public int? RoomId { get; set; }
    public ushort Row { get; set; }
    public ushort Col { get; set; }
    public ushort Angle { get; set; }
    public int? Hp { get; set; }

    public Item? Find(ItemKind kind) => Inventory.FirstOrDefault(i => i.Kind == kind);
    public Item? Find(uint id) => Inventory.FirstOrDefault(i => i.Id == id);
}
