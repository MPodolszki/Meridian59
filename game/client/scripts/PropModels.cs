using Godot;

namespace Meridian.Client;

/// <summary>
/// Einfache Platzhalter aus Grundformen für Objekte ohne eigenes Modell: Kuh (cow.bgf), Portal (portal.bgf),
/// Schild (razsign1.bgf, sign.bgf) und Knochenhaufen (bonepile.bgf). Blickrichtung -Z wie die Figuren.
/// </summary>
public static class PropModels
{
    /// <summary>Modell zum Grafiknamen ohne Endung, oder null für Figuren (CharacterModel).</summary>
    public static Node3D? Create(string kind) => kind switch
    {
        "cow" => Cow(),
        "portal" => Portal(),
        "razsign1" or "sign" => Sign(),
        "bonepile" => Bones(),
        _ => null,
    };

    /// <summary>„Zwergkuh“ (cow.kod): hüfthoch, gefleckt.</summary>
    private static Node3D Cow()
    {
        var root = new Node3D { Name = "Cow" };
        var hide = new Color(0.92f, 0.9f, 0.85f);
        var spot = new Color(0.18f, 0.14f, 0.12f);
        var hoof = new Color(0.12f, 0.1f, 0.08f);
        Add(root, new BoxMesh { Size = new Vector3(0.55f, 0.45f, 1.0f) }, hide, new Vector3(0, 0.62f, 0));
        Add(root, new BoxMesh { Size = new Vector3(0.56f, 0.3f, 0.35f) }, spot, new Vector3(0, 0.68f, 0.15f));
        Add(root, new BoxMesh { Size = new Vector3(0.32f, 0.32f, 0.38f) }, hide, new Vector3(0, 0.8f, -0.62f));
        Add(root, new BoxMesh { Size = new Vector3(0.26f, 0.16f, 0.12f) }, new Color(0.85f, 0.6f, 0.6f), new Vector3(0, 0.7f, -0.84f));
        foreach (float x in new[] { -0.16f, 0.16f })
            Add(root, new CylinderMesh { TopRadius = 0.03f, BottomRadius = 0.05f, Height = 0.16f }, new Color(0.85f, 0.82f, 0.7f),
                new Vector3(x, 1.0f, -0.6f));
        foreach (var (x, z) in new[] { (-0.2f, -0.38f), (0.2f, -0.38f), (-0.2f, 0.38f), (0.2f, 0.38f) })
        {
            Add(root, new CylinderMesh { TopRadius = 0.07f, BottomRadius = 0.06f, Height = 0.4f }, hide, new Vector3(x, 0.25f, z));
            Add(root, new CylinderMesh { TopRadius = 0.065f, BottomRadius = 0.065f, Height = 0.06f }, hoof, new Vector3(x, 0.03f, z));
        }
        return root;
    }

    /// <summary>Leuchtender Ring auf einem Steinsockel.</summary>
    private static Node3D Portal()
    {
        var root = new Node3D { Name = "Portal" };
        Add(root, new CylinderMesh { TopRadius = 0.9f, BottomRadius = 1.0f, Height = 0.2f }, new Color(0.45f, 0.43f, 0.4f), new Vector3(0, 0.1f, 0));
        var ring = Add(root, new TorusMesh { InnerRadius = 0.85f, OuterRadius = 1.05f }, new Color(0.4f, 0.6f, 1f), new Vector3(0, 1.25f, 0));
        ring.RotationDegrees = new Vector3(90, 0, 0);
        Glow(ring, new Color(0.35f, 0.55f, 1f), 2.5f);
        var veil = Add(root, new CylinderMesh { TopRadius = 0.85f, BottomRadius = 0.85f, Height = 0.02f }, new Color(0.5f, 0.7f, 1f, 0.35f),
            new Vector3(0, 1.25f, 0));
        veil.RotationDegrees = new Vector3(90, 0, 0);
        var veilMaterial = (StandardMaterial3D)veil.MaterialOverride;
        veilMaterial.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        Glow(veil, new Color(0.4f, 0.6f, 1f), 1.2f);
        root.AddChild(new OmniLight3D { Position = new Vector3(0, 1.25f, 0), LightColor = new Color(0.5f, 0.7f, 1f), LightEnergy = 1.5f, OmniRange = 5f });
        return root;
    }

    /// <summary>Holzschild auf einem Pfahl.</summary>
    private static Node3D Sign()
    {
        var root = new Node3D { Name = "Sign" };
        var wood = new Color(0.42f, 0.3f, 0.18f);
        Add(root, new BoxMesh { Size = new Vector3(0.1f, 1.2f, 0.1f) }, wood, new Vector3(0, 0.6f, 0));
        Add(root, new BoxMesh { Size = new Vector3(0.7f, 0.45f, 0.06f) }, new Color(0.62f, 0.48f, 0.3f), new Vector3(0, 1.15f, -0.06f));
        return root;
    }

    /// <summary>Kleiner Haufen bleicher Knochen.</summary>
    private static Node3D Bones()
    {
        var root = new Node3D { Name = "Bones" };
        var bone = new Color(0.86f, 0.83f, 0.74f);
        Add(root, new SphereMesh { Radius = 0.13f, Height = 0.22f }, bone, new Vector3(0, 0.1f, 0));
        foreach (var (x, z, yaw) in new[] { (-0.2f, 0.1f, 20f), (0.15f, -0.15f, -35f), (0.05f, 0.22f, 80f) })
        {
            var b = Add(root, new CylinderMesh { TopRadius = 0.03f, BottomRadius = 0.03f, Height = 0.45f }, bone, new Vector3(x, 0.04f, z));
            b.RotationDegrees = new Vector3(90, yaw, 0);
        }
        return root;
    }

    private static MeshInstance3D Add(Node3D parent, Mesh mesh, Color color, Vector3 position)
    {
        var node = new MeshInstance3D
        {
            Mesh = mesh,
            Position = position,
            MaterialOverride = new StandardMaterial3D { AlbedoColor = color, Roughness = 0.9f },
        };
        parent.AddChild(node);
        return node;
    }

    private static void Glow(MeshInstance3D node, Color color, float energy)
    {
        var m = (StandardMaterial3D)node.MaterialOverride;
        m.EmissionEnabled = true;
        m.Emission = color;
        m.EmissionEnergyMultiplier = energy;
    }
}
