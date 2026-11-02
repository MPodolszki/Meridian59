using Meridian.Protocol;

namespace Meridian.Server;

/// <summary>
/// Persönliche Verzauberungen (persench.kod und Unterklassen): Segen (bless.kod), Superstärke (strength.kod),
/// Eile (haste.kod) und Widerstände (resist.kod). Zustand und Ende liegen in <see cref="CharacterState.Enchantments"/>,
/// der Client zeigt sie über BP_ADD/REMOVE_ENCHANTMENT. Formeln in docs/magic.md.
/// </summary>
public sealed partial class World
{
    private static bool IsEnchantment(SpellDefinition s) => s.Effect is "bless" or "superstrength" or "haste" or "resist";

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
            _ => power,                                        // Segen, Eile: Spruchstärke
        };
        // haste.kod: 5 + 15 × Stärke / 100 Minuten (ganzzahlig); sonst Basis + Stärke × je Punkt Sekunden.
        int duration = spell.Effect == "haste"
            ? (5 + 15 * power / 100) * 60_000
            : (spell.DurationBase + power * spell.DurationPerPower) * 1000;
        target.State.Enchantments[spell.Sid] = new Enchantment(state, DateTime.UtcNow.AddMilliseconds(_random.Next(duration / 2, duration + 1)));
        Tell(target, spell.CastText ?? "Dein Körper kribbelt vor magischer Energie.");
        if (caster != target)
            Tell(caster, string.Format(spell.HitText ?? "Du verzauberst {0}.", target.Name));
        target.Session.Post(GameReplies.AddEnchantment(ToSpellObject(target.Session, spell)));
    }

    private void EndEnchantment(Avatar a, SpellDefinition spell, bool report)
    {
        if (!a.State.Enchantments.Remove(spell.Sid))
            return;
        if (report)
            Tell(a, spell.OffText ?? "Du spürst, wie dein magisches Feld knistert und vergeht.");
        a.Session.Post(GameReplies.RemoveEnchantment(spell.ObjectId));
    }

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
