namespace Meridian.Protocol;

/// <summary>Grafische Darstellung eines Objekts: Icon-Ressource und Animation (protocol.md 4.2/4.5).</summary>
public sealed record ObjectAppearance(uint IconRsc, ushort Group = 1);

/// <summary>Objekt in einem Raum (protocol.md 4.6). Koordinaten in KOD-Feinkoordinaten (Feld*64 + fein).</summary>
public sealed record RoomObject(
    uint Id,
    uint NameRsc,
    ObjectAppearance Appearance,
    ushort Row,
    ushort Col,
    ushort Angle,
    uint Flags = 0);

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

    public static byte[] Inventory(IReadOnlyCollection<RoomObject> items)
    {
        var w = new PacketWriter((byte)GameMessage.Inventory).U16((ushort)items.Count);
        foreach (var o in items)
            WriteObjectDesc(w, o);
        return w.ToArray();
    }

    private static PacketWriter WriteObjectDesc(PacketWriter w, RoomObject o)
    {
        w.U32(o.Id).U32(o.Appearance.IconRsc).U32(o.NameRsc).U32(o.Flags)
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
