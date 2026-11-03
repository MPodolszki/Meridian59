using Meridian.Protocol;

namespace Meridian.Server;

/// <summary>
/// Gruft 3a von Marion („Affirmation of the Forsaken“, marcry3a.kod): drei Fallenbereiche mit schlafenden Dreschern.
/// Wer im Bereich steht und den letzten Drescher darin tötet, öffnet die Falle (Sektoren) für 15/30/45 s; solange
/// jemand darin steht, bleibt sie offen, danach schließt sie und neue schlafende Drescher erscheinen. Falle 3 füllt
/// die Truhe (hier: Beute auf dem Boden davor). Spezifikation in docs/marion.md.
/// </summary>
public sealed class Crypt3a
{
    public const string Kind = "marioncrypt3a";

    /// <summary>Fallenbereiche als Rechtecke [Zeile1, Spalte1, Zeile2, Spalte2] (inklusive).</summary>
    public static readonly int[][][] Areas =
    [
        [[23, 17, 25, 26]],
        [[6, 3, 7, 4], [8, 3, 10, 9], [11, 8, 14, 9]],
        [[29, 31, 29, 32], [30, 28, 34, 34]],
    ];

    /// <summary>Truhennische (Blinkbereich 4): zählt für Falle 3 als „im Bereich“.</summary>
    public static readonly int[] ChestNiche = [30, 23, 33, 27];

    public static readonly TimeSpan[] OpenTime = [TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(45)];

    /// <summary>Schlafende Drescher je Falle: Feld und Blickrichtung.</summary>
    public static readonly (int Row, int Col, ushort Angle)[][] Sleepers =
    [
        [(23, 19, 1024)],
        [(10, 3, 0), (10, 7, 2048)],
        [(32, 29, 0), (30, 34, 2048), (33, 34, 2048)],
    ];

    /// <summary>Sektoren je Falle: (Sektor, Animation, offen, zu).</summary>
    public static readonly (int Sector, byte Animation, int Open, int Closed)[][] Sectors =
    [
        [(1, GameReplies.AnimateFloorLift, 1824, 1864)],
        [(2, GameReplies.AnimateCeilingLift, 1958, 1910), (3, GameReplies.AnimateCeilingLift, 1864, 1816)],
        [(5, GameReplies.AnimateCeilingLift, 2022, 1960)],
    ];

    /// <summary>Gitterfelder (ROOM_LOCKED_DOOR) je Falle; offen sind sie passierbar.</summary>
    public static readonly (int Row, int Col)[][] Gates =
    [
        [(22, 26), (23, 26)],
        [(14, 8), (14, 9), (15, 8), (15, 9)],
        [(31, 27), (31, 28), (32, 27), (32, 28)],
    ];

    /// <summary>ResetTreasureList: Chance (Summe 100), Gegenstand, Anzahl. Gegenstände, die es noch nicht gibt
    /// (Opfergaben, Prisma, Laute, Rose), und die Zusatzkräfte der Waffen fehlen.</summary>
    public static readonly (int Chance, string Item, int Number)[] Treasure =
    [
        (1, "rose", 1), (3, "scimitar", 1), (2, "scimitar", 1), (3, "scimitar", 1), (3, "longsword", 1), (3, "longsword", 1),
        (5, "hammer", 1), (3, "hammer", 1), (1, "mysticsword", 1), (2, "mysticsword", 1), (3, "axe", 1), (3, "axe", 1),
        (3, "axe", 1), (6, "platearmor", 1), (2, "offeringkraanan", 1), (2, "offeringshalille", 1), (3, "offeringriija", 1),
        (3, "offeringqor", 1), (2, "offeringfaren", 1), (2, "offeringjala", 1), (1, "prism", 1), (3, "truelute", 1),
        (5, "purplemushroom", 25), (7, "inkycap", 20), (5, "bluedragonscale", 20), (5, "darkangelfeather", 20),
        (5, "entrootberry", 50), (5, "yrxlsap", 20), (6, "ruby", 35), (3, "money", 25000),
    ];

    public bool[] Open { get; } = new bool[3];
    public DateTime?[] Due { get; } = new DateTime?[3];

    public static bool InArea(int trap, int row, int col)
    {
        if (Areas[trap].Any(q => row >= q[0] && row <= q[2] && col >= q[1] && col <= q[3]))
            return true;
        var n = ChestNiche;
        return trap == 2 && row >= n[0] && row <= n[2] && col >= n[1] && col <= n[3];
    }

    public bool GateOpen(int row, int col)
    {
        for (int t = 0; t < 3; t++)
            if (Open[t] && Gates[t].Contains((row, col)))
                return true;
        return false;
    }
}

public sealed partial class World
{
    private int ThrashersIn(Room room, int trap) => room.Monsters.Count(m =>
        m.Alive && m.Kind.Key == "thrasher" && Crypt3a.Areas[trap].Any(q =>
            m.Row / 64 >= q[0] && m.Row / 64 <= q[2] && m.Col / 64 >= q[1] && m.Col / 64 <= q[3]));

    private int PlayersIn(Room room, int trap) => room.Avatars.Values.Count(a => Crypt3a.InArea(trap, a.Row / 64, a.Col / 64));

    /// <summary>CreateThrashers: schlafende Drescher in leere Fallenbereiche (Falle 3 nur, wenn auch kein Spieler darin ist).</summary>
    private void CreateThrashers(Room room, Crypt3a crypt)
    {
        for (int t = 0; t < 3; t++)
        {
            if (crypt.Open[t] || ThrashersIn(room, t) > 0 || t == 2 && PlayersIn(room, t) > 0)
                continue;
            foreach (var (row, col, angle) in Crypt3a.Sleepers[t])
            {
                var m = SpawnMonsterLocked(room, MonsterKind.ByKey("thrasher"), new GridPos(row, col));
                m.Angle = angle;
                m.Asleep = true;
            }
        }
    }

    /// <summary>SomethingKilled: Drescher getötet von einem Spieler, der selbst in einem Fallenbereich steht, und es war
    /// der letzte Drescher dort → die Falle öffnet sich.</summary>
    private void Crypt3aKill(Room room, Crypt3a crypt, Avatar killer, Monster victim, DateTime now)
    {
        if (victim.Kind.Key != "thrasher")
            return;
        for (int t = 0; t < 3; t++)
        {
            if (crypt.Open[t] || !Crypt3a.InArea(t, killer.Row / 64, killer.Col / 64) || ThrashersIn(room, t) > 0)
                continue;
            crypt.Open[t] = true;
            crypt.Due[t] = now + Crypt3a.OpenTime[t];
            foreach (var (sector, anim, open, _) in Crypt3a.Sectors[t])
                SetSector(room, anim, sector, open, 20);
            if (t == 2)
                EndTreasure(room);
            return;
        }
    }

    /// <summary>DoEndTreasure: N = Random(1, Spieler im Bereich) bzw. Random(3, 5) ab sechs; je ein Wurf in die Liste.</summary>
    private void EndTreasure(Room room)
    {
        int players = Math.Max(1, PlayersIn(room, 2));
        int n = players > 5 ? _random.Next(3, 6) : _random.Next(1, players + 1);
        for (int i = 0; i < n; i++)
        {
            int roll = _random.Next(1, 101), sum = 0;
            foreach (var (chance, item, number) in Crypt3a.Treasure)
            {
                sum += chance;
                if (roll > sum)
                    continue;
                if (_items.TryGet(item, out var kind))
                    PutOnFloor(room, NewItem(kind, number), Room.Fine(32, _random.Next(8, 56)), Room.Fine(25, _random.Next(8, 56)));
                break;
            }
        }
    }

    /// <summary>StartTrapNTimer: ist niemand mehr im Bereich, schließt die Falle und neue Drescher schlafen darin.</summary>
    private void TickCrypt3a(Room room, Crypt3a crypt, DateTime now)
    {
        for (int t = 0; t < 3; t++)
        {
            if (crypt.Due[t] is not { } due || now < due)
                continue;
            if (PlayersIn(room, t) > 0)
            {
                crypt.Due[t] = now + Crypt3a.OpenTime[t];
                continue;
            }
            crypt.Due[t] = null;
            crypt.Open[t] = false;
            foreach (var (sector, anim, _, closed) in Crypt3a.Sectors[t])
                SetSector(room, anim, sector, closed, 150);
            CreateThrashers(room, crypt);
        }
    }

    /// <summary>Schlafender Drescher (AI_NOMOVE|AI_NOFIGHT): wacht auf, wenn ein Spieler innerhalb von 7 Feldern ist.</summary>
    private bool SleepTick(Monster m)
    {
        if (!m.Asleep)
            return false;
        var near = m.Room.Avatars.Values.FirstOrDefault(a => a.Hp > 0 && Distance(m.Row, m.Col, a.Row, a.Col) <= 7 * 64);
        if (near is null)
            return true;
        m.Asleep = false;
        m.Target = near;
        return false;
    }
}
