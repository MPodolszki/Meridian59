using Meridian.Protocol;
namespace Meridian.Server;

/// <summary>
/// Wirkungen der Schulsprüche nach kod/object/passive/spell (Formeln: docs/kod/spells_catalog.json, docs/magic.md):
/// Angriffszauber, Heilung und Heilen von Gift, Schwächungen auf Monster, Flächenzauber, Heiliges Symbol.
/// </summary>
public sealed partial class World
{
    /// <summary>Wirkungen, die ein Monster als Ziel brauchen (gegen Spieler noch nicht: kein PvP).</summary>
    private static readonly HashSet<string> MonsterEffects = ["attack", "drain", "hold", "blind", "dazzle", "enfeeble", "vertigo"];
    /// <summary>Schädliche Raumwirkungen; in Räumen ohne Kampf verboten (ReqSomethingAttack).</summary>
    private static readonly HashSet<string> RoomAttackEffects = ["earthquake", "finalrites", "flash", "holysymbol"];

    /// <summary>Zielprüfung der Schulsprüche (CanPayCosts der einzelnen Sprüche).</summary>
    private bool CanTarget(Avatar a, SpellDefinition spell, Avatar? target, Monster? victim)
    {
        string e = spell.Effect;
        if ((MonsterEffects.Contains(e) || RoomAttackEffects.Contains(e)) && a.Room.Definition.NoCombat)
        {
            Tell(a, "Hier ist kein Kampf erlaubt.");
            return false;
        }
        if (MonsterEffects.Contains(e))
        {
            if (victim is null)
            {
                Tell(a, target == a ? $"Du kannst {spell.Name} nicht auf dich selbst wirken." : "Das willst du nicht angreifen.");
                return false;
            }
            var now = DateTime.UtcNow;
            string? already = e switch
            {
                "hold" when now < victim.HeldUntil => "{0} wird bereits festgehalten.",
                "blind" or "dazzle" when now < victim.BlindUntil => "{0} ist bereits geblendet.",
                "enfeeble" or "vertigo" when now < victim.PalsyUntil => "{0} ist bereits geschwächt.",
                _ => null,
            };
            if (already is not null)
            {
                Tell(a, string.Format(already, Article(victim.Kind)));
                return false;
            }
            return true;
        }
        if (victim is not null)
        {
            Tell(a, $"Du kannst {spell.Name} nicht darauf wirken.");
            return false;
        }
        switch (e)
        {
            case "majorheal" when target is not null && target.Hp >= target.MaxHp:
                Tell(a, target == a ? "Du bist kerngesund." : $"{target.Name} ist kerngesund.");
                return false;
            case "breathoflife" or "relay" when target == a:
                Tell(a, $"Du kannst {spell.Name} nicht auf dich selbst wirken.");
                return false;
            case "breathoflife" when a.Hp <= 10:
                Tell(a, "Du hast nicht genug Lebenskraft, um sie weiterzugeben!");
                return false;
            case "breathoflife" when target is not null && target.Hp >= Math.Min(target.MaxHp * 3 / 2, 130):
                Tell(a, $"{target.Name} kann keine weitere Lebenskraft aufnehmen.");
                return false;
            case "relay" when a.State.Vigor <= 10:
                Tell(a, "Du hast nicht genug Ausdauer, um sie weiterzugeben!");
                return false;
            case "relay" when target is not null && target.State.Vigor >= 150:
                Tell(a, $"{target.Name} kann keine weitere Ausdauer aufnehmen.");
                return false;
            case "earthquake" when a.Room.Crypt3a is not null:
                Tell(a, "Dieser Ort sieht viel zu instabil für ein Erdbeben aus. Du brichst den Zauber klugerweise ab.");
                return false;
            case "weaponenchant" when Weapon(a) is null:
                Tell(a, "Du musst eine Waffe in der Hand halten, um sie zu weihen.");
                return false;
            case "weaponenchant" when Weapon(a)!.WeaponEnchant is { } we && we.Until > DateTime.UtcNow:
                Tell(a, $"{Capitalize(Weapon(a)!.Describe())} ist bereits geweiht.");
                return false;
            case "curepoison" when target is not null && !target.Poisoned:
                Tell(a, target == a ? "Du bist nicht vergiftet." : $"{target.Name} ist nicht vergiftet.");
                return false;
        }
        return true;
    }

    private void CastSchoolSpell(Avatar a, SpellDefinition spell, Avatar? target, Monster? victim, int power)
    {
        var now = DateTime.UtcNow;
        switch (spell.Effect)
        {
            case "attack" or "drain":
            {
                // atakspel.kod: Random(min, max) × (50 + Stärke/2) / 99, dann Resistenz des Ziels (AssessDamage).
                int d = _random.Next(spell.MinDamage, spell.MaxDamage + 1) * (50 + power / 2) / 99 + ManaFocusBonus(a);
                SpellDamage(a, spell, victim!, d, now, drain: spell.Effect == "drain");
                break;
            }
            case "majorheal":
                Heal(a, target!, Math.Clamp(_random.Next(7, 14) + power / 5 + target!.State.Karma / 5, 0, 50), spell);
                break;
            case "breathoflife":
            {
                // brthlife.kod: Anteil nach Karma des Ziels (10..80 %), höchstens bis 10 LP beim Wirker; das Ziel
                // erhält (75 + Stärke/2) % davon, bis 150 % seines Maximums bzw. höchstens 130.
                int pct = Math.Clamp(target!.State.Karma, 10, 80);
                int loss = Math.Min(a.Hp * pct / 100, a.Hp - 10);
                int gain = Math.Min(loss * (75 + power / 2) / 100, Math.Min(target.MaxHp * 3 / 2, 130) - target.Hp);
                if (loss <= 0 || gain <= 0)
                    break;
                a.Hp -= loss;
                target.Hp += gain;
                Tell(a, $"Du gibst {target.Name} einen Teil deiner Lebenskraft.");
                Tell(target, spell.CastText ?? "Fremde Lebenskraft erfüllt dich.");
                a.Session.SendHealth();
                target.Session.SendHealth();
                break;
            }
            case "relay":
            {
                // relay.kod: min(Stärke, eigene Ausdauer − 10, 150 − Ausdauer des Ziels); das Ziel erhält 5/4 davon.
                int amount = Math.Min(power, Math.Min(a.State.Vigor - 10, 150 - target!.State.Vigor));
                if (amount <= 0)
                    break;
                a.State.Vigor -= amount;
                target.State.Vigor = Math.Min(200, target.State.Vigor + amount * 5 / 4);
                Tell(a, $"Du gibst {target.Name} einen Teil deiner Ausdauer.");
                Tell(target, spell.CastText ?? "Fremde Kraft erfüllt dich.");
                a.Session.SendHealth();
                target.Session.SendHealth();
                break;
            }
            case "curepoison":
            {
                // curepois.kod: ganz geheilt bei Random(1,100) < Stärke oder schwachem Gift, sonst Verlust − Stärke %.
                int rate = target!.PoisonLossRate, after = rate - rate * power / 100;
                if (target != a)
                    Tell(a, $"Du wirkst {spell.Name} auf {target.Name}.");
                Tell(target, spell.CastText ?? "Das Gift wird bekämpft.");
                if (_random.Next(1, 101) < power || rate <= 100 || after < rate / 10 || after < 100)
                    CurePoison(target);
                else
                {
                    target.PoisonLossRate = after;
                    Tell(target, "Du spürst, wie das Gift schwächer wird.");
                }
                break;
            }
            case "hold":
            {
                // hold.kod: D = 2000 + 40 × Stärke ms, Random(D/2, D); Monster: AI_NOMOVE + AI_NOFIGHT.
                int d = 2000 + 40 * power;
                Afflict(a, spell, victim!, m => m.HeldUntil = now.AddMilliseconds(_random.Next(d / 2, d + 1)));
                break;
            }
            case "blind":
            {
                // blind.kod: d = 5 + Stärke/6 s, Random(d/2, d), 3..20 s; Monster laufen ziellos und kämpfen nicht.
                int d = 5 + power / 6;
                int secs = Math.Clamp(_random.Next(d / 2, d + 1), 3, 20);
                Afflict(a, spell, victim!, m => m.BlindUntil = now.AddSeconds(secs));
                break;
            }
            case "dazzle":
            {
                // dazzle.kod: Stärke/12 + (Karma Wirker − Karma Ziel)/20 Sekunden, 3..15.
                int secs = Math.Clamp(power / 12 + (a.State.Karma - victim!.Kind.Karma) / 20, 3, 15);
                Afflict(a, spell, victim, m => m.BlindUntil = now.AddSeconds(secs));
                break;
            }
            case "enfeeble":
            {
                // enfeeble.kod: Lähmung, d = (300 + 16 × Stärke) × 100 ms, Random(d/2, d).
                int d = (300 + 16 * power) * 100;
                Afflict(a, spell, victim!, m => m.PalsyUntil = now.AddMilliseconds(_random.Next(d / 2, d + 1)));
                break;
            }
            case "vertigo":
            {
                // vertigo.kod: auf Monster Lähmung, d = (10 + Stärke/2) × 250 ms, Random(d/2, d).
                int d = (10 + power / 2) * 250;
                Afflict(a, spell, victim!, m => m.PalsyUntil = now.AddMilliseconds(_random.Next(d / 2, d + 1)));
                break;
            }
            case "earthquake":
                Earthquake(a, spell, power, now);
                break;
            case "finalrites":
            {
                // finlrite.kod: alle Untoten im Raum, Random(1,5) + Stärke/20 + 2 × ((Karma Wirker − Karma Ziel)/40), heilig.
                Tell(a, spell.CastText ?? $"Du wirkst {spell.Name}.");
                foreach (var m in a.Room.Monsters.Where(m => m.Alive && m.Kind.Undead).ToList())
                {
                    int d = _random.Next(1, 6) + power / 20 + 2 * ((a.State.Karma - m.Kind.Karma) / 40);
                    SpellDamage(a, spell, m, d, now, drain: false);
                }
                break;
            }
            case "flash":
            {
                // flash.kod: jedes Monster mit 33 + 2 × Stärke/3 % für 1 s festgehalten (ohne Free Action).
                Tell(a, spell.CastText ?? "Ein greller Blitz!");
                foreach (var m in a.Room.Monsters.Where(m => m.Alive))
                    if (_random.Next(1, 101) <= 33 + 2 * power / 3)
                        m.HeldUntil = now.AddSeconds(1);
                break;
            }
            case "holysymbol":
                HolySymbol(a, spell, power, now);
                break;
        }
    }

    /// <summary>Schaden eines Zaubers auf ein Monster (AssessDamage mit Resistenz); das Monster wehrt sich.</summary>
    private void SpellDamage(Avatar a, SpellDefinition spell, Monster m, int damage, DateTime now, bool drain)
    {
        if (spell.AttackType is { } type)
            damage = CombatRules.Resist(damage, m.Kind.ResistanceTo(type));
        damage = Math.Max(damage, 1);
        m.Target ??= a;
        if (spell.HitText is { } hit)
            Tell(a, string.Format(hit, Article(m.Kind, capital: false, accusative: true)));
        BroadcastRoom(a.Room, GameReplies.Animate(ToRoomObject(a), GameReplies.AttackGroup, 4, 500));
        m.Hp -= damage;
        bool killed = m.Hp <= 0;
        if (drain)
        {
            // vampdrn.kod: Hälfte des Schadens heilt den Wirker, beim Töten die halbe Höchstwirkung.
            int heal = killed ? Math.Max(1, spell.MaxDamage / 2) : Math.Max(1, damage / 2);
            a.Hp = Math.Min(a.MaxHp, a.Hp + heal);
            a.Session.SendHealth();
        }
        if (!killed)
        {
            Tell(a, $"{spell.Name} trifft {Article(m.Kind, capital: false, accusative: true)} für {damage} Schaden ({m.Hp}/{m.MaxHp}).");
            return;
        }
        KillMonster(a, m, now);
    }

    /// <summary>Zustand auf ein Monster legen; es merkt, wer es war.</summary>
    private void Afflict(Avatar a, SpellDefinition spell, Monster m, Action<Monster> apply)
    {
        apply(m);
        m.Target ??= a;
        Tell(a, string.Format(spell.HitText ?? "{0} ist verzaubert.", Article(m.Kind)));
    }

    /// <summary>earthqua.kod: Stärke 1 + Stärke/25; Schaden Random(5,9) × Stärke, voll bis 8 Felder Abstand, ab 20
    /// keiner; auch der Wirker wird umgeworfen. Andere Spieler bleiben verschont (noch kein PvP).</summary>
    private void Earthquake(Avatar a, SpellDefinition spell, int power, DateTime now)
    {
        int severity = 1 + power / 25;
        Tell(a, spell.CastText ?? "Die Erde bebt!");
        foreach (var m in a.Room.Monsters.Where(m => m.Alive).ToList())
        {
            double dr = (m.Row - a.Row) / 64.0, dc = (m.Col - a.Col) / 64.0;
            double d2 = dr * dr + dc * dc;
            int pct = d2 <= 64 ? 100 : d2 > 400 ? 0 : (int)(100 * (400 - d2) / 336);
            if (pct > 0)
                SpellDamage(a, spell with { AttackType = "quake", HitText = null }, m, _random.Next(5, 10) * severity * pct / 100, now, drain: false);
        }
        int self = _random.Next(5, 10) * severity;
        a.Hp = Math.Max(1, a.Hp - self);
        Tell(a, $"Die Erde bäumt sich unter deinen Füßen auf und wirft dich umher ({self} Schaden).");
        a.Session.SendHealth();
    }

    /// <summary>holysymb.kod: jeder Untote würfelt gegen Stärke × 3/4 + (Grund-LP − Stufe/2) + Karmaunterschied
    /// (−5, wenn er schon ein Ziel hat; 2..98). Gelingt es, flieht er 9 min / Schwierigkeit vor dem Wirker; misslingt es
    /// deutlich (Wurf > Chance + 25), greift er den Wirker an.</summary>
    private void HolySymbol(Avatar a, SpellDefinition spell, int power, DateTime now)
    {
        Tell(a, spell.CastText ?? "Du hältst das heilige Symbol empor.");
        foreach (var m in a.Room.Monsters.Where(m => m.Alive && m.Kind.Undead))
        {
            int chance = power * 3 / 4 + (a.MaxHp - m.Kind.Level / 2) + (a.State.Karma - m.Kind.Karma);
            if (m.Target is not null)
                chance -= 5;
            chance = Math.Clamp(chance, 2, 98);
            int roll = _random.Next(1, 101);
            if (roll < chance)
            {
                m.TurnedBy = a;
                m.TurnedUntil = now.AddSeconds(540.0 / Math.Max(1, m.Kind.Difficulty));
                Tell(a, $"{Article(m.Kind)} weicht vor dem heiligen Zeichen zurück!");
            }
            else if (roll > chance + 25)
                m.Target = a;
        }
    }

    /// <summary>Nur für Tests: Gegenstände ins Inventar legen.</summary>
    /// <summary>Für Tests: ein Monster sofort besiegen (wie ein tödlicher Treffer).</summary>
    internal void KillForTest(Avatar killer, Monster m)
    {
        lock (_gate)
            if (m.Alive)
            {
                m.Hp = 0;
                KillMonster(killer, m, DateTime.UtcNow);
            }
    }

    internal void GiveItemsForTest(Avatar a, IEnumerable<(string Key, int Number)> items)
    {
        lock (_gate)
            foreach (var (key, number) in items)
                if (_items.TryGet(key, out var kind))
                    Give(a, kind, number);
    }
}
