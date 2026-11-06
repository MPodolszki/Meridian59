using Meridian.Protocol;

namespace Meridian.Server;

/// <summary>
/// Fernkampf mit Bögen (ranged.kod, bow.kod, stroke/fire.kod, ammo/*.kod): Reichweite der Waffe in Feldern, Munition
/// im Köcher (fehlt sie, wird der billigste passende Pfeil eingelegt), Schaden = Pfeil + Waffe, × 90 % (Schießen),
/// Fertigkeit + (100 + Zielen − 25 (0..40)) %. Jeder Schuss kostet einen Pfeil. Siehe docs/combat.md.
/// </summary>
public sealed partial class World
{
    /// <summary>FindLikelyAmmo: eingelegte Munition, sonst die billigste im Gepäck (wird eingelegt).</summary>
    private Item? Quiver(Avatar a)
    {
        if (a.State.Inventory.FirstOrDefault(i => i.Used && i.Kind.Slot == "quiver") is { } used)
            return used;
        var cheapest = a.State.Inventory.Where(i => i.Kind.Slot == "quiver").MinBy(i => i.Kind.Value);
        if (cheapest is null)
            return null;
        cheapest.Used = true;
        a.Session.Post(GameReplies.Use(cheapest.Id));
        return cheapest;
    }

    /// <summary>fire.kod PayCosts: „Du greifst nach …“, ein Pfeil weniger.</summary>
    private void UseArrow(Avatar a, Item arrow)
    {
        Tell(a, $"Du greifst nach {arrow.Kind.Plural ?? arrow.Kind.Name} und legst einen Pfeil auf.");
        TakeNumber(a, arrow, 1);
    }

    /// <summary>Pfeilschaden (ammo GetDamage) mit Bogen, Schießen-Faktor 90, Bogenfertigkeit und Zielen; Adleraugen + 1.
    /// Neruditpfeile zersplittern mit 10 % (3–8 statt 2–6), Silberpfeile treffen Untote mit 5–10.</summary>
    private int ArrowDamage(Avatar a, Item bow, Monster m, out List<string> types)
    {
        var arrow = Quiver(a);
        types = ["pierce"];
        int d = 0;
        if (arrow is not null)
        {
            var k = arrow.Kind;
            types.AddRange(k.AttackTypes ?? []);
            d = _random.Next(k.DamageMin, k.DamageMax + 1);
            if (k.Key == "neruditearrow" && _random.Next(1, 101) <= 10)
            {
                Tell(a, "Die Neruditspitze zersplittert!");
                d = _random.Next(3, 9);
            }
            else if (k.Key == "silverarrow" && m.Kind.Undead)
                d = _random.Next(5, 11);
        }
        d += bow.Kind.WeaponDamage;
        d = d * 90 / 100;                                                    // fire.kod viDamage_factor
        int aim = Math.Clamp(a.State.Attributes.Aim - 25, 0, 40);
        d = (SkillOf(a, bow.Kind.Proficiency) + 1) * 5 / 100 + (100 + aim) * d / 100;   // DamageFactors
        if (EnchantState(a, "eagleeyes") > 0)
            d += 1;
        return d;
    }
}
