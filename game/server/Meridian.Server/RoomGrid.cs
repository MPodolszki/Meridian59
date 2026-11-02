using System.Text.Json;

namespace Meridian.Server;

/// <summary>
/// Begehbarkeit eines Raums auf Feldebene aus dem Höhenraster (rooms/*.json, erzeugt von game/tools/roo2layout.py
/// aus den .roo-Dateien, dieselben Daten wie im Client). Ein Feld ist begehbar, wenn es keine massive Wand ist und
/// höchstens eine Stufe (30 KOD-Einheiten) über dem Bezugsboden liegt – wie RoomLayout.IsWalkable im Client.
/// Koordinaten sind KOD-Felder (1-basiert).
/// </summary>
public sealed class RoomGrid
{
    private const int WalkableRange = 30;
    /// <summary>Obergrenze für besuchte Felder pro Suche (größter Tos-Raum hat ~3000 Felder).</summary>
    private const int MaxVisited = 6000;

    private readonly int[,] _floor;
    private readonly int _ground;
    /// <summary>Gesperrte Felder, z. B. eine geschlossene Sektortür (BP_SECTOR_MOVE).</summary>
    private readonly HashSet<GridPos> _blocked = new();

    public RoomGrid(int[,] floor, GridPos? reference = null)
    {
        _floor = floor;
        Rows = floor.GetLength(0);
        Cols = floor.GetLength(1);
        int min = int.MaxValue;
        foreach (int f in floor)
            if (f >= 0)
                min = Math.Min(min, f);
        int refFloor = reference is { } r && Inside(r.Row, r.Col) ? _floor[r.Row - 1, r.Col - 1] : -1;
        _ground = refFloor >= 0 ? refFloor : min == int.MaxValue ? 0 : min;
    }

    public int Rows { get; }
    public int Cols { get; }
    /// <summary>Felder je Sektor-ID (rooms/*.json "tags"), für bewegliche Sektoren wie Türen.</summary>
    public Dictionary<int, List<GridPos>> Tags { get; } = new();

    public static RoomGrid Load(string path, GridPos? reference)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;
        int rows = root.GetProperty("rows").GetInt32(), cols = root.GetProperty("cols").GetInt32();
        var floor = new int[rows, cols];
        int r = 0;
        foreach (var line in root.GetProperty("floor").EnumerateArray())
        {
            int c = 0;
            foreach (var v in line.EnumerateArray())
                floor[r, c++] = v.GetInt32();
            r++;
        }
        var grid = new RoomGrid(floor, reference);
        if (root.TryGetProperty("tags", out var tags))
            foreach (var t in tags.EnumerateArray())
                grid.Tags[t.GetProperty("id").GetInt32()] = t.GetProperty("cells").EnumerateArray()
                    .Select(c => new GridPos(c[0].GetInt32(), c[1].GetInt32())).ToList();
        return grid;
    }

    private bool Inside(int row, int col) => row >= 1 && col >= 1 && row <= Rows && col <= Cols;

    public bool IsWalkable(int row, int col)
    {
        if (!Inside(row, col) || _blocked.Contains(new GridPos(row, col)))
            return false;
        int f = _floor[row - 1, col - 1];
        return f >= 0 && f - _ground <= WalkableRange;
    }

    /// <summary>Schritt von einem Feld aufs Nachbarfeld: begehbar wie oben, oder höchstens eine Stufe höher als das
    /// Ausgangsfeld (Rampen in mehrstöckigen Räumen wie dem Mausoleum).</summary>
    public bool CanStep(int fromRow, int fromCol, int row, int col)
    {
        if (IsWalkable(row, col))
            return true;
        if (!Inside(row, col) || !Inside(fromRow, fromCol) || _blocked.Contains(new GridPos(row, col)))
            return false;
        int f = _floor[row - 1, col - 1], from = _floor[fromRow - 1, fromCol - 1];
        return f >= 0 && from >= 0 && f - from <= WalkableRange;
    }

    /// <summary>Felder sperren oder freigeben (Tür zu/auf).</summary>
    public void SetBlocked(IEnumerable<GridPos> cells, bool blocked)
    {
        foreach (var c in cells)
            if (blocked) _blocked.Add(c); else _blocked.Remove(c);
    }

    /// <summary>Bodenhöhe von Feldern ändern (Sektor mit ANIMATE_FLOOR_LIFT).</summary>
    public void SetFloor(IEnumerable<GridPos> cells, int height)
    {
        foreach (var c in cells.Where(c => Inside(c.Row, c.Col)))
            _floor[c.Row - 1, c.Col - 1] = height;
    }

    private static readonly (int Dr, int Dc, int Cost)[] Steps =
    [
        (-1, 0, 10), (1, 0, 10), (0, -1, 10), (0, 1, 10),
        (-1, -1, 14), (-1, 1, 14), (1, -1, 14), (1, 1, 14),
    ];

    /// <summary>
    /// A*-Suche von <paramref name="from"/> nach <paramref name="to"/> über begehbare Felder (8 Richtungen,
    /// Diagonalen nur ohne Ecken zu schneiden). Start und Ziel dürfen selbst unbegehbar sein (Monster am Rand,
    /// Spieler auf einer Stufe). Liefert die Felder nach dem Start bis einschließlich Ziel, oder null.
    /// </summary>
    public List<GridPos>? FindPath(GridPos from, GridPos to)
    {
        if (from == to)
            return [];
        bool Passable(GridPos from, int r, int c) => CanStep(from.Row, from.Col, r, c) || (r == to.Row && c == to.Col && Inside(r, c));

        var open = new PriorityQueue<GridPos, int>();
        var cost = new Dictionary<GridPos, int> { [from] = 0 };
        var came = new Dictionary<GridPos, GridPos>();
        open.Enqueue(from, Heuristic(from, to));
        int visited = 0;
        while (open.TryDequeue(out var cur, out _))
        {
            if (cur == to)
            {
                var path = new List<GridPos>();
                for (var p = cur; p != from; p = came[p])
                    path.Add(p);
                path.Reverse();
                return path;
            }
            if (++visited > MaxVisited)
                break;
            foreach (var (dr, dc, step) in Steps)
            {
                int r = cur.Row + dr, c = cur.Col + dc;
                if (!Passable(cur, r, c))
                    continue;
                if (dr != 0 && dc != 0 && (!Passable(cur, cur.Row + dr, cur.Col) || !Passable(cur, cur.Row, cur.Col + dc)))
                    continue;
                var next = new GridPos(r, c);
                int g = cost[cur] + step;
                if (cost.TryGetValue(next, out int old) && old <= g)
                    continue;
                cost[next] = g;
                came[next] = cur;
                open.Enqueue(next, g + Heuristic(next, to));
            }
        }
        return null;
    }

    private static int Heuristic(GridPos a, GridPos b)
    {
        int dr = Math.Abs(a.Row - b.Row), dc = Math.Abs(a.Col - b.Col);
        return 10 * Math.Max(dr, dc) + 4 * Math.Min(dr, dc);
    }
}
