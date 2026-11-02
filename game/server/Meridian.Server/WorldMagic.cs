using Meridian.Protocol;

namespace Meridian.Server;

/// <summary>Zauber in Arbeit (Trance, spell.kod BeginCastingTrance) bzw. aktiver Berührungszauber.</summary>
public sealed record PendingCast(SpellDefinition Spell, Avatar? Target, int Power, DateTime Due);

public sealed record TouchAttack(SpellDefinition Spell, int Power, DateTime Until);

/// <summary>
/// Magie wie spell.kod/player.kod: Mana, Spruchfähigkeit, Spruchstärke, Erfolgschance, Kosten, Trance,
/// Verbesserung durch Üben und Lernen bei NPCs. Siehe docs/magic.md.
/// </summary>
public sealed partial class World
{
    private SpellBook _spells = SpellBook.Empty;

    public SpellBook Spells
    {
        get => _spells;
        init => _spells = value;
    }

    // ------------------------------------------------------------------ Charakter

    /// <summary>Werte, Mana und Startsprüche einmalig vergeben (player.kod Konstruktion: Startsprüche mit
    /// InitialAbility, Reagenzien für je 5 Zauber aus AddDefaultObjects und AddRealWorldObjects).</summary>
    private void InitMagic(Avatar a)
    {
        var s = a.State;
        if (s.MagicInitialized)
            return;
        s.MagicInitialized = true;
        s.MaxMana = MagicRules.InitialMaxMana(s.Attributes);
        s.Mana = s.MaxMana;
        foreach (var spell in _spells.Starting)
        {
            s.Spells[spell.Sid] = MagicRules.InitialAbility(spell, s.Attributes, _random);
            foreach (var r in spell.Reagents)
                if (_items.TryGet(r.Item, out var kind))
                    GiveSilently(s, kind, r.Number * 10);
        }
    }

    /// <summary>Gegenstand ohne Nachricht ins Inventar (vor dem Senden des Inventars).</summary>
    private void GiveSilently(CharacterState s, ItemKind kind, int number)
    {
        if (kind.Number && s.Find(kind) is { } stack)
            stack.Number += number;
        else
            s.Inventory.Add(NewItem(kind, number));
    }

    public IReadOnlyList<(SpellDefinition Spell, int Ability)> KnownSpells(Avatar a)
    {
        lock (_gate)
            return a.State.Spells.Select(kv => (_spells.BySid(kv.Key), kv.Value))
                .Where(x => x.Item1 is not null)
                .Select(x => (x.Item1!, x.Value)).ToList();
    }

    // ------------------------------------------------------------------ Spruchstärke

    /// <summary>GetSpellPower: Fähigkeit/2 + Schulbonus (0..30) + Nebenbonus (0..10), begrenzt 1..99.
    /// Raum-Boni (Gelände, Faren-Bonus) sind in Tos 0.</summary>
    public int SpellPower(Avatar a, SpellDefinition s)
    {
        int ability = a.State.Spells.GetValueOrDefault(s.Sid);
        int primary = 0, secondary = 0;
        switch (s.School)
        {
            case 1:   // Shal'ille: Gelände, Karma/10
                secondary = Math.Abs(a.State.Karma / 10);
                break;
            case 2:   // Qor: 2 + |Stunde − 12| × 2, Karma/10
                primary = 2 + Math.Abs(Hour(DateTime.UtcNow) - 12) * 2;
                secondary = Math.Abs(a.State.Karma / 10);
                break;
            case 3:   // Kraanan: Anzahl aktiver Objekte im Raum, Gesundheit
                primary = a.Room.Avatars.Count + a.Room.Npcs.Count + a.Room.Monsters.Count;
                secondary = a.Hp * 10 / Math.Max(a.MaxHp, 1);
                break;
            case 4:   // Faren: Raumbonus, Ausdauer/20
                secondary = a.State.Vigor / 20;
                break;
        }
        int power = ability / 2 + Math.Clamp(primary, 0, 30) + Math.Clamp(secondary, 0, 10);
        return Math.Clamp(power, MagicRules.SpellPowerMin, MagicRules.SpellPowerMax);
    }

    // ------------------------------------------------------------------ Zaubern (UserCast)

    /// <summary>BP_REQ_CAST: Spruch mit Zielen. Prüft Kosten, beginnt die Trance bzw. zaubert sofort.</summary>
    public void Cast(Avatar a, uint spellId, IReadOnlyList<ObjectRef> targets)
    {
        lock (_gate)
        {
            if (_spells.ByObjectId(spellId) is not { } spell || !a.State.Spells.ContainsKey(spell.Sid))
                return;
            if (a.Casting is not null)
            {
                Tell(a, "Du konzentrierst dich bereits auf einen Zauber.");
                return;
            }
            Avatar? target = null;
            if (spell.Targets == 1)
            {
                if (targets.Count != 1)
                    return;
                if (!a.Room.Avatars.TryGetValue(targets[0].Id, out target))
                {
                    Tell(a, $"Du kannst {spell.Name} nicht darauf wirken.");
                    return;
                }
            }

            int power = SpellPower(a, spell);
            if (!CanPayCosts(a, spell, target, power))
                return;

            var trance = MagicRules.TranceTime(spell, power);
            if (trance == TimeSpan.Zero)
            {
                Complete(a, spell, target, power);
                return;
            }
            Tell(a, $"Du konzentrierst dich ganz auf {spell.Name}.");
            a.Casting = new PendingCast(spell, target, power, DateTime.UtcNow + trance);
        }
    }

    /// <summary>CanPayCosts: spruchspezifische Prüfung, Ausdauer, Mana, Reagenzien.</summary>
    private bool CanPayCosts(Avatar a, SpellDefinition spell, Avatar? target, int power)
    {
        if (spell.Effect is "heal" or "hospice" && target is not null && target.Hp >= target.MaxHp)
        {
            Tell(a, target == a ? "Du bist kerngesund." : string.Format(spell.Unnecessary ?? "{0} ist kerngesund.", target.Name));
            return false;
        }
        if (spell.Effect == "touchattack" && a.Touch is { } t && t.Spell == spell && t.Until > DateTime.UtcNow)
        {
            Tell(a, $"Du bist bereits mit {spell.Name} verzaubert.");
            return false;
        }
        if (a.State.Vigor < spell.Exertion)
        {
            Tell(a, $"Du bist zu erschöpft, um {spell.Name} zu wirken!");
            return false;
        }
        if (a.State.Mana < MagicRules.ManaCost(spell, power))
        {
            Tell(a, $"Du hast nicht genug Mana für {spell.Name}!");
            return false;
        }
        foreach (var r in spell.Reagents)
        {
            if (!_items.TryGet(r.Item, out var kind) || (a.State.Find(kind)?.Number ?? 0) < r.Number)
            {
                Tell(a, $"Dir fehlen die Reagenzien für {spell.Name}.");
                return false;
            }
        }
        return true;
    }

    /// <summary>Nach der Trance: Erfolgswurf, Kosten (PayCosts), Wirkung (CastSpell), Üben (ImproveAbility).</summary>
    private void Complete(Avatar a, SpellDefinition spell, Avatar? target, int power)
    {
        if (target is not null && target.Room != a.Room)
        {
            Tell(a, $"Das Ziel von {spell.Name} ist nicht mehr da.");
            return;
        }
        if (!CanPayCosts(a, spell, target, power))
            return;

        int cost = MagicRules.ManaCost(spell, power);
        if (_random.Next(1, 101) > MagicRules.SuccessChance(spell, a.State.Attributes, power))
        {
            // SpellFailed: halbe Manakosten, halbe Anstrengung, keine Reagenzien.
            ChangeMana(a, -cost / 2);
            AddExertion(a, 10000 * spell.Exertion / 2);
            Tell(a, $"Dein Zauber {spell.Name} ist misslungen.");
            return;
        }
        ChangeMana(a, -cost);
        AddExertion(a, 10000 * spell.Exertion);
        foreach (var r in spell.Reagents)
            if (_items.TryGet(r.Item, out var kind) && a.State.Find(kind) is { } stack)
                TakeNumber(a, stack, r.Number);

        switch (spell.Effect)
        {
            case "heal":
            {
                int heal = Math.Clamp(_random.Next(1, 6) + (power + 1) / 20 + target!.State.Karma / 20, 1, 10);
                heal += _random.Next(2, 6);   // kein PvP-Flag gesetzt
                Heal(a, target, heal, spell);
                break;
            }
            case "hospice":
                Heal(a, target!, Math.Clamp(_random.Next(-3, 4) + power / 5 + target!.State.Karma / 10, 0, 25), spell);
                break;
            case "createfood":
                CreateFood(a, power, spell);
                break;
            case "touchattack":
            {
                // GetDuration: Random(Stärke/3, Stärke/2), begrenzt 10..75, × 6 s.
                int units = Math.Clamp(_random.Next(power / 3, power / 2 + 1), 10, 75);
                a.Touch = new TouchAttack(spell, power, DateTime.UtcNow.AddSeconds(units * 6));
                Tell(a, spell.CastText ?? $"{spell.Name} wirkt.");
                break;
            }
        }
        a.State.SchoolCasts[spell.School] = a.State.SchoolCasts.GetValueOrDefault(spell.School) + 1;
        if (spell.Effect != "touchattack")
            ImproveAbility(a, spell, null);
    }

    private void Heal(Avatar caster, Avatar target, int amount, SpellDefinition spell)
    {
        if (caster != target)
            Tell(caster, $"Du wirkst {spell.Name} auf {target.Name}.");
        Tell(target, spell.CastText ?? "Deine Wunden heilen.");
        target.Hp = Math.Min(target.MaxHp, target.Hp + amount);
        target.Session.SendHealth();
    }

    /// <summary>creafood.kod: Wurf Random(Stärke/2, Stärke) bestimmt das Essen.</summary>
    private void CreateFood(Avatar a, int power, SpellDefinition spell)
    {
        int roll = _random.Next(power / 2, power + 1);
        string key = roll < 30 ? "apple" : roll < 40 ? "bread" : roll < 60 ? "grapes" : roll < 70 ? "turkeyleg"
            : roll < 80 ? "meatpie" : roll < 95 || _random.Next(1, 4) != 1 ? "cheese" : "inkycap";
        if (!_items.TryGet(key, out var kind))
            return;
        var food = NewItem(kind, 1);
        Tell(a, string.Format(spell.CastText ?? "{0} erscheint.", Capitalize(food.Describe())));
        Give(a, food);
    }

    private static string Capitalize(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    /// <summary>Berührungszauber (touchatk.kod FindDamage) statt Waffe/Faust; null = keiner aktiv.</summary>
    private (int Damage, double HitChance, SpellDefinition Spell)? TouchStrike(Avatar a, DateTime now)
    {
        if (a.Touch is not { } t)
            return null;
        if (now >= t.Until)
        {
            a.Touch = null;
            Tell(a, t.Spell.OffText ?? $"{t.Spell.Name} wirkt nicht mehr.");
            return null;
        }
        int d = _random.Next(t.Spell.MinDamage, t.Spell.MaxDamage + 1);
        d = d / 2 + d / 2 * t.Power / MagicRules.SpellPowerMax + 1;
        // viHit_Factor 80 ist der Normalfall; Zap (50) trifft entsprechend seltener.
        return (Math.Max(d, 1), CombatRules.PlayerHitChance * t.Spell.HitFactor / 80.0, t.Spell);
    }

    // ------------------------------------------------------------------ Trance und Mana im Spieltakt

    private void TickMagic(Avatar a, DateTime now)
    {
        if (a.Casting is { } c && now >= c.Due)
        {
            a.Casting = null;
            Complete(a, c.Spell, c.Target, c.Power);
        }

        var s = a.State;
        if (s.Mana == s.MaxMana)
        {
            a.NextMana = default;
            return;
        }
        if (a.NextMana == default)
            a.NextMana = now + MagicRules.ManaTime(s.Attributes, s.Vigor, s.MaxMana);
        else if (now >= a.NextMana)
        {
            a.NextMana = now + MagicRules.ManaTime(s.Attributes, s.Vigor, s.MaxMana);
            ChangeMana(a, s.Mana < s.MaxMana ? 1 : -1);   // ManaTimer: über dem Maximum sinkt es wieder
        }
    }

    /// <summary>Bewegung bricht die Trance (BreakTrance).</summary>
    private void BreakTrance(Avatar a)
    {
        if (a.Casting is not { } c)
            return;
        a.Casting = null;
        Tell(a, $"Deine Konzentration auf {c.Spell.Name} ist gebrochen.");
    }

    private void ChangeMana(Avatar a, int delta)
    {
        if (delta == 0)
            return;
        a.State.Mana = Math.Max(0, a.State.Mana + delta);
        a.Session.SendMana();
    }

    // ------------------------------------------------------------------ Üben (ImproveAbility)

    private int TotalLearnPoints(CharacterState s, int exceptSchool) =>
        s.Spells.Keys.Select(_spells.BySid).OfType<SpellDefinition>()
            .Where(sp => sp.School != exceptSchool)
            .GroupBy(sp => sp.School)
            .Sum(g => MagicRules.LevelLearnPoints(g.Max(sp => sp.Level)));

    /// <summary>spell.kod ImproveAbility/GetInitialChance/GetSecondaryChance; <paramref name="monsterLevel"/> bei Treffern.</summary>
    private void ImproveAbility(Avatar a, SpellDefinition spell, int? monsterLevel)
    {
        var s = a.State;
        int initial = spell.ChanceToIncrease + spell.ChanceToIncrease * s.Attributes.Intellect / 100;
        if (_random.Next(1, 101) > initial)
            return;
        int ability = s.Spells.GetValueOrDefault(spell.Sid);
        if (ability is >= 99 or < 1)
            return;
        int req = MagicRules.RequisiteStat(spell, s.Attributes);
        int chance = 60 + req - spell.Level * 10 - TotalLearnPoints(s, spell.School);
        int difficulty = monsterLevel is { } lvl ? Math.Clamp(lvl, 1, 150) : 75;
        int factor = Math.Clamp(2 * difficulty - ability + 10, 50, 100);
        int final = Math.Max(chance * factor / 100, 5);
        final += s.SchoolCasts.GetValueOrDefault(spell.School) / spell.Level / MagicRules.CastsPerPercentBonus;
        if (ability > 2 * req - 1)
            final /= MagicRules.SoftcapPenalty;
        final = Math.Clamp(final, 1 + req / 10, 99);
        if (_random.Next(0, 101) >= final)
            return;
        s.Spells[spell.Sid] = ability + 1;
        s.SchoolCasts[spell.School] = 0;
        Tell(a, $"Du beherrschst {spell.Name} jetzt besser ({ability + 1} %).");
        a.Session.SendSpellStats();
    }

    // ------------------------------------------------------------------ Lernen (PlayerCanLearn)

    /// <summary>player.kod PlayerCanLearn: null = darf lernen, sonst der Grund.</summary>
    public string? CannotLearn(CharacterState s, SpellDefinition spell)
    {
        if (s.Spells.ContainsKey(spell.Sid))
            return "Diesen Spruch kennst du bereits.";
        var known = s.Spells.Select(kv => (Spell: _spells.BySid(kv.Key), Ability: kv.Value))
            .Where(x => x.Spell is not null && x.Spell.School == spell.School).ToList();
        if (known.Any(x => x.Spell!.Level == spell.Level + 1))
            return null;
        int atLevel = known.Count(x => x.Spell!.Level == spell.Level);
        bool knowOne = false;
        if (atLevel > 0)
        {
            if (spell.Level > 2 || atLevel >= 2)
                return null;
            knowOne = true;
        }
        int have = spell.Level > 1
            ? known.Where(x => x.Spell!.Level == spell.Level - 1).Select(x => x.Ability).OrderDescending().Take(3).Sum()
            : 3 * 99;
        if (have == 0)
            return $"Dir fehlen die Grundlagen der Schule für {spell.Name}.";
        int points = s.Spells.Keys.Select(_spells.BySid).OfType<SpellDefinition>()
            .GroupBy(sp => sp.School).Sum(g => MagicRules.LevelLearnPoints(g.Max(sp => sp.Level)));
        if (!knowOne)
            points += MagicRules.LevelLearnPoints(spell.Level) - MagicRules.LevelLearnPoints(spell.Level - 1);
        int need = points * MagicRules.PointsSlope + (297 - MagicRules.MaxLearnPoints * MagicRules.PointsSlope)
                   - s.Attributes.Intellect * 2 * MagicRules.PointsSlope / 5;
        need = Math.Max(need, MagicRules.MinNeededToAdvance);
        return have >= need ? null : $"Du bist in der Schule von {spell.Name} noch nicht weit genug.";
    }

    /// <summary>Spruch beim Händler kaufen (monster.kod Buy, Zweig für Sprüche): Preis ohne Aufschlag.</summary>
    private void BuySpell(Avatar a, Npc npc, SpellDefinition spell)
    {
        if (CannotLearn(a.State, spell) is { } reason)
        {
            NpcSay(npc, a.Room, reason);
            return;
        }
        if ((a.State.Find(_items.Money)?.Number ?? 0) < spell.Price)
        {
            NpcSay(npc, a.Room, $"Komm wieder, wenn du genug Geld für {spell.Name} hast.");
            return;
        }
        TakeMoney(a, spell.Price);
        a.State.Spells[spell.Sid] = MagicRules.InitialAbility(spell, a.State.Attributes, _random);
        Tell(a, $"Du hast {spell.Name} gelernt.");
        a.Session.SendSpellAdded(spell);
    }
}
