using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;

namespace Meridian.Client;

/// <summary>
/// Grobes Höhenraster eines Raums (rooms/*.json, erzeugt von game/tools/roo2layout.py).
/// Ein Wert pro Grid-Feld: Bodenhöhe in KOD-Einheiten (64 = 1 Feld), -1 = massive Wand.
/// Felder nahe der niedrigsten Bodenhöhe sind begehbar, höhere gelten als Gebäude/Dach.
/// </summary>
public sealed class RoomLayout
{
    /// <summary>Höhenunterschied (KOD-Einheiten), bis zu dem ein Feld noch als Straße/Boden zählt.</summary>
    private const int WalkableRange = 30;

    public int Rows { get; }
    public int Cols { get; }
    /// <summary>Bezugshöhe des begehbaren Bodens (KOD-Einheiten). Standard: tiefster Boden;
    /// besser per <see cref="SetGroundAt"/> auf die Höhe am Standort des Spielers setzen.</summary>
    public int Ground { get; private set; }
    public int Ceiling { get; }
    private readonly int[,] _floor;

    /// <summary>Ausgänge (0-basiertes Feld, Zielraum) und verschlossene Türen aus tools/export_exits.py.</summary>
    public List<(int Row, int Col, string Target)> Exits { get; } = new();
    public List<(int Row, int Col)> Locked { get; } = new();
    /// <summary>Gelände ("village": Wiese statt Pflaster), Wassertiefe je Feld (SF_DEPTH 0–3) und Bäume/Sträucher.</summary>
    public string? Terrain { get; private set; }
    /// <summary>Ränder mit Randausgang ("NORTH", "SOUTH", "WEST", "EAST"); nur dort darf man das Raster verlassen.</summary>
    public HashSet<string> Edges { get; } = new();
    private string[]? _depth;
    public List<(int Row, int Col, int FineRow, int FineCol, bool Shrub)> Trees { get; } = new();
    /// <summary>Sektoren mit ID (rooms/*.json "tags"): Felder (0-basiert) und aktuelle Deckenhöhe; der Server bewegt
    /// sie per BP_SECTOR_MOVE (Tür des Mausoleums).</summary>
    private readonly Dictionary<int, List<(int Row, int Col)>> _tagCells = new();
    private readonly Dictionary<int, int> _tagCeilings = new();
    private readonly Dictionary<int, int> _tagFloors = new();

    private RoomLayout(int rows, int cols, int ceiling, int[,] floor)
    {
        Rows = rows;
        Cols = cols;
        Ceiling = ceiling;
        _floor = floor;
        int ground = int.MaxValue;
        foreach (int f in floor)
            if (f >= 0)
                ground = Math.Min(ground, f);
        Ground = ground == int.MaxValue ? 0 : ground;
    }

    /// <summary>Lädt res://rooms/&lt;roo ohne Endung&gt;.json, oder null wenn nicht vorhanden.</summary>
    public static RoomLayout? Load(string rooFile)
    {
        string path = $"res://rooms/{System.IO.Path.GetFileNameWithoutExtension(rooFile).ToLowerInvariant()}.json";
        if (!FileAccess.FileExists(path))
            return null;
        using var doc = JsonDocument.Parse(FileAccess.GetFileAsString(path));
        var root = doc.RootElement;
        int rows = root.GetProperty("rows").GetInt32();
        int cols = root.GetProperty("cols").GetInt32();
        var floor = new int[rows, cols];
        int r = 0;
        foreach (var line in root.GetProperty("floor").EnumerateArray())
        {
            int c = 0;
            foreach (var v in line.EnumerateArray())
                floor[r, c++] = v.GetInt32();
            r++;
        }
        var layout = new RoomLayout(rows, cols, root.GetProperty("ceiling").GetInt32(), floor);
        if (root.TryGetProperty("exits", out var exits))
            foreach (var e in exits.EnumerateArray())
                layout.Exits.Add((e[0].GetInt32() - 1, e[1].GetInt32() - 1, e.GetArrayLength() > 2 ? e[2].GetString() ?? "" : ""));
        if (root.TryGetProperty("edges", out var edges))
            foreach (var e in edges.EnumerateArray())
                layout.Edges.Add(e.GetString() ?? "");
        if (root.TryGetProperty("terrain", out var terrain))
            layout.Terrain = terrain.GetString();
        if (root.TryGetProperty("depth", out var depth))
            layout._depth = depth.EnumerateArray().Select(d => d.GetString() ?? "").ToArray();
        if (root.TryGetProperty("trees", out var trees))
            foreach (var t in trees.EnumerateArray())
                layout.Trees.Add((t[0].GetInt32() - 1, t[1].GetInt32() - 1, t[2].GetInt32(), t[3].GetInt32(), t[4].GetString() == "shrub"));
        if (root.TryGetProperty("tags", out var tagList))
            foreach (var t in tagList.EnumerateArray())
            {
                int id = t.GetProperty("id").GetInt32();
                layout._tagCells[id] = t.GetProperty("cells").EnumerateArray().Select(c => (c[0].GetInt32() - 1, c[1].GetInt32() - 1)).ToList();
                layout._tagCeilings[id] = t.GetProperty("ceiling").GetInt32();
                layout._tagFloors[id] = t.GetProperty("floor").GetInt32();
            }
        if (root.TryGetProperty("locked", out var locked))
            foreach (var e in locked.EnumerateArray())
                layout.Locked.Add((e[0].GetInt32() - 1, e[1].GetInt32() - 1));
        return layout;
    }

    /// <summary>Setzt die Bezugshöhe auf den Boden des Felds, auf dem der Spieler steht.</summary>
    public void SetGroundAt(int row, int col)
    {
        int f = FloorAt(row, col);
        if (f >= 0)
            Ground = f;
    }

    /// <summary>Bodenhöhe des Felds (0-basierter Index), -1 außerhalb.</summary>
    public int FloorAt(int row, int col) =>
        row >= 0 && row < Rows && col >= 0 && col < Cols ? _floor[row, col] : -1;

    /// <summary>BP_SECTOR_MOVE: Boden (Animation 4) oder Decke (5) eines Sektors auf eine neue Höhe.</summary>
    public void SetSector(int id, int animation, int height)
    {
        if (!_tagCells.TryGetValue(id, out var cells))
            return;
        if (animation == 4)
        {
            _tagFloors[id] = height;
            foreach (var (r, c) in cells)
                _floor[r, c] = height;
        }
        else if (animation == 5)
            _tagCeilings[id] = height;
    }

    /// <summary>Geschlossene Sektortür: Decke liegt weniger als ein Feld (64) über dem Boden.</summary>
    public bool IsClosedDoor(int row, int col)
    {
        foreach (var (id, cells) in _tagCells)
            if (_tagCeilings[id] - _tagFloors[id] < 64 && cells.Contains((row, col)))
                return true;
        return false;
    }

    /// <summary>Schritt aufs Nachbarfeld: begehbar wie <see cref="IsWalkable"/> oder höchstens eine Stufe höher als das
    /// Ausgangsfeld (Rampen in mehrstöckigen Räumen); nie durch geschlossene Sektortüren.</summary>
    public bool CanStep(int fromRow, int fromCol, int row, int col)
    {
        int f = FloorAt(row, col), from = FloorAt(fromRow, fromCol);
        if (f < 0 || IsClosedDoor(row, col))
            return false;
        return IsWalkable(row, col) || from >= 0 && f - from <= WalkableRange;
    }

    /// <summary>Wassertiefe des Felds (0 = trocken, 1–3 = flach bis sehr tief).</summary>
    public int DepthAt(int row, int col) =>
        _depth is not null && row >= 0 && row < _depth.Length && col >= 0 && col < _depth[row].Length ? _depth[row][col] - '0' : 0;

    public bool IsWalkable(int row, int col)
    {
        int f = FloorAt(row, col);
        // Tiefer liegende Bereiche (Stufen, Senken) sind begehbar, höhere nur bis zur Stufenhöhe.
        return f >= 0 && f - Ground <= WalkableRange;
    }

    /// <summary>Höhe in Metern relativ zum tiefsten Boden.</summary>
    public float HeightMeters(int kodHeight, float cellSize) => (kodHeight - Ground) / 64f * cellSize;

    /// <summary>
    /// Baut Platzhalter-Geometrie: Bodenplatten für begehbare Felder, Quader für Gebäude (bis Dachhöhe)
    /// und Wände (-1). Gleichartige Nachbarfelder einer Zeile werden zu einem Quader zusammengefasst.
    /// Feld (r, c) 0-basiert entspricht KOD-Zeile r+1 / Spalte c+1 → Welt X = (c+1)·cellSize, Z = (r+1)·cellSize.
    /// </summary>
    public Node3D Build(float cellSize, bool outdoors)
    {
        var root = new Node3D { Name = "RoomGeometry" };
        float wallHeight = Math.Clamp(HeightMeters(Ceiling, cellSize), 3f, 8f);
        var materials = new Dictionary<string, StandardMaterial3D>();

        for (int r = 0; r < Rows; r++)
        {
            int c = 0;
            while (c < Cols)
            {
                var (kind, height) = Classify(r, c, cellSize, wallHeight);
                int end = c + 1;
                while (end < Cols && Classify(r, end, cellSize, wallHeight) == (kind, height))
                    end++;
                AddRun(root, materials, kind, height, r, c, end, cellSize, outdoors, Terrain);
                c = end;
            }
        }
        if (!outdoors)
            AddEnclosure(root, materials, cellSize, wallHeight);
        root.AddChild(RoomDecor.Build(this, cellSize, outdoors, wallHeight));
        foreach (var t in Trees)
            root.AddChild(Tree(t.Shrub, new Vector3((t.Col + 1 + t.FineCol / 64f) * cellSize,
                HeightMeters(Math.Max(FloorAt(t.Row, t.Col), 0), cellSize), (t.Row + 1 + t.FineRow / 64f) * cellSize)));
        return root;
    }

    /// <summary>Innenräume: Wände ringsum außerhalb des Rasters bis zur Decke, damit man nicht in den Himmel schaut
    /// (die .roo-Ränder haben oft keine Wandfelder).</summary>
    private void AddEnclosure(Node3D root, Dictionary<string, StandardMaterial3D> materials, float cellSize, float wallHeight)
    {
        float width = Cols * cellSize, depth = Rows * cellSize, bottom = -2f, top = wallHeight + 0.5f;
        float x0 = cellSize, z0 = cellSize, t = cellSize;
        var walls = new (Vector3 Size, Vector3 Center)[]
        {
            (new(width + 2 * t, top - bottom, t), new(x0 + width / 2, (top + bottom) / 2, z0 - t / 2)),
            (new(width + 2 * t, top - bottom, t), new(x0 + width / 2, (top + bottom) / 2, z0 + depth + t / 2)),
            (new(t, top - bottom, depth), new(x0 - t / 2, (top + bottom) / 2, z0 + depth / 2)),
            (new(t, top - bottom, depth), new(x0 + width + t / 2, (top + bottom) / 2, z0 + depth / 2)),
        };
        foreach (var (size, center) in walls)
            root.AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = size },
                Position = center,
                MaterialOverride = Material(materials, "plaster"),
            });
    }

    /// <summary>Deckenhöhe in Metern (Innenräume), wie in <see cref="Build"/>.</summary>
    public float CeilingMeters(float cellSize) => Math.Clamp(HeightMeters(Ceiling, cellSize), 3f, 8f);

    private (string Kind, float Height) Classify(int r, int c, float cellSize, float wallHeight)
    {
        int f = FloorAt(r, c);
        if (f < 0 || IsClosedDoor(r, c))
            return ("wall", wallHeight);
        float h = MathF.Round(HeightMeters(f, cellSize) * 4f) / 4f;   // auf 25 cm runden → größere Blöcke
        if (!IsWalkable(r, c))
            return ("building", Math.Max(h, 1f));
        return DepthAt(r, c) > 0 ? ("water", h) : ("floor", h);
    }

    private static void AddRun(Node3D root, Dictionary<string, StandardMaterial3D> materials,
        string kind, float height, int row, int c0, int c1, float cellSize, bool outdoors, string? terrain)
    {
        float width = (c1 - c0) * cellSize;
        float x = (c0 + 1) * cellSize + width / 2f;
        float z = (row + 1) * cellSize + cellSize / 2f;

        if (kind is "floor" or "water")
        {
            // Bodenplatte von einer festen Unterkante bis zur Bodenhöhe: Felder unter dem Bezugsboden (negative
            // Höhe, z. B. tiefer liegende Straßenteile) dürfen keine Quader mit negativer Dicke werden – das gab Löcher.
            const float bottom = -12f;
            var tile = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(width, height - bottom, cellSize) },
                Position = new Vector3(x, (height + bottom) / 2f, z),
                MaterialOverride = Material(materials, kind == "water" ? "water" : !outdoors ? "planks" : terrain == "village" ? "grass" : "street"),
            };
            root.AddChild(tile);
            return;
        }

        // Gebäude/Wände reichen bis unter den Boden, damit sie über tiefer liegenden Straßen nicht schweben.
        const float foundation = -12f;
        var block = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(width, height - foundation, cellSize) },
            Position = new Vector3(x, (height + foundation) / 2f, z),
            MaterialOverride = Material(materials, kind == "wall" ? (outdoors ? "rock" : "plaster") : "house"),
        };
        root.AddChild(block);
        if (kind == "building")
        {
            // Dachkante als dunklere Platte obenauf
            root.AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(width, 0.25f, cellSize) },
                Position = new Vector3(x, height + 0.125f, z),
                MaterialOverride = Material(materials, "roof"),
            });
        }
    }

    /// <summary>Baum (OO_RAZA_TREE1) bzw. Strauch aus Stamm und Kronenkugeln.</summary>
    private static Node3D Tree(bool shrub, Vector3 at)
    {
        var tree = new Node3D { Position = at, Name = shrub ? "Shrub" : "Tree" };
        var bark = new StandardMaterial3D { AlbedoColor = new Color(0.33f, 0.23f, 0.14f), Roughness = 1f };
        var leaves = new StandardMaterial3D { AlbedoColor = new Color(0.20f, 0.38f, 0.14f), Roughness = 1f };
        float trunk = shrub ? 0.3f : 2.6f;
        tree.AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = shrub ? 0.06f : 0.16f, BottomRadius = shrub ? 0.08f : 0.24f, Height = trunk },
            Position = new Vector3(0, trunk / 2f, 0),
            MaterialOverride = bark,
        });
        float r = shrub ? 0.6f : 1.6f;
        foreach (var (x, y, z, s) in new[] { (0f, 0f, 0f, 1f), (0.5f, -0.3f, 0.3f, 0.75f), (-0.45f, -0.25f, -0.35f, 0.7f), (0.1f, 0.45f, -0.2f, 0.65f) })
            tree.AddChild(new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = r * s, Height = 2f * r * s * 0.9f },
                Position = new Vector3(x * r, trunk + r * 0.7f + y * r, z * r),
                MaterialOverride = leaves,
            });
        return tree;
    }

    private static StandardMaterial3D Material(Dictionary<string, StandardMaterial3D> cache, string name)
    {
        if (cache.TryGetValue(name, out var m))
            return m;
        // Name → (Textur in textures/, Kachelgröße in m, Farbton als Rückfall ohne Textur)
        var (texture, tileMeters, color) = name switch
        {
            "street" => ("cobblestone", 3f, new Color(0.48f, 0.45f, 0.40f)),
            "planks" => ("planks", 3f, new Color(0.45f, 0.33f, 0.22f)),
            "house" => ("plaster", 4f, new Color(0.78f, 0.70f, 0.56f)),
            "roof" => ("roof", 3f, new Color(0.50f, 0.22f, 0.16f)),
            "rock" => ("stonewall", 4f, new Color(0.42f, 0.42f, 0.40f)),
            "grass" => ("grass", 4f, new Color(0.30f, 0.45f, 0.15f)),
            "water" => ("", 1f, new Color(0.18f, 0.32f, 0.42f, 0.85f)),
            _ => ("plaster", 4f, new Color(0.66f, 0.60f, 0.50f)),
        };
        m = new StandardMaterial3D { AlbedoColor = color, Roughness = 0.95f };
        if (name == "water")
        {
            // Wasser (SF_DEPTH): glatt, spiegelnd, leicht durchsichtig.
            m.Roughness = 0.08f;
            m.Metallic = 0.3f;
            m.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            cache[name] = m;
            return m;
        }
        string albedo = $"res://textures/{texture}_albedo.png";
        if (ResourceLoader.Exists(albedo))
        {
            m.AlbedoColor = name == "plaster" ? new Color(0.85f, 0.82f, 0.78f) : Colors.White;   // Innenwände etwas dunkler
            m.AlbedoTexture = GD.Load<Texture2D>(albedo);
            m.NormalEnabled = true;
            m.NormalTexture = GD.Load<Texture2D>($"res://textures/{texture}_normal.png");
            // Triplanar in Weltkoordinaten: Blöcke brauchen keine UVs, Muster läuft über Blockgrenzen weiter.
            m.Uv1Triplanar = true;
            m.Uv1WorldTriplanar = true;
            m.Uv1Scale = Vector3.One / tileMeters;
            m.TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic;
        }
        cache[name] = m;
        return m;
    }
}
