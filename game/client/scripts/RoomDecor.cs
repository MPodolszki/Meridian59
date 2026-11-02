using System;
using System.Collections.Generic;
using Godot;

namespace Meridian.Client;

/// <summary>
/// Schmuck für die Platzhalter-Geometrie aus <see cref="RoomLayout"/>, rein prozedural (eigene Assets):
/// Außen bekommen Hausfassaden an der Straße Putzfelder in wechselnden Farben, Fachwerkbalken, einen Steinsockel,
/// Fenster (nachts warm leuchtend), gelegentlich Türen und einen schrägen Dachüberstand.
/// Innen gibt es Holzvertäfelung an den Wänden, eine Holzdecke mit Balken und einige Lampen.
/// Gleichartige Teile werden als MultiMesh gezeichnet.
/// </summary>
public static class RoomDecor
{
    private static readonly Color[] PlasterTints =
    [
        new(0.93f, 0.88f, 0.76f),
        new(0.86f, 0.80f, 0.66f),
        new(0.95f, 0.91f, 0.84f),
        new(0.84f, 0.74f, 0.60f),
        new(0.80f, 0.82f, 0.78f),
    ];

    /// <summary>Fensterglas für alle Fassaden; das warme Leuchten wird mit der Tageszeit gesetzt (<see cref="SetDaylight"/>).</summary>
    private static readonly StandardMaterial3D WindowGlass = new()
    {
        AlbedoColor = new Color(0.10f, 0.12f, 0.16f),
        Roughness = 0.15f,
        Metallic = 0.4f,
        EmissionEnabled = true,
        Emission = new Color(1f, 0.72f, 0.38f),
        EmissionEnergyMultiplier = 0f,
    };

    /// <summary>Tageslicht 0 (Nacht) .. 1 (Mittag): nachts leuchten die Fenster warm.</summary>
    public static void SetDaylight(float daylight) =>
        WindowGlass.EmissionEnergyMultiplier = Mathf.Lerp(1.4f, 0f, Mathf.Clamp(daylight * 1.6f, 0f, 1f));

    /// <summary>Teile eines Typs: Mesh, Material und Transformationen (optional mit Farbe je Teil).</summary>
    private sealed class Batch(Mesh mesh, Material material, bool colored = false)
    {
        public readonly List<(Transform3D Xf, Color Color)> Items = new();
        public void Add(Transform3D xf, Color? color = null) => Items.Add((xf, color ?? Colors.White));

        public MultiMeshInstance3D? Build()
        {
            if (Items.Count == 0)
                return null;
            var mm = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                UseColors = colored,
                Mesh = mesh,
                InstanceCount = Items.Count,
            };
            for (int i = 0; i < Items.Count; i++)
            {
                mm.SetInstanceTransform(i, Items[i].Xf);
                if (colored)
                    mm.SetInstanceColor(i, Items[i].Color);
            }
            return new MultiMeshInstance3D { Multimesh = mm, MaterialOverride = material };
        }
    }

    public static Node3D Build(RoomLayout layout, float cell, bool outdoors, float wallHeight)
    {
        var root = new Node3D { Name = "RoomDecor" };
        if (outdoors)
            BuildFacades(root, layout, cell);
        else
            BuildInterior(root, layout, cell, wallHeight);
        BuildDoors(root, layout, cell);
        return root;
    }

    // ---------------------------------------------------------------- Außen

    private static void BuildFacades(Node3D root, RoomLayout layout, float cell)
    {
        var plaster = Textured("plaster", 3f, Colors.White, vertexColor: true);
        var timber = Textured("planks", 2f, new Color(0.30f, 0.20f, 0.13f));
        var stone = Textured("stonewall", 2f, new Color(0.75f, 0.73f, 0.70f));
        var roof = Textured("roof", 2f, Colors.White);
        var door = Textured("planks", 1.5f, new Color(0.42f, 0.27f, 0.16f));
        var glass = WindowGlass;

        var panels = new Batch(new BoxMesh { Size = Vector3.One }, plaster, colored: true);
        var beams = new Batch(new BoxMesh { Size = Vector3.One }, timber);
        var plinths = new Batch(new BoxMesh { Size = Vector3.One }, stone);
        var windows = new Batch(new BoxMesh { Size = Vector3.One }, glass);
        var frames = new Batch(new BoxMesh { Size = Vector3.One }, timber);
        var doors = new Batch(new BoxMesh { Size = Vector3.One }, door);
        var roofs = new Batch(new BoxMesh { Size = Vector3.One }, roof);

        (int Dr, int Dc)[] dirs = [(1, 0), (-1, 0), (0, 1), (0, -1)];
        for (int r = 0; r < layout.Rows; r++)
        {
            for (int c = 0; c < layout.Cols; c++)
            {
                int f = layout.FloorAt(r, c);
                if (f < 0 || layout.IsWalkable(r, c))
                    continue;
                float top = Mathf.Max(layout.HeightMeters(f, cell), 1f);
                foreach (var (dr, dc) in dirs)
                {
                    int nr = r + dr, nc = c + dc;
                    if (nr < 0 || nc < 0 || nr >= layout.Rows || nc >= layout.Cols || !layout.IsWalkable(nr, nc))
                        continue;
                    float street = Mathf.Max(layout.HeightMeters(layout.FloorAt(nr, nc), cell), 0f);
                    float height = top - street;
                    if (height < 1.5f)
                        continue;   // niedrige Kanten (Stufen, Mauern) bleiben schlicht
                    AddFacade(r, c, dr, dc, street, top, height);
                }
            }
        }

        void AddFacade(int r, int c, int dr, int dc, float bottom, float top, float height)
        {
            // Mittelpunkt der Außenfläche und Basis: Normal zeigt zur Straße, Tangente entlang der Fassade.
            var center = new Vector3((c + 1.5f) * cell + dc * cell / 2f, 0, (r + 1.5f) * cell + dr * cell / 2f);
            var normal = new Vector3(dc, 0, dr);
            var tangent = Vector3.Up.Cross(normal);                 // rechtshändig: X = Y × Z
            var basis = new Basis(tangent, Vector3.Up, normal);     // lokale X = Tangente, Z = Normal

            Transform3D Box(float along, float y, float outward, Vector3 size) =>
                new(Local(basis, size), center + tangent * along + Vector3.Up * y + normal * outward);

            // Putzfeld in Hausfarbe: Häuser wechseln etwa alle 3–5 Felder entlang der Straße.
            int house = Hash((r + c * 3) / 4, dr * 7 + dc * 13, (c - r) / 5);
            var tint = PlasterTints[house % PlasterTints.Length];
            panels.Add(Box(0, bottom + height / 2f, 0.02f, new Vector3(cell, height, 0.04f)), tint);

            plinths.Add(Box(0, bottom + 0.35f, 0.06f, new Vector3(cell, 0.7f, 0.12f)));
            // Fachwerk: Pfosten an der Feldkante, Riegel über dem Erdgeschoss und unter der Traufe.
            beams.Add(Box(-cell / 2f + 0.09f, bottom + height / 2f, 0.07f, new Vector3(0.18f, height, 0.1f)));
            beams.Add(Box(0, top - 0.12f, 0.07f, new Vector3(cell, 0.22f, 0.1f)));
            if (height > 4f)
                beams.Add(Box(0, bottom + 2.7f, 0.07f, new Vector3(cell, 0.2f, 0.1f)));
            if (height > 5.5f && Hash(r, c, 1) % 2 == 0)
            {
                // Diagonale Strebe im Obergeschoss
                var strut = new Basis(normal, Mathf.Atan2(height - 3f, cell) * (Hash(r, c, 2) % 2 == 0 ? 1 : -1)) * basis;
                beams.Add(new Transform3D(Local(strut, new Vector3(MathF.Sqrt(cell * cell + 4f), 0.16f, 0.09f)),
                    center + Vector3.Up * (bottom + 2.8f + (height - 3f) / 2f) + normal * 0.07f));
            }

            bool hasDoor = height > 2.8f && Hash(r, c, 3) % 7 == 0;
            if (hasDoor)
            {
                doors.Add(Box(0, bottom + 1.1f, 0.06f, new Vector3(1.1f, 2.2f, 0.1f)));
                frames.Add(Box(0, bottom + 2.25f, 0.09f, new Vector3(1.4f, 0.16f, 0.12f)));
            }
            else if (height > 3f)
                AddWindow(0, bottom + 1.6f);
            if (height > 5.2f)
                AddWindow(0, bottom + 3.9f);

            // Dachüberstand: schräge Dachfläche, die von der Traufe nach innen ansteigt.
            const float slope = 0.6f;   // rad ≈ 34°
            const float depth = 2.2f;
            var tilt = new Basis(tangent, -slope) * basis;   // äußere Kante unten, steigt nach innen
            var roofCenter = center + Vector3.Up * (top + MathF.Sin(slope) * depth / 2f - 0.05f)
                             + normal * (0.45f - MathF.Cos(slope) * depth / 2f);
            roofs.Add(new Transform3D(Local(tilt, new Vector3(cell + 0.02f, 0.14f, depth)), roofCenter));

            void AddWindow(float along, float y)
            {
                windows.Add(Box(along, y, 0.05f, new Vector3(0.8f, 1.0f, 0.06f)));
                frames.Add(Box(along, y - 0.58f, 0.1f, new Vector3(1.05f, 0.12f, 0.16f)));   // Fensterbank
                frames.Add(Box(along, y + 0.55f, 0.09f, new Vector3(0.95f, 0.1f, 0.1f)));
                frames.Add(Box(along, y, 0.09f, new Vector3(0.07f, 1.0f, 0.08f)));          // Sprosse
            }
        }

        foreach (var b in new[] { panels, plinths, beams, windows, frames, doors, roofs })
            if (b.Build() is { } node)
                root.AddChild(node);
    }

    // ---------------------------------------------------------------- Innen

    private static void BuildInterior(Node3D root, RoomLayout layout, float cell, float wallHeight)
    {
        var wood = Textured("planks", 2f, new Color(0.55f, 0.40f, 0.27f));
        var dark = Textured("planks", 2f, new Color(0.30f, 0.21f, 0.14f));
        var wainscot = new Batch(new BoxMesh { Size = Vector3.One }, wood);
        var beams = new Batch(new BoxMesh { Size = Vector3.One }, dark);

        int minR = int.MaxValue, minC = int.MaxValue, maxR = -1, maxC = -1;
        (int Dr, int Dc)[] dirs = [(1, 0), (-1, 0), (0, 1), (0, -1)];
        var walkable = new List<(int R, int C)>();
        for (int r = 0; r < layout.Rows; r++)
        {
            for (int c = 0; c < layout.Cols; c++)
            {
                if (!layout.IsWalkable(r, c))
                    continue;
                walkable.Add((r, c));
                minR = Math.Min(minR, r); maxR = Math.Max(maxR, r);
                minC = Math.Min(minC, c); maxC = Math.Max(maxC, c);
                float floor = Mathf.Max(layout.HeightMeters(layout.FloorAt(r, c), cell), 0f);
                foreach (var (dr, dc) in dirs)
                {
                    int nr = r + dr, nc = c + dc;
                    // Wände und höhere Einbauten (Theken, Podeste) bekommen Vertäfelung.
                    bool wall = nr < 0 || nc < 0 || nr >= layout.Rows || nc >= layout.Cols || layout.FloorAt(nr, nc) < 0
                        || !layout.IsWalkable(nr, nc) && layout.HeightMeters(layout.FloorAt(nr, nc), cell) - floor > 0.8f;
                    if (!wall)
                        continue;
                    // Vertäfelung an der Wandseite des Feldes, 1 m hoch, mit Abschlussleiste.
                    var center = new Vector3((c + 1.5f) * cell + dc * (cell / 2f - 0.04f), 0, (r + 1.5f) * cell + dr * (cell / 2f - 0.04f));
                    var size = dr != 0 ? new Vector3(cell, 1f, 0.06f) : new Vector3(0.06f, 1f, cell);
                    wainscot.Add(new Transform3D(Basis.FromScale(size), center + Vector3.Up * (floor + 0.5f)));
                    var rail = dr != 0 ? new Vector3(cell, 0.08f, 0.12f) : new Vector3(0.12f, 0.08f, cell);
                    beams.Add(new Transform3D(Basis.FromScale(rail), center + Vector3.Up * (floor + 1.02f)));
                }
            }
        }
        if (walkable.Count == 0)
            return;

        // Holzdecke über dem ganzen Raum, nur von unten sichtbar (Kamera kann darüber liegen).
        float x0 = (minC + 1) * cell, x1 = (maxC + 2) * cell, z0 = (minR + 1) * cell, z1 = (maxR + 2) * cell;
        var ceilingMat = Textured("planks", 2.5f, new Color(0.45f, 0.33f, 0.22f));
        ceilingMat.CullMode = BaseMaterial3D.CullModeEnum.Back;
        root.AddChild(new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = new Vector2(x1 - x0, z1 - z0), FlipFaces = true },
            Position = new Vector3((x0 + x1) / 2f, wallHeight, (z0 + z1) / 2f),
            MaterialOverride = ceilingMat,
        });
        // Deckenbalken alle zwei Felder quer durch den Raum.
        for (int c = minC; c <= maxC + 1; c += 2)
            beams.Add(new Transform3D(Basis.FromScale(new Vector3(0.25f, 0.3f, z1 - z0)),
                new Vector3((c + 1) * cell, wallHeight - 0.15f, (z0 + z1) / 2f)));

        // Lampen: etwa alle 4 Felder über begehbarem Boden, höchstens 14.
        var lampMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 0.8f, 0.5f),
            EmissionEnabled = true,
            Emission = new Color(1f, 0.75f, 0.4f),
            EmissionEnergyMultiplier = 3f,
        };
        int lamps = 0;
        foreach (var (r, c) in walkable)
        {
            if (lamps >= 14 || (r - minR) % 4 != 1 || (c - minC) % 4 != 1)
                continue;
            var pos = new Vector3((c + 1.5f) * cell, wallHeight - 0.7f, (r + 1.5f) * cell);
            root.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.15f, Height = 0.3f }, Position = pos, MaterialOverride = lampMat });
            root.AddChild(new OmniLight3D { Position = pos + Vector3.Down * 0.3f, LightColor = new Color(1f, 0.78f, 0.5f), LightEnergy = 2.2f, OmniRange = 9.5f, ShadowEnabled = false });
            lamps++;
        }

        foreach (var b in new[] { wainscot, beams })
            if (b.Build() is { } node)
                root.AddChild(node);
    }

    // ---------------------------------------------------------------- Türen

    /// <summary>Türen an den Ausgängen (auf der Wand neben dem Ausgangsfeld) mit leuchtender Schwelle und dem Ziel
    /// als Schrift; verschlossene Türen ohne Leuchten.</summary>
    private static void BuildDoors(Node3D root, RoomLayout layout, float cell)
    {
        var wood = Textured("planks", 1.5f, new Color(0.38f, 0.24f, 0.13f));
        var frame = Textured("planks", 1.5f, new Color(0.22f, 0.14f, 0.08f));
        var glow = new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 0.8f, 0.4f, 0.55f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            EmissionEnabled = true,
            Emission = new Color(1f, 0.75f, 0.35f),
            EmissionEnergyMultiplier = 1.5f,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };
        var doors = new Batch(new BoxMesh { Size = Vector3.One }, wood);
        var frames = new Batch(new BoxMesh { Size = Vector3.One }, frame);
        var marks = new Batch(new CylinderMesh { TopRadius = 0.5f, BottomRadius = 0.5f, Height = 1f }, glow);

        void Place(int r, int c, bool open, string target)
        {
            float floor = Mathf.Max(layout.HeightMeters(Math.Max(layout.FloorAt(r, c), 0), cell), 0f);
            var center = new Vector3((c + 1.5f) * cell, floor, (r + 1.5f) * cell);
            // Wand suchen: Nachbar, der nicht begehbar ist (bevorzugt massive Wand).
            (int Dr, int Dc)? wall = null;
            foreach (var (dr, dc) in new[] { (0, 1), (0, -1), (1, 0), (-1, 0) })
            {
                int nr = r + dr, nc = c + dc;
                bool outside = nr < 0 || nc < 0 || nr >= layout.Rows || nc >= layout.Cols;
                if (outside || !layout.IsWalkable(nr, nc))
                {
                    wall = (dr, dc);
                    if (outside || layout.FloorAt(nr, nc) < 0)
                        break;
                }
            }
            if (wall is { } w)
            {
                var normal = new Vector3(-w.Dc, 0, -w.Dr);          // zeigt vom Wandstück ins Feld
                var tangent = Vector3.Up.Cross(normal);
                var basis = new Basis(tangent, Vector3.Up, normal);
                var face = center - normal * (cell / 2f - 0.06f);
                doors.Add(new Transform3D(Local(basis, new Vector3(1.3f, 2.3f, 0.12f)), face + Vector3.Up * 1.15f));
                frames.Add(new Transform3D(Local(basis, new Vector3(1.6f, 0.18f, 0.18f)), face + Vector3.Up * 2.38f));
                frames.Add(new Transform3D(Local(basis, new Vector3(0.15f, 2.4f, 0.18f)), face + tangent * 0.72f + Vector3.Up * 1.2f));
                frames.Add(new Transform3D(Local(basis, new Vector3(0.15f, 2.4f, 0.18f)), face - tangent * 0.72f + Vector3.Up * 1.2f));
            }
            if (!open)
                return;
            marks.Add(new Transform3D(Basis.FromScale(new Vector3(1.4f, 0.03f, 1.4f)), center + Vector3.Up * 0.03f));
            if (target.Length > 0)
                root.AddChild(new Label3D
                {
                    Text = "→ " + target,
                    Position = center + Vector3.Up * 2.8f,
                    Modulate = new Color(1f, 0.85f, 0.5f),
                    FontSize = 28,
                    Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                });
        }

        foreach (var (r, c, target) in layout.Exits)
            Place(r, c, open: true, target);
        foreach (var (r, c) in layout.Locked)
            Place(r, c, open: false, "");
        foreach (var b in new[] { doors, frames, marks })
            if (b.Build() is { } node)
                root.AddChild(node);
    }

    // ---------------------------------------------------------------- Hilfen

    /// <summary>Skaliert entlang der lokalen Achsen der Basis (Spalten), nicht der Weltachsen.</summary>
    private static Basis Local(Basis b, Vector3 size) => new(b.Column0 * size.X, b.Column1 * size.Y, b.Column2 * size.Z);

    private static StandardMaterial3D Textured(string texture, float tileMeters, Color tint, bool vertexColor = false)
    {
        var m = new StandardMaterial3D { AlbedoColor = tint, Roughness = 0.9f, VertexColorUseAsAlbedo = vertexColor };
        string albedo = $"res://textures/{texture}_albedo.png";
        if (ResourceLoader.Exists(albedo))
        {
            m.AlbedoTexture = GD.Load<Texture2D>(albedo);
            m.NormalEnabled = true;
            m.NormalTexture = GD.Load<Texture2D>($"res://textures/{texture}_normal.png");
            m.Uv1Triplanar = true;
            m.Uv1WorldTriplanar = true;
            m.Uv1Scale = Vector3.One / tileMeters;
            m.TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic;
        }
        return m;
    }

    private static int Hash(int a, int b, int c)
    {
        unchecked
        {
            int h = a * 73856093 ^ b * 19349663 ^ c * 83492791;
            h ^= h >> 13;
            h *= 0x5bd1e995;
            return Math.Abs(h ^ (h >> 15));
        }
    }
}
