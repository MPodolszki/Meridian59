using Godot;

namespace Meridian.Client;

/// <summary>
/// Lädt animierte Figuren aus models/&lt;art&gt;.glb (erzeugt mit game/tools/blender/make_character.py)
/// und steuert ihre Animationen: Idle/Walk/Run als Schleife, Attack/Die einmalig.
/// </summary>
public static class CharacterModel
{
    private const string LockMeta = "anim_lock_until";

    /// <summary>Figur passend zur Icon-Ressource ("zombie.bgf" → models/zombie.glb, "skelbody.bgf" → skeleton,
    /// "cow.bgf"/"portal.bgf"/Schilder → <see cref="PropModels"/>); sonst Zwerg.</summary>
    public static Node3D Create(string icon)
    {
        string kind = System.IO.Path.GetFileNameWithoutExtension(icon).ToLowerInvariant();
        // Gegenstände/Tiere ohne Figur (Kuh, Portal, Schild) aus Grundformen.
        if (PropModels.Create(kind) is { } prop)
            return prop;
        // Grafiknamen des Originals (KOD vrIcon) auf eigene Modelle abbilden; Spieler/NPCs sind Zwerge.
        // Mumien und Drescher (res0001a.bgf) nutzen vorerst das Zombie-Modell.
        kind = kind switch { "skelbody" => "skeleton", "mummy" or "res0001a" => "zombie", _ => kind };
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
            foreach (string name in new[] { "Idle", "Walk", "Run", "Sprint", "Jump_Loop", "Idle_Torch" })
                if (player.HasAnimation(name))
                    player.GetAnimation(name).LoopMode = Animation.LoopModeEnum.Linear;
            player.Play("Idle");
        }
        return holder;
    }

    /// <summary>Natürliches Tempo der Bewegungsclips (Universal Animation Library) in m/s bei Zwergengröße.</summary>
    private const float WalkClipSpeed = 1.6f, RunClipSpeed = 4f, SprintClipSpeed = 7f;

    /// <summary>Spielt „Idle“, „Walk“, „Run“ (Joggen) oder „Sprint“ passend zur Geschwindigkeit (m/s), außer während
    /// einmaliger Animationen. Mit Überblendung und Hysterese, damit die Clips nicht hin- und herspringen.</summary>
    public static void Animate(Node3D figure, float speed, bool backwards = false)
    {
        if (Player(figure) is not { } player || IsLocked(figure))
            return;
        string current = player.CurrentAnimation;
        float up = current == "Idle" ? 0.4f : 0.25f;   // Hysterese um die Schwellen
        string wanted = speed < up ? "Idle"
            : speed < (current == "Run" ? 2.4f : 2.9f) ? "Walk"
            : speed < (current == "Sprint" ? 5.6f : 6.2f) || !player.HasAnimation("Sprint") ? "Run"
            : "Sprint";
        if (current != wanted && player.HasAnimation(wanted))
            player.Play(wanted, customBlend: 0.25);
        // Tempo des Clips an die Geschwindigkeit koppeln (begrenzt), damit die Füße weniger rutschen.
        player.SpeedScale = wanted switch
        {
            "Walk" => Mathf.Clamp(speed / WalkClipSpeed, 0.7f, 1.6f),
            "Run" => Mathf.Clamp(speed / RunClipSpeed, 0.7f, 1.4f),
            "Sprint" => Mathf.Clamp(speed / SprintClipSpeed, 0.8f, 1.3f),
            _ => 1f,
        };
        if (backwards)
            player.SpeedScale = -player.SpeedScale;   // rückwärts laufen: Clip rückwärts (WoW)
    }

    /// <summary>Einmalige Animation; „Die“ bleibt in der letzten Pose liegen. <paramref name="lockSeconds"/> gibt die
    /// Bewegungsanimation früher wieder frei (z. B. Landung beim Weiterlaufen).</summary>
    public static void PlayOnce(Node3D figure, string name, double? lockSeconds = null)
    {
        if (Player(figure) is not { } player || !player.HasAnimation(name))
            return;
        player.SpeedScale = 1f;
        player.Play(name, customBlend: 0.15);
        double until = name == "Die" ? double.MaxValue : Time.GetTicksMsec() / 1000.0 + (lockSeconds ?? player.GetAnimation(name).Length);
        figure.SetMeta(LockMeta, until);
    }

    /// <summary>Schleife (z. B. „Jump_Loop“) spielen, sobald keine einmalige Animation mehr läuft.</summary>
    public static void Hold(Node3D figure, string name)
    {
        if (Player(figure) is not { } player || IsLocked(figure) || player.CurrentAnimation == name || !player.HasAnimation(name))
            return;
        player.SpeedScale = 1f;
        player.Play(name, customBlend: 0.2);
    }

    /// <summary>Hebt eine liegende Pose auf (z. B. nach Wiederbelebung).</summary>
    public static void Unlock(Node3D figure) => figure.SetMeta(LockMeta, 0.0);

    private static bool IsLocked(Node3D figure) =>
        figure.HasMeta(LockMeta) && figure.GetMeta(LockMeta).AsDouble() > Time.GetTicksMsec() / 1000.0;

    private static AnimationPlayer? Player(Node3D figure) =>
        figure.FindChild("AnimationPlayer", recursive: true, owned: false) as AnimationPlayer;
}
