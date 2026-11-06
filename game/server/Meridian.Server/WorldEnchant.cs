using Meridian.Protocol;

namespace Meridian.Server;

/// <summary>
/// Persönliche Verzauberungen (persench.kod und Unterklassen): Segen (bless.kod), Superstärke (strength.kod),
/// Eile (haste.kod) und Widerstände (resist.kod). Zustand und Ende liegen in <see cref="CharacterState.Enchantments"/>,
/// der Client zeigt sie über BP_ADD/REMOVE_ENCHANTMENT. Formeln in docs/magic.md.
/// </summary>
public sealed partial class World
{
    private static bool IsEnchantment(SpellDefinition s) =>
        s.Effect is "bless" or "superstrength" or "haste" or "resist" or "gort" or "resistmagic" or "karahol" or "nightvision"
        || MoreEnchantments.Contains(s.Effect);

    /// <summary>CanPayCosts: auf sich selbst darf man neu wirken (CanRecastSelfEnchantment = TRUE), auf andere nicht.</summary>
    private bool CanEnchant(Avatar caster, Avatar target, SpellDefinition spell)
    {
        if (!target.State.Enchantments.ContainsKey(spell.Sid) || caster == target)
            return true;
        Tell(caster, $"{target.Name} steht bereits unter diesem Zauber.");
        Tell(target, $"{caster.Name} versuchte, {spell.Name} auf dich zu wirken.");
        return false;
    }

    /// <summary>CastSpell + StartEnchantment: Zustand bestimmen, Dauer würfeln, Symbol schicken.</summary>
    private void Enchant(Avatar caster, Avatar target, SpellDefinition spell, int power)
    {
        if (target.State.Enchantments.ContainsKey(spell.Sid))
            EndEnchantment(target, spell, report: false);
        int state = spell.Effect switch
        {
            "superstrength" => power / 5 + 1,                 // AddMight
            "resist" => power / 2 + spell.ResistBase,         // GetResistanceStrength
            "resistmagic" => power / 3 + 7,                   // resmagic.kod
            "nightvision" => power + 50,                      // nightv.kod GainLight
            "invisibility" => 25 + power,                     // invis.kod: Abwehr + 25 + Stärke
            _ => power,                                        // Segen, Eile: Spruchstärke
        };
        // haste.kod: 5 + 15 × Stärke / 100 Minuten (ganzzahlig); sonst Basis + Stärke × je Punkt Sekunden.
        int duration = spell.Effect == "haste"
            ? (5 + 15 * power / 100) * 60_000
            : (spell.DurationBase + power * spell.DurationPerPower) * 1000;
        // gort.kod: fest (Stärke/8 + 1) Minuten; sonst Random(Dauer/2, Dauer).
        int ms = spell.Effect switch
        {
            "gort" => (power / 8 + 1) * 60_000,
            "karahol" => (40 + power / 2) * 1000,            // karahol.kod: fest
            _ => _random.Next(duration / 2, duration + 1),
        };
        if (MoreEnchantment(target, spell, power) is { } more)
            (state, ms) = more;
        target.State.Enchantments[spell.Sid] = new Enchantment(state, DateTime.UtcNow.AddMilliseconds(ms));
        Tell(target, spell.CastText ?? "Dein Körper kribbelt vor magischer Energie.");
        if (caster != target)
            Tell(caster, string.Format(spell.HitText ?? "Du verzauberst {0}.", target.Name));
        target.Session.Post(GameReplies.AddEnchantment(ToSpellObject(target.Session, spell)));
        if (spell.Effect == "nightvision")
            target.Session.Post(GameReplies.LightPlayer(PlayerLight(target)));
    }

    /// <summary>Eigenes Licht (Nachtsicht) für BP_PLAYER und BP_LIGHT_PLAYER.</summary>
    public byte PlayerLight(Avatar a) => (byte)Math.Min(EnchantState(a, "nightvision"), 255);

    private void EndEnchantment(Avatar a, SpellDefinition spell, bool report)
    {
        if (!a.State.Enchantments.Remove(spell.Sid, out var old))
            return;
        if (report)
            Tell(a, spell.OffText ?? "Du spürst, wie dein magisches Feld knistert und vergeht.");
        a.Session.Post(GameReplies.RemoveEnchantment(spell.ObjectId));
        if (spell.Effect == "nightvision")
            a.Session.Post(GameReplies.LightPlayer(PlayerLight(a)));
        EndMoreEnchantment(a, spell, old.State);
        // karahol.kod EndEnchantment: danach gelähmt für ein Sechstel der Dauer (ohne Schutz durch freie Bewegung).
        if (spell.Effect == "karahol" && report)
            Paralyze(a, TimeSpan.FromMilliseconds((40 + old.State / 2) * 1000 / 6));
    }

    private void Paralyze(Avatar a, TimeSpan duration)
    {
        a.HeldUntil = DateTime.UtcNow + duration;
        BreakTrance(a);
        a.Session.Post(GameReplies.Effect(GameReplies.EffectParalyze));
    }

    private void TickHeld(Avatar a, DateTime now)
    {
        if (a.HeldUntil is not { } until || now < until)
            return;
        Release(a);
    }

    private void Release(Avatar a)
    {
        a.HeldUntil = null;
        a.Session.Post(GameReplies.Effect(GameReplies.EffectRelease));
        Tell(a, "Du kannst dich wieder bewegen.");
    }

    /// <summary>Gelähmt: keine Handlung (hold.kod). true = blockiert.</summary>
    private bool IsHeld(Avatar a)
    {
        if (a.HeldUntil is null)
            return false;
        Tell(a, "Du bist wie gelähmt und kannst dich nicht rühren.");
        return true;
    }

    /// <summary>Schirm (umbrella.kod): Abwehr + 250; Unsichtbarkeit (invis.kod): + 25 + Stärke.</summary>
    private int DefenseBonus(Avatar a) => (EnchantState(a, "umbrella") > 0 ? 250 : 0) + EnchantState(a, "invisibility") + MoreDefense(a);

    /// <summary>Schirm gegen Untote: Schaden − Schaden × (Stärke/20)/10, mindestens 1.</summary>
    private int UmbrellaReduce(Avatar a, MonsterKind attacker, int damage)
    {
        int state = EnchantState(a, "umbrella");
        if (state == 0 || !attacker.Undead)
            return damage;
        return Math.Clamp(damage - damage * (state / 20) / 10, 1, damage);
    }

    /// <summary>Unsichtbar (PFLAG_INVISIBLE): Monster bemerken den Spieler nicht von selbst.</summary>
    private bool Invisible(Avatar a) => EnchantState(a, "invisibility") > 0 || a.State.Inventory.Any(i => i.Used && i.Kind.Invisible);

    private static Item? BerserkRing(Avatar a) => a.State.Inventory.FirstOrDefault(i => i.Used && i.Kind.Berserk);

    /// <summary>Ein Schlag mit dem Berserkerring kostet eine Ladung; leer zerbricht er.</summary>
    private void WearRing(Avatar a, Item ring, string broken)
    {
        ring.Charges = (ring.Charges ?? 1) - 1;
        if (ring.Charges > 0)
            return;
        UnuseLocked(a, ring, quiet: true);
        Tell(a, broken);
        TakeNumber(a, ring, 1);
    }

    /// <summary>Ring der Unsichtbarkeit: jede Minute im Gebrauch eine Ladung (RINGINVISIBILITY_CHARGE_RATE).</summary>
    private void TickRings(Avatar a, DateTime now)
    {
        if (a.State.Inventory.FirstOrDefault(i => i.Used && i.Kind.Invisible) is not { } ring)
        {
            a.NextRingCharge = default;
            return;
        }
        if (a.NextRingCharge == default)
            a.NextRingCharge = now.AddMinutes(1);
        else if (now >= a.NextRingCharge)
        {
            a.NextRingCharge = now.AddMinutes(1);
            WearRing(a, ring, "Der Ring der Unsichtbarkeit zerfällt, du bist wieder sichtbar.");
        }
    }

    /// <summary>Kara'hols Fluch (karahol.kod): Treffer + 100 + 2 × Stärke.</summary>
    private int KaraholHit(Avatar a) => EnchantState(a, "karahol") is > 0 and var s ? 100 + 2 * s : 0;

    /// <summary>Kara'hols Fluch: Schaden + 1 + Random(0, Stärke/20).</summary>
    private int KaraholDamage(Avatar a) => EnchantState(a, "karahol") is > 0 and var s ? 1 + _random.Next(0, s / 20 + 1) : 0;

    /// <summary>Abgelaufene Verzauberungen beenden (Enchantment-Timer).</summary>
    private void TickEnchantments(Avatar a, DateTime now)
    {
        if (a.State.Enchantments.Count == 0)
            return;
        foreach (var (sid, e) in a.State.Enchantments.ToList())
            if (now >= e.Until && _spells.BySid(sid) is { } spell)
                EndEnchantment(a, spell, report: true);
    }

    /// <summary>Summe der Zustände aller aktiven Verzauberungen einer Wirkung (für Widerstände: nur dieser Art).</summary>
    private int EnchantState(Avatar a, string effect, string? attackType = null)
    {
        int sum = 0;
        foreach (var (sid, e) in a.State.Enchantments)
            if (_spells.BySid(sid) is { } s && s.Effect == effect && (attackType is null || s.AttackType == attackType))
                sum += e.State;
        // Widerstandsringe (resring.kod AddResistances), solange getragen.
        if (effect == "resist" && attackType is not null)
            sum += a.State.Inventory.Where(i => i.Used && i.Kind.ResistType == attackType).Sum(i => i.Kind.ResistValue);
        return sum;
    }

    /// <summary>Stärke einschließlich Superstärke (AddMight ohne Grenzen, piMightMod).</summary>
    private int EffectiveMight(Avatar a) => Math.Max(a.State.Attributes.Might + EnchantState(a, "superstrength"), 1);

    /// <summary>bless.kod ModifyDamage: + Random(0, Zustand/33).</summary>
    private int BlessDamage(Avatar a)
    {
        int bless = EnchantState(a, "bless");
        return bless > 0 ? _random.Next(0, bless / 33 + 1) : 0;
    }

    /// <summary>Für BP_SEND_ENCHANTMENTS (Typ 1): alle aktiven Verzauberungen als Symbole.</summary>
    public IReadOnlyList<RoomObject> EnchantmentObjects(Avatar a)
    {
        lock (_gate)
            return a.State.Enchantments.Keys.Select(_spells.BySid).OfType<SpellDefinition>()
                .Select(s => ToSpellObject(a.Session, s)).ToList();
    }
}
