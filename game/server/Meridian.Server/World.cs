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
    public DateTime NextRest { get; set; }
    /// <summary>Teleport-Erkennung wie user.kod (piMoveOldRow/Col/Room, piLastMoveUpdateTime, piCheaterLogs).</summary>
    public int MoveOldRoom { get; set; }
    public int MoveOldRow { get; set; }
    public int MoveOldCol { get; set; }
    public long LastMoveSecond { get; set; }
    public int CheaterLogs { get; set; }

    /// <summary>Besitz (Inventar, Bankkonten) des Charakters.</summary>
    public CharacterState State { get; set; } = new();
    /// <summary>Laufender Handel (KOD poOffer_who/plOffer_items): NPC, angebotene Gegenstände, Bezahlung.</summary>
    public Npc? OfferTo { get; set; }
    public List<(Item Source, Item Offer)> OfferItems { get; set; } = [];
    public Item? OfferPayment { get; set; }
    /// <summary>Handel mit einem anderen Spieler: Partner, ob man selbst angeboten hat (nur der Anbietende
    /// nimmt an) und ob der Partner ein Gegenangebot gemacht hat (pbOffer_OtherAccepted).</summary>
    public Avatar? OfferPlayer { get; set; }
    public bool OfferInitiator { get; set; }
    public bool OfferOtherAccepted { get; set; }
    public bool Trading => OfferTo is not null || OfferPlayer is not null;
    /// <summary>Laufende Trance, aktiver Berührungszauber, nächster Manapunkt.</summary>
    public PendingCast? Casting { get; set; }
    public TouchAttack? Touch { get; set; }
    public DateTime NextMana { get; set; }
    /// <summary>Aktuelles Kampfziel (poKill_target) und ob man seitdem verfehlt wurde (PFLAG_DODGED).</summary>
    public Monster? KillTarget { get; set; }
    public bool Dodged { get; set; }
    /// <summary>Portal der Neulingsinsel: schon gewarnt, steht gerade darauf.</summary>
    public bool PortalWarned { get; set; }
    public bool OnPortal { get; set; }
    /// <summary>Vergiftung (player.kod AddPoison): Verlust je 10 s in Tausendstel LP, Ende, nächster Tick, Rest.</summary>
    public int PoisonLossRate { get; set; }
    public DateTime PoisonUntil { get; set; }
    public DateTime NextPoisonTick { get; set; }
    public int PoisonRemainder { get; set; }
    public bool Poisoned => PoisonLossRate > 0;
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
    public bool NightOnly { get; set; }
    /// <summary>Begehbarkeit (aus rooms/*.json); null = keine Wände bekannt, Monster laufen geradlinig.</summary>
    public RoomGrid? Grid { get; set; }
    public DateTime NextSpawn { get; set; }
    /// <summary>Raumrätsel (Mausoleum von Raza), sonst null.</summary>
    public Mausoleum? Puzzle { get; set; }

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
        SetUpAreaMonsters();
        SetUpMerchants(shops ?? ShopFile.Empty);
        SetUpTreasure(_items.Treasure);
        StartRoom = _rooms.TryGetValue(definition.StartRoom, out var start)
            ? start
            : throw new InvalidDataException($"Startraum {definition.StartRoom} fehlt in der Weltbeschreibung");
        RespawnRoom = definition.RespawnRoom is { } r && _rooms.TryGetValue(r, out var respawn) ? respawn : StartRoom;
    }

    /// <summary>Spieler in der Welt (alle Räume).</summary>
    public int OnlineCount
    {
        get { lock (_gate) return _rooms.Values.Sum(r => r.Avatars.Count); }
    }

    /// <summary>Hier beginnen neue Charaktere (Original: Gasthaus von Raza, RID_NEWB1).</summary>
    public Room StartRoom { get; }
    /// <summary>Hier erwacht man nach dem Tod, wenn der Raum keinen eigenen Wiederbelebungsraum hat.</summary>
    public Room RespawnRoom { get; }

    /// <summary>Feste Spielstunde (Tests/Entwicklung), sonst die Uhr des Originals.</summary>
    public int? FixedHour { get; init; }
    private int _hour = -1;

    public int Hour(DateTime now) => FixedHour ?? GameClock.Hour(now);

    /// <summary>Aktuelles Umgebungslicht eines Raums (für BP_PLAYER).</summary>
    public byte RoomLight(Room room) => GameClock.RoomLight(room.Definition, Hour(DateTime.UtcNow));

    /// <summary>Neue Spielstunde: Räume mit geändertem Licht melden BP_LIGHT_AMBIENT (room.kod AmbientLightChanged).</summary>
    private void TickHour(DateTime now)
    {
        int hour = Hour(now);
        if (hour == _hour)
            return;
        int old = _hour;
        _hour = hour;
        if (old < 0)
            return;
        Log.Info($"Spielstunde {hour} (Helligkeit {GameClock.Brightness(hour)} %)");
        foreach (var room in _rooms.Values)
        {
            byte light = GameClock.RoomLight(room.Definition, hour);
            if (room.Avatars.Count > 0 && light != GameClock.RoomLight(room.Definition, old))
                BroadcastRoom(room, GameReplies.LightAmbient(light));
        }
    }
    public string PlayerIcon { get; }
    public IReadOnlyCollection<Room> Rooms => _rooms.Values;

    public RoomObject ToRoomObject(Avatar a) =>
        new(a.Id, _resources[a.Name], new ObjectAppearance(_resources[a.State.Appearance.Body], 1, PlayerOverlays(a), ShirtTranslation(a)),
            a.Row, a.Col, a.Angle, FlagPlayer);

    /// <summary>Getragenes Hemd färbt Körper und Arme (player.kod GetCurrentShirtColor); ohne Hemd keine Übersetzung.</summary>
    private static byte ShirtTranslation(Avatar a) =>
        a.State.Inventory.FirstOrDefault(i => i.Used && i.Kind.Slot == "shirt")?.Kind.ClothingTranslation(a.State.Appearance.SkinXlat) ?? 0;

    /// <summary>Körperteile des männlichen Standard-Charakters (player.kod SendOverlays, system.kod charinfo_*_ax):
    /// linker/rechter Arm, Beine, Kopf, Mund, Augen, Nase, Haare; dazu die Waffe in der Hand.</summary>
    public static readonly string[] PlayerOverlayIcons =
        ["bla.bgf", "bra.bgf", "bfa.bgf", "phax.bgf", "pmax.bgf", "peax.bgf", "pnax.bgf", "ptac.bgf"];

    private List<Overlay> PlayerOverlays(Avatar a)
    {
        var weapon = a.State.Inventory.FirstOrDefault(i => i.Used && i.Kind.Overlay is not null);
        bool leftHandBusy = a.State.Inventory.Any(i => i.Used && i.Kind.Slot == "shield");
        var look = a.State.Appearance;
        byte shirt = ShirtTranslation(a);
        // Hose/Rock: eigene Beingrafik je Geschlecht und Farbe (pants.kod).
        var pants = a.State.Inventory.FirstOrDefault(i => i.Used && i.Kind.Slot == "legs")?.Kind;
        string legs = (look.Gender == Appearance.Female ? pants?.LegsFemale : pants?.LegsMale) ?? look.Legs;
        var list = new List<Overlay>
        {
            new(_resources[look.LeftArm], 31 /* HS_LEFT_HAND */, leftHandBusy ? (ushort)7 : (ushort)1, shirt),
            new(_resources[look.RightArm], 21 /* HS_RIGHT_HAND */, weapon is null ? (ushort)1 : (ushort)17, shirt),
            new(_resources[legs], 41 /* HS_LEGS */, 1, pants?.ClothingTranslation(look.SkinXlat) ?? 0),
            new(_resources[look.Head], 1 /* HS_HEAD */, 1, look.SkinXlat),
            new(_resources[look.Mouth], 12 /* HS_MOUTH */, 1, look.SkinXlat),
            new(_resources[look.Eyes], 11 /* HS_EYES */, 1, look.SkinXlat),
            new(_resources[look.Nose], 14 /* HS_NOSE */, 1, look.SkinXlat),
            new(_resources[look.Hair], 13 /* HS_TOUPEE */, 1, look.HairXlat),
        };
        if (weapon?.Kind.Overlay is { } overlay)
            list.Add(new(_resources[overlay], 22 /* HS_RIGHT_WEAPON */, 4));
        return list;
    }

    /// <summary>Aussehen eines Spielers hat sich geändert (z. B. Waffe angelegt): BP_CHANGE an den Raum.</summary>
    private void AppearanceChanged(Avatar a)
    {
        if (a.Room is not null)
            BroadcastRoom(a.Room, GameReplies.Change(ToRoomObject(a)));
    }

    public RoomObject ToRoomObject(Monster m) =>
        new(m.Id, _resources[m.Kind.Name], new ObjectAppearance(_resources[m.Kind.Icon]), m.Row, m.Col, m.Angle, FlagAttackable | m.Kind.DrawFlags);

    /// <summary>OF_ATTACKABLE aus include/proto.h.</summary>
    public const uint FlagAttackable = 0x00000008;

    /// <summary>Händler tragen OF_BUYABLE (verkauft) bzw. OF_OFFERABLE (kauft/nimmt an), wie im Original.</summary>
    public RoomObject ToRoomObject(Npc n) =>
        new(n.Id, _resources[n.DisplayName], new ObjectAppearance(_resources[NpcIcon(n)], n.Group), n.Row, n.Col, n.Angle, NpcFlags(n));

    /// <summary>Grafik eines NPCs (KOD vrIcon, z. B. tsinnk.bgf); ohne Angabe die Spielergrafik.</summary>
    public string NpcIcon(Npc n) => n.Definition.Icon ?? PlayerIcon;

    private static uint NpcFlags(Npc n)
    {
        if (n.Definition.KodClass == Mausoleum.LeverClass)
            return ObjectFlags.Activatable;   // gstlever.kod ACTIVATE_YES
        if (n.Merchant?.Definition is not { } d)
            return 0;
        return (d.Seller ? ObjectFlags.Buyable : 0) | (d.Buyer || d.Receive ? ObjectFlags.Offerable : 0);
    }

    /// <summary>Beschreibung eines Objekts im Raum des Spielers (BP_REQ_LOOK), oder null.</summary>
    public string? Look(Avatar avatar, uint id) => LookAt(avatar, id)?.Text;

    /// <summary>Objekt und Beschreibung für BP_LOOK (Ressourcen für Name/Icon sind beim Anschauenden sichergestellt).</summary>
    public (RoomObject Object, string Text)? LookAt(Avatar avatar, uint id)
    {
        lock (_gate)
        {
            var s = avatar.Session;
            if (avatar.Room.Npcs.FirstOrDefault(n => n.Id == id) is { } npc)
            {
                s.EnsureResource(npc.DisplayName);
                s.EnsureResource(NpcIcon(npc));
                return (ToRoomObject(npc), npc.Describe());
            }
            if (avatar.Room.Avatars.TryGetValue(id, out var other))
            {
                s.EnsureResource(other.Name);
                s.EnsureResource(PlayerIcon);
                string desc = other.State.Description.Length > 0 ? other.State.Description
                    : other == avatar ? "Das bist du selbst, ein stämmiger Zwerg." : $"Du siehst {other.Name}, einen Zwerg.";
                return (ToRoomObject(other), desc);
            }
            if (avatar.Room.Monsters.FirstOrDefault(m => m.Id == id) is { } monster)
            {
                s.EnsureResource(monster.Kind.Name);
                s.EnsureResource(monster.Kind.Icon);
                string desc = monster.Kind.Description ?? monster.Kind.Gender switch
                {
                    "f" => $"Eine {monster.Kind.Name}. Sie sieht nicht freundlich aus.",
                    "n" => $"Ein {monster.Kind.Name}. Es sieht nicht freundlich aus.",
                    _ => $"Ein {monster.Kind.Name}. Er sieht nicht freundlich aus.",
                };
                return (ToRoomObject(monster), desc);
            }
            var item = avatar.Room.Items.FirstOrDefault(f => f.Item.Id == id)?.Item ?? avatar.State.Find(id);
            if (item is null)
                return null;
            EnsureItemResources(s, item);
            return (ToRoomObject(item), item.Kind.Description is { } d ? $"{item.Describe()}: {d}" : $"Du siehst {item.Describe()}.");
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
            InitMagic(avatar);
            // Wie im Original erscheint man dort, wo man ausgeloggt hat.
            if (avatar.State.RoomId is { } roomId && _rooms.TryGetValue(roomId, out var saved))
            {
                avatar.Angle = avatar.State.Angle;
                avatar.Hp = Math.Clamp(avatar.State.Hp ?? avatar.MaxHp, 1, avatar.MaxHp);
                // Gespeicherte Position außerhalb des Bodens (Wand, außerhalb des Rasters): an den Ankunftspunkt.
                var (r, c) = saved.Grid is { } grid && !grid.HasFloor(avatar.State.Row / 64, avatar.State.Col / 64)
                    ? saved.ArrivalPoint
                    : (avatar.State.Row, avatar.State.Col);
                Place(avatar, saved, r, c);
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
            if (Portal(avatar, row, col) is { } portal)
                return portal;
            if (avatar.Row / 16 != row / 16 || avatar.Col / 16 != col / 16)
                BreakTrance(avatar);
            avatar.Row = row;
            avatar.Col = col;
            DetectTeleport(avatar);
            // user.kod: Anstrengung pro Bewegung = EXERTION_PER_MOVE × (Tempo·5/6)²
            int s56 = speed * 5 / 6;
            // Eile: Anstrengung × (100 − Stärke) / 100.
            AddExertion(avatar, CombatRules.ExertionPerMove * s56 * s56 * (100 - Math.Min(EnchantState(avatar, "haste"), 100)) / 100);
            // Bit 7: Objekt dreht sich in Laufrichtung.
            Broadcast(avatar, GameReplies.Move(avatar.Id, row, col, (byte)(speed | 0x80)));
            return null;
        }
    }

    /// <summary>MAX_LOGGED_THRESHOLD: so oft wird ein Spieler höchstens protokolliert.</summary>
    public const int MaxCheaterLogs = 25;

    /// <summary>
    /// Teleport-Erkennung wie user.kod UserMove: Bewegungen werden wie im Original nicht gegen Wände geprüft
    /// („already checked by client“). Höchstens einmal pro Sekunde wird verglichen; liegen ≥ 200 Felder² zwischen
    /// zwei Prüfungen im selben Raum bei weniger als 3 s Abstand, wird das protokolliert und kostet Ausdauer.
    /// </summary>
    private void DetectTeleport(Avatar a)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long delta = now - a.LastMoveSecond;
        if (a.CheaterLogs < MaxCheaterLogs && a.LastMoveSecond != now)
        {
            int row = a.Row / 64, col = a.Col / 64, room = a.Room.Definition.Id;
            int squared = (a.MoveOldRow - row) * (a.MoveOldRow - row) + (a.MoveOldCol - col) * (a.MoveOldCol - col);
            var tp = a.Room.Definition.Teleport;
            if (room == a.MoveOldRoom && (tp is null || row != tp.Row && col != tp.Col) && squared >= 200 && delta < 3)
            {
                Log.Warn($"ALERT! {a.Name} bewegte sich {squared} Felder² in {delta} s ({a.MoveOldRow},{a.MoveOldCol}) → ({row},{col}) in Raum {room}. Möglicher Speedhacker.");
                a.CheaterLogs++;
                AddExertion(a, squared * CombatRules.ExertionPerMove);
            }
            a.MoveOldRow = row;
            a.MoveOldCol = col;
            a.MoveOldRoom = room;
        }
        a.LastMoveSecond = now;
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
    /// <summary>Drehung beim Raumwechsel: Grad relativ, oder feste Richtung (SET_ANGLE_WEST → "WEST" usw.).</summary>
    public static ushort RotateAngle(ushort angle, string? rotate) => rotate switch
    {
        "EAST" => 0, "SOUTH" => 1024, "WEST" => 2048, "NORTH" => 3072,
        _ when int.TryParse(rotate, out int degrees) => (ushort)((angle + degrees * 4096 / 360) % 4096),
        _ => angle,
    };

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
            graveyard.NightOnly = true;
        }
        if (_rooms.TryGetValue(71, out var crypt))
        {
            crypt.Generators.AddRange([new(11, 7), new(11, 11), new(15, 7), new(15, 11), new(19, 7), new(19, 11)]);
            crypt.MonsterTable.AddRange([(MonsterKind.Zombie, 80), (MonsterKind.Skeleton, 20)]);
            crypt.MaxMonsters = 5;
            crypt.NightOnly = true;
        }
    }

    /// <summary>Monster aus den Gebietsdateien: feste Bewohner (Kühe) und Monsterräume (Mausoleum von Raza).</summary>
    private void SetUpAreaMonsters()
    {
        foreach (var room in _rooms.Values)
        {
            var d = room.Definition;
            if (d.Puzzle == "mausoleum")
                room.Puzzle = new Mausoleum();
            foreach (var f in d.FixedMonsters)
                SpawnMonsterLocked(room, MonsterKind.ByKey(f.Kind), new GridPos(f.Row, f.Col));
            if (d.Monsters.Count == 0)
                continue;
            room.Generators.AddRange(d.Generators);
            room.MonsterTable.AddRange(d.Monsters.Select(e => (MonsterKind.ByKey(e.Kind), e.Weight)));
            room.MaxMonsters = d.MaxMonsters;
        }
    }

    private uint _nextMonsterId = Monster.ObjectIdBase;

    /// <summary>Spieltakt (ca. 4×/s): Monster erscheinen, verfolgen und greifen an; Spieler regenerieren.</summary>
    public void Tick(DateTime now, double seconds)
    {
        lock (_gate)
        {
            TickHour(now);
            foreach (var room in _rooms.Values)
            {
                TickSpawns(room, now);
                TickPuzzle(room, now);
                TickOffers(room, now);
                foreach (var monster in room.Monsters.ToList())
                    TickMonster(monster, now, seconds);
                foreach (var avatar in room.Avatars.Values.ToList())
                {
                    TickRegen(avatar, now);
                    TickMagic(avatar, now);
                    TickPoison(avatar, now);
                }
            }
        }
    }

    private void TickSpawns(Room room, DateTime now)
    {
        if (room.MonsterTable.Count == 0 || now < room.NextSpawn)
            return;
        // Friedhof und Krypta: neue Monster nur nachts (TryCreateMonster), vorhandene bleiben.
        if (room.NightOnly && !GameClock.IsNight(Hour(now)))
            return;
        // Leerer Raum füllt sich schnell bis zur Hälfte, danach ein neues Monster alle 20 s.
        room.NextSpawn = now.AddSeconds(room.Monsters.Count < room.MaxMonsters / 2 ? 2 : 20);
        if (room.Monsters.Count(m => m.Alive) >= room.MaxMonsters)
            return;
        int roll = _random.Next(room.MonsterTable.Sum(e => e.Weight));
        var kind = room.MonsterTable.First(e => (roll -= e.Weight) < 0).Kind;
        // ReqNewHold des Mausoleums kann den Spawn verweigern (Grube).
        if (PuzzleSpawnPoint(room, kind, room.Generators[_random.Next(room.Generators.Count)]) is { } at)
            SpawnMonsterLocked(room, kind, at);
    }

    /// <summary>Setzt ein Monster an ein Feld (auch für Tests).</summary>
    public Monster SpawnMonster(int roomId, MonsterKind kind, GridPos at)
    {
        lock (_gate)
            return SpawnMonsterLocked(_rooms[roomId], kind, at);
    }

    private Monster SpawnMonsterLocked(Room room, MonsterKind kind, GridPos home)
    {
        int hp = kind.RollHitPoints(_random);
        var monster = new Monster(++_nextMonsterId, kind, room, home) { Angle = (ushort)_random.Next(4096), Hp = hp, MaxHp = hp };
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
        if (m.Kind.Peaceful)
            return;   // Kühe stehen nur herum

        // Ziel wählen (brain.kod GetHatred, vereinfacht): aggressive Monster greifen den nächsten Spieler in Sichtweite
        // an, karma-aggressive nur Spieler mit entgegengesetztem Karma; alle wehren sich, wenn man sie angreift.
        // Ziel verlieren, wenn es zu weit weg ist.
        if (m.Target is { } t && (t.Room != room || t.Hp <= 0 || Distance(m.Row, m.Col, t.Row, t.Col) > m.Kind.AggroRange * 2))
            m.Target = null;
        m.Target ??= room.Avatars.Values
            .Where(a => a.Hp > 0 && Distance(m.Row, m.Col, a.Row, a.Col) <= m.Kind.AggroRange && Hates(m.Kind, a))
            .OrderBy(a => Distance(m.Row, m.Col, a.Row, a.Col))
            .FirstOrDefault();

        // Flucht (HealthBelowWimpy): unter viWimpy Prozent Lebenspunkten vom Ziel weg statt angreifen.
        bool fleeing = m.Target is not null && m.Kind.Wimpy > 0 && m.Hp * 100 / Math.Max(1, m.MaxHp) < m.Kind.Wimpy;
        (int row, int col) goal = m.Target is { } target
            ? fleeing ? FleePoint(m, target) : (target.Row, target.Col)
            : (Room.Fine(m.Home.Row), Room.Fine(m.Home.Col));
        double dist = Distance(m.Row, m.Col, goal.row, goal.col);
        bool inRange = m.Target is not null && !fleeing && dist <= CombatRules.MeleeRange * 0.8;
        // Unbewegliche Monster (AI_NOMOVE, Tempo 0: Bäume, Spinnenkönigin) schlagen nur zu, wenn man nah genug ist.
        if (m.Kind.Immobile)
            dist = 0;

        if (!inRange && dist > 8)
        {
            // Mit bekannten Wänden: zum nächsten Feld des A*-Wegs statt geradlinig zum Ziel.
            var waypoint = room.Grid is null ? goal : NextWaypoint(m, room.Grid, goal);
            double toWaypoint = waypoint is { } w ? Distance(m.Row, m.Col, w.row, w.col) : 0;
            double step = Math.Min(dist - (m.Target is null ? 0 : CombatRules.MeleeRange * 0.6), m.Kind.SpeedFinePerSecond * seconds);
            step = Math.Min(step, toWaypoint);
            if (step > 0 && waypoint is { } wp)
            {
                double dr = (wp.row - m.Row) / toWaypoint, dc = (wp.col - m.Col) / toWaypoint;
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
            m.NextAttack = now + m.Kind.RollAttackTime(_random);
            m.Angle = AngleTowards(victim.Row - m.Row, victim.Col - m.Col);
            BroadcastRoom(room, GameReplies.Turn(m.Id, m.Angle));
            BroadcastRoom(room, GameReplies.Animate(ToRoomObject(m), GameReplies.AttackGroup, 4, 600));
            victim.LastCombat = now;
            if (CombatRules.HitChance(m.Kind.Offense, PlayerDefense(victim)) < _random.Next(1, 101))
            {
                victim.Dodged = true;
                Tell(victim, $"{Article(m.Kind)} verfehlt dich.");
                return;
            }
            int damage = m.Kind.RollDamage(_random);
            // Widerstände (resist.kod AddResistance) gegen die Angriffsart des Monsters, falls es eine hat.
            if (m.Kind.AttackType is { } type)
                damage = CombatRules.Resist(damage, EnchantState(victim, "resist", type));
            damage = AssessPlayerDamage(victim, damage);
            victim.Hp = Math.Max(0, victim.Hp - damage);
            Tell(victim, $"{Article(m.Kind)} trifft dich für {damage} Schaden.");
            if (victim.Hp > 0 && m.Kind.Poison is { } poison && _random.Next(poison.OneIn) == 0)
                Poison(victim, poison, now);
            victim.Session.SendHealth();
            if (victim.Hp == 0)
                KillPlayer(victim, m);
        }
    }

    /// <summary>Erholung wie player.kod: Lebenspunkte im Takt von CalculateHealthTime (mehr Ausdauer = schneller),
    /// Ausdauer im Takt von GetRestTime bis zur Ruheschwelle 80.</summary>
    /// <summary>Nächster Wegpunkt (Feldmitte) auf dem A*-Weg zum Ziel; der Weg wird neu berechnet, wenn sich das
    /// Zielfeld ändert. null = Ziel unerreichbar (Monster bleibt stehen).</summary>
    private static (int row, int col)? NextWaypoint(Monster m, RoomGrid grid, (int row, int col) goal)
    {
        var from = new GridPos(m.Row / 64, m.Col / 64);
        var to = new GridPos(goal.row / 64, goal.col / 64);
        if (from == to)
            return goal;
        if (m.Path is null || m.PathGoal != to || m.Path.Count == 0)
        {
            m.PathGoal = to;
            m.Path = grid.FindPath(from, to);
        }
        if (m.Path is null)
            return null;
        // Erreichte Felder abhaken; das letzte Feld ist das Zielfeld, dort direkt aufs Ziel zulaufen.
        while (m.Path.Count > 0 && m.Path[0] == from)
            m.Path.RemoveAt(0);
        if (m.Path.Count == 0 || m.Path[0] == to)
            return goal;
        return (Room.Fine(m.Path[0].Row), Room.Fine(m.Path[0].Col));
    }

    /// <summary>Höhenraster der Räume laden (data/rooms/&lt;roo&gt;.json), Bezugsboden am Ankunftspunkt.</summary>
    public void LoadGrids(string directory)
    {
        if (!Directory.Exists(directory))
            return;
        foreach (var room in _rooms.Values)
        {
            string path = Path.Combine(directory, Path.GetFileNameWithoutExtension(room.Definition.Roo).ToLowerInvariant() + ".json");
            if (File.Exists(path))
                room.Grid = RoomGrid.Load(path, room.Definition.Teleport);
            // Die Tür des Mausoleums ist anfangs zu.
            if (room.Puzzle is not null && room.Grid?.Tags.GetValueOrDefault(Mausoleum.SectorDoor) is { } door)
                room.Grid.SetBlocked(door, true);
        }
    }

    private void TickRegen(Avatar a, DateTime now)
    {
        if (a.Hp < a.MaxHp && a.Hp > 0)
        {
            if (a.NextRegen == default)
                a.NextRegen = now + CombatRules.HealthTime(a.State.Vigor, a.MaxHp);
            else if (now >= a.NextRegen)
            {
                a.NextRegen = now + CombatRules.HealthTime(a.State.Vigor, a.MaxHp);
                a.Hp++;
                a.Session.SendHealth();
            }
        }
        else
            a.NextRegen = default;

        if (a.State.Vigor < CombatRules.VigorRestThreshold)
        {
            if (a.NextRest == default)
                a.NextRest = now + CombatRules.RestTime;
            else if (now >= a.NextRest)
            {
                a.NextRest = now + CombatRules.RestTime;
                a.State.Vigor++;
                a.Session.SendVigor();
            }
        }
        else
            a.NextRest = default;
    }

    /// <summary>Angriff eines Spielers (BP_REQ_ATTACK) auf ein Monster.</summary>
    public void Attack(Avatar avatar, uint targetId, DateTime now)
    {
        lock (_gate)
        {
            if (avatar.Room.Definition.NoCombat)
            {
                Tell(avatar, "Hier ist kein Kampf erlaubt.");   // room.kod ReqSomethingAttack
                return;
            }
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
            if (avatar.State.Vigor < CombatRules.AttackVigor)
            {
                Tell(avatar, "Du bist zu erschöpft, um zu kämpfen.");
                return;
            }
            avatar.NextAttack = now + CombatRules.PlayerAttackCooldown;
            // PayCosts der Technik: 1000 × viSkillExertion (Hieb 2, Faustschlag 5).
            var strokeSkill = _skills.BySkid(Weapon(avatar) is null || avatar.Touch is not null ? Skill.Punch : Skill.Slash);
            AddExertion(avatar, 1000 * (strokeSkill?.Exertion ?? 2));
            if (Weapon(avatar) is { } swung)
                SendWindowOverlay(avatar, swung, visible: true, attack: true);
            avatar.LastCombat = now;
            avatar.Angle = AngleTowards(m.Row - avatar.Row, m.Col - avatar.Col);
            BroadcastRoom(avatar.Room, GameReplies.Animate(ToRoomObject(avatar), GameReplies.AttackGroup, 4, 500));
            m.Target ??= avatar;   // angegriffene Monster wehren sich

            if (avatar.KillTarget != m)
            {
                avatar.KillTarget = m;      // ResetGainFlags
                avatar.Dodged = false;
            }

            // battler.kod TryAttack: Offensive gegen Defensive des Monsters.
            bool hit = CombatRules.HitChance(PlayerOffense(avatar), m.Kind.Defense) >= _random.Next(1, 101);
            PracticeStrike(avatar, m, hit);
            if (!hit)
            {
                Tell(avatar, $"{Article(m.Kind)} weicht deinem Schlag aus.");
                return;
            }
            var touchSpell = avatar.Touch?.Spell;
            int damage = PlayerDamage(avatar, m, out _);
            if (touchSpell is not null && avatar.Touch is not null)
            {
                if (touchSpell.HitText is { } hitText)
                    Tell(avatar, string.Format(hitText, Article(m.Kind, capital: false, accusative: true)));
                ImproveAbility(avatar, touchSpell, m.Kind.Level);   // ImproveStroke des Berührungszaubers
            }
            m.Hp -= damage;
            if (m.Hp > 0)
            {
                Tell(avatar, $"Du triffst {Article(m.Kind, accusative: true)} für {damage} Schaden ({m.Hp}/{m.MaxHp}).");
                return;
            }
            m.DiedAt = now;
            m.Target = null;
            Tell(avatar, $"Du hast {Article(m.Kind, accusative: true)} besiegt!");
            BroadcastRoom(m.Room, GameReplies.Animate(ToRoomObject(m), GameReplies.DeathGroup, 9, 1500));
            PracticeDefense(avatar, m);
            avatar.KillTarget = null;
            CreateTreasure(m);
            MonsterKilled(m.Room);
        }
    }

    private void KillPlayer(Avatar victim, Monster killer)
    {
        killer.Target = null;
        // Wiederbelebung mit vollen Lebenspunkten: in Raza im Gasthaus (user.kod), sonst im Wiederbelebungsraum.
        var home = victim.Room.Definition.RespawnRoom is { } r && _rooms.TryGetValue(r, out var own) ? own : RespawnRoom;
        Tell(victim, $"Du wurdest von {Article(killer.Kind, dative: true)} getötet. Du erwachst in {home.Definition.Name}.");
        BroadcastRoom(victim.Room, GameReplies.Animate(ToRoomObject(victim), GameReplies.DeathGroup, 9, 1500));
        victim.Casting = null;
        Remove(victim);
        var (row, col) = home.ArrivalPoint;
        Place(victim, home, row, col);
        victim.Hp = victim.MaxHp;
        victim.Session.NotifyRoomChanged();
        victim.Session.SendHealth();
    }

    private static string Article(MonsterKind kind, bool capital = true, bool accusative = false, bool dative = false)
    {
        // der/den/dem Zombie, das/das/dem Skelett, die/die/der Mumie
        string article = kind.Gender switch
        {
            "f" => dative ? "der" : "die",
            "n" => dative ? "dem" : "das",
            _ => dative ? "dem" : accusative ? "den" : "der",
        };
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
        if (room.Avatars.Count == 0)
            FirstUserEntered(room);
        avatar.Room = room;
        avatar.Row = row;
        avatar.Col = col;
        // Raumwechsel/Teleport ist kein Speedhack.
        avatar.MoveOldRoom = room.Definition.Id;
        avatar.MoveOldRow = row / 64;
        avatar.MoveOldCol = col / 64;
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
