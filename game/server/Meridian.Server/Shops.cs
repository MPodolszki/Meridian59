using System.Text.Json;

namespace Meridian.Server;

/// <summary>
/// Händler-Daten je NPC-Klasse (data/shops/*.json), aus den KOD-Klassen der Tos-NPCs übernommen
/// (plFor_sale, InitCondSale, viMerchant_markup, viAttributes, ObjectDesired). Siehe docs/trade.md.
/// </summary>
public sealed record ShopDefinition
{
    /// <summary>MERCHANT_*: 0 flat, 1 bargain, 2 discount, 3 normal, 4 expensive, 5 ripoff.</summary>
    public int Markup { get; init; } = 3;
    /// <summary>MOB_BUYER: kauft Gegenstände per Angebot (BP_REQ_OFFER).</summary>
    public bool Buyer { get; init; }
    /// <summary>MOB_RECEIVE: nimmt Geschenke an (Gegenstände aus <see cref="Wanted"/>).</summary>
    public bool Receive { get; init; }
    /// <summary>MOB_BANKER mit Banknummer.</summary>
    public int? Bank { get; init; }
    /// <summary>ObjectDesired eines Käufers: Gegenstand muss einen dieser Typen haben …</summary>
    public IReadOnlyList<string> BuysTypes { get; init; } = [];
    /// <summary>… und keinen dieser.</summary>
    public IReadOnlyList<string> BuysExceptTypes { get; init; } = [];
    /// <summary>plWantedItems (Gegenstandsschlüssel).</summary>
    public IReadOnlyList<string> Wanted { get; init; } = [];
    /// <summary>Edelsteintausch des Tos-Bankiers (TsBanker CheckWhyWanted).</summary>
    public bool GemTrade { get; init; }
    public IReadOnlyList<SaleEntry> ForSale { get; init; } = [];
    /// <summary>Waren, die erst nach Nennung ihres Namens angeboten werden (LIBACT_CONDITIONAL).</summary>
    public IReadOnlyList<ConditionalSale> Conditional { get; init; } = [];
    /// <summary>Antwort auf ungewollte Geschenke (vrUnwanted_give), %s = Name des NPCs.</summary>
    public string? UnwantedGive { get; init; }

    public bool Seller => ForSale.Count > 0 || Conditional.Count > 0;
}

public sealed record SaleEntry(string Item)
{
    /// <summary>Angezeigte Menge bei Mengen-Gegenständen (KOD Create(&Cheese,#number=3)).</summary>
    public int Number { get; init; } = 1;
}

/// <summary>Bedingte Ware: Auslösewörter, Festpreis pro Stück und Antwort des Händlers.</summary>
public sealed record ConditionalSale(string Item, int Number, int Price, IReadOnlyList<string> Triggers, string Reply);

public sealed record ShopFile(IReadOnlyDictionary<string, ShopDefinition> Shops)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    public static ShopFile Load(string path) =>
        JsonSerializer.Deserialize<ShopFile>(File.ReadAllText(path), Json) ?? Empty;

    public static ShopFile Empty { get; } = new(new Dictionary<string, ShopDefinition>());
}

/// <summary>Preisregeln aus monster.kod (GetPrice, Offer). Fraktions-Bonus ist immer 100 %.</summary>
public static class TradeRules
{
    /// <summary>MAX_BUY_AMOUNT (blakston.khd).</summary>
    public const int MaxBuyAmount = 500;
    /// <summary>viCancel_offer_time: Händler zieht sein Gegenangebot nach 30 s zurück.</summary>
    public static readonly TimeSpan OfferTimeout = TimeSpan.FromSeconds(30);

    /// <summary>Verkaufspreis pro Stück: Wert × (100 + 20 × Aufschlag) / 100, mindestens 1.</summary>
    public static int SellPrice(ItemKind kind, int markup) => Math.Max(1, kind.Value * (100 + 20 * markup) / 100);

    /// <summary>Ankaufspreis für einen Gegenstand (ganze Menge): Wert × (100 − 10 × Aufschlag) / 100, mindestens 1.</summary>
    public static int BuyBackPrice(Item item, int markup) => Math.Max(1, item.Value * (100 - 10 * markup) / 100);
}

/// <summary>Händler-Zustand eines NPCs zur Laufzeit.</summary>
public sealed class Merchant(ShopDefinition definition)
{
    public ShopDefinition Definition => definition;
    /// <summary>Waren mit festen Objekt-IDs (für BP_REQ_BUY_ITEMS). Festpreis bei bedingten Waren.</summary>
    public List<(Item Item, int? FixedPrice)> Stock { get; } = new();
    /// <summary>Bedingte Waren, die gerade nicht angeboten werden.</summary>
    public List<(ConditionalSale Sale, Item Item)> Hidden { get; } = new();
    /// <summary>Aktueller Kunde mit offenem Gegenangebot (poCustomer).</summary>
    public Avatar? Customer { get; set; }
    public DateTime CustomerTimeout { get; set; }
}
