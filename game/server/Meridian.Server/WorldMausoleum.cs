using Meridian.Protocol;

namespace Meridian.Server;

/// <summary>
/// Zustand des Mausoleum-Rätsels (monsroom/guest6.kod, Raza-Fassung newb6.kod): zwei Hebel heben die Tür (Sektor 3),
/// im Endbereich warten vier Mumien und eine Schattenmumie; sind sie besiegt, senkt sich die Säule (Sektoren 4/5)
/// und die Belohnung liegt bereit. Siehe docs/raza.md.
/// </summary>
public sealed class Mausoleum
{
    public const string LeverClass = "GuestLever";
    public const int SectorDoor = 3, SectorNode1 = 4, SectorNode2 = 5;
    /// <summary>Türhöhen (Decke): zu 84, halb 128 (ein Hebel), offen 172 (beide). Boden der Tür 84.</summary>
    public const int DoorClosed = 84, DoorHalf = 128, DoorOpen = 172;
    public static readonly TimeSpan SlamTime = TimeSpan.FromMilliseconds(2000), FightTime = TimeSpan.FromMilliseconds(10000);
    /// <summary>Grube (viPit_row/col) und höchstens zwei Mumien ohne Beute darin (Newb6 viPit_monsters_max).</summary>
    public static readonly GridPos Pit = new(11, 4);
    public const int PitMax = 2;

    public bool ColumnDown { get; set; }
    /// <summary>Laufender Zeitgeber (ptDoor): Tür zuschlagen oder Endbereich prüfen.</summary>
    public DateTime? DoorDue { get; set; }
    public bool DoorFight { get; set; }
    /// <summary>Aktuelle Sektorhöhen, die vom .roo-Ausgangszustand abweichen (für neu Eintretende).</summary>
    public Dictionary<int, (byte Animation, int Height)> Sectors { get; } = new();

    /// <summary>CountInFinal: Endbereich Zeilen 28–40 × Spalten 1–9, Gang 16–28 × 1–2, oder Spalte &lt; 3 ab Zeile 16.</summary>
    public static bool InFinal(int row, int col) =>
        row is >= 28 and <= 40 && col is >= 1 and <= 9
        || row is >= 16 and <= 28 && col is >= 1 and <= 2
        || col < 3 && row > 15;

    /// <summary>ResetMonstersInFinal löscht nur im Endbereich ohne den Gang (MIN/MAX_FINAL oder Spalte &lt; 3 ab Zeile 16).</summary>
    public static bool InFinalForReset(int row, int col) =>
        row is >= 28 and <= 40 && col is >= 1 and <= 9 || col < 3 && row > 15;
}

public sealed partial class World
{
    /// <summary>BP_REQ_ACTIVATE: Hebel umlegen (gstlever.kod TryActivate).</summary>
    public void Activate(Avatar a, uint id)
    {
        lock (_gate)
        {
            var npc = a.Room.Npcs.FirstOrDefault(n => n.Id == id);
            if (npc?.Definition.KodClass != Mausoleum.LeverClass || a.Room.Puzzle is not { } puzzle)
            {
                Tell(a, "Das lässt sich nicht betätigen.");
                return;
            }
            if (Math.Abs(a.Row / 64 - npc.Row / 64) > 1 || Math.Abs(a.Col / 64 - npc.Col / 64) > 1)   // GUESTLEVER_RANGE 1
            {
                Tell(a, "Der Hebel ist nicht nah genug, um ihn umzulegen.");
                return;
            }
            if (npc.Group == 2)
            {
                Tell(a, "Der Hebel steckt in seiner Stellung fest.");
                return;
            }
            Tell(a, "Du legst den Hebel um.");
            SetLever(a.Room, npc, true);
            LeverChanged(a.Room, puzzle, DateTime.UtcNow);
        }
    }

    private void SetLever(Room room, Npc lever, bool down)
    {
        ushort group = (ushort)(down ? 2 : 1);
        if (lever.Group == group)
            return;
        lever.Group = group;
        BroadcastRoom(room, GameReplies.Change(ToRoomObject(lever)));
    }

    private IEnumerable<Npc> Levers(Room room) => room.Npcs.Where(n => n.Definition.KodClass == Mausoleum.LeverClass);

    /// <summary>LeverChanged: ein Hebel hebt die Tür halb und sie schlägt nach 2 s zu; beide öffnen sie, dann wird
    /// alle 10 s geprüft, ob noch jemand im Endbereich ist.</summary>
    private void LeverChanged(Room room, Mausoleum puzzle, DateTime now)
    {
        int switched = Levers(room).Count(l => l.Group == 2);
        puzzle.DoorDue = null;
        if (switched == 1)
        {
            SetSector(room, GameReplies.AnimateCeilingLift, Mausoleum.SectorDoor, Mausoleum.DoorHalf, 16);
            puzzle.DoorDue = now + Mausoleum.SlamTime;
            puzzle.DoorFight = false;
        }
        else if (switched == 2)
        {
            SetSector(room, GameReplies.AnimateCeilingLift, Mausoleum.SectorDoor, Mausoleum.DoorOpen, 16);
            puzzle.DoorDue = now + Mausoleum.FightTime;
            puzzle.DoorFight = true;
        }
    }

    /// <summary>SetSector: an alle im Raum senden und das Raster anpassen (Tür sperrt, Säule ändert die Bodenhöhe).</summary>
    private void SetSector(Room room, byte animation, int sector, int height, byte speed)
    {
        if (room.Puzzle is { } puzzle)
            puzzle.Sectors[sector] = (animation, height);
        BroadcastRoom(room, GameReplies.SectorMove(animation, (ushort)sector, (ushort)height, speed));
        if (room.Grid?.Tags.GetValueOrDefault(sector) is not { } cells)
            return;
        if (sector == Mausoleum.SectorDoor)
            room.Grid.SetBlocked(cells, height - Mausoleum.DoorClosed < 64);
        else if (animation == GameReplies.AnimateFloorLift)
            room.Grid.SetFloor(cells, height);
    }

    /// <summary>Sektorzustände für einen neu Eintretenden (BP_SECTOR_MOVE mit Tempo 0).</summary>
    public IReadOnlyList<byte[]> SectorPackets(Room room)
    {
        lock (_gate)
            return room.Puzzle is { } p
                ? p.Sectors.Select(kv => GameReplies.SectorMove(kv.Value.Animation, (ushort)kv.Key, (ushort)kv.Value.Height, 0)).ToList()
                : [];
    }

    /// <summary>Zeitgeber des Rätsels (SlamTimer, FightTimer).</summary>
    private void TickPuzzle(Room room, DateTime now)
    {
        if (room.Puzzle is not { DoorDue: { } due } puzzle || now < due)
            return;
        puzzle.DoorDue = null;
        if (!puzzle.DoorFight)
        {
            // SlamTimer: Tür fällt schnell zu, Hebel springen zurück.
            SetSector(room, GameReplies.AnimateCeilingLift, Mausoleum.SectorDoor, Mausoleum.DoorClosed, 128);
            foreach (var lever in Levers(room))
                SetLever(room, lever, false);
            return;
        }
        // FightTimer: niemand mehr im Endbereich → alles zurücksetzen, sonst weiter prüfen.
        if (CountInFinal(room, players: true) == 0)
            ResetFinal(room, puzzle);
        else
            puzzle.DoorDue = now + Mausoleum.FightTime;
    }

    private int CountInFinal(Room room, bool players) => players
        ? room.Avatars.Values.Count(a => Mausoleum.InFinal(a.Row / 64, a.Col / 64))
        : room.Monsters.Count(m => m.Alive && Mausoleum.InFinal(m.Row / 64, m.Col / 64));

    private void ResetFinal(Room room, Mausoleum puzzle)
    {
        puzzle.DoorDue = null;
        puzzle.ColumnDown = false;
        SetSector(room, GameReplies.AnimateFloorLift, Mausoleum.SectorNode1, 500, 0);
        SetSector(room, GameReplies.AnimateFloorLift, Mausoleum.SectorNode2, 519, 0);
        foreach (var lever in Levers(room))
            SetLever(room, lever, false);
        SetSector(room, GameReplies.AnimateCeilingLift, Mausoleum.SectorDoor, Mausoleum.DoorClosed, 0);
        ResetMonstersInFinal(room);
    }

    /// <summary>Endbereich leeren und neu besetzen: vier Mumien und eine Schattenmumie (Zeilen 29–37, Spalten 3–8).</summary>
    private void ResetMonstersInFinal(Room room)
    {
        foreach (var m in room.Monsters.Where(m => Mausoleum.InFinalForReset(m.Row / 64, m.Col / 64)).ToList())
        {
            room.Monsters.Remove(m);
            BroadcastRoom(room, GameReplies.Remove(m.Id));
        }
        foreach (var kind in new[] { MonsterKind.Mummy, MonsterKind.Mummy, MonsterKind.Mummy, MonsterKind.Mummy, MonsterKind.BlackMummy })
            SpawnMonsterLocked(room, kind, new GridPos(_random.Next(29, 38), _random.Next(3, 9)));
    }

    /// <summary>FirstUserEntered: eine Mumie in die Grube, Endbereich neu besetzen.</summary>
    private void FirstUserEntered(Room room)
    {
        if (room.Puzzle is null)
            return;
        if (PitMummies(room) < Mausoleum.PitMax)
            SpawnMonsterLocked(room, MonsterKind.MummyNoTreasure, Mausoleum.Pit);
        ResetMonstersInFinal(room);
    }

    private int PitMummies(Room room) => room.Monsters.Count(m => m.Alive && m.Kind == MonsterKind.MummyNoTreasure);

    /// <summary>ReqNewHold: Mumien ohne Beute nur in der Grube (höchstens zwei), andere nie dort. null = verweigert.</summary>
    private GridPos? PuzzleSpawnPoint(Room room, MonsterKind kind, GridPos at)
    {
        if (room.Puzzle is null)
            return at;
        bool pit = at == Mausoleum.Pit;
        if (kind == MonsterKind.MummyNoTreasure)
            return pit && PitMummies(room) < Mausoleum.PitMax ? at : null;
        return pit ? null : at;
    }

    /// <summary>CheckAllDeadMonsters + DoColumnDown (newb6.kod): Endbereich frei und ein Spieler darin → Belohnung,
    /// Säule senkt sich, Tür schließt, der Kampfzeitgeber läuft.</summary>
    private void MonsterKilled(Room room)
    {
        if (room.Puzzle is not { ColumnDown: false } puzzle
            || CountInFinal(room, players: false) != 0 || CountInFinal(room, players: true) == 0)
            return;
        puzzle.ColumnDown = true;
        DropReward(room, "elderberry", _random.Next(5, 11), 34, 4, 48, 48);
        DropReward(room, "herbs", _random.Next(10, 16), 34, 5, 48, 16);
        DropReward(room, "mushroom", _random.Next(10, 16), 35, 4, 16, 48);
        DropReward(room, "money", _random.Next(45, 56) * 10, 35, 5, 16, 16);
        SetSector(room, GameReplies.AnimateFloorLift, Mausoleum.SectorNode1, 105, 32);
        SetSector(room, GameReplies.AnimateFloorLift, Mausoleum.SectorNode2, 124, 32);
        SetSector(room, GameReplies.AnimateCeilingLift, Mausoleum.SectorDoor, Mausoleum.DoorClosed, 0);
        puzzle.DoorDue = DateTime.UtcNow + Mausoleum.FightTime;
        puzzle.DoorFight = true;
    }

    private void DropReward(Room room, string key, int number, int row, int col, int fineRow, int fineCol)
    {
        if (_items.TryGet(key, out var kind))
            PutOnFloor(room, NewItem(kind, number), Room.Fine(row, fineRow), Room.Fine(col, fineCol));
    }
}
