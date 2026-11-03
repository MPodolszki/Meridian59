namespace Meridian.Server;

/// <summary>
/// Gebietsregeln aus den Gebietsdateien: Portal der Neulingsinsel (newbport.kod) und die Übungsregel der
/// Neulingsräume (newbN.kod ModifyChanceToImprove). Siehe docs/raza.md.
/// </summary>
public sealed partial class World
{
    /// <summary>newbport.kod TeleportSomething: erstes Betreten warnt, nach Verlassen und erneutem Betreten geht es
    /// aufs Festland mit voller Ausdauer und der Ausrüstung aus AddRealWorldObjects.</summary>
    /// <summary>Zonen-Übergang (SomethingMoved → UtilGoNearSquare): beim Betreten eines Feldes der Zone in den
    /// Zielraum, Blickrichtung wie angegeben.</summary>
    private MoveResult? RegionExit(Avatar a, ushort row, ushort col)
    {
        var exits = a.Room.Definition.RegionExits;
        if (exits.Count == 0)
            return null;
        int r = row / 64, c = col / 64;
        if (r == a.Row / 64 && c == a.Col / 64)
            return null;
        var exit = exits.FirstOrDefault(e => e.Contains(r, c));
        if (exit is null)
            return null;
        if (!_rooms.TryGetValue(exit.To, out var target))
            return new MoveResult.Blocked("Dieser Weg ist noch nicht begehbar.");
        Remove(a);
        a.Angle = RotateAngle(a.Angle, exit.Angle);
        Place(a, target, Room.Fine(exit.ToRow), Room.Fine(exit.ToCol));
        return new MoveResult.ChangedRoom(target);
    }

    /// <summary>user.kod SetRandomHomeroom: Marion 3, Cor Noth 4, Jasper 3 von 10 (nur vorhandene Gasthäuser).</summary>
    private int RandomHometown()
    {
        int roll = _random.Next(1, 11);
        int room = roll < 4 ? 202 : roll > 7 ? 370 : 153;
        return _rooms.ContainsKey(room) ? room : RespawnRoom.Definition.Id;
    }

    /// <summary>AdminGoToSafety: das Heimatgasthaus; ohne gültige Heimat wird eine ausgewürfelt.</summary>
    private Room HomeRoom(Avatar a)
    {
        if (a.State.Hometown is not { } h || !_rooms.TryGetValue(Math.Abs(h), out var room))
        {
            a.State.Hometown = RandomHometown();
            room = _rooms.GetValueOrDefault(a.State.Hometown.Value) ?? RespawnRoom;
        }
        return room;
    }

    /// <summary>Wohin man nach dem Tod bzw. einer Rettung kommt: Raza hat eigene Regeln, sonst die Heimatstadt.</summary>
    private Room SafePlace(Avatar a) =>
        a.Room.Definition.RespawnRoom is { } r && _rooms.TryGetValue(r, out var own) ? own : HomeRoom(a);

    /// <summary>Ahnenhalle von Cor Noth (Coruniv.kod SomeoneSaid): wer den Schwur seiner Stadt spricht, legt die
    /// Heimatstadt fest – nur einmal.</summary>
    private static readonly (string Oath, int Room, string Reply)[] HometownOaths =
    [
        ("Ich bin in den alten Straßen von Tos aufgewachsen.", 52, "Gewieft und bereit für Ruhm und Reichtum trägst du Tos stolz als deine Heimat."),
        ("Gutes Barloque, deine königlichen Straßen seien meine Heimat.", 106, "Bleib deinem edlen Blut treu."),
        ("Ich vertraue mein Wissen der freien Stadt Cor Noth an.", 153, "Vergiss nicht, deine Studien in deiner Beschreibung zu vermerken."),
        ("In Einigkeit stehen wir in Jasper.", 370, "Suche und vertraue nur deinen Mitbürgern aus Jasper."),
        ("Marion, trautes Heim.", 202, "Sei freundlich zu allen, auch zu denen, die den städtischen Weg gehen."),
    ];

    private void HometownOath(Avatar a, string text)
    {
        if (a.Room.Definition.KodClass != "CornothUniversity")
            return;
        var oath = HometownOaths.FirstOrDefault(o => string.Equals(o.Oath.TrimEnd('.'), text.Trim().TrimEnd('.'), StringComparison.OrdinalIgnoreCase));
        if (oath.Oath is null || !_rooms.ContainsKey(oath.Room))
            return;
        if (a.State.Hometown is < 0)
        {
            Tell(a, "Du kannst deine Heimatstadt nicht mehr ändern – sie ist bereits gewählt.");
            return;
        }
        a.State.Hometown = -oath.Room;
        Tell(a, oath.Reply);
    }

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
        a.State.Hometown ??= target.Definition.Id;   // SetRandomHomeroom: das Gasthaus, in dem man ankommt
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
