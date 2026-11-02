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
    public int Hp { get; set; } = CombatRules.PlayerMaxHp;
    public int MaxHp { get; set; } = CombatRules.PlayerMaxHp;
    public DateTime NextAttack { get; set; }
    public DateTime LastCombat { get; set; }
    public DateTime NextRegen { get; set; }

    /// <summary>Besitz (Inventar, Bankkonten) des Charakters.</summary>
    public CharacterState State { get; set; } = new();
    /// <summary>Laufender Handel (KOD poOffer_who/plOffer_items): NPC, angebotene Gegenstände, Bezahlung.</summary>
    public Npc? OfferTo { get; set; }
    public List<(Item Source, Item Offer)> OfferItems { get; set; } = [];
    public Item? OfferPayment { get; set; }
}

/// <summary>Laufzeitzustand eines Raums.</summary>
public sealed class Room(RoomDefinition definition)
{
    /// <summary>Objekt-IDs der Räume: fester Offset plus KOD-Raumnummer (unter 2^28, Tag-Bits frei).</summary>
    public const uint ObjectIdBase = 0x00100000;

    public RoomDefinition Definition => definition;
    public uint ObjectId => ObjectIdBase + (uint)definition.Id;
    internal Dictionary<uint, Avatar> Avatars { get; } = new();
    public List<Npc> Npcs { get; } = new();
    public List<Monster> Monsters { get; } = new();
    /// <summary>Gegenstände auf dem Boden.</summary>
    public List<FloorItem> Items { get; } = new();

    /// <summary>Spawn-Punkte und Monsterarten mit Gewicht (KOD plGenerators/plMonsters).</summary>
    public List<GridPos> Generators { get; } = new();
    public List<(MonsterKind Kind, int Weight)> MonsterTable { get; } = new();
    public int MaxMonsters { get; set; }
    public DateTime NextSpawn { get; set; }

    /// <summary>KOD-Feinkoordinate für die Mitte eines Grid-Felds.</summary>
    public static ushort Fine(int cell, int fine = 32) => (ushort)(cell * 64 + fine);

    public (ushort Row, ushort Col) ArrivalPoint =>
        definition.Teleport is { } t ? (Fine(t.Row), Fine(t.Col)) : (Fine(5), Fine(5));
}

/// <summary>Ergebnis von „Gehen/Öffnen“ oder einer Bewegung über den Raumrand.</summary>
public abstract record MoveResult
{
    public sealed record ChangedRoom(Room Room) : MoveResult;
    public sealed record Blocked(string? Message) : MoveResult;
}

/// <summary>
/// Die Spielwelt: Räume aus einer <see cref="WorldDefinition"/>. Alle Änderungen laufen unter einem Lock,
/// Nachrichten an andere Sessions werden nur eingereiht (Session.Post), nie direkt gesendet.
/// </summary>
public sealed partial class World
{
    private readonly object _gate = new();
    private readonly Dictionary<int, Room> _rooms;
    private readonly ResourceRegistry _resources;

    /// <summary>OF_PLAYER aus include/proto.h: kennzeichnet Spieler; NPCs haben es nicht.</summary>
    public const uint FlagPlayer = 0x00000004;

    private readonly Random _random;

    public World(WorldDefinition definition, ResourceRegistry resources, string playerIcon, DialogFile? dialogs = null, int? seed = null,
        ItemCatalog? items = null, ShopFile? shops = null)
    {
        _random = seed is { } s ? new Random(s) : new Random();
        _items = items ?? ItemCatalog.Empty;
        _resources = resources;
        PlayerIcon = playerIcon;
        _rooms = definition.Rooms.ToDictionary(r => r.Id, r => new Room(r));
        uint nextNpc = Npc.ObjectIdBase;
        foreach (var room in _rooms.Values)
            foreach (var npc in room.Definition.Npcs)
                room.Npcs.Add(new Npc(++nextNpc, npc, (dialogs ?? DialogFile.Empty).Npcs.GetValueOrDefault(npc.KodClass)));
        SetUpMonsterRooms();
        SetUpMerchants(shops ?? ShopFile.Empty);
        SetUpTreasure(_items.Treasure);
        StartRoom = _rooms.TryGetValue(definition.StartRoom, out var start)
            ? start
            : throw new InvalidDataException($"Startraum {definition.StartRoom} fehlt in der Weltbeschreibung");
    }

    public Room StartRoom { get; }
    public string PlayerIcon { get; }
    public IReadOnlyCollection<Room> Rooms => _rooms.Values;

    public RoomObject ToRoomObject(Avatar a) =>
        new(a.Id, _resources[a.Name], new ObjectAppearance(_resources[PlayerIcon]), a.Row, a.Col, a.Angle, FlagPlayer);

    public RoomObject ToRoomObject(Monster m) =>
        new(m.Id, _resources[m.Kind.Name], new ObjectAppearance(_resources[m.Kind.Icon]), m.Row, m.Col, m.Angle, FlagAttackable);

    /// <summary>OF_ATTACKABLE aus include/proto.h.</summary>
    public const uint FlagAttackable = 0x00000008;

    /// <summary>Händler tragen OF_BUYABLE (verkauft) bzw. OF_OFFERABLE (kauft/nimmt an), wie im Original.</summary>
    public RoomObject ToRoomObject(Npc n) =>
        new(n.Id, _resources[n.DisplayName], new ObjectAppearance(_resources[PlayerIcon]), n.Row, n.Col, n.Angle, NpcFlags(n));

    private static uint NpcFlags(Npc n)
    {
        if (n.Merchant?.Definition is not { } d)
            return 0;
        return (d.Seller ? ObjectFlags.Buyable : 0) | (d.Buyer || d.Receive ? ObjectFlags.Offerable : 0);
    }

    /// <summary>Beschreibung eines Objekts im Raum des Spielers (BP_REQ_LOOK), oder null.</summary>
    public string? Look(Avatar avatar, uint id)
    {
        lock (_gate)
        {
            if (avatar.Room.Npcs.FirstOrDefault(n => n.Id == id) is { } npc)
                return npc.Describe();
            if (avatar.Room.Avatars.TryGetValue(id, out var other))
                return other == avatar ? "Das bist du selbst, ein stämmiger Zwerg." : $"Du siehst {other.Name}, einen Zwerg.";
            var item = avatar.Room.Items.FirstOrDefault(f => f.Item.Id == id)?.Item ?? avatar.State.Find(id);
            if (item is not null)
                return item.Kind.Description is { } d ? $"{item.Describe()}: {d}" : $"Du siehst {item.Describe()}.";
            return null;
        }
    }

    public List<Avatar> Snapshot(Room room)
    {
        lock (_gate)
            return room.Avatars.Values.ToList();
    }

    public List<Monster> MonstersIn(Room room)
    {
        lock (_gate)
            return room.Monsters.Where(m => m.Alive).ToList();
    }

    /// <summary>Spieler betritt die Welt im Startraum.</summary>
    public void Enter(Avatar avatar)
    {
        lock (_gate)
        {
            avatar.State = StateFor(avatar.Id);
            // Wie im Original erscheint man dort, wo man ausgeloggt hat.
            if (avatar.State.RoomId is { } roomId && _rooms.TryGetValue(roomId, out var saved))
            {
                avatar.Angle = avatar.State.Angle;
                avatar.Hp = Math.Clamp(avatar.State.Hp ?? avatar.MaxHp, 1, avatar.MaxHp);
                Place(avatar, saved, avatar.State.Row, avatar.State.Col);
                return;
            }
            var (row, col) = StartRoom.ArrivalPoint;
            Place(avatar, StartRoom, row, col);
        }
    }

    public void Leave(Avatar avatar)
    {
        lock (_gate)
        {
            CancelOfferLocked(avatar);
            RememberPosition(avatar);
            Remove(avatar);
        }
    }

    /// <summary>Bewegung des Spielers (BP_REQ_MOVE). Normale Ausgänge lösen erst mit <see cref="TryGo"/> aus,
    /// Ausgänge am Raumrand (plEdge_Exits) sofort, wenn die Position außerhalb des Rasters liegt.</summary>
    public MoveResult? Move(Avatar avatar, ushort row, ushort col, byte speed)
    {
        lock (_gate)
        {
            if (EdgeExit(avatar.Room.Definition, row, col) is { } edge)
            {
                if (!_rooms.TryGetValue(edge.To, out var target))
                    return new MoveResult.Blocked("Dieser Weg ist noch nicht begehbar.");
                Remove(avatar);
                avatar.Angle = RotateAngle(avatar.Angle, edge.Rotate);
                Place(avatar, target, Room.Fine(edge.ToRow), Room.Fine(edge.ToCol));
                return new MoveResult.ChangedRoom(target);
            }
            avatar.Row = row;
            avatar.Col = col;
            // Bit 7: Objekt dreht sich in Laufrichtung.
            Broadcast(avatar, GameReplies.Move(avatar.Id, row, col, (byte)(speed | 0x80)));
            return null;
        }
    }

    /// <summary>Rand-Ausgang wie KOD: Zeile &gt; Zeilenzahl → SOUTH, &lt; 1 → NORTH, Spalte &gt; Spaltenzahl → EAST, &lt; 1 → WEST.</summary>
    private static EdgeExitDefinition? EdgeExit(RoomDefinition room, ushort row, ushort col)
    {
        if (room.Rows is not { } rows || room.Cols is not { } cols || room.EdgeExits.Count == 0)
            return null;
        int r = row / 64, c = col / 64;
        string? edge = r > rows ? "SOUTH" : r < 1 ? "NORTH" : c > cols ? "EAST" : c < 1 ? "WEST" : null;
        return edge is null ? null : room.EdgeExits.FirstOrDefault(e => e.Edge == edge);
    }

    /// <summary>
    /// „Gehen/Öffnen“ (BP_REQ_GO) wie KOD room.kod SomethingTryGo: erster Eintrag in plExits,
    /// dessen Feld (Zeile/Spalte, ohne Feinkoordinaten) dem aktuellen Feld entspricht.
    /// Verschlossene Tür → nur Nachricht. Ausgang → Zielfeld (Mitte), Winkel gemäß ROTATE_*.
    /// </summary>
    public MoveResult TryGo(Avatar avatar)
    {
        lock (_gate)
        {
            var def = avatar.Room.Definition;
            int cellRow = avatar.Row / 64, cellCol = avatar.Col / 64;

            if (def.LockedDoors.FirstOrDefault(d => d.Row == cellRow && d.Col == cellCol) is { } door)
                return new MoveResult.Blocked(door.Message ?? "This door is locked.");

            if (def.Exits.FirstOrDefault(e => e.Row == cellRow && e.Col == cellCol) is not { } exit)
                return new MoveResult.Blocked("Hier ist kein Durchgang.");

            if (!_rooms.TryGetValue(exit.To, out var target))
                return new MoveResult.Blocked("Dieser Weg ist noch nicht begehbar.");

            Remove(avatar);
            avatar.Angle = RotateAngle(avatar.Angle, exit.Rotate);
            Place(avatar, target, Room.Fine(exit.ToRow), Room.Fine(exit.ToCol));
            return new MoveResult.ChangedRoom(target);
        }
    }

    /// <summary>ROTATE_* aus KOD (TranslateAngleChange): "NONE" = keine Drehung, "90"/"180"/… Grad relativ.
    /// Absolute ANGLE_*-Einträge wirken im Original als keine Drehung.</summary>
    public static ushort RotateAngle(ushort angle, string? rotate) =>
        int.TryParse(rotate, out int degrees)
            ? (ushort)((angle + degrees * 4096 / 360) % 4096)
            : angle;

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
            // NPCs im Raum antworten, wenn sie sich angesprochen fühlen (alle im Raum hören es).
            foreach (var npc in avatar.Room.Npcs)
            {
                if (TryConditionalSale(npc, avatar.Room, text))
                    continue;
                if (npc.Reply(text) is not { } reply)
                    continue;
                byte[] answer = GameReplies.Said(npc.Id, _resources[npc.DisplayName], SayType.Normal, _resources["%q"], $"{npc.Name}: {reply}");
                foreach (var a in avatar.Room.Avatars.Values)
                {
                    a.Session.EnsureResource(npc.DisplayName);
                    a.Session.Post(answer);
                }
            }
        }
    }

    // ------------------------------------------------------------------ Kampf und Monster

    /// <summary>Monsterräume aus KOD (tosgrave.kod / toscrypt.kod). Im Original spawnen sie nur nachts;
    /// ohne Tag-Nacht-Wechsel vorerst immer.</summary>
    private void SetUpMonsterRooms()
    {
        if (_rooms.TryGetValue(70, out var graveyard))
        {
            graveyard.Generators.AddRange([new(9, 31), new(7, 24), new(6, 7), new(12, 6), new(20, 27), new(23, 16), new(23, 7), new(16, 14)]);
            graveyard.MonsterTable.AddRange([(MonsterKind.Zombie, 85), (MonsterKind.Skeleton, 15)]);
            graveyard.MaxMonsters = 6;
        }
        if (_rooms.TryGetValue(71, out var crypt))
        {
            crypt.Generators.AddRange([new(11, 7), new(11, 11), new(15, 7), new(15, 11), new(19, 7), new(19, 11)]);
            crypt.MonsterTable.AddRange([(MonsterKind.Zombie, 80), (MonsterKind.Skeleton, 20)]);
            crypt.MaxMonsters = 5;
        }
    }

    private uint _nextMonsterId = Monster.ObjectIdBase;

    /// <summary>Spieltakt (ca. 4×/s): Monster erscheinen, verfolgen und greifen an; Spieler regenerieren.</summary>
    public void Tick(DateTime now, double seconds)
    {
        lock (_gate)
        {
            foreach (var room in _rooms.Values)
            {
                TickSpawns(room, now);
                TickOffers(room, now);
                foreach (var monster in room.Monsters.ToList())
                    TickMonster(monster, now, seconds);
                foreach (var avatar in room.Avatars.Values)
                    TickRegen(avatar, now);
            }
        }
    }

    private void TickSpawns(Room room, DateTime now)
    {
        if (room.MonsterTable.Count == 0 || now < room.NextSpawn)
            return;
        // Leerer Raum füllt sich schnell bis zur Hälfte, danach ein neues Monster alle 20 s.
        room.NextSpawn = now.AddSeconds(room.Monsters.Count < room.MaxMonsters / 2 ? 2 : 20);
        if (room.Monsters.Count(m => m.Alive) >= room.MaxMonsters)
            return;
        int roll = _random.Next(room.MonsterTable.Sum(e => e.Weight));
        var kind = room.MonsterTable.First(e => (roll -= e.Weight) < 0).Kind;
        SpawnMonsterLocked(room, kind, room.Generators[_random.Next(room.Generators.Count)]);
    }

    /// <summary>Setzt ein Monster an ein Feld (auch für Tests).</summary>
    public Monster SpawnMonster(int roomId, MonsterKind kind, GridPos at)
    {
        lock (_gate)
            return SpawnMonsterLocked(_rooms[roomId], kind, at);
    }

    private Monster SpawnMonsterLocked(Room room, MonsterKind kind, GridPos home)
    {
        var monster = new Monster(++_nextMonsterId, kind, room, home) { Angle = (ushort)_random.Next(4096) };
        room.Monsters.Add(monster);
        foreach (var a in room.Avatars.Values)
        {
            a.Session.EnsureResource(kind.Name);
            a.Session.EnsureResource(kind.Icon);
            a.Session.Post(GameReplies.Create(ToRoomObject(monster)));
        }
        return monster;
    }

    private void TickMonster(Monster m, DateTime now, double seconds)
    {
        var room = m.Room;
        if (!m.Alive)
        {
            if (now - m.DiedAt!.Value >= CombatRules.CorpseTime)
            {
                room.Monsters.Remove(m);
                BroadcastRoom(room, GameReplies.Remove(m.Id));
            }
            return;
        }

        // Ziel wählen: nächster Spieler in Aggro-Reichweite; Ziel verlieren, wenn es zu weit weg ist.
        if (m.Target is { } t && (t.Room != room || t.Hp <= 0 || Distance(m.Row, m.Col, t.Row, t.Col) > m.Kind.AggroRange * 2))
            m.Target = null;
        m.Target ??= room.Avatars.Values
            .Where(a => a.Hp > 0 && Distance(m.Row, m.Col, a.Row, a.Col) <= m.Kind.AggroRange)
            .OrderBy(a => Distance(m.Row, m.Col, a.Row, a.Col))
            .FirstOrDefault();

        (int row, int col) goal = m.Target is { } target
            ? (target.Row, target.Col)
            : (Room.Fine(m.Home.Row), Room.Fine(m.Home.Col));
        double dist = Distance(m.Row, m.Col, goal.row, goal.col);
        bool inRange = m.Target is not null && dist <= CombatRules.MeleeRange * 0.8;

        if (!inRange && dist > 8)
        {
            double step = Math.Min(dist - (m.Target is null ? 0 : CombatRules.MeleeRange * 0.6), m.Kind.SpeedFinePerSecond * seconds);
            if (step > 0)
            {
                double dr = (goal.row - m.Row) / dist, dc = (goal.col - m.Col) / dist;
                m.Row = (ushort)Math.Round(m.Row + dr * step);
                m.Col = (ushort)Math.Round(m.Col + dc * step);
                m.Angle = AngleTowards(dr, dc);
                // Bit 7 = in Laufrichtung drehen; speed ≈ Felder pro 10 s
                byte speed = (byte)Math.Clamp(m.Kind.SpeedFinePerSecond * 10 / 64, 1, 127);
                BroadcastRoom(room, GameReplies.Move(m.Id, m.Row, m.Col, (byte)(speed | 0x80)));
            }
        }

        if (inRange && now >= m.NextAttack && m.Target is { } victim)
        {
            m.NextAttack = now + m.Kind.AttackCooldown;
            m.Angle = AngleTowards(victim.Row - m.Row, victim.Col - m.Col);
            BroadcastRoom(room, GameReplies.Turn(m.Id, m.Angle));
            BroadcastRoom(room, GameReplies.Animate(ToRoomObject(m), GameReplies.AttackGroup, 4, 600));
            victim.LastCombat = now;
            if (_random.NextDouble() > m.Kind.HitChance)
            {
                Tell(victim, $"{Article(m.Kind)} verfehlt dich.");
                return;
            }
            int damage = _random.Next(m.Kind.MinDamage, m.Kind.MaxDamage + 1);
            victim.Hp = Math.Max(0, victim.Hp - damage);
            Tell(victim, $"{Article(m.Kind)} trifft dich für {damage} Schaden.");
            victim.Session.SendHealth();
            if (victim.Hp == 0)
                KillPlayer(victim, m);
        }
    }

    private void TickRegen(Avatar a, DateTime now)
    {
        if (a.Hp >= a.MaxHp || now - a.LastCombat < CombatRules.OutOfCombat || now < a.NextRegen)
            return;
        a.NextRegen = now + CombatRules.RegenInterval;
        a.Hp++;
        a.Session.SendHealth();
    }

    /// <summary>Angriff eines Spielers (BP_REQ_ATTACK) auf ein Monster.</summary>
    public void Attack(Avatar avatar, uint targetId, DateTime now)
    {
        lock (_gate)
        {
            var m = avatar.Room.Monsters.FirstOrDefault(x => x.Id == targetId && x.Alive);
            if (m is null)
            {
                Tell(avatar, avatar.Room.Npcs.Any(n => n.Id == targetId) || avatar.Room.Avatars.ContainsKey(targetId)
                    ? "Das willst du nicht angreifen."
                    : "Kein Ziel.");
                return;
            }
            if (Distance(avatar.Row, avatar.Col, m.Row, m.Col) > CombatRules.MeleeRange)
            {
                Tell(avatar, $"{Article(m.Kind, capital: true)} ist zu weit weg.");
                return;
            }
            if (now < avatar.NextAttack)
                return;
            avatar.NextAttack = now + CombatRules.PlayerAttackCooldown;
            avatar.LastCombat = now;
            avatar.Angle = AngleTowards(m.Row - avatar.Row, m.Col - avatar.Col);
            BroadcastRoom(avatar.Room, GameReplies.Animate(ToRoomObject(avatar), GameReplies.AttackGroup, 4, 500));
            m.Target ??= avatar;   // angegriffene Monster wehren sich

            if (_random.NextDouble() > CombatRules.PlayerHitChance)
            {
                Tell(avatar, $"Du verfehlst {Article(m.Kind, accusative: true)}.");
                return;
            }
            int damage = _random.Next(CombatRules.PlayerMinDamage, CombatRules.PlayerMaxDamage + 1);
            m.Hp -= damage;
            if (m.Hp > 0)
            {
                Tell(avatar, $"Du triffst {Article(m.Kind, accusative: true)} für {damage} Schaden ({m.Hp}/{m.Kind.MaxHp}).");
                return;
            }
            m.DiedAt = now;
            m.Target = null;
            Tell(avatar, $"Du hast {Article(m.Kind, accusative: true)} besiegt!");
            BroadcastRoom(m.Room, GameReplies.Animate(ToRoomObject(m), GameReplies.DeathGroup, 9, 1500));
            CreateTreasure(m);
        }
    }

    private void KillPlayer(Avatar victim, Monster killer)
    {
        killer.Target = null;
        Tell(victim, $"Du wurdest von {Article(killer.Kind, dative: true)} getötet. Du erwachst in Tos.");
        BroadcastRoom(victim.Room, GameReplies.Animate(ToRoomObject(victim), GameReplies.DeathGroup, 9, 1500));
        // Wiederbelebung am Ankunftspunkt des Startraums mit vollen Lebenspunkten.
        Remove(victim);
        var (row, col) = StartRoom.ArrivalPoint;
        Place(victim, StartRoom, row, col);
        victim.Hp = victim.MaxHp;
        victim.Session.NotifyRoomChanged();
        victim.Session.SendHealth();
    }

    private static string Article(MonsterKind kind, bool capital = true, bool accusative = false, bool dative = false)
    {
        // Zombie: der/den/dem Zombie; Skelett: das/das/dem Skelett
        bool neuter = kind == MonsterKind.Skeleton;
        string article = dative ? "dem" : neuter ? "das" : accusative ? "den" : "der";
        if (capital && !accusative && !dative)
            article = char.ToUpperInvariant(article[0]) + article[1..];
        return $"{article} {kind.Name}";
    }

    private void Tell(Avatar avatar, string text) => avatar.Session.SendText(text);

    private static double Distance(int r1, int c1, int r2, int c2) =>
        Math.Sqrt((double)(r1 - r2) * (r1 - r2) + (double)(c1 - c2) * (c1 - c2));

    /// <summary>KOD-Winkel (0 = Osten/+Spalte, 1024 = Süden/+Zeile) für eine Richtung.</summary>
    private static ushort AngleTowards(double dRow, double dCol) =>
        (ushort)((int)Math.Round(Math.Atan2(dRow, dCol) / (2 * Math.PI) * 4096 + 4096) % 4096);

    private static void BroadcastRoom(Room room, byte[] payload)
    {
        foreach (var a in room.Avatars.Values)
            a.Session.Post(payload);
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
        CancelOfferLocked(avatar);
        Broadcast(avatar, GameReplies.Remove(avatar.Id));
    }

    private static void Broadcast(Avatar from, byte[] payload)
    {
        foreach (var a in from.Room.Avatars.Values)
            if (a != from)
                a.Session.Post(payload);
    }
}
