using Meridian.Protocol;

namespace Meridian.Server;

/// <summary>Ein Spieler in der Welt.</summary>
public sealed class Avatar(uint id, string name, Session session)
{
    public uint Id => id;
    public string Name => name;
    public Session Session => session;
    public Room Room { get; set; } = null!;
    public ushort Row { get; set; }
    public ushort Col { get; set; }
    public ushort Angle { get; set; }
}

/// <summary>Laufzeitzustand eines Raums.</summary>
public sealed class Room(RoomDefinition definition)
{
    /// <summary>Objekt-IDs der Räume: fester Offset plus KOD-Raumnummer (unter 2^28, Tag-Bits frei).</summary>
    public const uint ObjectIdBase = 0x00100000;

    public RoomDefinition Definition => definition;
    public uint ObjectId => ObjectIdBase + (uint)definition.Id;
    internal Dictionary<uint, Avatar> Avatars { get; } = new();

    /// <summary>KOD-Feinkoordinate für die Mitte eines Grid-Felds.</summary>
    public static ushort Fine(int cell, int fine = 32) => (ushort)(cell * 64 + fine);

    public (ushort Row, ushort Col) ArrivalPoint =>
        definition.Teleport is { } t ? (Fine(t.Row), Fine(t.Col)) : (Fine(5), Fine(5));
}

/// <summary>Ergebnis einer Bewegung.</summary>
public abstract record MoveResult
{
    public sealed record Moved : MoveResult;
    public sealed record ChangedRoom(Room Room) : MoveResult;
    public sealed record Blocked(string? Message) : MoveResult;
}

/// <summary>
/// Die Spielwelt: Räume aus einer <see cref="WorldDefinition"/>. Alle Änderungen laufen unter einem Lock,
/// Nachrichten an andere Sessions werden nur eingereiht (Session.Post), nie direkt gesendet.
/// </summary>
public sealed class World
{
    private readonly object _gate = new();
    private readonly Dictionary<int, Room> _rooms;
    private readonly ResourceRegistry _resources;

    public World(WorldDefinition definition, ResourceRegistry resources, string playerIcon)
    {
        _resources = resources;
        PlayerIcon = playerIcon;
        _rooms = definition.Rooms.ToDictionary(r => r.Id, r => new Room(r));
        StartRoom = _rooms.TryGetValue(definition.StartRoom, out var start)
            ? start
            : throw new InvalidDataException($"Startraum {definition.StartRoom} fehlt in der Weltbeschreibung");
    }

    public Room StartRoom { get; }
    public string PlayerIcon { get; }
    public IReadOnlyCollection<Room> Rooms => _rooms.Values;

    public RoomObject ToRoomObject(Avatar a) =>
        new(a.Id, _resources[a.Name], new ObjectAppearance(_resources[PlayerIcon]), a.Row, a.Col, a.Angle);

    public List<Avatar> Snapshot(Room room)
    {
        lock (_gate)
            return room.Avatars.Values.ToList();
    }

    /// <summary>Spieler betritt die Welt im Startraum.</summary>
    public void Enter(Avatar avatar)
    {
        lock (_gate)
        {
            var (row, col) = StartRoom.ArrivalPoint;
            Place(avatar, StartRoom, row, col);
        }
    }

    public void Leave(Avatar avatar)
    {
        lock (_gate)
            Remove(avatar);
    }

    /// <summary>Bewegung des Spielers (BP_REQ_MOVE). Löst Ausgänge und verschlossene Türen aus.</summary>
    public MoveResult Move(Avatar avatar, ushort row, ushort col, byte speed)
    {
        lock (_gate)
        {
            var def = avatar.Room.Definition;
            int cellRow = row / 64, cellCol = col / 64;

            if (def.LockedDoors.FirstOrDefault(d => d.Row == cellRow && d.Col == cellCol) is { } door)
                return new MoveResult.Blocked(door.Message ?? "Die Tür ist verschlossen.");

            if (def.Exits.FirstOrDefault(e => e.Row == cellRow && e.Col == cellCol) is { } exit)
            {
                if (!_rooms.TryGetValue(exit.To, out var target))
                    return new MoveResult.Blocked("Dieser Weg ist noch nicht begehbar.");
                Remove(avatar);
                Place(avatar, target, Room.Fine(exit.ToRow), Room.Fine(exit.ToCol));
                return new MoveResult.ChangedRoom(target);
            }

            avatar.Row = row;
            avatar.Col = col;
            // Bit 7: Objekt dreht sich in Laufrichtung.
            Broadcast(avatar, GameReplies.Move(avatar.Id, row, col, (byte)(speed | 0x80)));
            return new MoveResult.Moved();
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

    /// <summary>Normales Sagen geht an alle im selben Raum.</summary>
    public void Say(Avatar avatar, SayType type, string text)
    {
        byte[] said = GameReplies.Said(avatar.Id, _resources[avatar.Name], type, _resources["%q"], $"{avatar.Name}: {text}");
        lock (_gate)
        {
            foreach (var a in avatar.Room.Avatars.Values)
            {
                a.Session.EnsureResource("%q");
                a.Session.EnsureResource(avatar.Name);
                a.Session.Post(said);
            }
        }
    }

    private void Place(Avatar avatar, Room room, ushort row, ushort col)
    {
        avatar.Room = room;
        avatar.Row = row;
        avatar.Col = col;
        foreach (var other in room.Avatars.Values)
        {
            other.Session.EnsureResource(avatar.Name);
            other.Session.Post(GameReplies.Create(ToRoomObject(avatar)));
        }
        room.Avatars[avatar.Id] = avatar;
    }

    private void Remove(Avatar avatar)
    {
        if (avatar.Room is null || !avatar.Room.Avatars.Remove(avatar.Id))
            return;
        Broadcast(avatar, GameReplies.Remove(avatar.Id));
    }

    private static void Broadcast(Avatar from, byte[] payload)
    {
        foreach (var a in from.Room.Avatars.Values)
            if (a != from)
                a.Session.Post(payload);
    }
}
