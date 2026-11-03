using Meridian.Protocol;

namespace Meridian.Server;

/// <summary>
/// Nahkampf nach dem Original (battler.kod TryAttack, player.kod GetOffense/GetDefense/GetDamage/AssessDamage,
/// stroke.kod, skill.kod ImproveAbility). Formeln in docs/combat-original.md.
/// </summary>
public sealed partial class World
{
    private SkillBook _skills = SkillBook.Empty;

    public SkillBook Skills
    {
        get => _skills;
        init => _skills = value;
    }

    /// <summary>Grund-Lebenspunkte (piBase_Max_Health); bisher steigen sie nicht.</summary>
    private static int BaseMaxHealth(Avatar a) => a.MaxHp;

    private static int SkillOf(Avatar a, int skid) => a.State.Skills.GetValueOrDefault(skid);

    // ------------------------------------------------------------------ Werte

    /// <summary>player.kod GetOffense: Technik×3 + Fertigkeit×2 + Zielen×4 + Grund-LP×3/2, Waffenmodifikator, 1..1000.</summary>
    public int PlayerOffense(Avatar a)
    {
        var weapon = a.Touch is null ? Weapon(a) : null;   // ein Berührungszauber zählt nicht als Waffe
        int stroke = SkillOf(a, weapon is null ? Skill.Punch : Skill.Slash);
        int prof = weapon is null ? SkillOf(a, Skill.Brawling) : SkillOf(a, weapon.Kind.Proficiency);
        int off = stroke * 3 + prof * 2 + a.State.Attributes.Aim * 4 + BaseMaxHealth(a) * 3 / 2;
        if (weapon is not null)
        {
            off += weapon.Kind.WeaponType switch { "bludgeon" => 75, "thrust" => 125, _ => 0 };
            off += weapon.Kind.WeaponQuality == "high" ? 50 : 0;
        }
        off += EnchantState(a, "bless");   // plAttack_modifiers: bless.kod ModifyHitRoll
        off += ForcesOfLightBonus(a);       // forceslt.kod ModifyHitRoll
        off += KaraholHit(a);
        off -= WindsPenalty(a);
        return Math.Clamp(off, 1, 1000);
    }

    /// <summary>player.kod GetDefense: Parieren×2 + Blocken + Ausweichen×3 + Geschick×4 + Grund-LP×3/2 + Rüstung, 1..1000.</summary>
    public int PlayerDefense(Avatar a)
    {
        var used = a.State.Inventory.Where(i => i.Used).ToList();
        bool armed = used.Any(i => i.Kind.Slot == "weapon") || a.Touch is not null;
        var shield = used.FirstOrDefault(i => i.Kind.Slot == "shield" && !i.Kind.Light);
        int parry = armed ? SkillOf(a, Skill.Parry) : 0;
        int block = shield is not null && SkillOf(a, Skill.Block) > 0
            ? Math.Clamp(SkillOf(a, Skill.Block) + shield.Kind.DefenseBonus, 1, 120) : 0;
        int def = parry * 2 + block + SkillOf(a, Skill.Dodge) * 3 + a.State.Attributes.Agility * 4 + BaseMaxHealth(a) * 3 / 2;
        def += used.Where(i => i.Kind.Slot == "armor").Sum(i => i.Kind.DefenseBonus);
        def += DefenseBonus(a);
        return Math.Clamp(def, 1, 1000);
    }

    /// <summary>Schaden eines Spielerschlags (stroke.kod FindDamage/DamageFactors) inkl. Resistenz des Monsters.</summary>
    private int PlayerDamage(Avatar a, Monster m, out string attackType)
    {
        var s = a.State;
        int mightBonus = Math.Clamp(EffectiveMight(a) - 25, 0, 40);
        int d;
        if (TouchStrike(a, DateTime.UtcNow) is { } touch)
        {
            d = touch.Damage;
            attackType = touch.Spell.AttackType ?? "magic";
        }
        else if (Weapon(a) is { } weapon)
        {
            var (min, max) = CombatRules.WeaponDamage(weapon.Kind);
            d = _random.Next(min, max + 1) * 80 / 100;                     // Hieb: viDamage_factor 80
            int profBonus = (SkillOf(a, weapon.Kind.Proficiency) + 1) * 5 / 100;
            d = profBonus + (100 + mightBonus) * d / 100;
            attackType = weapon.Kind.WeaponType ?? "slash";
            // ATCK_WEAP_NONMAGIC bzw. _MAGIC (verzauberte Waffe), dazu die Weihe heilig/unheilig.
            var types = new List<string> { attackType };
            var ench = WeaponEnchantOf(a, weapon);
            types.Add(ench == "magic" || weapon.Kind.WeaponQuality == "magic" ? "magic" : "nonmagic");
            if (ench is "holy" or "unholy")
                types.Add(ench);
            d += BlessDamage(a) + KillingFieldsBonus(a) + KaraholDamage(a);
            d = CombatRules.Resist(d, m.Kind.ResistanceTo(types));
            return Math.Max(d, 1);
        }
        else
        {
            d = _random.Next(1, 5);                                          // Faustschlag
            d = SkillOf(a, Skill.Brawling) * 5 / 100 + (100 + mightBonus) * d / 100;
            attackType = "punch";
        }
        d += BlessDamage(a);   // plAttack_modifiers: bless.kod ModifyDamage
        d += KillingFieldsBonus(a);
        d += KaraholDamage(a);
        d = CombatRules.Resist(d, m.Kind.ResistanceTo(attackType));
        return Math.Max(d, 1);
    }

    /// <summary>Weihe der Waffe, sofern noch nicht abgelaufen (sonst endet sie mit Meldung).</summary>
    private string? WeaponEnchantOf(Avatar a, Item weapon)
    {
        if (weapon.WeaponEnchant is not { } e)
            return null;
        if (DateTime.UtcNow < e.Until)
            return e.Type;
        weapon.WeaponEnchant = null;
        Tell(a, $"{Capitalize(weapon.Describe())} wirkt plötzlich wieder ganz gewöhnlich.");
        return null;
    }

    /// <summary>player.kod AssessDamage: Rüstung/Schild mindern, mind. 1, höchstens (Grund-LP+2)/3 solange
    /// LP &lt; 2 × Grund-LP, höchstens 30.</summary>
    private int AssessPlayerDamage(Avatar a, int damage)
    {
        foreach (var piece in a.State.Inventory.Where(i => i.Used && i.Kind.DamageReduce > 0))
        {
            // Schilde mindern nur, wenn Blocken gelingt (shield.kod ModifyDefenseDamage).
            if (piece.Kind.Slot == "shield" && !SkillSucceeds(a, Skill.Block, piece.Kind.DefenseBonus))
                continue;
            int r = piece.Kind.DamageReduce;
            damage -= Math.Clamp(_random.Next(r / 3, r + 1), 0, Math.Max(0, damage - 1));
        }
        damage = Math.Max(damage, 1);
        if (a.Hp < 2 * BaseMaxHealth(a))
            damage = Math.Min(damage, (BaseMaxHealth(a) + 2) / 3);
        return Math.Min(damage, 30);
    }

    /// <summary>skill.kod SuccessChance: (100 − Req) × Fähigkeit / 100 + Req + Modifikator.</summary>
    private bool SkillSucceeds(Avatar a, int skid, int modifier)
    {
        int ability = SkillOf(a, skid);
        if (ability <= 0 || _skills.BySkid(skid) is not { } skill)
            return false;
        int req = skill.RequisiteStat(a.State.Attributes);
        return _random.Next(1, 101) <= (100 - req) * ability / 100 + req + modifier;
    }

    // ------------------------------------------------------------------ Üben

    /// <summary>Advancement Points: höchstens 10 Verbesserungen je 15–22 Minuten.</summary>
    private bool CanAdvance(CharacterState s, DateTime now)
    {
        if (now >= s.AdvancementReset)
        {
            s.AdvancementPoints = 0;
            s.AdvancementReset = now.AddMilliseconds(_random.Next(900000, 1320001));
        }
        return s.AdvancementPoints < 10;
    }

    /// <summary>skill.kod ImproveAbility (inkl. des Originalfehlers, der die Fähigkeit in der Zweitchance als 0 liest).</summary>
    private void ImproveSkill(Avatar a, int skid, int? monsterLevel)
    {
        var s = a.State;
        if (_skills.BySkid(skid) is not { } skill || !s.Skills.TryGetValue(skid, out int ability) || !CanAdvance(s, DateTime.UtcNow))
            return;
        int initial = skill.ChanceToIncrease + skill.ChanceToIncrease * s.Attributes.Intellect / 100;
        if (_random.Next(1, 101) > initial || ability is >= 99 or < 1)
            return;
        int req = skill.RequisiteStat(s.Attributes);
        int chance = 60 + req - skill.Level * 10 - TotalLearnPoints(s, exceptSchool: -1);
        int difficulty = monsterLevel is { } lvl ? Math.Clamp(lvl, 1, 150) : 60;
        int factor = Math.Clamp(2 * difficulty + 10, 50, 100);
        chance = Math.Max(chance * factor / 100, 5);
        if (ModifyChanceToImprove(a, ability, chance) is not { } modified)
            return;
        chance = Math.Clamp(modified, 1 + req / 10, 99);
        if (_random.Next(0, 101) >= chance)
            return;
        s.Skills[skid] = ability + 1;
        s.AdvancementPoints++;
        Tell(a, $"Du beherrschst {skill.Name} jetzt besser ({ability + 1} %).");
        a.Session.SendSkillStats();
    }

    /// <summary>Schläge zählen (alle 75 derselben Waffenart ein Bonus) und bei Treffern Technik bzw. Waffenfertigkeit
    /// üben, solange sie unter der Monsterstufe liegt (stroke.kod ImproveStroke).</summary>
    private void PracticeStrike(Avatar a, Monster m, bool hit)
    {
        var weapon = a.Touch is null ? Weapon(a) : null;
        int prof = weapon?.Kind.Proficiency ?? Skill.Brawling;
        var swings = a.State.Swings;
        swings[prof] = swings.GetValueOrDefault(prof) + 1;
        if (!hit || swings[prof] < 75)
            return;
        swings[prof] = 0;
        int stroke = weapon is null ? Skill.Punch : Skill.Slash;
        bool trainProf = a.State.Skills.ContainsKey(prof) && _random.Next(1, 101) <= (weapon is null ? 50 : 40);
        int skid = trainProf ? prof : stroke;
        if (weapon is null || SkillOf(a, skid) < m.Kind.Level)
            ImproveSkill(a, skid, m.Kind.Level);
    }

    /// <summary>player.kod AdvancementCheck nach einem Sieg: wurde man verfehlt, Ausweichen (bzw. zu 30 % Parieren
    /// mit Waffe, sonst zu 30 % Blocken mit Schild) üben.</summary>
    private void PracticeDefense(Avatar a, Monster m)
    {
        if (a.KillTarget != m || !a.Dodged)
            return;
        int skid = Skill.Dodge;
        var used = a.State.Inventory.Where(i => i.Used).ToList();
        if (used.Any(i => i.Kind.Slot == "weapon") && a.State.Skills.ContainsKey(Skill.Parry) && _random.Next(1, 101) < 30)
            skid = Skill.Parry;
        else if (used.Any(i => i.Kind.Slot == "shield" && !i.Kind.Light) && a.State.Skills.ContainsKey(Skill.Block) && _random.Next(1, 101) < 30)
            skid = Skill.Block;
        ImproveSkill(a, skid, m.Kind.Level);
    }

    public IReadOnlyList<(SkillDefinition Skill, int Ability)> KnownSkills(Avatar a)
    {
        lock (_gate)
            return a.State.Skills.Select(kv => (_skills.BySkid(kv.Key), kv.Value))
                .Where(x => x.Item1 is not null).Select(x => (x.Item1!, x.Value)).ToList();
    }

    public RoomObject ToSkillObject(Session session, SkillDefinition skill)
    {
        session.EnsureResource(skill.Name);
        session.EnsureResource(skill.Icon);
        return new RoomObject(skill.ObjectId, _resources[skill.Name], new ObjectAppearance(_resources[skill.Icon]));
    }
}
