namespace Meridian.Server;

/// <summary>
/// Gebietsregeln aus den Gebietsdateien: Portal der Neulingsinsel (newbport.kod) und die Übungsregel der
/// Neulingsräume (newbN.kod ModifyChanceToImprove). Siehe docs/raza.md.
/// </summary>
public sealed partial class World
{
    /// <summary>newbport.kod TeleportSomething: erstes Betreten warnt, nach Verlassen und erneutem Betreten geht es
    /// aufs Festland mit voller Ausdauer und der Ausrüstung aus AddRealWorldObjects.</summary>
    private MoveResult? Portal(Avatar a, ushort row, ushort col)
    {
        if (a.Room.Definition.Portal is not { } portal)
            return null;
        bool on = row / 64 == portal.Row && col / 64 == portal.Col;
        bool entered = on && !a.OnPortal;
        a.OnPortal = on;
        if (!entered)
            return null;
        if (!a.PortalWarned)
        {
            a.PortalWarned = true;
            Tell(a, portal.Warning ?? "Achtung: Durch dieses Portal gibt es keinen Weg zurück.");
            return null;
        }
        if (PortalTarget(portal) is not { } target)
            return new MoveResult.Blocked("Das Portal flimmert, aber nichts geschieht.");
        a.PortalWarned = a.OnPortal = false;
        Remove(a);
        var (r, c) = target.ArrivalPoint;
        Place(a, target, r, c);
        AddExertion(a, -2_000_000);   // volle Ausdauer
        GiveRealWorldObjects(a);
        Tell(a, portal.Text ?? "Du betrittst das Portal und beginnst dein Abenteuer.");
        return new MoveResult.ChangedRoom(target);
    }

    /// <summary>user.kod SetRandomHomeroom: gewichteter Wurf über die vorhandenen Heimatstädte, sonst das feste Ziel.</summary>
    private Room? PortalTarget(PortalDefinition portal)
    {
        var known = portal.Destinations.Where(d => _rooms.ContainsKey(d.Room)).ToList();
        if (known.Count == 0)
            return _rooms.GetValueOrDefault(portal.ToRoom);
        int roll = _random.Next(known.Sum(d => d.Weight));
        return _rooms[known.First(d => (roll -= d.Weight) < 0).Room];
    }

    /// <summary>player.kod AddRealWorldObjects: 1000 Schillinge und Reagenzien für je 5 Zauber der bekannten
    /// Sprüche (Neustart-Bedingung entfällt), immer ein Streitkolben und 5 Äpfel.</summary>
    private void GiveRealWorldObjects(Avatar a)
    {
        if (_items.TryGet("money", out var money))
            Give(a, NewItem(money, 1000));
        foreach (var spell in a.State.Spells.Keys.Select(_spells.BySid).OfType<SpellDefinition>())
            foreach (var r in spell.Reagents)
                if (_items.TryGet(r.Item, out var reagent))
                    Give(a, NewItem(reagent, r.Number * 5));
        if (_items.TryGet("mace", out var mace))
            Give(a, NewItem(mace));
        if (_items.TryGet("apple", out var apple))
            Give(a, NewItem(apple, 5));
    }

    /// <summary>ModifyChanceToImprove der Neulingsräume: ab der Grenze keine Verbesserung (CANNOT_IMPROVE),
    /// sonst Chance × 3/2. Außerhalb unverändert. null = kann nicht steigen.</summary>
    private int? ModifyChanceToImprove(Avatar a, int ability, int chance)
    {
        if (a.Room.Definition.ImproveCap is not { } cap)
            return chance;
        return ability >= cap ? null : chance * 3 / 2;
    }
}
