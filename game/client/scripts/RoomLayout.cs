using System;
using System.Collections.Generic;
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
                AddRun(root, materials, kind, height, r, c, end, cellSize, outdoors);
                c = end;
            }
        }
        root.AddChild(RoomDecor.Build(this, cellSize, outdoors, wallHeight));
        return root;
    }

    /// <summary>Deckenhöhe in Metern (Innenräume), wie in <see cref="Build"/>.</summary>
    public float CeilingMeters(float cellSize) => Math.Clamp(HeightMeters(Ceiling, cellSize), 3f, 8f);

    private (string Kind, float Height) Classify(int r, int c, float cellSize, float wallHeight)
    {
        int f = FloorAt(r, c);
        if (f < 0)
            return ("wall", wallHeight);
        float h = MathF.Round(HeightMeters(f, cellSize) * 4f) / 4f;   // auf 25 cm runden → größere Blöcke
        return IsWalkable(r, c) ? ("floor", h) : ("building", Math.Max(h, 1f));
    }

    private static void AddRun(Node3D root, Dictionary<string, StandardMaterial3D> materials,
        string kind, float height, int row, int c0, int c1, float cellSize, bool outdoors)
    {
        float width = (c1 - c0) * cellSize;
        float x = (c0 + 1) * cellSize + width / 2f;
        float z = (row + 1) * cellSize + cellSize / 2f;

        if (kind == "floor")
        {
            var tile = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(width, 0.1f + height, cellSize) },
                Position = new Vector3(x, (height - 0.1f) / 2f, z),
                MaterialOverride = Material(materials, outdoors ? "street" : "planks"),
            };
            root.AddChild(tile);
            return;
        }

        var block = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(width, height, cellSize) },
            Position = new Vector3(x, height / 2f, z),
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
            _ => ("plaster", 4f, new Color(0.66f, 0.60f, 0.50f)),
        };
        m = new StandardMaterial3D { AlbedoColor = color, Roughness = 0.95f };
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
