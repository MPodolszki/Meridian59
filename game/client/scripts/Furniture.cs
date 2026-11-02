using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Meridian.Client;

/// <summary>
/// Einrichtung der Räume aus dem Original (rooms/*.json "props", erzeugt von tools/add_furniture.py): Tische, Hocker,
/// Kohlebecken, Feuerstellen, Kerzen, Truhen, Lesepulte, Säulen, Laternen, Fässer, Urnen usw. als eigene einfache
/// Modelle; Feuer und Lichtpunkte (DynamicLight) leuchten. Position wie im KOD: Feld + Feinkoordinate (64 je Feld).
/// Dazu in Innenräumen eine Wandausstattung je Raumart (<see cref="Dress"/>): Das Original hat Regale, Fässer und
/// Kamine nur auf die Wandtexturen gemalt; hier stehen sie als Modelle an den Wänden (Abweichung, nur Optik).
/// </summary>
public static class Furniture
{
    private static readonly Color Wood = new(0.42f, 0.28f, 0.16f), DarkWood = new(0.28f, 0.18f, 0.10f);
    private static readonly Color Iron = new(0.22f, 0.22f, 0.24f), Stone = new(0.55f, 0.53f, 0.50f);
    private static readonly Color Clay = new(0.62f, 0.38f, 0.24f), Brass = new(0.70f, 0.55f, 0.25f);
    private static readonly Color Fire = new(1f, 0.55f, 0.18f), Straw = new(0.80f, 0.68f, 0.32f);
    private static readonly Dictionary<Color, StandardMaterial3D> Materials = new();

    /// <param name="wallHeight">Raumhöhe in Metern; 0 für Außenräume (dann keine Wandausstattung).</param>
    public static Node3D Build(RoomLayout layout, float cell, float wallHeight)
    {
        var root = new Node3D { Name = "Furniture" };
        if (wallHeight > 0)
            Dress(root, layout, cell, wallHeight);
        foreach (var p in layout.Props)
        {
            int r = p.Row - 1, c = p.Col - 1;
            float ground = layout.HeightMeters(Math.Max(layout.FloorAt(r, c), 0), cell);
            var node = Create(p.Type, p.Intensity);
            if (node is null)
                continue;
            node.Position = new Vector3((p.Col + p.FineCol / 64f) * cell, ground, (p.Row + p.FineRow / 64f) * cell);
            node.Rotation = new Vector3(0, -(p.Angle / 4096f * Mathf.Tau) - Mathf.Pi / 2, 0);
            root.AddChild(node);
        }
        return root;
    }

    private static Node3D? Create(string type, int intensity) => type switch
    {
        "Table" => Table(),
        "BarStool" => Stool(0.75f),
        "Stool" => Stool(0.45f),
        "Brazier" => Brazier(),
        "Firepit" => Firepit(),
        "Candle" => Candle(),
        "Candelabra" => Candelabra(),
        "Chest" => Chest(),
        "BookPedestal" => Pedestal(),
        "Pillar" => Pillar(),
        "Lamp" => StreetLamp(),
        "Throne" => Throne(),
        "Skull" or "OO_SKULL7" => Skull(),
        "Tombstone" => Tombstone(),
        "Fountain" => Fountain(),
        "Statue" => Statue(),
        "Light" => Light(intensity),
        "OO_BARREL" or "OO_BARREL2" => Barrel(),
        "OO_TALLURN" or "OO_URN" or "OO_DECORATIVE_POT" => Urn(1f),
        "OO_JUG" or "OO_NECBOTTLE" => Urn(0.4f),
        "OO_FLASK_GREEN" or "OO_POTION" or "OO_FLASK_STAND" => Flasks(),
        "OO_BAG_OF_BEANS" => Sack(),
        "OO_BUCKET" or "OO_BUCkET" => Bucket(),
        "OO_HAY" => Hay(),
        "OO_CORN" or "OO_LETTUCE" => Crop(type == "OO_CORN"),
        "OO_ROCK1" or "OO_ROCKB" or "OO_ROCKC" => Rock(),
        "OO_STONECHAIR" => StoneChair(),
        "OO_FIXINS" or "OO_MORTARANDPESTLE" => Bowl(),
        "OO_DUNG" or "OO_NESTOBJ1" or "OO_NESTOBJ2" => Lump(type == "OO_DUNG" ? new Color(0.30f, 0.22f, 0.12f) : Straw),
        _ when type.StartsWith("OO_MUSEUM") => MuseumExhibit(type),
        _ when type.StartsWith("OO_") => Crate(),
        _ => null,
    };

    // ------------------------------------------------------------------ Wandausstattung

    private enum RoomKind { Tavern, Smithy, Shop, Bank, Hall, Abandoned, Crypt, Other }

    /// <summary>Raumart aus dem Namen der Raumdatei (Gasthäuser, Schmieden, Läden, Banken, Hallen, Grüfte).</summary>
    private static RoomKind KindOf(string source) => source switch
    {
        "guest1" or "guest7" or "barinn" or "tostavern" => RoomKind.Tavern,
        "guest3" => RoomKind.Smithy,
        "guest6" => RoomKind.Crypt,
        _ when source.EndsWith("inn") || source.Contains("tavrn") || source.Contains("lbar") => RoomKind.Tavern,
        _ when source.Contains("smith") || source == "weaponsm" => RoomKind.Smithy,
        _ when source.Contains("bank") || source.Contains("vault") => RoomKind.Bank,
        _ when source.StartsWith("jasab") => RoomKind.Abandoned,
        _ when source.Contains("crypt") || source.Contains("dun") || source.Contains("cellar") || source == "res00001" => RoomKind.Crypt,
        _ when source.Contains("hall") || source.Contains("elder") || source.Contains("court") || source.Contains("univ")
            || source.Contains("heal") || source == "kochoh" || source == "guest8" => RoomKind.Hall,
        _ when source.Contains("apoth") || source.Contains("merch") || source.Contains("groc") || source.Contains("store")
            || source.Contains("tan") || source.Contains("tail") || source == "guest4" => RoomKind.Shop,
        _ => RoomKind.Other,
    };

    /// <summary>
    /// Stellt Regale, Fässer, Kisten usw. an die Wände: jedes Wandstück eines begehbaren Felds ist ein möglicher
    /// Platz; ein fester Zufall (Feld) wählt etwa drei von vier. Frei bleiben Felder an Ausgängen und mit Möbeln
    /// aus dem Original. Gasthäuser bekommen einen Kamin, Gasthäuser/Hallen/Banken einen Teppich in der Mitte.
    /// </summary>
    private static void Dress(Node3D root, RoomLayout layout, float cell, float wallHeight)
    {
        var kind = KindOf(layout.Source);
        var blocked = new HashSet<(int, int)>();
        foreach (var (er, ec, _) in layout.Exits)
            for (int dr = -1; dr <= 1; dr++)
                for (int dc = -1; dc <= 1; dc++)
                    blocked.Add((er + dr, ec + dc));
        foreach (var p in layout.Props)
            blocked.Add((p.Row - 1, p.Col - 1));

        var slots = new List<(int R, int C, int Dr, int Dc, float Floor)>();
        (int, int)[] dirs = [(1, 0), (-1, 0), (0, 1), (0, -1)];
        int minR = int.MaxValue, maxR = -1, minC = int.MaxValue, maxC = -1;
        for (int r = 0; r < layout.Rows; r++)
            for (int c = 0; c < layout.Cols; c++)
            {
                if (!layout.IsWalkable(r, c))
                    continue;
                (minR, maxR, minC, maxC) = (Math.Min(minR, r), Math.Max(maxR, r), Math.Min(minC, c), Math.Max(maxC, c));
                if (blocked.Contains((r, c)))
                    continue;
                float floor = Mathf.Max(layout.HeightMeters(layout.FloorAt(r, c), cell), 0f);
                foreach (var (dr, dc) in dirs)
                {
                    int nf = layout.FloorAt(r + dr, c + dc);
                    if (nf < 0 || layout.HeightMeters(nf, cell) - floor > 2f)
                        slots.Add((r, c, dr, dc, floor));
                }
            }
        if (slots.Count == 0)
            return;

        var used = new HashSet<(int, int, int, int)>();
        bool fireplace = kind != RoomKind.Tavern;
        // Kamin an die Stelle mit dem kleinsten Zufallswert, damit er in jedem Gasthaus genau einmal steht.
        if (!fireplace)
        {
            var s = slots.OrderBy(x => Hash(x.R, x.C, x.Dr * 3 + x.Dc)).First();
            Place(root, Fireplace(), s, cell);
            used.Add((s.R, s.C, s.Dr, s.Dc));
        }
        foreach (var s in slots)
        {
            int h = Hash(s.R, s.C, s.Dr * 3 + s.Dc);
            if (used.Contains((s.R, s.C, s.Dr, s.Dc)) || h % 100 >= (kind == RoomKind.Crypt ? 30 : 75))
                continue;
            used.Add((s.R, s.C, s.Dr, s.Dc));
            Place(root, WallItem(kind, h / 100, wallHeight), s, cell);
        }

        if (kind is RoomKind.Tavern or RoomKind.Hall or RoomKind.Bank && maxR > minR + 2 && maxC > minC + 2)
        {
            float cr = (minR + maxR) / 2f, cc = (minC + maxC) / 2f;
            var rug = Box(root, new Vector3(Math.Min(4f, (maxC - minC) * cell * 0.4f), 0.02f, Math.Min(3f, (maxR - minR) * cell * 0.4f)),
                new Vector3((cc + 1.5f) * cell, Mathf.Max(layout.HeightMeters(layout.FloorAt((int)cr, (int)cc), cell), 0f) + 0.01f, (cr + 1.5f) * cell),
                kind == RoomKind.Bank ? new Color(0.18f, 0.22f, 0.45f) : new Color(0.50f, 0.12f, 0.10f));
            rug.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            var inner = Box(root, ((BoxMesh)rug.Mesh).Size * new Vector3(0.8f, 1f, 0.75f) + new Vector3(0, 0.005f, 0), rug.Position,
                kind == RoomKind.Bank ? new Color(0.55f, 0.48f, 0.25f) : new Color(0.62f, 0.45f, 0.22f));
            inner.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            var core = Box(root, ((BoxMesh)rug.Mesh).Size * new Vector3(0.7f, 1f, 0.6f) + new Vector3(0, 0.01f, 0), rug.Position, ((StandardMaterial3D)rug.MaterialOverride).AlbedoColor);
            core.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        }
    }

    /// <summary>Stellt ein Teil mit dem Rücken (lokal −Z) an die Wand des Felds.</summary>
    private static void Place(Node3D root, Node3D item, (int R, int C, int Dr, int Dc, float Floor) s, float cell)
    {
        var center = new Vector3((s.C + 1.5f) * cell, s.Floor, (s.R + 1.5f) * cell);
        item.Position = center + new Vector3(s.Dc, 0, s.Dr) * (cell / 2f - 0.35f);
        item.Rotation = new Vector3(0, Mathf.Atan2(-s.Dc, -s.Dr), 0);
        root.AddChild(item);
    }

    private static Node3D WallItem(RoomKind kind, int pick, float wallHeight)
    {
        Func<Node3D>[] choices = kind switch
        {
            RoomKind.Tavern => [() => Barrel(), BarrelStack, () => Shelf(Bottles), () => Shelf(Bottles), () => Crate()],
            RoomKind.Smithy => [Anvil, WeaponRack, WeaponRack, () => Barrel(), () => Crate(), () => Shelf(Tools)],
            RoomKind.Shop => [() => Shelf(Goods), () => Shelf(Goods), () => Shelf(Bottles), () => Crate(), () => Sack(), CrateStack],
            RoomKind.Bank => [() => Chest(), () => Shelf(Books), () => Banner(wallHeight)],
            RoomKind.Hall => [() => Shelf(Books), () => Banner(wallHeight), () => Banner(wallHeight), () => Chest()],
            RoomKind.Abandoned => [BrokenCrate, () => Crate(), FallenBarrel, () => Lump(new Color(0.35f, 0.33f, 0.30f), 0.35f)],
            RoomKind.Crypt => [() => Urn(1f), () => Skull(), () => Urn(0.6f), Bones],
            _ => [() => Shelf(Goods), () => Crate(), () => Barrel(), () => Shelf(Books)],
        };
        return choices[pick % choices.Length]();
    }

    private static int Hash(int a, int b, int c)
    {
        unchecked
        {
            uint h = (uint)(a * 73856093 ^ b * 19349663 ^ c * 83492791);
            h ^= h >> 13;
            h *= 0x5bd1e995;
            h ^= h >> 15;
            return (int)(h & 0x7fffffff);
        }
    }

    private static readonly Color[] Bottles = [new(0.25f, 0.45f, 0.22f), new(0.45f, 0.25f, 0.12f), new(0.55f, 0.15f, 0.15f), new(0.6f, 0.55f, 0.3f)];
    private static readonly Color[] Goods = [new(0.62f, 0.52f, 0.36f), new(0.45f, 0.30f, 0.18f), new(0.55f, 0.45f, 0.30f), new(0.35f, 0.40f, 0.30f)];
    private static readonly Color[] Books = [new(0.45f, 0.12f, 0.10f), new(0.15f, 0.22f, 0.40f), new(0.20f, 0.35f, 0.18f), new(0.40f, 0.30f, 0.15f)];
    private static readonly Color[] Tools = [Iron, new(0.35f, 0.33f, 0.32f), DarkWood];

    /// <summary>Wandregal mit drei Böden; darauf Flaschen, Waren oder Bücher in den angegebenen Farben.</summary>
    private static Node3D Shelf(Color[] colors)
    {
        var n = new Node3D { Name = "Shelf" };
        float w = 1.6f, d = 0.4f, h = 2.0f;
        Box(n, new Vector3(w, h, 0.04f), new Vector3(0, h / 2, -d / 2), DarkWood);
        foreach (float x in new[] { -w / 2, w / 2 })
            Box(n, new Vector3(0.05f, h, d), new Vector3(x, h / 2, 0), Wood);
        bool books = colors == Books;
        int k = 0;
        foreach (float y in new[] { 0.1f, 0.75f, 1.4f })
        {
            Box(n, new Vector3(w, 0.04f, d), new Vector3(0, y, 0), Wood);
            for (float x = -w / 2 + 0.15f; x < w / 2 - 0.1f; x += books ? 0.09f : 0.24f)
            {
                var col = colors[(k++ * 7 + (int)(y * 10)) % colors.Length];
                if (books)
                    Box(n, new Vector3(0.07f, 0.32f + k % 3 * 0.04f, 0.26f), new Vector3(x, y + 0.18f + k % 3 * 0.02f, 0), col);
                else if (colors == Bottles)
                {
                    Cyl(n, 0.05f, 0.05f, 0.2f, new Vector3(x, y + 0.12f, 0), col);
                    Cyl(n, 0.02f, 0.02f, 0.08f, new Vector3(x, y + 0.26f, 0), col);
                }
                else if (k % 2 == 0)
                    Box(n, new Vector3(0.18f, 0.2f, 0.25f), new Vector3(x, y + 0.12f, 0), col);
                else
                    Sphere(n, 0.1f, new Vector3(x, y + 0.12f, 0), col);
            }
        }
        return n;
    }

    private static Node3D BarrelStack()
    {
        var n = new Node3D { Name = "Barrels" };
        foreach (float x in new[] { -0.35f, 0.35f })
        {
            var b = Barrel();
            b.Position = new Vector3(x, 0, 0);
            n.AddChild(b);
        }
        var top = FallenBarrel();
        top.Position = new Vector3(0, 0.92f, 0);
        n.AddChild(top);
        return n;
    }

    private static Node3D FallenBarrel()
    {
        var n = new Node3D { Name = "FallenBarrel" };
        var b = Barrel();
        b.RotationDegrees = new Vector3(0, 0, 90);
        b.Position = new Vector3(0.45f, 0.32f, 0);
        n.AddChild(b);
        return n;
    }

    private static Node3D CrateStack()
    {
        var n = new Node3D { Name = "Crates" };
        foreach (var at in new[] { new Vector3(-0.3f, 0, 0), new Vector3(0.3f, 0, 0.05f), new Vector3(0, 0.55f, 0) })
        {
            var c = Crate();
            c.Position = at;
            c.RotationDegrees = new Vector3(0, at.X * 30, 0);
            n.AddChild(c);
        }
        return n;
    }

    private static Node3D BrokenCrate()
    {
        var n = new Node3D { Name = "BrokenCrate" };
        for (int i = 0; i < 4; i++)
        {
            var plank = Box(n, new Vector3(0.7f, 0.04f, 0.14f), new Vector3(-0.3f + i * 0.2f, 0.03f + i % 2 * 0.04f, 0.1f * i - 0.15f), DarkWood);
            plank.RotationDegrees = new Vector3(0, i * 37, i % 2 * 8);
        }
        return n;
    }

    private static Node3D Bones()
    {
        var n = new Node3D { Name = "Bones" };
        var bone = new Color(0.86f, 0.83f, 0.74f);
        for (int i = 0; i < 4; i++)
            Cyl(n, 0.025f, 0.025f, 0.45f, new Vector3(-0.2f + i * 0.12f, 0.03f, 0.05f * i), bone).RotationDegrees = new Vector3(90, i * 50, 0);
        Sphere(n, 0.1f, new Vector3(0.25f, 0.1f, -0.1f), bone);
        return n;
    }

    private static Node3D Anvil()
    {
        var n = new Node3D { Name = "Anvil" };
        Cyl(n, 0.25f, 0.3f, 0.5f, new Vector3(0, 0.25f, 0.3f), DarkWood);
        Box(n, new Vector3(0.2f, 0.2f, 0.25f), new Vector3(0, 0.6f, 0.3f), Iron);
        Box(n, new Vector3(0.6f, 0.12f, 0.25f), new Vector3(0.05f, 0.76f, 0.3f), Iron);
        Cyl(n, 0.0f, 0.06f, 0.25f, new Vector3(-0.35f, 0.76f, 0.3f), Iron).RotationDegrees = new Vector3(0, 0, 90);
        return n;
    }

    private static Node3D WeaponRack()
    {
        var n = new Node3D { Name = "WeaponRack" };
        Box(n, new Vector3(1.4f, 0.08f, 0.3f), new Vector3(0, 0.3f, 0), Wood);
        Box(n, new Vector3(1.4f, 0.08f, 0.08f), new Vector3(0, 1.3f, -0.1f), Wood);
        foreach (float x in new[] { -0.6f, 0.6f })
            Box(n, new Vector3(0.08f, 1.4f, 0.08f), new Vector3(x, 0.7f, -0.1f), DarkWood);
        for (int i = 0; i < 4; i++)
        {
            float x = -0.42f + i * 0.28f;
            Box(n, new Vector3(0.05f, 1.0f, 0.015f), new Vector3(x, 0.85f, 0), new Color(0.7f, 0.7f, 0.72f));
            Box(n, new Vector3(0.18f, 0.03f, 0.04f), new Vector3(x, 1.38f, 0), Brass);
        }
        return n;
    }

    private static Node3D Banner(float wallHeight)
    {
        var n = new Node3D { Name = "Banner" };
        float top = Math.Min(wallHeight - 0.4f, 3.2f);
        Cyl(n, 0.025f, 0.025f, 1.0f, new Vector3(0, top, -0.1f), Brass).RotationDegrees = new Vector3(0, 0, 90);
        Box(n, new Vector3(0.8f, 1.6f, 0.02f), new Vector3(0, top - 0.82f, -0.1f), new Color(0.45f, 0.10f, 0.12f));
        Box(n, new Vector3(0.3f, 0.3f, 0.025f), new Vector3(0, top - 0.7f, -0.09f), Brass).RotationDegrees = new Vector3(0, 0, 45);
        return n;
    }

    /// <summary>Gemauerter Kamin mit Feuer und warmem Licht.</summary>
    private static Node3D Fireplace()
    {
        var n = new Node3D { Name = "Fireplace" };
        var stone = new Color(0.42f, 0.40f, 0.38f);
        foreach (float x in new[] { -0.75f, 0.75f })
            Box(n, new Vector3(0.3f, 1.2f, 0.6f), new Vector3(x, 0.6f, -0.05f), stone);
        Box(n, new Vector3(1.8f, 0.35f, 0.7f), new Vector3(0, 1.35f, -0.05f), stone);
        Box(n, new Vector3(1.2f, 1.2f, 0.1f), new Vector3(0, 0.6f, -0.3f), new Color(0.08f, 0.07f, 0.06f));
        Box(n, new Vector3(1.0f, 2.5f, 0.5f), new Vector3(0, 2.75f, -0.1f), stone);
        foreach (float a in new[] { 20f, -20f })
            Cyl(n, 0.06f, 0.06f, 0.8f, new Vector3(0, 0.1f, -0.05f), DarkWood).RotationDegrees = new Vector3(90, a, 0);
        Flame(n, new Vector3(0, 0.3f, -0.05f), 0.22f);
        Glow(n, new Vector3(0, 0.8f, 0.6f), 2.0f, 9f);
        return n;
    }

    // ------------------------------------------------------------------ Möbel

    private static Node3D Table()
    {
        var n = new Node3D { Name = "Table" };
        Box(n, new Vector3(1.5f, 0.08f, 0.9f), new Vector3(0, 0.76f, 0), Wood);
        foreach (var (x, z) in new[] { (-0.65f, -0.35f), (0.65f, -0.35f), (-0.65f, 0.35f), (0.65f, 0.35f) })
            Box(n, new Vector3(0.08f, 0.72f, 0.08f), new Vector3(x, 0.36f, z), DarkWood);
        return n;
    }

    private static Node3D Stool(float height)
    {
        var n = new Node3D { Name = "Stool" };
        Cyl(n, 0.2f, 0.2f, 0.06f, new Vector3(0, height, 0), Wood);
        foreach (var (x, z) in new[] { (-0.12f, -0.12f), (0.12f, -0.12f), (0, 0.15f) })
            Cyl(n, 0.025f, 0.03f, height, new Vector3(x, height / 2, z), DarkWood);
        return n;
    }

    private static Node3D Chest()
    {
        var n = new Node3D { Name = "Chest" };
        Box(n, new Vector3(0.9f, 0.5f, 0.55f), new Vector3(0, 0.25f, 0), Wood);
        Box(n, new Vector3(0.92f, 0.12f, 0.57f), new Vector3(0, 0.56f, 0), DarkWood);
        Box(n, new Vector3(0.95f, 0.06f, 0.6f), new Vector3(0, 0.36f, 0), Iron);
        return n;
    }

    private static Node3D Pedestal()
    {
        var n = new Node3D { Name = "Pedestal" };
        Box(n, new Vector3(0.5f, 0.1f, 0.5f), new Vector3(0, 0.05f, 0), DarkWood);
        Box(n, new Vector3(0.18f, 1.0f, 0.18f), new Vector3(0, 0.55f, 0), Wood);
        var top = Box(n, new Vector3(0.6f, 0.06f, 0.45f), new Vector3(0, 1.08f, 0), Wood);
        top.RotationDegrees = new Vector3(-20, 0, 0);
        var book = Box(n, new Vector3(0.45f, 0.06f, 0.32f), new Vector3(0, 1.14f, 0), new Color(0.45f, 0.12f, 0.10f));
        book.RotationDegrees = new Vector3(-20, 0, 0);
        return n;
    }

    private static Node3D Throne()
    {
        var n = new Node3D { Name = "Throne" };
        Box(n, new Vector3(1f, 0.55f, 0.9f), new Vector3(0, 0.28f, 0), DarkWood);
        Box(n, new Vector3(1f, 1.6f, 0.15f), new Vector3(0, 1.1f, 0.4f), DarkWood);
        Box(n, new Vector3(0.85f, 0.08f, 0.75f), new Vector3(0, 0.6f, -0.02f), new Color(0.5f, 0.1f, 0.1f));
        return n;
    }

    private static Node3D Pillar()
    {
        var n = new Node3D { Name = "Pillar" };
        Box(n, new Vector3(0.8f, 0.25f, 0.8f), new Vector3(0, 0.12f, 0), Stone);
        Cyl(n, 0.3f, 0.33f, 4f, new Vector3(0, 2.2f, 0), Stone);
        Box(n, new Vector3(0.8f, 0.25f, 0.8f), new Vector3(0, 4.3f, 0), Stone);
        return n;
    }

    private static Node3D StoneChair()
    {
        var n = new Node3D { Name = "StoneChair" };
        Box(n, new Vector3(0.7f, 0.45f, 0.6f), new Vector3(0, 0.22f, 0), Stone);
        Box(n, new Vector3(0.7f, 0.8f, 0.15f), new Vector3(0, 0.8f, 0.25f), Stone);
        return n;
    }

    private static Node3D Tombstone()
    {
        var n = new Node3D { Name = "Tombstone" };
        Box(n, new Vector3(0.7f, 1.0f, 0.18f), new Vector3(0, 0.5f, 0), new Color(0.48f, 0.48f, 0.47f));
        Cyl(n, 0.35f, 0.35f, 0.18f, new Vector3(0, 1.0f, 0), new Color(0.48f, 0.48f, 0.47f)).RotationDegrees = new Vector3(90, 0, 0);
        return n;
    }

    private static Node3D Statue()
    {
        var n = new Node3D { Name = "Statue" };
        Box(n, new Vector3(0.9f, 0.6f, 0.9f), new Vector3(0, 0.3f, 0), Stone);
        Cyl(n, 0.22f, 0.3f, 1.5f, new Vector3(0, 1.35f, 0), Stone);
        Sphere(n, 0.2f, new Vector3(0, 2.25f, 0), Stone);
        return n;
    }

    private static Node3D Fountain()
    {
        var n = new Node3D { Name = "Fountain" };
        Cyl(n, 1.6f, 1.7f, 0.6f, new Vector3(0, 0.3f, 0), Stone);
        var water = Cyl(n, 1.45f, 1.45f, 0.05f, new Vector3(0, 0.55f, 0), new Color(0.25f, 0.42f, 0.55f, 0.8f));
        ((StandardMaterial3D)water.MaterialOverride).Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        ((StandardMaterial3D)water.MaterialOverride).Roughness = 0.05f;
        Cyl(n, 0.2f, 0.3f, 1.4f, new Vector3(0, 1.0f, 0), Stone);
        Cyl(n, 0.6f, 0.4f, 0.2f, new Vector3(0, 1.7f, 0), Stone);
        return n;
    }

    // ------------------------------------------------------------------ Licht und Feuer

    private static Node3D Brazier()
    {
        var n = new Node3D { Name = "Brazier" };
        foreach (float a in new[] { 0f, 120f, 240f })
        {
            var leg = Cyl(n, 0.025f, 0.03f, 0.9f, new Vector3(Mathf.Cos(Mathf.DegToRad(a)) * 0.18f, 0.42f, Mathf.Sin(Mathf.DegToRad(a)) * 0.18f), Iron);
            leg.RotationDegrees = new Vector3(Mathf.Sin(Mathf.DegToRad(a)) * 12, 0, -Mathf.Cos(Mathf.DegToRad(a)) * 12);
        }
        Cyl(n, 0.32f, 0.18f, 0.22f, new Vector3(0, 0.95f, 0), Iron);
        Flame(n, new Vector3(0, 1.12f, 0), 0.22f);
        Glow(n, new Vector3(0, 1.4f, 0), 1.6f, 7f);
        return n;
    }

    private static Node3D Firepit()
    {
        var n = new Node3D { Name = "Firepit" };
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.Tau / 8;
            Sphere(n, 0.16f, new Vector3(Mathf.Cos(a) * 0.5f, 0.1f, Mathf.Sin(a) * 0.5f), Stone);
        }
        foreach (float a in new[] { 0f, 60f, 120f })
            Cyl(n, 0.05f, 0.05f, 0.7f, new Vector3(0, 0.12f, 0), DarkWood).RotationDegrees = new Vector3(90, a, 0);
        Flame(n, new Vector3(0, 0.3f, 0), 0.3f);
        Glow(n, new Vector3(0, 0.8f, 0), 1.8f, 8f);
        return n;
    }

    private static Node3D Candle()
    {
        var n = new Node3D { Name = "Candle" };
        Cyl(n, 0.035f, 0.035f, 0.22f, new Vector3(0, 0.11f, 0), new Color(0.92f, 0.88f, 0.75f));
        Flame(n, new Vector3(0, 0.26f, 0), 0.04f);
        Glow(n, new Vector3(0, 0.4f, 0), 0.5f, 3.5f);
        return n;
    }

    private static Node3D Candelabra()
    {
        var n = new Node3D { Name = "Candelabra" };
        Cyl(n, 0.2f, 0.25f, 0.06f, new Vector3(0, 0.03f, 0), Brass);
        Cyl(n, 0.03f, 0.04f, 1.4f, new Vector3(0, 0.72f, 0), Brass);
        Box(n, new Vector3(0.6f, 0.04f, 0.04f), new Vector3(0, 1.4f, 0), Brass);
        foreach (float x in new[] { -0.28f, 0f, 0.28f })
        {
            Cyl(n, 0.03f, 0.03f, 0.18f, new Vector3(x, 1.5f, 0), new Color(0.92f, 0.88f, 0.75f));
            Flame(n, new Vector3(x, 1.62f, 0), 0.035f);
        }
        Glow(n, new Vector3(0, 1.7f, 0), 0.9f, 5f);
        return n;
    }

    private static Node3D StreetLamp()
    {
        var n = new Node3D { Name = "Lamp" };
        Cyl(n, 0.06f, 0.09f, 2.6f, new Vector3(0, 1.3f, 0), Iron);
        Box(n, new Vector3(0.3f, 0.4f, 0.3f), new Vector3(0, 2.75f, 0), Iron);
        var glass = Box(n, new Vector3(0.24f, 0.32f, 0.24f), new Vector3(0, 2.75f, 0), new Color(1f, 0.85f, 0.55f));
        Emit(glass, new Color(1f, 0.8f, 0.45f), 2f);
        Glow(n, new Vector3(0, 2.6f, 0), 1.2f, 8f);
        return n;
    }

    /// <summary>DynamicLight des Originals: nur Licht (Stärke 30 ≈ eine Fackel).</summary>
    private static Node3D Light(int intensity)
    {
        var n = new Node3D { Name = "Light" };
        Glow(n, new Vector3(0, 2.0f, 0), 0.6f + intensity / 30f, 4f + intensity / 6f);
        return n;
    }

    // ------------------------------------------------------------------ Kleinkram

    private static Node3D Barrel()
    {
        var n = new Node3D { Name = "Barrel" };
        Cyl(n, 0.32f, 0.32f, 0.9f, new Vector3(0, 0.45f, 0), Wood);
        Cyl(n, 0.34f, 0.34f, 0.05f, new Vector3(0, 0.2f, 0), Iron);
        Cyl(n, 0.34f, 0.34f, 0.05f, new Vector3(0, 0.7f, 0), Iron);
        return n;
    }

    private static Node3D Urn(float scale)
    {
        var n = new Node3D { Name = "Urn" };
        Sphere(n, 0.3f * scale, new Vector3(0, 0.35f * scale, 0), Clay);
        Cyl(n, 0.12f * scale, 0.18f * scale, 0.3f * scale, new Vector3(0, 0.7f * scale, 0), Clay);
        return n;
    }

    private static Node3D Flasks()
    {
        var n = new Node3D { Name = "Flasks" };
        foreach (var (x, z, col) in new[] { (-0.1f, 0f, new Color(0.2f, 0.6f, 0.25f)), (0.1f, 0.05f, new Color(0.25f, 0.35f, 0.7f)) })
        {
            Sphere(n, 0.08f, new Vector3(x, 0.08f, z), col);
            Cyl(n, 0.025f, 0.025f, 0.1f, new Vector3(x, 0.19f, z), col);
        }
        return n;
    }

    private static Node3D Sack()
    {
        var n = new Node3D { Name = "Sack" };
        Sphere(n, 0.3f, new Vector3(0, 0.28f, 0), new Color(0.6f, 0.52f, 0.36f)).Scale = new Vector3(1, 1.2f, 0.9f);
        return n;
    }

    private static Node3D Bucket()
    {
        var n = new Node3D { Name = "Bucket" };
        Cyl(n, 0.2f, 0.16f, 0.32f, new Vector3(0, 0.16f, 0), Wood);
        return n;
    }

    private static Node3D Hay() => Lump(Straw, 0.7f);

    private static Node3D Lump(Color color, float size = 0.25f)
    {
        var n = new Node3D { Name = "Lump" };
        Sphere(n, size, new Vector3(0, size * 0.4f, 0), color).Scale = new Vector3(1.3f, 0.6f, 1.1f);
        return n;
    }

    private static Node3D Crop(bool corn)
    {
        var n = new Node3D { Name = "Crop" };
        var green = new Color(0.3f, 0.55f, 0.2f);
        if (corn)
            foreach (var (x, z) in new[] { (-0.3f, -0.2f), (0.2f, 0.25f), (0.25f, -0.3f), (-0.2f, 0.3f) })
                Cyl(n, 0.03f, 0.05f, 1.6f, new Vector3(x, 0.8f, z), green);
        else
            Sphere(n, 0.22f, new Vector3(0, 0.15f, 0), green);
        return n;
    }

    private static Node3D Rock()
    {
        var n = new Node3D { Name = "Rock" };
        Sphere(n, 0.45f, new Vector3(0, 0.2f, 0), new Color(0.45f, 0.44f, 0.42f)).Scale = new Vector3(1.2f, 0.7f, 1f);
        return n;
    }

    private static Node3D Skull()
    {
        var n = new Node3D { Name = "Skull" };
        Sphere(n, 0.1f, new Vector3(0, 0.1f, 0), new Color(0.86f, 0.83f, 0.74f));
        return n;
    }

    private static Node3D Bowl()
    {
        var n = new Node3D { Name = "Bowl" };
        Cyl(n, 0.15f, 0.1f, 0.1f, new Vector3(0, 0.05f, 0), Clay);
        return n;
    }

    private static Node3D Crate()
    {
        var n = new Node3D { Name = "Crate" };
        Box(n, new Vector3(0.55f, 0.55f, 0.55f), new Vector3(0, 0.28f, 0), Wood);
        return n;
    }

    /// <summary>Museumsstück: Sockel mit kleiner Figur (die Exponate des Originals stellen Miniaturen dar).</summary>
    private static Node3D MuseumExhibit(string type)
    {
        var n = new Node3D { Name = "Exhibit" };
        Box(n, new Vector3(0.6f, 0.9f, 0.6f), new Vector3(0, 0.45f, 0), new Color(0.35f, 0.3f, 0.28f));
        var hue = (uint)type.GetHashCode() % 360 / 360f;
        Sphere(n, 0.18f, new Vector3(0, 1.08f, 0), Color.FromHsv(hue, 0.45f, 0.75f)).Scale = new Vector3(1, 1.4f, 1);
        return n;
    }

    // ------------------------------------------------------------------ Bausteine

    private static void Flame(Node3D parent, Vector3 at, float size)
    {
        var flame = Sphere(parent, size, at, Fire);
        flame.Scale = new Vector3(1, 1.8f, 1);
        Emit(flame, Fire, 4f);
        flame.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
    }

    private static void Glow(Node3D parent, Vector3 at, float energy, float range) =>
        parent.AddChild(new OmniLight3D { Position = at, LightColor = new Color(1f, 0.72f, 0.42f), LightEnergy = energy, OmniRange = range });

    private static void Emit(MeshInstance3D node, Color color, float energy)
    {
        var m = (StandardMaterial3D)node.MaterialOverride;
        m.EmissionEnabled = true;
        m.Emission = color;
        m.EmissionEnergyMultiplier = energy;
    }

    private static MeshInstance3D Box(Node3D parent, Vector3 size, Vector3 at, Color color) =>
        Add(parent, new BoxMesh { Size = size }, at, color);

    private static MeshInstance3D Cyl(Node3D parent, float top, float bottom, float height, Vector3 at, Color color) =>
        Add(parent, new CylinderMesh { TopRadius = top, BottomRadius = bottom, Height = height, RadialSegments = 12 }, at, color);

    private static MeshInstance3D Sphere(Node3D parent, float radius, Vector3 at, Color color) =>
        Add(parent, new SphereMesh { Radius = radius, Height = radius * 2, RadialSegments = 12, Rings = 6 }, at, color);

    private static MeshInstance3D Add(Node3D parent, Mesh mesh, Vector3 at, Color color)
    {
        // Eigenes Material je Objekt nur für Leuchtendes/Durchsichtiges; sonst geteilt je Farbe.
        var node = new MeshInstance3D { Mesh = mesh, Position = at, MaterialOverride = new StandardMaterial3D { AlbedoColor = color, Roughness = 0.85f } };
        parent.AddChild(node);
        return node;
    }
}
