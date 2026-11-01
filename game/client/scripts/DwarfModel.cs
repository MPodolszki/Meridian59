using Godot;

namespace Meridian.Client;

/// <summary>
/// Platzhalter-Spielfigur „Zwerg“ aus Grundformen: klein (ca. 1,35 m), breit und gedrungen,
/// großer Kopf mit Vollbart, einfache Startkleidung (Hemd, Hose, Stiefel), keine Rüstung.
/// Wird ersetzt, sobald eigene Modelle (z. B. aus Blender) vorliegen. Blickrichtung: -Z.
/// </summary>
public static class DwarfModel
{
    private static readonly Color Skin = new(0.87f, 0.68f, 0.55f);
    private static readonly Color Hair = new(0.55f, 0.27f, 0.12f);   // rotbraun
    private static readonly Color Shirt = new(0.45f, 0.38f, 0.28f);  // ungefärbtes Leinen
    private static readonly Color Trousers = new(0.30f, 0.25f, 0.20f);
    private static readonly Color Boots = new(0.22f, 0.15f, 0.10f);
    private static readonly Color Belt = new(0.35f, 0.22f, 0.12f);
    private static readonly Color Eyes = new(0.08f, 0.06f, 0.05f);

    public static Node3D Create()
    {
        var root = new Node3D { Name = "Dwarf" };

        // Beine: kurz und stämmig
        foreach (float x in new[] { -0.14f, 0.14f })
        {
            Add(root, new CylinderMesh { TopRadius = 0.11f, BottomRadius = 0.12f, Height = 0.38f }, Trousers, new Vector3(x, 0.27f, 0));
            Add(root, new BoxMesh { Size = new Vector3(0.2f, 0.12f, 0.3f) }, Boots, new Vector3(x, 0.06f, -0.04f));
        }

        // Rumpf: breit, leicht tonnenförmig
        Add(root, new CapsuleMesh { Radius = 0.3f, Height = 0.75f }, Shirt, new Vector3(0, 0.78f, 0), scale: new Vector3(1.15f, 1f, 0.85f));
        Add(root, new CylinderMesh { TopRadius = 0.33f, BottomRadius = 0.33f, Height = 0.07f }, Belt, new Vector3(0, 0.56f, 0), scale: new Vector3(1.05f, 1f, 0.8f));

        // Arme: kräftig, hängen leicht schräg
        foreach (float x in new[] { -1f, 1f })
        {
            var arm = Add(root, new CapsuleMesh { Radius = 0.09f, Height = 0.55f }, Shirt, new Vector3(x * 0.42f, 0.82f, 0));
            arm.RotationDegrees = new Vector3(0, 0, x * 12f);
            Add(root, new SphereMesh { Radius = 0.08f, Height = 0.16f }, Skin, new Vector3(x * 0.47f, 0.54f, 0));
        }

        // Kopf: verhältnismäßig groß
        Add(root, new SphereMesh { Radius = 0.18f, Height = 0.36f }, Skin, new Vector3(0, 1.2f, 0));
        Add(root, new SphereMesh { Radius = 0.045f, Height = 0.09f }, Skin, new Vector3(0, 1.19f, -0.185f));   // Knollennase
        foreach (float x in new[] { -0.065f, 0.065f })
        {
            Add(root, new SphereMesh { Radius = 0.022f, Height = 0.044f }, Eyes, new Vector3(x, 1.235f, -0.16f));
            Add(root, new BoxMesh { Size = new Vector3(0.08f, 0.03f, 0.04f) }, Hair, new Vector3(x, 1.27f, -0.16f));   // buschige Brauen
        }
        // Haare: Oberkopf und Hinterkopf, Stirn bleibt frei
        Add(root, new SphereMesh { Radius = 0.19f, Height = 0.38f }, Hair, new Vector3(0, 1.27f, 0.05f), scale: new Vector3(1.02f, 0.85f, 0.95f));
        // Vollbart: vom Kinn bis auf den Bauch, breit an den Wangen
        var beard = Add(root, new CylinderMesh { TopRadius = 0.17f, BottomRadius = 0.06f, Height = 0.52f }, Hair, new Vector3(0, 0.9f, -0.2f));
        beard.RotationDegrees = new Vector3(-10f, 0, 0);
        beard.Scale = new Vector3(1.05f, 1f, 0.45f);
        Add(root, new SphereMesh { Radius = 0.06f, Height = 0.12f }, Hair, new Vector3(0, 1.11f, -0.2f), scale: new Vector3(2.2f, 1f, 0.8f));   // Schnurrbart

        return root;
    }

    private static MeshInstance3D Add(Node3D parent, Mesh mesh, Color color, Vector3 position, Vector3? scale = null)
    {
        var node = new MeshInstance3D
        {
            Mesh = mesh,
            Position = position,
            Scale = scale ?? Vector3.One,
            MaterialOverride = new StandardMaterial3D { AlbedoColor = color, Roughness = 0.9f },
        };
        parent.AddChild(node);
        return node;
    }
}
