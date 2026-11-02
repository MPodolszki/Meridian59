namespace Meridian.Protocol;

/// <summary>Grafische Darstellung eines Objekts: Icon-Ressource und Animation (protocol.md 4.2/4.5).</summary>
public sealed record ObjectAppearance(uint IconRsc, ushort Group = 1);

/// <summary>Objekt in einem Raum (protocol.md 4.6). Koordinaten in KOD-Feinkoordinaten (Feld*64 + fein).
/// Amount != null kennzeichnet Mengen-Gegenstände (KOD NumberItem): Tag 1 in der ID, danach die Menge.</summary>
public sealed record RoomObject(
    uint Id,
    uint NameRsc,
    ObjectAppearance Appearance,
    ushort Row = 0,
    ushort Col = 0,
    ushort Angle = 0,
    uint Flags = 0,
    uint? Amount = null);

/// <summary>Eintrag einer Ladenliste (BP_BUY_LIST): Gegenstand und Preis pro Stück.</summary>
public sealed record BuyListEntry(RoomObject Item, uint Price);

/// <summary>Objekt-Flags (OF_* aus include/proto.h).</summary>
public static class ObjectFlags
{
    public const uint Player = 0x00000004;
    public const uint Attackable = 0x00000008;
    public const uint Gettable = 0x00000010;
    public const uint Offerable = 0x00000200;
    public const uint Buyable = 0x00000400;
}

/// <summary>Tag in den oberen 4 Bit einer Objekt-ID (CLIENT_TAG_NUMBER): Mengen-Gegenstand, es folgt u32 amount.</summary>
public static class ObjectId
{
    public const uint NumberTag = 0x10000000;
    public const uint Mask = 0x0FFFFFFF;
}

/// <summary>Eintrag einer object_list vom Client (C→S): ID und bei Mengen-Gegenständen die gewünschte Menge.</summary>
public readonly record struct ObjectRef(uint Id, uint? Amount)
{
    /// <summary>Liest eine object_list (u16 n, n × {id [, u32 amount]}) wie clientd3d/protocol.c sie sendet.</summary>
    public static List<ObjectRef> ReadList(ref PacketReader r)
    {
        int n = r.U16();
        var list = new List<ObjectRef>(n);
        for (int i = 0; i < n; i++)
        {
            uint raw = r.U32();
            list.Add(new ObjectRef(raw & ObjectId.Mask, raw >> 28 == 1 ? r.U32() : null));
        }
        return list;
    }

    public static PacketWriter WriteList(PacketWriter w, IReadOnlyCollection<ObjectRef> list)
    {
        w.U16((ushort)list.Count);
        foreach (var o in list)
        {
            if (o.Amount is { } amount)
                w.U32(o.Id | ObjectId.NumberTag).U32(amount);
            else
                w.U32(o.Id);
        }
        return w;
    }
}

/// <summary>Daten für BP_PLAYER (protocol.md 5.5, exakt 61 Byte Payload).</summary>
public sealed record PlayerInfo(
    uint PlayerId,
    uint IconRsc,
    uint NameRsc,
    uint RoomId,
    uint RoomFileRsc,
    uint RoomNameRsc,
    uint RoomSecurity,
    byte AmbientLight,
    byte PlayerLight,
    uint BackgroundRsc = 0,
    uint WadingSoundRsc = 0,
    uint RoomFlags = 0);

public sealed record CharacterEntry(uint Id, string Name, bool FirstTime);

/// <summary>Fertige Server→Client-Payloads für den Game-Modus.</summary>
public static class GameReplies
{
    /// <summary>BP_ECHO_PING: neuer Token (XOR 0xED) und Redbook-Ressource (0 = Default-String).</summary>
    public static byte[] EchoPing(byte newToken, uint redbookRsc = 0) =>
        new PacketWriter((byte)GameMessage.EchoPing).U8((byte)(newToken ^ 0xED)).U32(redbookRsc).ToArray();

    public static byte[] ChangeResource(uint rsc, string value) =>
        new PacketWriter((byte)GameMessage.ChangeResource).U32(rsc).Str(value).ToArray();

    public static byte[] LoadModule(uint moduleRsc) =>
        new PacketWriter((byte)GameMessage.LoadModule).U32(moduleRsc).ToArray();

    public static byte[] PlayMusic(uint musicRsc) =>
        new PacketWriter((byte)GameMessage.PlayMusic).U32(musicRsc).ToArray();

    public static byte[] Quit() => new PacketWriter((byte)GameMessage.Quit).ToArray();

    /// <summary>BP_MESSAGE ohne Parameter; der Format-String darf keine Platzhalter enthalten.</summary>
    public static byte[] Message(uint formatRsc) =>
        new PacketWriter((byte)GameMessage.Message).U32(formatRsc).ToArray();

    public static byte[] Characters(IReadOnlyList<CharacterEntry> characters, string motd)
    {
        var w = new PacketWriter((byte)GameMessage.Characters).U16((ushort)characters.Count);
        foreach (var c in characters)
            w.U32(c.Id).Str(c.Name).U8(c.FirstTime ? (byte)1 : (byte)0);
        return w.Str(motd).U8(0).ToArray();   // keine Werbebanner
    }

    public static byte[] Player(PlayerInfo p) => new PacketWriter((byte)GameMessage.Player)
        .U32(p.PlayerId).U32(p.IconRsc).U32(p.NameRsc)
        .U32(p.RoomId).U32(p.RoomFileRsc).U32(p.RoomNameRsc).U32(p.RoomSecurity)
        .U8(p.AmbientLight).U8(p.PlayerLight)
        .U32(p.BackgroundRsc).U32(p.WadingSoundRsc).U32(p.RoomFlags)
        .U32(0).U32(0).U32(0)   // Tiefen-Overrides
        .ToArray();

    public static byte[] RoomContents(uint roomId, IReadOnlyCollection<RoomObject> objects)
    {
        var w = new PacketWriter((byte)GameMessage.RoomContents).U32(roomId).U16((ushort)objects.Count);
        foreach (var o in objects)
            WriteRoomObject(w, o);
        return w.ToArray();
    }

    public static byte[] Create(RoomObject o) => WriteRoomObject(new PacketWriter((byte)GameMessage.Create), o).ToArray();

    /// <summary>BP_STAT mit einem numerischen Wert (protocol.md 4.9), z. B. Gruppe 1 / Nr. 1 = Lebenspunkte.</summary>
    public static byte[] Stat(byte group, byte num, uint nameRsc, int value, int max) =>
        new PacketWriter((byte)GameMessage.Stat)
            .U8(group).U8(num).U32(nameRsc).U8(1 /* STATS_NUMERIC */).U8(1 /* STAT_INT */)
            .U32((uint)value).U32(0).U32(100).U32((uint)max).ToArray();

    /// <summary>
    /// BP_CHANGE mit einmaliger Animation (ANIMATE_ONCE). Konvention des neuen Clients:
    /// Gruppe 2..4 = Angriff, Gruppe 9 = Sterben.
    /// </summary>
    public static byte[] Animate(RoomObject o, ushort low, ushort high, uint periodMs)
    {
        var w = new PacketWriter((byte)GameMessage.Change);
        WriteObjectDesc(w, o);
        w.U8((byte)AnimationType.Once).U32(periodMs).U16(low).U16(high).U16(1);
        return w.U8(0).ToArray();
    }

    public const ushort AttackGroup = 2;
    public const ushort DeathGroup = 9;

    public static byte[] Remove(uint id) => new PacketWriter((byte)GameMessage.Remove).U32(id).ToArray();

    /// <summary>BP_MOVE; speed 0 = Teleport, Bit 7 = in Laufrichtung drehen.</summary>
    public static byte[] Move(uint id, ushort row, ushort col, byte speed) =>
        new PacketWriter((byte)GameMessage.Move).U32(id).U16(row).U16(col).U8(speed).ToArray();

    public static byte[] Turn(uint id, ushort angle) =>
        new PacketWriter((byte)GameMessage.Turn).U32(id).U16(angle).ToArray();

    /// <summary>BP_SAID mit Format-Ressource, die genau ein %q (Text) erwartet.</summary>
    public static byte[] Said(uint senderId, uint senderNameRsc, SayType type, uint formatRsc, string text) =>
        new PacketWriter((byte)GameMessage.Said)
            .U32(senderId).U32(senderNameRsc).U8((byte)type).U32(formatRsc).Str(text).ToArray();

    public static byte[] Inventory(IReadOnlyCollection<RoomObject> items) =>
        WriteObjectList(new PacketWriter((byte)GameMessage.Inventory), items).ToArray();

    public static byte[] InventoryAdd(RoomObject o) => WriteObjectDesc(new PacketWriter((byte)GameMessage.InventoryAdd), o).ToArray();

    /// <summary>BP_USE / BP_UNUSE: Gegenstand wird benutzt (ausgerüstet) bzw. abgelegt.</summary>
    public static byte[] Use(uint id) => new PacketWriter((byte)GameMessage.Use).U32(id).ToArray();

    public static byte[] Unuse(uint id) => new PacketWriter((byte)GameMessage.Unuse).U32(id).ToArray();

    /// <summary>BP_USE_LIST: alle benutzten Gegenstände (nach BP_INVENTORY).</summary>
    public static byte[] UseList(IReadOnlyCollection<uint> ids)
    {
        var w = new PacketWriter((byte)GameMessage.UseList).U16((ushort)ids.Count);
        foreach (uint id in ids)
            w.U32(id);
        return w.ToArray();
    }

    public static byte[] InventoryRemove(uint id) => new PacketWriter((byte)GameMessage.InventoryRemove).U32(id).ToArray();

    /// <summary>BP_CHANGE ohne Animation, z. B. geänderte Menge eines Gegenstands (KOD SomethingChanged).</summary>
    public static byte[] Change(RoomObject o)
    {
        var w = WriteObjectDesc(new PacketWriter((byte)GameMessage.Change), o);
        WriteAnimation(w, o.Appearance.Group);
        return w.U8(0).ToArray();
    }

    /// <summary>BP_BUY_LIST: Verkäufer, dann je Ware object_desc und Preis (KOD user.kod UserBuy).</summary>
    public static byte[] BuyList(RoomObject seller, IReadOnlyCollection<BuyListEntry> items)
    {
        var w = WriteObjectDesc(new PacketWriter((byte)GameMessage.BuyList), seller).U16((ushort)items.Count);
        foreach (var e in items)
            WriteObjectDesc(w, e.Item).U32(e.Price);
        return w.ToArray();
    }

    /// <summary>BP_OFFERED: Bestätigung der eigenen angebotenen Gegenstände.</summary>
    public static byte[] Offered(IReadOnlyCollection<RoomObject> items) =>
        WriteObjectList(new PacketWriter((byte)GameMessage.Offered), items).ToArray();

    /// <summary>BP_COUNTEROFFER: Gegenangebot des Handelspartners (bei NPCs: Geld).</summary>
    public static byte[] CounterOffer(IReadOnlyCollection<RoomObject> items) =>
        WriteObjectList(new PacketWriter((byte)GameMessage.CounterOffer), items).ToArray();

    /// <summary>BP_OFFER: Ein Spieler bietet dir etwas an (Anbieter, Gegenstände).</summary>
    public static byte[] Offer(RoomObject offerer, IReadOnlyCollection<RoomObject> items) =>
        WriteObjectList(WriteObjectDesc(new PacketWriter((byte)GameMessage.Offer), offerer), items).ToArray();

    /// <summary>BP_COUNTEROFFERED: Bestätigung des eigenen Gegenangebots.</summary>
    public static byte[] CounterOffered(IReadOnlyCollection<RoomObject> items) =>
        WriteObjectList(new PacketWriter((byte)GameMessage.CounterOffered), items).ToArray();

    public static byte[] OfferCanceled() => new PacketWriter((byte)GameMessage.OfferCanceled).ToArray();

    private static PacketWriter WriteObjectList(PacketWriter w, IReadOnlyCollection<RoomObject> items)
    {
        w.U16((ushort)items.Count);
        foreach (var o in items)
            WriteObjectDesc(w, o);
        return w;
    }

    private static PacketWriter WriteObjectDesc(PacketWriter w, RoomObject o)
    {
        if (o.Amount is { } amount)
            w.U32(o.Id | ObjectId.NumberTag).U32(amount);
        else
            w.U32(o.Id);
        w.U32(o.Appearance.IconRsc).U32(o.NameRsc).U32(o.Flags)
            .U32(0)    // rarity
            .U16(0);   // dlight: keine Lichtquelle
        WriteAnimation(w, o.Appearance.Group);
        return w.U8(0);   // keine Overlays
    }

    private static PacketWriter WriteRoomObject(PacketWriter w, RoomObject o)
    {
        WriteObjectDesc(w, o);
        w.U16(o.Row).U16(o.Col).U16(o.Angle);
        WriteAnimation(w, o.Appearance.Group);   // Bewegungsanimation
        return w.U8(0);                          // Bewegungs-Overlays
    }

    private static void WriteAnimation(PacketWriter w, ushort group) =>
        w.U8((byte)AnimationType.None).U16(group);
}
