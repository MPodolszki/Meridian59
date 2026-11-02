using Godot;

namespace Meridian.Client;

/// <summary>
/// Lädt animierte Figuren aus models/&lt;art&gt;.glb (erzeugt mit game/tools/blender/make_character.py)
/// und steuert ihre Animationen: Idle/Walk/Run als Schleife, Attack/Die einmalig.
/// </summary>
public static class CharacterModel
{
    private const string LockMeta = "anim_lock_until";

    /// <summary>Figur passend zur Icon-Ressource ("zombie.bgf" → models/zombie.glb, "skelbody.bgf" → skeleton); sonst Zwerg.</summary>
    public static Node3D Create(string icon)
    {
        string kind = System.IO.Path.GetFileNameWithoutExtension(icon).ToLowerInvariant();
        // Grafiknamen des Originals (KOD vrIcon) auf eigene Modelle abbilden; Spieler/NPCs sind Zwerge.
        kind = kind switch { "skelbody" => "skeleton", _ => kind };
        string path = $"res://models/{kind}.glb";
        if (!ResourceLoader.Exists(path))
            path = "res://models/dwarf.glb";
        if (!ResourceLoader.Exists(path))
            return DwarfModel.CreatePrimitive();

        var model = GD.Load<PackedScene>(path).Instantiate<Node3D>();
        // glTF schaut nach +Z, die Spielfiguren im Client nach -Z.
        model.RotationDegrees = new Vector3(0, 180, 0);
        var holder = new Node3D { Name = "Character" };
        holder.AddChild(model);
        if (Player(holder) is { } player)
        {
            // glTF-Animationen werden ohne Schleife importiert.
            foreach (string name in new[] { "Idle", "Walk", "Run" })
                if (player.HasAnimation(name))
                    player.GetAnimation(name).LoopMode = Animation.LoopModeEnum.Linear;
            player.Play("Idle");
        }
        return holder;
    }

    /// <summary>Spielt „Idle“, „Walk“ oder „Run“ passend zur Geschwindigkeit (m/s), außer während Attack/Die.</summary>
    public static void Animate(Node3D figure, float speed)
    {
        if (Player(figure) is not { } player || IsLocked(figure))
            return;
        string wanted = speed > 5.5f ? "Run" : speed > 0.3f ? "Walk" : "Idle";
        if (player.CurrentAnimation != wanted && player.HasAnimation(wanted))
            player.Play(wanted, customBlend: 0.2);
        // Laufanimation an das Tempo koppeln, damit die Füße weniger rutschen.
        player.SpeedScale = wanted == "Walk" ? speed / 4f : wanted == "Run" ? speed / 8f : 1f;
    }

    /// <summary>Einmalige Animation; „Die“ bleibt in der letzten Pose liegen.</summary>
    public static void PlayOnce(Node3D figure, string name)
    {
        if (Player(figure) is not { } player || !player.HasAnimation(name))
            return;
        player.SpeedScale = 1f;
        player.Play(name, customBlend: 0.1);
        double until = name == "Die" ? double.MaxValue : Time.GetTicksMsec() / 1000.0 + player.GetAnimation(name).Length;
        figure.SetMeta(LockMeta, until);
    }

    /// <summary>Hebt eine liegende Pose auf (z. B. nach Wiederbelebung).</summary>
    public static void Unlock(Node3D figure) => figure.SetMeta(LockMeta, 0.0);

    private static bool IsLocked(Node3D figure) =>
        figure.HasMeta(LockMeta) && figure.GetMeta(LockMeta).AsDouble() > Time.GetTicksMsec() / 1000.0;

    private static AnimationPlayer? Player(Node3D figure) =>
        figure.FindChild("AnimationPlayer", recursive: true, owned: false) as AnimationPlayer;
}
