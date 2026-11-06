using Meridian.Protocol;

namespace Meridian.Server;

/// <summary>Lied, das auf einem Raum liegt (jala.kod): Barde, Spruchstärke, Lautstärke, nächste Manazahlung und die
/// Anwesenden, die von Frohsinn/Schwermut erfasst wurden.</summary>
public sealed class RoomSong(SpellDefinition spell, Avatar singer, int power, int volume)
{
    public SpellDefinition Spell => spell;
    public Avatar Singer => singer;
    public int Power => power;
    public int Volume => volume;
    public DateTime NextDrain { get; set; }
    public HashSet<uint> Moved { get; } = new();
}

/// <summary>
/// Jala-Lieder (jala.kod und Unterklassen): der Barde braucht ein Instrument und singt in Trance; das Lied liegt auf dem
/// Raum (nur eines, ein lauteres verdrängt ein leiseres: Lautstärke = Stärke × Mana je 5 s / 5) und kostet alle 5 s
/// Mana. Es endet, wenn der Barde sich bewegt, kämpft, etwas anderes zaubert, den Raum verlässt oder das Mana unter das
/// Doppelte der Zahlung fällt. Formeln in docs/magic.md.
/// </summary>
public sealed partial class World
{
    private static readonly TimeSpan DrainTime = TimeSpan.FromSeconds(5);

    /// <summary>GetInstrumentLevel: das stärkste getragene Instrument.</summary>
    private static int InstrumentLevel(Avatar a) =>
        a.State.Inventory.Where(i => i.Used).Select(i => i.Kind.InstrumentLevel).DefaultIfEmpty(0).Max();

    private RoomSong? Song(Room room, string effect) => room.Song is { } s && s.Spell.Effect == effect ? s : null;

    private void StartSong(Avatar a, SpellDefinition spell, int power)
    {
        var room = a.Room;
        int volume = Math.Max(1, power * spell.SongDrain);   // CalculateVolume (Stärke × Zahlung × 1000 / 5000, ×5)
        if (room.Song is { } old && volume <= old.Volume)
        {
            foreach (var other in room.Avatars.Values)
                Tell(other, "Ein Lied versucht, die Musik zu übertönen, aber es ist zu leise.");
            return;
        }
        if (room.Song is { } replaced)
            EndSong(room, replaced, quiet: true);
        var song = new RoomSong(spell, a, power, volume) { NextDrain = DateTime.UtcNow + DrainTime };
        room.Song = song;
        foreach (var other in room.Avatars.Values)
        {
            Tell(other, spell.CastText ?? $"{a.Name} stimmt {spell.Name} an.");
            SongTouches(song, other);
        }
    }

    /// <summary>Frohsinn/Schwermut: jeder Anwesende (und wer hinzukommt) wird mit Stärke % Chance erfasst.</summary>
    private void SongTouches(RoomSong song, Avatar a)
    {
        if (song.Spell.Effect is "mirth" or "melancholy" && _random.Next(1, 101) < song.Power)
        {
            song.Moved.Add(a.Id);
            Tell(a, song.Spell.Effect == "mirth" ? "Das Lied erfüllt dich mit fröhlichem Übermut." : "Das Lied stimmt dich traurig und grimmig.");
        }
    }

    private void TickSong(Room room, DateTime now)
    {
        if (room.Song is not { } song || now < song.NextDrain)
            return;
        var singer = song.Singer;
        if (singer.Room != room || singer.Hp <= 0 || singer.State.Mana < song.Spell.SongDrain * 2)
        {
            EndSong(room, song, quiet: false);
            return;
        }
        song.NextDrain = now + DrainTime;
        ChangeMana(singer, -song.Spell.SongDrain);
    }

    private void EndSong(Room room, RoomSong song, bool quiet)
    {
        if (room.Song != song)
            return;
        room.Song = null;
        if (!quiet)
            foreach (var a in room.Avatars.Values)
                Tell(a, song.Spell.OffText ?? "Das Lied verklingt.");
    }

    /// <summary>Der Barde bricht die Trance (Bewegung, Kampf, anderer Zauber): sein Lied endet.</summary>
    private void StopSinging(Avatar a)
    {
        EndFeignDeath(a);
        if (a.Room.Song is { } song && song.Singer == a)
            EndSong(a.Room, song, quiet: false);
    }

    /// <summary>Lieder gegen eine Schule (Wahrheit, Höflichkeit, Versöhnung, Heilige/Unheilige Resonanz, Missklang):
    /// Erfolgschance − Random(Stärke/2, 2×Stärke/3), danach 5..95.</summary>
    private int SongChance(Avatar a, SpellDefinition spell, int chance)
    {
        if (a.Room.Song is { Spell.Effect: "songbane" } song && song.Spell.TargetSchool == spell.School)
            return Math.Clamp(chance - _random.Next(song.Power / 2, song.Power * 2 / 3 + 1), 5, 95);
        return chance;
    }

    /// <summary>Zauberbann: Sprüche schwächer als das Lied scheitern (Zauber aus Gegenständen nicht).</summary>
    private bool SpellBanned(Room room, int power) => Song(room, "spellbane") is { } s && s.Power > power;

    /// <summary>Manakonvergenz: Spruchstärke + Stärke/2 + 25 für jeden Spruch im Raum.</summary>
    private int ConvergenceBonus(Room room) => Song(room, "manaconvergence") is { } s ? s.Power / 2 + 25 : 0;

    /// <summary>Zeitverzerrung: Trancezeiten × (100 + Stärke)/100.</summary>
    private TimeSpan WarpTrance(Room room, TimeSpan trance) =>
        Song(room, "warptime") is { } s ? TimeSpan.FromMilliseconds(trance.TotalMilliseconds * (100 + s.Power) / 100) : trance;

    /// <summary>Wiederherstellung (Lebenspunkte), Belebung (Ausdauer), Verjüngung (Mana): kürzere Takte.</summary>
    private TimeSpan SongRegen(Room room, string effect, TimeSpan time)
    {
        if (Song(room, effect) is not { } s)
            return time;
        double ms = time.TotalMilliseconds;
        return effect switch
        {
            "restorate" => TimeSpan.FromMilliseconds(Math.Clamp(Math.Clamp(ms, 1000, 60000) * (400 - (40 + s.Power)) / 400, 670, 60000)),
            "rejuvenate" => TimeSpan.FromMilliseconds(Math.Clamp(ms * (200 - s.Power) / 200, 500, 60000)),
            _ => TimeSpan.FromMilliseconds(ms * (200 - s.Power) / 200),
        };
    }

    /// <summary>Frohsinn (gegen Böse) bzw. Schwermut (gegen Gute): Treffer + 100, Schaden + Random(1, 5).</summary>
    private (int Hit, int Damage) SongCombat(Avatar a, Monster m)
    {
        if (a.Room.Song is not { } s || !s.Moved.Contains(a.Id))
            return (0, 0);
        bool fits = s.Spell.Effect == "mirth" && m.Kind.Karma < 0 || s.Spell.Effect == "melancholy" && m.Kind.Karma > 0;
        return fits ? (100, _random.Next(1, 6)) : (0, 0);
    }
}
