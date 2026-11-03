using Meridian.Protocol;

namespace Meridian.Server;

/// <summary>
/// Zaubernde Monster (orcwiza.kod, avarsham.kod, dangel.kod, ghost.kod, ent.kod, yeti.kod; monsters.json "casting"):
/// statt eines Schlags mit Chance 1/n ein Spruch nach Wurf 1..100, Mana wie im Original (+1 je Sekunde). Wirkungen auf
/// Spieler: Angriffssprüche mit Widerstand, Festhalten, Blindheit, Schwächen, Erdbeben, Manabombe, Raumverzauberungen.
/// </summary>
public sealed partial class World
{
    /// <summary>Versucht einen Spruch; true = das Monster hat gezaubert (kein Schlag in dieser Runde).</summary>
    private bool TryMonsterCast(Monster m, Avatar victim, MonsterCasting cast, bool inMeleeRange, DateTime now)
    {
        // Mana: Start = Maximum, +1 je Sekunde (piMana, ManaTimer).
        if (cast.Mana > 0)
        {
            if (m.Mana < 0)
                m.Mana = cast.Mana;
            int regen = (int)(now - m.ManaTick).TotalSeconds;
            if (regen > 0)
            {
                m.Mana = Math.Min(cast.Mana, m.Mana + regen);
                m.ManaTick = now;
            }
        }
        if (cast.MinRange && inMeleeRange)
            return false;
        if (Distance(m.Row, m.Col, victim.Row, victim.Col) > cast.Range * 64)
            return false;
        if (cast.IfRoomEnchanted && m.Room.Enchantments.Count == 0)
            return false;
        if (_random.Next(cast.OneIn) != 0)
            return false;
        int roll = _random.Next(1, 101);
        var pick = cast.Spells.FirstOrDefault(s => roll <= s.UpTo);
        if (pick is null || pick.Spell == "skip" || _spells[pick.Spell] is not { } spell)
            return false;
        if (cast.Mana > 0 && m.Mana < pick.Mana)
            return false;
        m.Mana -= pick.Mana;
        m.NextAttack = now + m.Kind.RollAttackTime(_random);
        m.Angle = AngleTowards(victim.Row - m.Row, victim.Col - m.Col);
        var room = m.Room;
        BroadcastRoom(room, GameReplies.Turn(m.Id, m.Angle));
        BroadcastRoom(room, GameReplies.Animate(ToRoomObject(m), GameReplies.AttackGroup, 4, 600));
        foreach (var a in room.Avatars.Values)
            Tell(a, $"{Article(m.Kind)} wirkt {spell.Name}!");
        victim.LastCombat = now;
        MonsterSpellEffect(m, victim, spell, cast.Power, now);
        return true;
    }

    private void MonsterSpellEffect(Monster m, Avatar victim, SpellDefinition spell, int power, DateTime now)
    {
        switch (spell.Effect)
        {
            case "attack":
            {
                // atakspel.kod: Random(min, max) × (50 + Stärke/2) / 99, Widerstand des Spielers (Art und alle Sprüche).
                int d = _random.Next(spell.MinDamage, spell.MaxDamage + 1) * (50 + power / 2) / 99;
                int resist = Math.Max(spell.AttackType is { } t ? EnchantState(victim, "resist", t) : 0, EnchantState(victim, "resistmagic"));
                HurtPlayer(victim, m, Math.Max(1, CombatRules.Resist(d, resist)), spell.Name);
                break;
            }
            case "hold":
            {
                int d = 2000 + 40 * power;
                Tell(victim, "Deine Muskeln erstarren – du kannst dich nicht mehr bewegen!");
                Paralyze(victim, TimeSpan.FromMilliseconds(_random.Next(d / 2, d + 1)));
                break;
            }
            case "blind":
            {
                int d = 5 + power / 6;
                Blind(victim, TimeSpan.FromSeconds(Math.Clamp(_random.Next(d / 2, d + 1), 3, 20)));
                break;
            }
            case "enfeeble":
            {
                // enfeeble.kod: d = (300 + 16 × Stärke) × 100 ms; auf Spielern hier wie bei Monstern Lähmung:
                // Treffer und Schaden × 3/4.
                int d = (300 + 16 * power) * 100;
                victim.PalsyUntil = now.AddMilliseconds(_random.Next(d / 2, d + 1));
                Tell(victim, "Eine lähmende Schwäche kriecht in deine Glieder.");
                break;
            }
            case "earthquake":
            {
                int severity = 1 + power / 25;
                foreach (var a in m.Room.Avatars.Values.ToList())
                {
                    double dr = (a.Row - m.Row) / 64.0, dc = (a.Col - m.Col) / 64.0;
                    double d2 = dr * dr + dc * dc;
                    int pct = d2 <= 64 ? 100 : d2 > 400 ? 0 : (int)(100 * (400 - d2) / 336);
                    if (pct > 0)
                        HurtPlayer(a, m, Math.Max(1, _random.Next(5, 10) * severity * pct / 100), "Das Erdbeben");
                }
                break;
            }
            case "manabomb":
            {
                // manabomb.kod: Radius 6 + Stärke/20 Felder; Monster als Wirker M = Stärke/3,
                // Verlust M/2 + (M/2) × Stärke/100.
                int radius = (6 + power / 20) * 64, mm = power / 3;
                int loss = mm / 2 + mm / 2 * power / 100;
                foreach (var a in m.Room.Avatars.Values)
                    if (Distance(m.Row, m.Col, a.Row, a.Col) <= radius && a.State.Mana > 0)
                    {
                        Tell(a, "Du spürst, wie Mana aus deinem Körper gerissen wird und in einer magischen Kettenreaktion verbrennt.");
                        ChangeMana(a, -Math.Min(loss, a.State.Mana));
                    }
                break;
            }
            case "killingfields" or "discordance":
                if (spell.Effect == "discordance")
                {
                    foreach (int sid in m.Room.Enchantments.Keys.ToList())
                        if (power > _random.Next(1, 101))
                            m.Room.Enchantments[sid] = m.Room.Enchantments[sid] with { Until = DateTime.MinValue };
                    TickRoomEnchantments(m.Room, now);
                }
                else if (!m.Room.Enchantments.ContainsKey(spell.Sid))
                    EnchantRoom(m.Room, spell, power);
                break;
        }
    }

    /// <summary>Spruchschaden eines Monsters an einem Spieler (wie ein Treffer, mit Tod).</summary>
    private void HurtPlayer(Avatar victim, Monster m, int damage, string what)
    {
        victim.Hp = Math.Max(0, victim.Hp - damage);
        Tell(victim, $"{what} trifft dich für {damage} Schaden.");
        victim.Session.SendHealth();
        if (victim.Hp == 0)
            KillPlayer(victim, m);
    }

    /// <summary>blind.kod auf Spielern: EFFECT_BLIND, nach Ablauf EFFECT_SEE.</summary>
    private void Blind(Avatar a, TimeSpan duration)
    {
        a.BlindUntil = DateTime.UtcNow + duration;
        a.Session.Post(GameReplies.Effect(GameReplies.EffectBlind));
        Tell(a, "Du bist geblendet!");
    }

    private void TickAfflictions(Avatar a, DateTime now)
    {
        if (a.BlindUntil is { } b && now >= b)
            Unblind(a);
        if (a.PalsyUntil is { } p && now >= p)
        {
            a.PalsyUntil = null;
            Tell(a, "Die Schwäche weicht aus deinen Gliedern.");
        }
    }

    private void Unblind(Avatar a)
    {
        a.BlindUntil = null;
        a.Session.Post(GameReplies.Effect(GameReplies.EffectSee));
        Tell(a, "Du kannst wieder sehen.");
    }
}
