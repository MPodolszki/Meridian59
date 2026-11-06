using Meridian.Protocol;

namespace Meridian.Server;

/// <summary>
/// Sonderstäbe (specwand/*.kod, SpecialWand): jede Benutzung kostet eine Ladung; bei 0 ist der Stab verbraucht.
/// Feuerstab und Vampirstab treffen das nächste Monster mit Random(1,100) &lt; Mystik + Zielen (10..90), der Heilstab
/// heilt den Benutzer, der Stab der Erschütterung trifft nur Spieler (kein PvP: nur die Meldung). Siehe docs/trade.md.
/// </summary>
public sealed partial class World
{
    private void UseSpecialWand(Avatar a, Item item)
    {
        if (IsHeld(a))
            return;
        item.Charges ??= _random.Next(item.Kind.ChargesMin, item.Kind.ChargesMax + 1);
        if (item.Charges <= 0)
        {
            Tell(a, $"{Capitalize(item.Describe())} ist verbraucht.");
            return;
        }
        var kind = item.Kind;
        var now = DateTime.UtcNow;
        Monster? victim = null;
        if (kind.Special is "fire" or "vampire")
        {
            if (a.Room.Definition.NoCombat)
            {
                Tell(a, "Hier ist Kampf nicht erlaubt.");
                return;
            }
            victim = a.Room.Monsters.Where(m => m.Alive && !m.Kind.Peaceful && Distance(a.Row, a.Col, m.Row, m.Col) <= 10 * 64)
                .OrderBy(m => m.Target == a ? 0 : 1).ThenBy(m => Distance(a.Row, a.Col, m.Row, m.Col)).FirstOrDefault();
            if (victim is null)
            {
                Tell(a, kind.Special == "fire"
                    ? "Es wäre töricht, die mystische Kraft an etwas Unbelebtes zu verschwenden!"
                    : "Du kannst einem leeren Gefäß kein Leben entziehen!");
                return;
            }
        }
        item.Charges--;
        Tell(a, "Der Stab pulsiert einmal in deiner Hand.");
        switch (kind.Special)
        {
            case "fire":
                WandStrike(a, victim!, kind, ["spell_all", "fire"], now, "Ein Feuerball hüllt {0} ein!");
                break;
            case "vampire":
                WandStrike(a, victim!, kind, ["spell_all", "unholy", "shock"], now, "Du saugst {0} die Lebenskraft aus!");
                break;
            case "heal":
                Tell(a, "Du fühlst dich besser.");
                a.Hp = Math.Min(a.MaxHp, a.Hp + _random.Next(kind.DamageMin, kind.DamageMax + 1));
                a.Session.SendHealth();
                break;
            case "jolt":
                Tell(a, "Der Stab lässt den Boden erzittern!");
                foreach (var other in a.Room.Avatars.Values.Where(o => o != a))
                    Tell(other, $"{a.Name} stößt einen knorrigen Stab auf den Boden.");
                break;
        }
        if (item.Charges <= 0)
            Tell(a, $"{Capitalize(item.Describe())} hat seine Kraft verloren.");
    }

    /// <summary>firewand.kod/vampwand.kod CastSpell: Treffer bei Random(1,100) &lt; Mystik + Zielen (10..90), Schaden
    /// Random(min, max) mit Resistenz; der Vampirstab heilt den Benutzer um Schaden − 1.</summary>
    private void WandStrike(Avatar a, Monster m, ItemKind kind, string[] types, DateTime now, string hit)
    {
        var at = a.State.Attributes;
        m.Target ??= a;
        BroadcastRoom(a.Room, GameReplies.Animate(ToRoomObject(a), GameReplies.AttackGroup, 4, 500));
        if (_random.Next(1, 101) >= Math.Clamp(at.Mysticism + at.Aim, 10, 90))
        {
            Tell(a, $"Du verfehlst {Article(m.Kind, capital: false, accusative: true)}.");
            return;
        }
        int damage = _random.Next(kind.DamageMin, kind.DamageMax + 1);
        if (kind.Special == "vampire" && damage > 1)
        {
            a.Hp = Math.Min(a.MaxHp, a.Hp + damage - 1);
            a.Session.SendHealth();
        }
        damage = Math.Max(1, CombatRules.Resist(damage, m.Kind.ResistanceTo(types)));
        Tell(a, string.Format(hit, Article(m.Kind, capital: false, accusative: true)));
        m.Hp -= damage;
        if (m.Hp > 0)
            Tell(a, $"Der Stab trifft {Article(m.Kind, capital: false, accusative: true)} für {damage} Schaden ({m.Hp}/{m.MaxHp}).");
        else
            KillMonster(a, m, now);
    }
}
