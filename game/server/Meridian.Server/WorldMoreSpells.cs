using Meridian.Protocol;

namespace Meridian.Server;

/// <summary>
/// Weitere Sprüche (Teil 3, docs/magic.md): Schutz-Verzauberungen (Gift widerstehen, Freie Bewegung, Magischer Schild,
/// Ablenken, Adleraugen, Schattengestalt, Unsichtbares/Böses/Gutes erkennen, Umhang, Verleugnung, Namenlosigkeit),
/// Fluch lösen, Krankheit heilen, Köder, Umnachtung, Mal der Schande, Waffenruhe, Hitze, Illusion bannen, Erkennen.
/// Sprüche nur gegen andere Spieler (Vergessen, Spröde, Zerschmettern, Tausch) verbietet der Schutzengel, solange es
/// kein PvP gibt. Formeln aus docs/kod/spells_catalog.json.
/// </summary>
public sealed partial class World
{
    /// <summary>Persönliche Verzauberungen dieses Teils (persench.kod-Unterklassen).</summary>
    private static readonly HashSet<string> MoreEnchantments =
    [
        "resistpoison", "freeaction", "magicshield", "deflect", "detectinvisible", "detectevil", "detectgood",
        "eagleeyes", "shadowform", "cloak", "denial", "anonymity",
    ];

    /// <summary>Zustand und Dauer (ms) der neuen Verzauberungen; null = nicht aus diesem Teil.</summary>
    private (int State, int Ms)? MoreEnchantment(Avatar target, SpellDefinition spell, int power)
    {
        int Half(int d) => _random.Next(d / 2, d + 1);
        int Standard() => Half((spell.DurationBase + power * spell.DurationPerPower) * 1000);
        return spell.Effect switch
        {
            "resistpoison" => (Math.Clamp(power, 10, 99), _random.Next(450_000, 900_001)),   // respois.kod (Dauer ohne Stärke)
            "freeaction" => (Math.Clamp(power / 2 + 5, 10, 50), Standard()),
            "magicshield" => (50 + 2 * power, (1 + (power + 5) / 10) * 60_000),                // mshield.kod: fest
            "deflect" => (Math.Clamp(power * 3 / 4, 10, 75), Standard()),
            "detectinvisible" or "detectevil" or "detectgood" => (1, Half(2400 * (100 + power))),
            "eagleeyes" => (Math.Clamp(power + 1, 10, 100), Standard()),
            "shadowform" => (power, 60_000 + 3000 * power),
            "cloak" => (Math.Clamp(power / 2 + 5, 10, 50), Standard()),
            "denial" => (DenialHeal(target) * 1000 + power, Math.Clamp(_random.Next(10_000, 20_001) + power * 1000, 20_000, 120_000)),
            "anonymity" => (1, (power / 10 + 1) * 60_000),
            _ => null,
        };
    }

    /// <summary>denial.kod: heilt sofort voll; der Betrag kommt in den Zustand (Rückzahlung am Ende).</summary>
    private int DenialHeal(Avatar a)
    {
        int gain = Math.Max(0, a.MaxHp - a.Hp);
        a.Hp = a.MaxHp;
        a.Session.SendHealth();
        return gain;
    }

    /// <summary>Ende einer Verzauberung dieses Teils.</summary>
    private void EndMoreEnchantment(Avatar a, SpellDefinition spell, int state)
    {
        switch (spell.Effect)
        {
            case "denial":
            {
                // Rückzahlung: Gewinn − Gewinn × ((Stärke + 1)/5) / 100, nie unter 1 LP.
                int gain = state / 1000, power = state % 1000;
                int payback = gain - gain * ((power + 1) / 5) / 100;
                a.Hp = Math.Max(1, a.Hp - payback);
                a.Session.SendHealth();
                break;
            }
            case "detectevil" or "detectgood":
                a.Session.NotifyRoomChanged();   // Blinken neu berechnen (ToCliRoomContents)
                break;
        }
    }

    /// <summary>respois.kod/poison.kod: neue Vergiftung abgewehrt, wenn Random(1,100) &lt; Zustand.</summary>
    private bool ResistsPoison(Avatar a)
    {
        int state = EnchantState(a, "resistpoison");
        if (state == 0 || _random.Next(1, 101) >= state)
            return false;
        Tell(a, "Kraanans Magie wappnet dich gegen das verfluchte Gift!");
        return true;
    }

    /// <summary>freeact.kod: Festhalten ausweichen (Random(0,100) ≤ Zustand × 15/100), sonst Dauer − Dauer × Zustand/150.
    /// null = ganz ausgewichen.</summary>
    private TimeSpan? FreeAction(Avatar a, TimeSpan hold)
    {
        int state = EnchantState(a, "freeaction");
        if (state == 0)
            return hold;
        if (_random.Next(0, 101) <= state * 15 / 100)
        {
            Tell(a, "Du entgehst einem Versuch, deine Bewegung einzuschränken.");
            return null;
        }
        Tell(a, "Du spürst, wie Kraanans Magie dich zu befreien versucht.");
        return hold - hold * state / 150;
    }

    /// <summary>Eagleyes.kod: Blendung ganz abwehren (Random(0,100) ≤ Zustand × 5/100), sonst Dauer − Dauer × Zustand/200.</summary>
    private TimeSpan? EagleEyes(Avatar a, TimeSpan blind)
    {
        int state = EnchantState(a, "eagleeyes");
        if (state == 0)
            return blind;
        if (_random.Next(0, 101) <= state * 5 / 100)
        {
            Tell(a, "Die Magie Kraanans bewahrt dein Augenlicht.");
            return null;
        }
        Tell(a, "Kraanans Magie kämpft gegen den Schleier der Dunkelheit an.");
        return blind - blind * state / 200;
    }

    /// <summary>Magischer Schild (+ 50 + 2 × Stärke) und Schattengestalt (30 + 164 − Raumlicht, 10..150) auf die Abwehr.</summary>
    private int MoreDefense(Avatar a)
    {
        int def = EnchantState(a, "magicshield");
        if (EnchantState(a, "shadowform") > 0)
            def += Math.Clamp(30 + (164 - CurrentLight(a.Room, Hour(DateTime.UtcNow))), 10, 150);
        return def;
    }

    /// <summary>deflect.kod TryDeflect: Angriffszauber mit Random(0,100) &lt; Zustand auf den Wirker zurückwerfen
    /// (mit halber Stärke). true = abgelenkt.</summary>
    private bool Deflects(Avatar victim, Monster caster, SpellDefinition spell, int power)
    {
        int state = EnchantState(victim, "deflect");
        if (state == 0 || _random.Next(0, 101) >= state)
            return false;
        Tell(victim, $"Dein Ablenkzauber wirft den Spruch {Article(caster.Kind, dative: true)} zurück!");
        int d = _random.Next(spell.MinDamage, spell.MaxDamage + 1) * (50 + power / 4) / 99;
        d = Math.Max(1, CombatRules.Resist(d, caster.Kind.ResistanceTo(spell.AttackType ?? "magic")));
        caster.Hp -= d;
        if (caster.Hp <= 0)
            KillMonster(victim, caster, DateTime.UtcNow);
        return true;
    }

    /// <summary>Böses/Gutes erkennen (user.kod 2364–2387): blinkt für diesen Betrachter.</summary>
    private bool Detects(Avatar viewer, int karma, bool player)
    {
        int own = viewer.State.Karma;
        if (EnchantState(viewer, "detectevil") > 0 && own > 0 && karma < -10 && (player || Math.Abs(karma) > own))
            return true;
        return EnchantState(viewer, "detectgood") > 0 && own < 0 && karma > 10 && (player || karma > Math.Abs(own));
    }

    /// <summary>OF_FLASHING (proto.h) für erkannte böse/gute Wesen.</summary>
    public const uint FlagFlashing = 0x00040000;

    /// <summary>Monster für einen bestimmten Betrachter (Blinken durch Böses/Gutes erkennen).</summary>
    public RoomObject ToRoomObject(Monster m, Avatar viewer)
    {
        var o = ToRoomObject(m);
        return Detects(viewer, m.Kind.Karma, player: false) ? o with { Flags = o.Flags | FlagFlashing } : o;
    }

    /// <summary>Waffenruhe (truce.kod ROOM_NO_MOB_COMBAT): kein Kampf zwischen Spielern und Monstern.</summary>
    private bool Truce(Room room) => RoomEnchantState(room, "truce") > 0;

    private bool CanTargetMore(Avatar a, SpellDefinition spell, Avatar? target)
    {
        var t = target ?? a;
        switch (spell.Effect)
        {
            case "pvp":
                // Ohne PvP: wie der Schutzengel im Original (viNoNewbieOffense, HappyLand).
                Tell(a, target is null || target == a
                    ? $"Du kannst {spell.Name} nicht auf dich selbst wirken."
                    : "Dein Schutzengel lässt nicht zu, dass du andere Spieler angreifst.");
                return false;
            case "removecurse" when !t.State.Inventory.Any(i => i.Used && i.Kind.Cursed):
            case "curedisease" when !Diseased(t):
                Tell(a, string.Format(spell.Unnecessary ?? "Das ist nicht nötig.", t.Name));
                return false;
            case "denial" when a.Hp >= a.MaxHp:
                Tell(a, spell.Unnecessary ?? "Du bist kerngesund.");
                return false;
            case "truce" when a.Room.Definition.NoCombat || Truce(a.Room):
                Tell(a, spell.Unnecessary ?? "Kampf mit Monstern ist hier ohnehin unmöglich.");
                return false;
            case "heat" when a.Room.Definition.NoCombat:
                Tell(a, "Hier ist kein Kampf erlaubt.");
                return false;
            case "shadowform" when t.State.Enchantments.Keys.Any(sid => _spells.BySid(sid)?.Effect is "shadowform" or "invisibility") && t != a:
                Tell(a, "Die Schatten weigern sich zu verbergen, was du willst.");
                return false;
        }
        if (MoreEnchantments.Contains(spell.Effect) && t.State.Enchantments.ContainsKey(spell.Sid) && spell.Effect is "denial" or "anonymity")
        {
            Tell(a, spell.Effect == "denial"
                ? "So etwas kannst du dir nicht zweimal gleichzeitig vormachen."
                : $"Du stehst bereits unter {spell.Name}.");
            return false;
        }
        return true;
    }

    /// <summary>Krank: Lähmung (Palsy) durch Schwächen oder Umnachtung (Dementia).</summary>
    private static bool Diseased(Avatar a) => a.PalsyUntil is not null || a.DementedUntil is not null;

    /// <summary>Wirkung der Sprüche ohne eigene Verzauberung; true = behandelt.</summary>
    private bool ApplyMoreSpell(Avatar a, SpellDefinition spell, Avatar? target, Monster? victim, int power)
    {
        var now = DateTime.UtcNow;
        var t = target ?? a;
        switch (spell.Effect)
        {
            case var e when MoreEnchantments.Contains(e):
                if (CanEnchant(a, t, spell))
                    Enchant(a, t, spell, power);
                if (e is "detectevil" or "detectgood")
                    t.Session.NotifyRoomChanged();
                return true;
            case "removecurse":
                if (t != a)
                    Tell(a, $"Du wirkst {spell.Name} auf {t.Name}.");
                if (RemoveCurses(t))
                    Tell(t, spell.CastText ?? "Der Fluch ist gebrochen.");
                return true;
            case "curedisease":
                // cdisease.kod: ganz geheilt bei Random(1,100) < Stärke, sonst schwächer (hier: Restdauer − Stärke %).
                if (t != a)
                    Tell(a, $"Du wirkst {spell.Name} auf {t.Name}.");
                if (_random.Next(1, 101) < power)
                {
                    t.PalsyUntil = null;
                    t.DementedUntil = null;
                    Tell(t, spell.CastText ?? "Die Krankheit verlässt deinen Körper.");
                }
                else
                {
                    if (t.PalsyUntil is { } p)
                        t.PalsyUntil = now + (p - now) * (100 - power) / 100;
                    if (t.DementedUntil is { } d)
                        t.DementedUntil = now + (d - now) * (100 - power) / 100;
                    Tell(t, "Du spürst, wie die Krankheit schwächer wird.");
                }
                return true;
            case "bait":
                // bait.kod: je Monster Random(1,100) < 25 + Stärke/2 (ohne Ziel) bzw. Stärke/4 (mit Ziel).
                Tell(a, spell.CastText ?? "Du riechst plötzlich sehr appetitlich.");
                foreach (var m in a.Room.Monsters.Where(m => m.Alive && !m.Kind.Peaceful))
                    if (_random.Next(1, 101) < 25 + power / (m.Target is null ? 2 : 4))
                        m.Target = a;
                return true;
            case "dement":
            {
                // dement.kod: Dauer (300 + 16 × Stärke) × 100 ms, Random(d/2, d); Monster zaubern halb so oft.
                int d = (300 + 16 * power) * 100;
                Afflict(a, spell, victim!, m => m.DementedUntil = now.AddMilliseconds(_random.Next(d / 2, d + 1)));
                return true;
            }
            case "markofdishonor":
                // dishonor.kod: ein Monster wendet sich gegen den Wirker (TargetSwitch, Hass 1000).
                victim!.Target = a;
                Tell(a, string.Format(spell.HitText ?? "{0} ist gezeichnet.", Article(victim.Kind, capital: false, accusative: true)));
                return true;
            case "truce":
                EnchantRoom(a.Room, spell, power);
                return true;
            case "heat":
                // heat.kod: trifft nur Spieler mit PK-Erlaubnis; ohne PvP bleibt es bei der Meldung (5 Minuten).
                EnchantRoom(a.Room, spell, power);
                return true;
            case "dispelillusion":
                // dispillu.kod: wirkt nur, wenn der Wirker PK-fähig ist; ohne PvP nur die Meldung.
                Tell(a, spell.CastText ?? "Die Illusionen verblassen.");
                return true;
            case "identify":
                Identify(a, spell);
                return true;
        }
        return false;
    }

    /// <summary>identify.kod: Beschreibung mit verborgenen Eigenschaften (hier: Ladungen, Fluch, Spruchmodifikator).</summary>
    private void Identify(Avatar a, SpellDefinition spell)
    {
        Tell(a, spell.CastText ?? "Du konzentrierst dich auf deine Habe und siehst …");
        foreach (var item in a.State.Inventory.Where(i => !i.Kind.IsMoney))
        {
            var k = item.Kind;
            var notes = new List<string>();
            if (item.Charges is { } c)
                notes.Add($"{c} Ladungen");
            else if (k.ChargesMax > 1)
                notes.Add("ungenutzt");
            if (k.Cursed)
                notes.Add("verflucht");
            if (k.SpellModifier != 0)
                notes.Add($"Spruchstärke {k.SpellModifier:+0;-0}");
            if (k.Spell is { } s && _spells[s] is { } sp)
                notes.Add($"wirkt {sp.Name}");
            if (notes.Count > 0)
                Tell(a, $"{Capitalize(item.Describe())}: {string.Join(", ", notes)}.");
        }
    }
}
