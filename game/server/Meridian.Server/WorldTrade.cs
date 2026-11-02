using Meridian.Protocol;

namespace Meridian.Server;

/// <summary>
/// Gegenstände, Inventar, Geld und Handel mit NPCs, eng an KOD angelehnt (monster.kod Buy/ReqOffer/Offer,
/// user.kod UserBuy/UserOffer/UserAcceptOffer, BankDeposit/BankWithdraw, CreateTreasure). Siehe docs/trade.md.
/// </summary>
public sealed partial class World
{
    private readonly ItemCatalog _items;
    private readonly Dictionary<uint, CharacterState> _characters = new();
    private uint _nextItemId = Item.ObjectIdBase;

    /// <summary>Höchstens so viele Gegenstände liegen in einem Raum herum; ältere verschwinden (Abweichung, s. trade.md).</summary>
    public const int MaxFloorItems = 40;

    /// <summary>Reichweite zum Aufheben in Feineinheiten (2 Felder).</summary>
    public const int GetRange = 128;

    public ItemCatalog Items => _items;

    private Item NewItem(ItemKind kind, int number = 1) => new(++_nextItemId, kind, number);

    // ------------------------------------------------------------------ Darstellung

    public RoomObject ToRoomObject(Item item, ushort row = 0, ushort col = 0, uint flags = 0) =>
        new(item.Id, _resources[item.DisplayName], new ObjectAppearance(_resources[item.Kind.Icon]), row, col, 0,
            flags, item.Kind.Number ? (uint)item.Number : null);

    private RoomObject ToRoomObject(FloorItem f) => ToRoomObject(f.Item, f.Row, f.Col, ObjectFlags.Gettable);

    private static void EnsureItemResources(Session session, Item item)
    {
        session.EnsureResource(item.DisplayName);
        session.EnsureResource(item.Kind.Icon);
    }

    /// <summary>Inventar des Spielers für BP_INVENTORY (stellt die Ressourcen sicher).</summary>
    public List<RoomObject> InventoryOf(Avatar avatar)
    {
        lock (_gate)
        {
            foreach (var i in avatar.State.Inventory)
                EnsureItemResources(avatar.Session, i);
            return avatar.State.Inventory.Select(i => ToRoomObject(i)).ToList();
        }
    }

    /// <summary>Gegenstände auf dem Boden für BP_ROOM_CONTENTS.</summary>
    public List<RoomObject> FloorItemsIn(Room room, Session session)
    {
        lock (_gate)
        {
            foreach (var f in room.Items)
                EnsureItemResources(session, f.Item);
            return room.Items.Select(ToRoomObject).ToList();
        }
    }

    /// <summary>Geldbetrag im Inventar (KOD GetMoneyObject).</summary>
    public int MoneyOf(Avatar avatar)
    {
        lock (_gate)
            return avatar.State.Find(_items.Money)?.Number ?? 0;
    }

    public int BankBalance(Avatar avatar, int bank = 1)
    {
        lock (_gate)
            return avatar.State.Bank.GetValueOrDefault(bank);
    }

    // ------------------------------------------------------------------ Charakter

    /// <summary>Besitz eines Charakters (bleibt bis Serverende erhalten). Beim ersten Mal die Startausrüstung
    /// wie player.kod AddDefaultObjects (Streitkolben, 500 + 2000 Schillinge) plus AddRealWorldObjects (1000),
    /// weil neue Charaktere direkt in Tos statt auf Raza beginnen.</summary>
    private CharacterState StateFor(uint characterId)
    {
        if (!_characters.TryGetValue(characterId, out var state))
            _characters[characterId] = state = new CharacterState();
        if (!state.Initialized)
        {
            state.Initialized = true;
            if (_items.TryGet("mace", out var mace))
                state.Inventory.Add(NewItem(mace));
            state.Inventory.Add(NewItem(_items.Money, 500 + 2000 + 1000));
        }
        return state;
    }

    // ------------------------------------------------------------------ Inventar-Grundoperationen (unter Lock)

    /// <summary>Gegenstand ins Inventar (KOD NewHold): Mengen-Gegenstände werden zusammengelegt.</summary>
    private void Give(Avatar a, Item item)
    {
        if (item.Kind.Number && a.State.Find(item.Kind) is { } stack)
        {
            stack.Number += item.Number;
            EnsureItemResources(a.Session, stack);
            a.Session.Post(GameReplies.Change(ToRoomObject(stack)));
            return;
        }
        a.State.Inventory.Add(item);
        EnsureItemResources(a.Session, item);
        a.Session.Post(GameReplies.InventoryAdd(ToRoomObject(item)));
    }

    private void Give(Avatar a, ItemKind kind, int number) => Give(a, NewItem(kind, number));

    /// <summary>Menge abziehen (KOD SubtractNumber); bei 0 verschwindet der Gegenstand.</summary>
    private void TakeNumber(Avatar a, Item item, int number)
    {
        item.Number -= number;
        if (item.Number > 0)
        {
            EnsureItemResources(a.Session, item);
            a.Session.Post(GameReplies.Change(ToRoomObject(item)));
            return;
        }
        Take(a, item);
    }

    /// <summary>Gegenstand aus dem Inventar entfernen (KOD LeaveHold); benutzte werden vorher abgelegt.</summary>
    private void Take(Avatar a, Item item)
    {
        if (item.Used)
            UnuseLocked(a, item, quiet: true);
        if (a.State.Inventory.Remove(item))
            a.Session.Post(GameReplies.InventoryRemove(item.Id));
    }

    private void TakeMoney(Avatar a, int amount)
    {
        if (amount > 0 && a.State.Find(_items.Money) is { } money)
            TakeNumber(a, money, Math.Min(amount, money.Number));
    }

    // ------------------------------------------------------------------ Benutzen / Ausrüsten (BP_REQ_USE, BP_REQ_UNUSE)

    /// <summary>IDs der benutzten Gegenstände für BP_USE_LIST.</summary>
    public List<uint> UsedItems(Avatar avatar)
    {
        lock (_gate)
            return avatar.State.Inventory.Where(i => i.Used).Select(i => i.Id).ToList();
    }

    private static Item? Weapon(Avatar a) => a.State.Inventory.FirstOrDefault(i => i.Used && i.Kind.Slot == "weapon");

    /// <summary>Rüstung und Schild mindern Schaden: je Teil Random(r/3, r), insgesamt höchstens Schaden − 1 (defmod.kod).</summary>
    private int ReduceByArmor(Avatar a, int damage)
    {
        foreach (var piece in a.State.Inventory.Where(i => i.Used && i.Kind.DamageReduce > 0))
        {
            int r = piece.Kind.DamageReduce;
            damage -= Math.Clamp(_random.Next(r / 3, r + 1), 0, Math.Max(0, damage - 1));
        }
        return damage;
    }

    /// <summary>Gegenstand benutzen: Essen wird gegessen, Waffen/Schilde/Rüstungen werden angelegt
    /// (ein Gegenstand je Platz; ein bereits angelegter wird dabei abgelegt).</summary>
    public void Use(Avatar avatar, uint id)
    {
        lock (_gate)
        {
            if (avatar.State.Find(id) is not { } item)
                return;
            if (item.Kind.IsFood)
            {
                Eat(avatar, item);
                return;
            }
            if (item.Kind.Slot is not { } slot)
            {
                Tell(avatar, $"Mit {item.Describe()} kannst du nichts anfangen.");
                return;
            }
            if (item.Used)
                return;
            foreach (var other in avatar.State.Inventory.Where(i => i.Used && i.Kind.Slot == slot).ToList())
                UnuseLocked(avatar, other, quiet: false);
            item.Used = true;
            avatar.Session.Post(GameReplies.Use(item.Id));
            Tell(avatar, slot switch
            {
                "weapon" => $"Du nimmst {item.Describe()} in die Hand.",
                "armor" => $"Du ziehst {item.Describe()} an.",
                _ => item.Kind.Light ? $"Du zündest {item.Describe()} an." : $"Du nimmst {item.Describe()} in die Hand.",
            });
        }
    }

    public void Unuse(Avatar avatar, uint id)
    {
        lock (_gate)
        {
            if (avatar.State.Find(id) is { Used: true } item)
                UnuseLocked(avatar, item, quiet: false);
        }
    }

    private void UnuseLocked(Avatar avatar, Item item, bool quiet)
    {
        item.Used = false;
        avatar.Session.Post(GameReplies.Unuse(item.Id));
        if (!quiet)
            Tell(avatar, item.Kind.Slot == "armor" ? $"Du ziehst {item.Describe()} aus." : $"Du legst {item.Describe()} weg.");
    }

    /// <summary>Essen und Trinken wie food.kod/player.kod EatSomething: Magen darf nicht über 100,
    /// Nährwert wird zu Ausdauer, eine Einheit wird verbraucht.</summary>
    private void Eat(Avatar a, Item food)
    {
        UpdateStomach(a.State);
        if (a.State.Stomach + food.Kind.Filling > 100)
        {
            Tell(a, food.Kind.Beverage ? "Du kannst keinen Schluck mehr trinken." : "Du bist zu satt, um noch etwas zu essen.");
            return;
        }
        double before = a.State.Stomach;
        a.State.Stomach += food.Kind.Filling;
        Tell(a, food.Kind.Beverage ? $"Du trinkst {food.Kind.Describe(1)}." : $"Du isst {food.Kind.Describe(1)}.");
        AddExertion(a, -10000 * food.Kind.Nutrition);
        if (a.State.Stomach >= 50 && a.State.Stomach < 75 && before < 50)
            Tell(a, "Du bist halb satt.");
        if (a.State.Stomach >= 75 && before < 75)
            Tell(a, "Du bist fast satt.");
        TakeNumber(a, food, 1);
    }

    private static void UpdateStomach(CharacterState s)
    {
        var now = DateTime.UtcNow;
        s.Stomach = Math.Clamp(s.Stomach - (now - s.StomachUpdated).TotalSeconds * CombatRules.StomachPerSecond, 1, 100);
        s.StomachUpdated = now;
    }

    /// <summary>player.kod AddExertion: Anstrengung sammelt sich und kostet ab MIN_VIGOR_CHANGE Ausdauer;
    /// negative Anstrengung (Essen) gibt Ausdauer.</summary>
    private void AddExertion(Avatar a, int amount)
    {
        var s = a.State;
        s.Exertion += amount;
        if (Math.Abs(s.Exertion) <= CombatRules.MinVigorChange)
            return;
        int lost = s.Exertion / 10000;
        s.Vigor = Math.Clamp(s.Vigor - lost, 0, CombatRules.MaxVigor);
        s.Exertion -= lost * 10000;
        a.Session.SendVigor();
    }

    /// <summary>Ausdauer für BP_STAT.</summary>
    public int VigorOf(Avatar a)
    {
        lock (_gate)
            return a.State.Vigor;
    }

    // ------------------------------------------------------------------ Boden: Aufheben und Ablegen

    private void PutOnFloor(Room room, Item item, ushort row, ushort col)
    {
        var floor = new FloorItem(item, row, col);
        room.Items.Add(floor);
        foreach (var a in room.Avatars.Values)
        {
            EnsureItemResources(a.Session, item);
            a.Session.Post(GameReplies.Create(ToRoomObject(floor)));
        }
        while (room.Items.Count > MaxFloorItems)
        {
            var old = room.Items[0];
            room.Items.RemoveAt(0);
            BroadcastRoom(room, GameReplies.Remove(old.Item.Id));
        }
    }

    /// <summary>Gegenstand vom Boden aufheben (BP_REQ_GET).</summary>
    public void Get(Avatar avatar, uint id)
    {
        lock (_gate)
        {
            var room = avatar.Room;
            var floor = room.Items.FirstOrDefault(f => f.Item.Id == id);
            if (floor is null)
            {
                Tell(avatar, "Das kannst du nicht aufheben.");
                return;
            }
            if (Distance(avatar.Row, avatar.Col, floor.Row, floor.Col) > GetRange)
            {
                Tell(avatar, "Das ist zu weit weg.");
                return;
            }
            room.Items.Remove(floor);
            BroadcastRoom(room, GameReplies.Remove(floor.Item.Id));
            Tell(avatar, $"Du hast {floor.Item.Describe()} aufgehoben.");
            Give(avatar, floor.Item);
        }
    }

    /// <summary>Gegenstände ablegen (BP_REQ_DROP). Mengen-Gegenstände werden bei Teilmengen geteilt.</summary>
    public void Drop(Avatar avatar, IReadOnlyList<ObjectRef> refs)
    {
        lock (_gate)
        {
            foreach (var r in refs)
            {
                if (avatar.State.Find(r.Id) is not { } item)
                    continue;
                Item dropped;
                if (item.Kind.Number)
                {
                    int amount = (int)Math.Min(r.Amount ?? (uint)item.Number, (uint)item.Number);
                    if (amount <= 0)
                        continue;
                    dropped = NewItem(item.Kind, amount);
                    TakeNumber(avatar, item, amount);
                }
                else
                {
                    dropped = item;
                    Take(avatar, item);
                }
                Tell(avatar, $"Du hast {dropped.Describe()} fallen gelassen.");
                PutOnFloor(avatar.Room, dropped, avatar.Row, avatar.Col);
            }
        }
    }

    // ------------------------------------------------------------------ Beute (monster.kod CreateTreasure)

    private void CreateTreasure(Monster m)
    {
        var kind = m.Kind;
        var drops = new List<Item>();
        // Skelette lassen zusätzlich immer ein (beschädigtes) Langschwert fallen (skel.kod CreateTreasure).
        if (kind.AlwaysDrops is { } always && _items.TryGet(always, out var alwaysKind))
            drops.Add(NewItem(alwaysKind));

        if (kind.Treasure is { } table && _treasure.TryGetValue(table, out var entries) && entries.Count > 0)
        {
            int count = Math.Clamp(1 + kind.Level / 55 + _random.Next(0, kind.Difficulty / 3 + 1), 1, 6);
            for (int i = 0; i < count; i++)
                if (GenerateTreasure(entries, kind.Level) is { } item)
                    drops.Add(item);
        }

        foreach (var item in drops)
            PutOnFloor(m.Room, item, m.Row, m.Col);
    }

    /// <summary>TreasureType.GenerateTreasure: Gewichte als Prozent (Rest auf den ersten Eintrag),
    /// Wurf 0..100; Geld: 1 + 2 × Random(level/2, 3·level/2).</summary>
    private Item? GenerateTreasure(IReadOnlyList<(ItemKind Kind, int Percent)> entries, int level)
    {
        int roll = _random.Next(0, 101), sum = 0;
        foreach (var (kind, percent) in entries)
        {
            sum += percent;
            if (roll > sum)
                continue;
            int number = kind.IsMoney ? 1 + 2 * Math.Max(1, _random.Next(level / 2, 3 * level / 2 + 1)) : 1;
            return NewItem(kind, number);
        }
        return null;
    }

    /// <summary>Beutetabellen mit auf 100 normierten Gewichten (TreasureType.Constructed).</summary>
    private readonly Dictionary<string, List<(ItemKind Kind, int Percent)>> _treasure = new(StringComparer.OrdinalIgnoreCase);

    private void SetUpTreasure(IReadOnlyDictionary<string, IReadOnlyList<TreasureEntry>> tables)
    {
        foreach (var (name, table) in tables)
        {
            var known = table.Where(e => _items.TryGet(e.Item, out _)).ToList();
            int total = known.Sum(e => e.Weight);
            if (total == 0)
                total = 100;
            var list = known.Select(e => (_items[e.Item], e.Weight * 100 / total)).ToList();
            int sum = list.Sum(e => e.Item2);
            if (list.Count > 0 && sum < 100)
                list[0] = (list[0].Item1, list[0].Item2 + 100 - sum);
            _treasure[name] = list;
        }
    }

    // ------------------------------------------------------------------ NPC-Texte

    /// <summary>NPC spricht zu allen im Raum (SomeoneSaid).</summary>
    private void NpcSay(Npc npc, Room room, string text)
    {
        byte[] said = GameReplies.Said(npc.Id, _resources[npc.DisplayName], SayType.Normal, _resources["%q"], $"{npc.Name}: {text}");
        foreach (var a in room.Avatars.Values)
        {
            a.Session.EnsureResource("%q");
            a.Session.EnsureResource(npc.DisplayName);
            a.Session.Post(said);
        }
    }

    /// <summary>NPC spricht nur zu einem Spieler (SayToOne).</summary>
    private void NpcTell(Npc npc, Avatar to, string text)
    {
        to.Session.EnsureResource("%q");
        to.Session.EnsureResource(npc.DisplayName);
        to.Session.Post(GameReplies.Said(npc.Id, _resources[npc.DisplayName], SayType.Normal, _resources["%q"], $"{npc.Name} sagt dir: {text}"));
    }

    private Npc? NpcInRoom(Avatar avatar, uint id) => avatar.Room.Npcs.FirstOrDefault(n => n.Id == id);

    // ------------------------------------------------------------------ Kaufen (UserBuy / Buy)

    /// <summary>Waren in der Reihenfolge von KOD GetForSale (Liste wird per cons aufgebaut: Waren rückwärts,
    /// danach die freigeschalteten bedingten Waren).</summary>
    private static List<(Item Item, int? FixedPrice)> ForSale(Merchant merchant)
    {
        var items = merchant.Stock.Where(s => s.FixedPrice is null).Reverse();
        var conditional = merchant.Stock.Where(s => s.FixedPrice is not null).Reverse();
        return items.Concat(conditional).ToList();
    }

    private static int PriceOf((Item Item, int? FixedPrice) entry, Merchant merchant) =>
        entry.FixedPrice ?? TradeRules.SellPrice(entry.Item.Kind, merchant.Definition.Markup);

    /// <summary>Ladenliste anfordern (BP_REQ_BUY → BP_BUY_LIST).</summary>
    public void RequestBuyList(Avatar avatar, uint npcId)
    {
        lock (_gate)
        {
            if (NpcInRoom(avatar, npcId) is not { Merchant: { Definition.Seller: true } merchant } npc)
                return;
            var list = ForSale(merchant);
            if (list.Count == 0)
            {
                NpcSay(npc, avatar.Room, "Ich habe im Moment leider nichts zu verkaufen.");
                return;
            }
            avatar.Session.EnsureResource(npc.DisplayName);
            avatar.Session.EnsureResource(PlayerIcon);
            foreach (var (item, _) in list)
                EnsureItemResources(avatar.Session, item);
            var entries = list.Select(e => new BuyListEntry(ToRoomObject(e.Item), (uint)PriceOf(e, merchant))).ToList();
            avatar.Session.Post(GameReplies.BuyList(ToRoomObject(npc), entries));
        }
    }

    /// <summary>Waren kaufen (BP_REQ_BUY_ITEMS) wie monster.kod Buy.</summary>
    public void BuyItems(Avatar avatar, uint npcId, IReadOnlyList<ObjectRef> refs)
    {
        lock (_gate)
        {
            if (NpcInRoom(avatar, npcId) is not { Merchant: { } merchant } npc)
                return;   // nicht im selben Raum (Original: Debug-Alarm)
            if (refs.Any(r => r.Amount is 0))
                return;   // „Bad buy quantity“

            foreach (var r in refs)
            {
                var entry = merchant.Stock.FirstOrDefault(s => s.Item.Id == r.Id);
                int price = entry.Item is null ? 0 : PriceOf(entry, merchant);
                int amount = 1;
                if (entry.Item is { Kind.Number: true })
                {
                    amount = (int)Math.Clamp(r.Amount ?? 1, 1, TradeRules.MaxBuyAmount);
                    price = Math.Max(1, price * amount);
                }

                if (entry.Item is null)
                {
                    NpcSay(npc, avatar.Room, "Du wolltest etwas kaufen, das hier gar nicht verkauft wird.");
                    continue;
                }
                if ((avatar.State.Find(_items.Money)?.Number ?? 0) < price)
                {
                    NpcSay(npc, avatar.Room, $"Komm wieder, wenn du genug Geld für {entry.Item.Kind.Describe(amount)} hast.");
                    continue;
                }

                Give(avatar, entry.Item.Kind, amount);
                TakeMoney(avatar, price);
                // Bedingte Ware ist nach dem Kauf wieder verborgen (RemoveFromConditionalList).
                if (entry.FixedPrice is not null)
                {
                    merchant.Stock.Remove(entry);
                    var sale = merchant.Definition.Conditional.First(c => _items[c.Item] == entry.Item.Kind);
                    merchant.Hidden.Add((sale, entry.Item));
                }
            }
        }
    }

    /// <summary>Bedingte Ware freischalten, wenn ihr Name im Gespräch fällt (Bibliothek LIBACT_CONDITIONAL).</summary>
    private bool TryConditionalSale(Npc npc, Room room, string text)
    {
        if (npc.Merchant is not { } merchant)
            return false;
        string lower = text.ToLowerInvariant();
        foreach (var hidden in merchant.Hidden.ToList())
        {
            if (!hidden.Sale.Triggers.Any(t => lower.Contains(t, StringComparison.Ordinal)))
                continue;
            merchant.Hidden.Remove(hidden);
            merchant.Stock.Add((hidden.Item, hidden.Sale.Price));
            NpcSay(npc, room, string.Format(hidden.Sale.Reply, hidden.Item.Describe()));
            return true;
        }
        return false;
    }

    // ------------------------------------------------------------------ Anbieten / Verkaufen (UserOffer, ReqOffer, Offer)

    /// <summary>Gegenstände einem NPC anbieten (BP_REQ_OFFER). Käufer antworten mit einem Gegenangebot in Geld,
    /// Empfänger (Bankier) nehmen Gewolltes direkt an.</summary>
    public void Offer(Avatar avatar, uint npcId, IReadOnlyList<ObjectRef> refs)
    {
        lock (_gate)
        {
            if (refs.Any(r => r.Amount is 0))
                return;
            if (avatar.Room.Avatars.TryGetValue(npcId, out var player) && player != avatar)
            {
                OfferToPlayer(avatar, player, refs);
                return;
            }
            var npc = NpcInRoom(avatar, npcId);
            var merchant = npc?.Merchant;
            if (npc is null || merchant is null || avatar.Trading
                || !(merchant.Definition.Buyer || merchant.Definition.Receive))
            {
                string who = npc?.Name ?? (avatar.Room.Avatars.TryGetValue(npcId, out var other) ? other.Name : "dem");
                Tell(avatar, $"Du kannst {who} nichts anbieten.");
                return;
            }

            // Angebotene Gegenstände: bei Mengen-Gegenständen eine Kopie mit der gewünschten Menge.
            var offered = new List<(Item Source, Item Offer)>();
            foreach (var r in refs)
            {
                if (avatar.State.Find(r.Id) is not { } item)
                    return;
                offered.Add(item.Kind.Number
                    ? (item, NewItem(item.Kind, (int)Math.Min(r.Amount ?? 1, (uint)item.Number)))
                    : (item, item));
            }
            if (offered.Count == 0)
                return;

            var def = merchant.Definition;
            if (def.Receive)
            {
                bool allWanted = ReceiveGifts(avatar, npc, merchant, offered, notifyUnwanted: !def.Buyer);
                if (allWanted || !def.Buyer)
                    return;
            }

            // MOB_BUYER
            if (merchant.Customer is not null)
            {
                Tell(avatar, $"{npc.Name} ist gerade mit einem anderen Kunden beschäftigt.");
                return;
            }
            if (offered.Any(o => !Desires(def, o.Offer.Kind)))
            {
                NpcTell(npc, avatar, "Daran habe ich kein Interesse.");
                return;
            }
            if (offered.Sum(o => o.Offer.Value) == 0)
            {
                NpcTell(npc, avatar, "Dafür kann ich dir nicht einmal einen Schilling geben.");
                return;
            }

            foreach (var (_, o) in offered)
                EnsureItemResources(avatar.Session, o);
            avatar.Session.Post(GameReplies.Offered(offered.Select(o => ToRoomObject(o.Offer)).ToList()));

            // Offer: Geld als Gegenangebot, Timer 30 s.
            int money = offered.Sum(o => TradeRules.BuyBackPrice(o.Offer, def.Markup));
            var payment = NewItem(_items.Money, money);
            avatar.OfferTo = npc;
            avatar.OfferItems = offered;
            avatar.OfferPayment = payment;
            merchant.Customer = avatar;
            merchant.CustomerTimeout = DateTime.UtcNow + TradeRules.OfferTimeout;
            EnsureItemResources(avatar.Session, payment);
            avatar.Session.Post(GameReplies.CounterOffer([ToRoomObject(payment)]));
        }
    }

    /// <summary>ObjectDesired eines Käufers (z. B. Frisconar: Reagenzien, aber keine Edelsteine).</summary>
    private static bool Desires(ShopDefinition def, ItemKind kind) =>
        (def.BuysTypes.Count == 0 || def.BuysTypes.Any(kind.Is)) && !def.BuysExceptTypes.Any(kind.Is);

    /// <summary>ReqGive/GotWantedItem/GotUnwantedItem. Liefert true, wenn alles gewollt war.</summary>
    private bool ReceiveGifts(Avatar avatar, Npc npc, Merchant merchant, List<(Item Source, Item Offer)> offered, bool notifyUnwanted)
    {
        var def = merchant.Definition;
        bool allWanted = true;
        foreach (var (source, offer) in offered)
        {
            bool wanted = def.Wanted.Any(w => _items.TryGet(w, out var k) && k == offer.Kind);
            if (!wanted)
            {
                allWanted = false;
                if (notifyUnwanted)
                    Tell(avatar, string.Format(def.UnwantedGive ?? "{0} sagt dir: „Das will ich nicht.“", npc.Name));
                continue;
            }

            if (def.Bank is { } bank && offer.Kind.IsMoney)
            {
                BankDeposit(avatar, npc, bank, offer.Number);
                continue;
            }
            if (def.GemTrade && offer.Kind.Is("gem"))
                TradeGems(avatar, npc, merchant, source, offer);
        }
        return allWanted;
    }

    /// <summary>Edelsteintausch des Tos-Bankiers (TsBanker CheckWhyWanted).</summary>
    private void TradeGems(Avatar avatar, Npc npc, Merchant merchant, Item source, Item offer)
    {
        string plural = offer.Kind.Plural ?? offer.Kind.Name;
        if (offer.Number < 4)
        {
            NpcSay(npc, avatar.Room, "Einzelne Steine nehme ich nicht. Bring mir mindestens vier auf einmal.");
            return;
        }
        if (_random.Next(0, 20) == 0)
        {
            NpcSay(npc, avatar.Room, $"Diese Steine taugen nichts. Ich entsorge die wertlosen {plural} für dich.");
            TakeNumber(avatar, source, offer.Number);
            return;
        }
        if (source.Number < offer.Number)
        {
            NpcSay(npc, avatar.Room, "Hmm. Du scheinst gar nicht zu besitzen, was du mir verkaufen willst.");
            return;
        }
        int value = offer.Value * (100 - 10 * merchant.Definition.Markup) / 100;
        Give(avatar, _items.Money, value);
        NpcSay(npc, avatar.Room, $"Schönere {plural} habe ich selten gesehen. Ein Vergnügen, mit dir Geschäfte zu machen.");
        TakeNumber(avatar, source, offer.Number);
    }

    /// <summary>Gegenangebot annehmen (BP_ACCEPT_OFFER) wie user.kod UserAcceptOffer.</summary>
    public void AcceptOffer(Avatar avatar)
    {
        lock (_gate)
        {
            if (avatar.OfferPlayer is not null)
            {
                AcceptPlayerOffer(avatar);
                return;
            }
            if (avatar.OfferTo is not { Merchant: { } merchant } || avatar.OfferPayment is not { } payment)
            {
                Tell(avatar, "Es gibt kein Angebot, das du annehmen könntest.");
                return;
            }
            // CheckOfferStuff: Der Spieler muss die Gegenstände noch besitzen.
            bool stillThere = avatar.OfferItems.All(o => avatar.State.Inventory.Contains(o.Source) && o.Source.Number >= o.Offer.Number);
            if (!stillThere)
            {
                Tell(avatar, "Du besitzt nicht mehr alles, was du angeboten hast.");
                CancelOfferLocked(avatar);
                return;
            }
            foreach (var (source, offer) in avatar.OfferItems)
            {
                if (source.Kind.Number)
                    TakeNumber(avatar, source, offer.Number);
                else
                    Take(avatar, source);
            }
            Give(avatar, payment);
            merchant.Customer = null;
            avatar.OfferTo = null;
            avatar.OfferItems = [];
            avatar.OfferPayment = null;
        }
    }

    /// <summary>Angebot abbrechen (BP_CANCEL_OFFER).</summary>
    public void CancelOffer(Avatar avatar)
    {
        lock (_gate)
            CancelOfferLocked(avatar);
    }

    private void CancelOfferLocked(Avatar avatar)
    {
        if (avatar.OfferPlayer is { } partner)
        {
            // user.kod CancelIfOffer / OfferCanceled: beide Seiten schließen den Handel.
            Tell(partner, $"{avatar.Name} hat den Handel abgebrochen.");
            EndPlayerTrade(partner);
            EndPlayerTrade(avatar);
            return;
        }
        if (avatar.OfferTo is not { } npc)
            return;
        if (npc.Merchant is { } merchant && merchant.Customer == avatar)
            merchant.Customer = null;
        avatar.OfferTo = null;
        avatar.OfferItems = [];
        avatar.OfferPayment = null;
        avatar.Session.Post(GameReplies.OfferCanceled());
    }

    // ------------------------------------------------------------------ Handel zwischen Spielern (user.kod)

    /// <summary>Angebotene Gegenstände prüfen und vorbereiten (UserOffer/UserCounterOffer): eigene, keine doppelten,
    /// Mengen 1..Bestand. null = ungültig.</summary>
    private List<(Item Source, Item Offer)>? PrepareOffer(Avatar a, IReadOnlyList<ObjectRef> refs)
    {
        var list = new List<(Item Source, Item Offer)>();
        foreach (var r in refs)
        {
            if (a.State.Find(r.Id) is not { } item || list.Any(o => o.Source == item))
                return null;
            if (item.Kind.Number)
            {
                int amount = (int)(r.Amount ?? 1);
                if (amount < 1 || amount > item.Number)
                {
                    Tell(a, $"Du hast keine {amount} davon.");
                    return null;
                }
                list.Add((item, NewItem(item.Kind, amount)));
            }
            else
                list.Add((item, item));
        }
        return list;
    }

    private List<RoomObject> ShowItems(Avatar to, IEnumerable<(Item Source, Item Offer)> items)
    {
        var list = new List<RoomObject>();
        foreach (var (_, o) in items)
        {
            EnsureItemResources(to.Session, o);
            list.Add(ToRoomObject(o));
        }
        return list;
    }

    private void OfferToPlayer(Avatar from, Avatar to, IReadOnlyList<ObjectRef> refs)
    {
        if (from.Trading)
        {
            Tell(from, $"Du kannst {to.Name} nichts anbieten.");
            return;
        }
        if (to.Trading)
        {
            Tell(from, $"{to.Name} ist gerade mit einem anderen Handel beschäftigt.");
            return;
        }
        if (PrepareOffer(from, refs) is not { Count: > 0 } items)
            return;
        from.OfferPlayer = to;
        from.OfferInitiator = true;
        from.OfferItems = items;
        from.Session.Post(GameReplies.Offered(ShowItems(from, items)));

        to.OfferPlayer = from;
        to.OfferInitiator = false;
        to.OfferItems = [];
        Tell(to, $"{from.Name} bietet dir etwas an.");
        to.Session.EnsureResource(from.Name);
        to.Session.EnsureResource(PlayerIcon);
        to.Session.Post(GameReplies.Offer(ToRoomObject(from), ShowItems(to, items)));
    }

    /// <summary>Gegenangebot (BP_REQ_COUNTEROFFER) des Angebotenen; eine leere Liste nimmt das Angebot als Geschenk an.</summary>
    public void CounterOffer(Avatar avatar, IReadOnlyList<ObjectRef> refs)
    {
        lock (_gate)
        {
            if (avatar.OfferPlayer is not { } partner || avatar.OfferInitiator)
            {
                Tell(avatar, "Du hast kein Angebot, auf das du antworten könntest.");
                return;
            }
            if (PrepareOffer(avatar, refs) is not { } items)
            {
                CancelOfferLocked(avatar);
                return;
            }
            avatar.OfferItems = items;
            avatar.Session.Post(GameReplies.CounterOffered(ShowItems(avatar, items)));
            partner.OfferOtherAccepted = true;
            partner.Session.Post(GameReplies.CounterOffer(ShowItems(partner, items)));
        }
    }

    /// <summary>UserAcceptOffer zwischen Spielern: beide Seiten müssen ihre Gegenstände noch besitzen, dann wird getauscht.</summary>
    private void AcceptPlayerOffer(Avatar a)
    {
        var b = a.OfferPlayer!;
        if (!a.OfferInitiator || !a.OfferOtherAccepted)
        {
            Tell(a, "Es gibt noch kein Gegenangebot, das du annehmen könntest.");
            return;
        }
        static bool StillHas(Avatar x) =>
            x.OfferItems.All(o => x.State.Inventory.Contains(o.Source) && o.Source.Number >= o.Offer.Number);
        if (!StillHas(a) || !StillHas(b))
        {
            Tell(a, "Nicht mehr alle angebotenen Gegenstände sind vorhanden.");
            CancelOfferLocked(a);
            return;
        }
        var fromA = a.OfferItems;
        var fromB = b.OfferItems;
        foreach (var (source, offer) in fromA)
            Remove(a, source, offer);
        foreach (var (source, offer) in fromB)
            Remove(b, source, offer);
        foreach (var (_, offer) in fromA)
            Give(b, offer);
        foreach (var (_, offer) in fromB)
            Give(a, offer);
        Tell(a, $"Der Handel mit {b.Name} ist abgeschlossen.");
        Tell(b, $"Der Handel mit {a.Name} ist abgeschlossen.");
        EndPlayerTrade(b);   // AcceptOffer schickt dem Partner BP_OFFER_CANCELED, damit sein Dialog schließt
        EndPlayerTrade(a, notify: false);

        void Remove(Avatar owner, Item source, Item offer)
        {
            if (source.Kind.Number)
                TakeNumber(owner, source, offer.Number);
            else
                Take(owner, source);
        }
    }

    private static void EndPlayerTrade(Avatar a, bool notify = true)
    {
        a.OfferPlayer = null;
        a.OfferInitiator = false;
        a.OfferOtherAccepted = false;
        a.OfferItems = [];
        if (notify)
            a.Session.Post(GameReplies.OfferCanceled());
    }

    /// <summary>CancelOfferTimer: Händler zieht sein Gegenangebot zurück.</summary>
    private void TickOffers(Room room, DateTime now)
    {
        foreach (var npc in room.Npcs)
        {
            if (npc.Merchant is not { Customer: { } customer } merchant || now < merchant.CustomerTimeout)
                continue;
            NpcTell(npc, customer, "Kannst dich nicht entscheiden? Denk darüber nach und komm wieder, wenn du so weit bist.");
            CancelOfferLocked(customer);
            merchant.Customer = null;
        }
    }

    // ------------------------------------------------------------------ Bank (UC_DEPOSIT / UC_WITHDRAW / UC_BALANCE)

    /// <summary>Bankbefehl an einen Bankier im selben Raum (SomeoneTryUserCommand).</summary>
    public void BankCommand(Avatar avatar, UserCommandType command, int amount = 0)
    {
        lock (_gate)
        {
            var banker = avatar.Room.Npcs.FirstOrDefault(n => n.Merchant?.Definition.Bank is not null);
            if (banker?.Merchant?.Definition.Bank is not { } bank)
            {
                Tell(avatar, command switch
                {
                    UserCommandType.Deposit => "Hier ist niemand, bei dem du Geld einzahlen kannst.",
                    UserCommandType.Withdraw => "Hier ist niemand, bei dem du Geld abheben kannst.",
                    _ => "Hier ist niemand, der dir deinen Kontostand nennen kann.",
                });
                return;
            }
            switch (command)
            {
                case UserCommandType.Deposit:
                    BankDeposit(avatar, banker, bank, amount);
                    break;
                case UserCommandType.Withdraw:
                    BankWithdraw(avatar, banker, bank, amount);
                    break;
                case UserCommandType.Balance:
                    NpcTell(banker, avatar, $"Du hast {avatar.State.Bank.GetValueOrDefault(bank)} Schillinge auf deinem Konto.");
                    break;
            }
        }
    }

    private void BankDeposit(Avatar avatar, Npc banker, int bank, int amount)
    {
        if (amount <= 0)
            return;
        int money = avatar.State.Find(_items.Money)?.Number ?? 0;
        if (money < amount)
        {
            NpcTell(banker, avatar, $"Aber du hast doch nur {money} Schillinge bei dir!");
            return;
        }
        TakeMoney(avatar, amount);
        avatar.State.Bank[bank] = avatar.State.Bank.GetValueOrDefault(bank) + amount;
        NpcTell(banker, avatar, $"Danke für deine Einzahlung. Du hast jetzt {avatar.State.Bank[bank]} Schillinge auf deinem Konto.");
    }

    private void BankWithdraw(Avatar avatar, Npc banker, int bank, int amount)
    {
        if (amount < 0)
            return;
        int balance = avatar.State.Bank.GetValueOrDefault(bank);
        if (balance == 0)
        {
            NpcTell(banker, avatar, "Du hast kein Geld, das du abheben könntest!");
            return;
        }
        if (amount > balance)
        {
            NpcTell(banker, avatar, $"Aber du hast doch nur {balance} Schillinge auf deinem Konto!");
            return;
        }
        if (amount == 0)
            return;
        avatar.State.Bank[bank] = balance - amount;
        Give(avatar, _items.Money, amount);
        NpcTell(banker, avatar, $"Hier sind deine {amount} Schillinge. Danke für dein Vertrauen.");
    }

    // ------------------------------------------------------------------ Spielstand (GameStore)

    private void RememberPosition(Avatar a)
    {
        if (a.Room is null)
            return;
        _leftSinceSave.Add(a.Id);
        a.State.RoomId = a.Room.Definition.Id;
        a.State.Row = a.Row;
        a.State.Col = a.Col;
        a.State.Angle = a.Angle;
        a.State.Hp = a.Hp;
    }

    /// <summary>Charaktere, deren Stand seit dem letzten Speichern festgehalten wurde (eingeloggt oder ausgeloggt).</summary>
    private readonly HashSet<uint> _leftSinceSave = new();

    /// <summary>Zu speichernde Charaktere: alle eingeloggten (mit aktueller Position) und die seit dem
    /// letzten Speichern ausgeloggten. Die Liste der Ausgeloggten wird dabei geleert.</summary>
    public Dictionary<uint, SavedCharacter> TakeCharactersToSave()
    {
        lock (_gate)
        {
            foreach (var a in _rooms.Values.SelectMany(r => r.Avatars.Values))
                RememberPosition(a);
            var ids = new HashSet<uint>(_leftSinceSave);
            _leftSinceSave.Clear();
            return ids.Where(_characters.ContainsKey).ToDictionary(id => id, id => ToSaved(_characters[id]));
        }
    }

    /// <summary>Nach einem fehlgeschlagenen Speichern beim nächsten Mal erneut versuchen.</summary>
    public void SaveFailed(IEnumerable<uint> ids)
    {
        lock (_gate)
            _leftSinceSave.UnionWith(ids);
    }

    private static SavedCharacter ToSaved(CharacterState c) => new()
    {
        Room = c.RoomId,
        Row = c.Row,
        Col = c.Col,
        Angle = c.Angle,
        Hp = c.Hp,
        Inventory = c.Inventory.Select(i => new SavedItem(i.Kind.Key, i.Number, i.Used)).ToList(),
        Vigor = c.Vigor,
        Stomach = (int)c.Stomach,
        Bank = new Dictionary<int, int>(c.Bank),
    };

    /// <summary>Gespeicherte Charaktere laden (beim Serverstart, bevor jemand eingeloggt ist).</summary>
    public void LoadCharacters(IReadOnlyDictionary<uint, SavedCharacter> characters)
    {
        lock (_gate)
        {
            foreach (var (id, saved) in characters)
            {
                var state = new CharacterState
                {
                    Initialized = true,
                    RoomId = saved.Room,
                    Row = saved.Row,
                    Col = saved.Col,
                    Angle = saved.Angle,
                    Hp = saved.Hp,
                    Vigor = saved.Vigor ?? CombatRules.StartVigor,
                    Stomach = saved.Stomach,
                };
                foreach (var item in saved.Inventory)
                {
                    if (_items.TryGet(item.Item, out var kind))
                        state.Inventory.Add(new Item(++_nextItemId, kind, item.Number) { Used = item.Used && kind.Slot is not null });
                    else
                        Log.Warn($"Charakter {id}: unbekannter Gegenstand '{item.Item}' im Spielstand – entfällt");
                }
                foreach (var (bank, amount) in saved.Bank)
                    state.Bank[bank] = amount;
                _characters[id] = state;
            }
        }
    }

    // ------------------------------------------------------------------ Händler einrichten

    private void SetUpMerchants(ShopFile shops)
    {
        foreach (var npc in _rooms.Values.SelectMany(r => r.Npcs))
        {
            if (!shops.Shops.TryGetValue(npc.Definition.KodClass, out var def))
                continue;
            var merchant = new Merchant(def);
            foreach (var sale in def.ForSale)
                if (_items.TryGet(sale.Item, out var kind))
                    merchant.Stock.Add((NewItem(kind, sale.Number), null));
            foreach (var cond in def.Conditional)
                if (_items.TryGet(cond.Item, out var kind))
                    merchant.Hidden.Add((cond, NewItem(kind, cond.Number)));
            npc.Merchant = merchant;
        }
    }
}

/// <summary>Eintrag einer Beutetabelle (plTreasure): Gegenstand und Gewicht.</summary>
public sealed record TreasureEntry(string Item, int Weight);
