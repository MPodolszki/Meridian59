using Meridian.Protocol;

namespace Meridian.Server;

/// <summary>Startraum. Die Werte beziehen sich auf Dateien des Clients (.roo/.bgf).</summary>
public sealed record RoomOptions
{
    public string RooFile { get; init; } = "tosinn.roo";
    public string Name { get; init; } = "Gasthaus";
    /// <summary>Muss zur .roo-Datei des Clients passen (Bytes 8..11), sonst wird der Spieler „blind“.
    /// Wird beim Start aus der Datei gelesen, wenn <see cref="ServerOptions.ClientResourceDir"/> gesetzt ist.</summary>
    public uint Security { get; init; }
    public ushort StartRow { get; init; } = 5 * 64 + 32;
    public ushort StartCol { get; init; } = 5 * 64 + 32;
    public byte AmbientLight { get; init; } = 200;
    public string PlayerIcon { get; init; } = "blank.bgf";
}

/// <summary>Ein Spieler im Raum.</summary>
public sealed class Avatar(uint id, string name, Session session)
{
    public uint Id => id;
    public string Name => name;
    public Session Session => session;
    public ushort Row { get; set; }
    public ushort Col { get; set; }
    public ushort Angle { get; set; }
}

/// <summary>
/// Die Spielwelt – vorerst ein einziger Raum. Alle Änderungen laufen unter einem Lock,
/// Nachrichten an andere Sessions werden nur eingereiht (Session.Post), nie direkt gesendet.
/// </summary>
public sealed class World
{
    public const uint RoomId = 1;

    private readonly object _gate = new();
    private readonly Dictionary<uint, Avatar> _avatars = new();
    private readonly RoomOptions _room;
    private readonly ResourceRegistry _resources;

    public World(RoomOptions room, ResourceRegistry resources)
    {
        _room = room;
        _resources = resources;
    }

    public RoomOptions Room => _room;

    public RoomObject ToRoomObject(Avatar a) =>
        new(a.Id, _resources[a.Name], new ObjectAppearance(_resources[_room.PlayerIcon]), a.Row, a.Col, a.Angle);

    public List<Avatar> Snapshot()
    {
        lock (_gate)
            return _avatars.Values.ToList();
    }

    /// <summary>Spieler betritt den Raum. Liefert alle Objekte im Raum (inkl. sich selbst).</summary>
    public List<Avatar> Enter(Avatar avatar)
    {
        lock (_gate)
        {
            avatar.Row = _room.StartRow;
            avatar.Col = _room.StartCol;
            foreach (var other in _avatars.Values)
            {
                other.Session.EnsureResource(avatar.Name);
                other.Session.Post(GameReplies.Create(ToRoomObject(avatar)));
            }
            _avatars[avatar.Id] = avatar;
            return _avatars.Values.ToList();
        }
    }

    public void Leave(Avatar avatar)
    {
        lock (_gate)
        {
            if (!_avatars.Remove(avatar.Id))
                return;
            Broadcast(avatar, GameReplies.Remove(avatar.Id));
        }
    }

    public void Move(Avatar avatar, ushort row, ushort col, byte speed)
    {
        lock (_gate)
        {
            avatar.Row = row;
            avatar.Col = col;
            // Bit 7: Objekt dreht sich in Laufrichtung.
            Broadcast(avatar, GameReplies.Move(avatar.Id, row, col, (byte)(speed | 0x80)));
        }
    }

    public void Turn(Avatar avatar, ushort angle)
    {
        lock (_gate)
        {
            avatar.Angle = angle;
            Broadcast(avatar, GameReplies.Turn(avatar.Id, angle));
        }
    }

    public void Say(Avatar avatar, SayType type, string text)
    {
        uint format = _resources["%q"];
        byte[] said = GameReplies.Said(avatar.Id, _resources[avatar.Name], type, format, $"{avatar.Name}: {text}");
        lock (_gate)
        {
            foreach (var a in _avatars.Values)
            {
                a.Session.EnsureResource("%q");
                a.Session.EnsureResource(avatar.Name);
                a.Session.Post(said);
            }
        }
    }

    private void Broadcast(Avatar from, byte[] payload)
    {
        foreach (var a in _avatars.Values)
            if (a != from)
                a.Session.Post(payload);
    }
}
