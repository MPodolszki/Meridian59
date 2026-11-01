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
    public int Ground { get; }
    public int Ceiling { get; }
    private readonly int[,] _floor;

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
        return new RoomLayout(rows, cols, root.GetProperty("ceiling").GetInt32(), floor);
    }

    /// <summary>Bodenhöhe des Felds (0-basierter Index), -1 außerhalb.</summary>
    public int FloorAt(int row, int col) =>
        row >= 0 && row < Rows && col >= 0 && col < Cols ? _floor[row, col] : -1;

    public bool IsWalkable(int row, int col)
    {
        int f = FloorAt(row, col);
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
        return root;
    }

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
        var color = name switch
        {
            "street" => new Color(0.48f, 0.45f, 0.40f),   // Pflaster
            "planks" => new Color(0.45f, 0.33f, 0.22f),   // Holzboden
            "house" => new Color(0.78f, 0.70f, 0.56f),    // verputzte Hauswand
            "roof" => new Color(0.50f, 0.22f, 0.16f),     // Ziegeldach
            "rock" => new Color(0.42f, 0.42f, 0.40f),     // Stadtmauer/Fels
            _ => new Color(0.66f, 0.60f, 0.50f),          // Innenwand
        };
        m = new StandardMaterial3D { AlbedoColor = color, Roughness = 0.95f };
        cache[name] = m;
        return m;
    }
}
