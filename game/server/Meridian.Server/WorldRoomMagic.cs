using Meridian.Protocol;

namespace Meridian.Server;

/// <summary>Aktive Raumverzauberung (roomench.kod): Zustand, Ende, Zaubernder.</summary>
public sealed record RoomEnchantment(SpellDefinition Spell, int State, DateTime Until);

/// <summary>
/// Raumverzauberungen (roomench.kod: Licht, Dunkelheit, Schlachtfeld, Kräfte des Lichts) sowie Rettung (rescue.kod)
/// und Manafokus (focus.kod). Formeln in docs/magic.md.
/// </summary>
public sealed partial class World
{
    private static bool IsRoomEnchantment(SpellDefinition s) => s.Effect is "roomlight" or "roomdark" or "killingfields" or "forcesoflight";

    /// <summary>Lichtänderung durch Licht/Dunkelheit (AddBaseLight).</summary>
    private static int LightDelta(Room room) => room.Enchantments.Values.Sum(e => e.Spell.Effect switch
    {
        "roomlight" => e.State,
        "roomdark" => -e.State,
        _ => 0,
    });

    private static byte CurrentLight(Room room, int hour) =>
        (byte)Math.Clamp(GameClock.RoomLight(room.Definition, hour) + LightDelta(room), 0, 255);

    private int RoomEnchantState(Room room, string effect) =>
        room.Enchantments.Values.Where(e => e.Spell.Effect == effect).Sum(e => e.State);

    /// <summary>roomench.kod CastSpell: nur eine Verzauberung derselben Art je Raum.</summary>
    private bool CanEnchantRoom(Avatar a, SpellDefinition spell)
    {
        if (!a.Room.Enchantments.ContainsKey(spell.Sid))
            return true;
        Tell(a, spell.Unnecessary ?? "Dieser Ort steht bereits unter diesem Zauber.");
        return false;
    }

    private void EnchantRoom(Avatar a, SpellDefinition spell, int power)
    {
        var room = a.Room;
        // Dauer (s) und Zustand je Spruch: light.kod, darkness.kod, killing.kod, forceslt.kod.
        var (state, seconds) = spell.Effect switch
        {
            "roomlight" => (25 + power, Half(45 + 3 * power)),
            "roomdark" => (power + 25, Half(30 + 2 * power)),
            "killingfields" => (1, 50 + power),
            _ => (power, 6 * (power / 3 + 1)),                    // Kräfte des Lichts
        };
        room.Enchantments[spell.Sid] = new RoomEnchantment(spell, state, DateTime.UtcNow.AddSeconds(seconds));
        foreach (var other in room.Avatars.Values)
            Tell(other, spell.CastText ?? $"{spell.Name} liegt auf diesem Ort.");
        if (LightDelta(room) != 0 || spell.Effect is "roomlight" or "roomdark")
            BroadcastRoom(room, GameReplies.LightAmbient(CurrentLight(room, Hour(DateTime.UtcNow))));
    }

    /// <summary>random(d/2, d) wie GetDuration der meisten Verzauberungen.</summary>
    private int Half(int d) => _random.Next(d / 2, d + 1);

    private void TickRoomEnchantments(Room room, DateTime now)
    {
        if (room.Enchantments.Count == 0)
            return;
        foreach (var (sid, e) in room.Enchantments.ToList())
        {
            if (now < e.Until)
                continue;
            room.Enchantments.Remove(sid);
            foreach (var a in room.Avatars.Values)
                Tell(a, e.Spell.OffText ?? $"{e.Spell.Name} ist vergangen.");
            if (e.Spell.Effect is "roomlight" or "roomdark")
                BroadcastRoom(room, GameReplies.LightAmbient(CurrentLight(room, Hour(now))));
        }
    }

    /// <summary>Kräfte des Lichts (forceslt.kod ModifyHitRoll): + (Zustand + 50) für Spieler mit gutem Karma.</summary>
    private int ForcesOfLightBonus(Avatar a) =>
        a.State.Karma > 0 && a.Room.Enchantments.Values.FirstOrDefault(e => e.Spell.Effect == "forcesoflight") is { } e
            ? e.State + 50 : 0;

    /// <summary>Schlachtfeld (killing.kod ModifyDamage): + Random(2, 5) auf jeden Nahkampftreffer.</summary>
    private int KillingFieldsBonus(Avatar a) => RoomEnchantState(a.Room, "killingfields") > 0 ? _random.Next(2, 6) : 0;

    /// <summary>Rüstung des Gort (gort.kod ModifyDefenseDamage, nur Waffenschaden): Abzug bound(Random(0, Stärke/25), 1, 4),
    /// dann höchstens 15 Schaden pro Treffer.</summary>
    private int GortAbsorb(Avatar a, int damage)
    {
        int state = EnchantState(a, "gort");
        if (state == 0)
            return damage;
        int absorb = Math.Clamp(_random.Next(0, state / 25 + 1), 1, 4);
        return Math.Clamp(damage - absorb, 1, 15);
    }

    // ------------------------------------------------------------------ Rettung

    /// <summary>rescue.kod: nach 15000 − Stärke×15000/400 ms (zur Hälfte der Fälle + 5–10 s) an einen sicheren Ort –
    /// hier der Raum, in dem man nach dem Tod erwacht.</summary>
    private void StartRescue(Avatar a, SpellDefinition spell, int power)
    {
        if (a.RescueAt is not null)
        {
            Tell(a, "Du wirst bereits gerettet.");
            return;
        }
        int delay = 15000 - power * 15000 / 100 / 4;
        if (_random.Next(2) == 0)
            delay += _random.Next(5000, 10001);
        a.RescueAt = DateTime.UtcNow.AddMilliseconds(delay);
        Tell(a, spell.CastText ?? "Du beginnst mit den Vorbereitungen für deine Rettung.");
    }

    private void TickRescue(Avatar a, DateTime now)
    {
        if (a.RescueAt is not { } at || now < at)
            return;
        a.RescueAt = null;
        var home = a.Room.Definition.RespawnRoom is { } r && _rooms.TryGetValue(r, out var own) ? own : RespawnRoom;
        Tell(a, "Eine heilige Kraft rettet dich aus deiner Lage.");
        Remove(a);
        var (row, col) = home.ArrivalPoint;
        Place(a, home, row, col);
    }

    // ------------------------------------------------------------------ Manafokus

    /// <summary>focus.kod: Trance ohne Ende; Mana erholt sich im Takt × (200 − Stärke)/200 (0,5 … 60 s), Angriffssprüche
    /// machen (Zustand × 5)/99 + 1 mehr Schaden. Endet bei Bewegung oder Nahkampf.</summary>
    private void StartManaFocus(Avatar a, SpellDefinition spell, int power)
    {
        if (a.ManaFocus is not null)
        {
            Tell(a, $"Du hast bereits {spell.Name}.");
            return;
        }
        a.ManaFocus = power;
        a.NextMana = default;
        Tell(a, spell.CastText ?? "Neu gebündelte Magie tobt durch deinen Körper.");
    }

    private void EndManaFocus(Avatar a)
    {
        if (a.ManaFocus is null)
            return;
        a.ManaFocus = null;
        Tell(a, "Du hast deinen Manafokus verloren.");
    }

    private TimeSpan FocusedManaTime(Avatar a, TimeSpan normal) => a.ManaFocus is { } sp
        ? TimeSpan.FromMilliseconds(Math.Clamp(normal.TotalMilliseconds * (200 - sp) / 200, 500, 60000))
        : normal;

    private static int ManaFocusBonus(Avatar a) => a.ManaFocus is { } state ? state * 5 / 99 + 1 : 0;
}
