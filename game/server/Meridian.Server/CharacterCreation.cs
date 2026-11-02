using Meridian.Protocol;

namespace Meridian.Server;

/// <summary>Aussehen eines Charakters (player.kod prHead … prToupee, Farbübersetzungen, Geschlecht).</summary>
public sealed record Appearance(int Gender, string Head, string Hair, string Eyes, string Nose, string Mouth,
    byte HairXlat, byte SkinXlat)
{
    public const int Male = 1;
    public const int Female = 2;

    /// <summary>Standard-Charakter (system.kod charinfo_*_ax, Haare ptac), wenn keine Erstellung stattfand.</summary>
    public static Appearance Default { get; } = new(Male, "phax.bgf", "ptac.bgf", "peax.bgf", "pnax.bgf", "pmax.bgf", 0, 1);

    public string Body => Gender == Female ? "btb.bgf" : "bta.bgf";
    public string LeftArm => Gender == Female ? "blb.bgf" : "bla.bgf";
    public string RightArm => Gender == Female ? "brb.bgf" : "bra.bgf";
    public string Legs => Gender == Female ? "bfb.bgf" : "bfa.bgf";
}

/// <summary>
/// Charaktererstellung wie util/system.kod SendCharInfo/BP_NEW_CHARINFO und player.kod (Konstruktion):
/// Auswahllisten für Gesicht/Haare/Farben, Prüfung von Name, Werten und Startsprüchen.
/// </summary>
public static class CharacterCreation
{
    public const byte SendCharInfo = 49;
    public const byte NewCharInfo = 48;
    public const int MinNameLength = 3;
    public const int MaxNameLength = 30;
    public const int MaxDescriptionLength = 1000;
    /// <summary>Punkte für Startsprüche/-fertigkeiten: Stufe 1 kostet 10, Stufe 2 kostet 25 (player.kod).</summary>
    public const int SpellBudget = 45;

    /// <summary>14 Haarfarben (PT_GRAY_TO_*) und 4 Hautfarben (PT_BLUE_TO_SKIN*).</summary>
    public static readonly byte[] HairXlats = [0, 0x0A, 0x12, 0x1A, 0x1B, 0x1C, 0x1D, 0x1E, 0x30, 0x22, 0x2A, 0x2B, 0x2C, 0x2F];
    public static readonly byte[] SkinXlats = [1, 2, 3, 4];

    public sealed record FaceChoices(string[] Hair, string Head, string[] Eyes, string[] Noses, string[] Mouths);

    public static FaceChoices Choices(int gender) => gender == Appearance.Female
        ? new(["ptcd.bgf", "ptbc.bgf", "ptca.bgf", "ptdb.bgf", "ptbd.bgf", "ptcb.bgf", "ptdc.bgf", "ptdr.bgf", "ptxb.bgf", "blank.bgf"],
            "phkx.bgf", ["pekx.bgf", "pelx.bgf", "pemx.bgf"], ["pnkx.bgf", "pnlx.bgf", "pnmx.bgf"], ["pmkx.bgf", "pmlx.bgf", "pmmx.bgf"])
        : new(["ptcd.bgf", "ptac.bgf", "ptba.bgf", "ptad.bgf", "ptbb.bgf", "blank.bgf", "ptxa.bgf"],
            "phax.bgf", ["peax.bgf", "pebx.bgf", "pecx.bgf", "pedx.bgf"], ["pnax.bgf", "pnbx.bgf", "pncx.bgf"], ["pmax.bgf", "pmbx.bgf", "pmcx.bgf"]);

    /// <summary>Alle Grafiken, die in Auswahl und Darstellung vorkommen (für BP_CHANGE_RESOURCE).</summary>
    public static IEnumerable<string> AllIcons =>
        new[] { Appearance.Male, Appearance.Female }.SelectMany(g =>
        {
            var c = Choices(g);
            return c.Hair.Concat([c.Head]).Concat(c.Eyes).Concat(c.Noses).Concat(c.Mouths);
        }).Concat(["bta.bgf", "btb.bgf", "bla.bgf", "blb.bgf", "bra.bgf", "brb.bgf", "bfa.bgf", "bfb.bgf"]).Distinct();

    /// <summary>Spruchkosten bei der Erstellung.</summary>
    public static int SpellCost(SpellDefinition s) => s.Level >= 2 ? 25 : 10;

    /// <summary>OfferToNewCharacters: Stufe ≤ 2 und eine der Schulen Shal'ille, Qor, Kraanan, Faren.</summary>
    public static bool Offered(SpellDefinition s) => s.Level <= 2 && s.School is >= 1 and <= 4;

    /// <summary>BP_CHARINFO.</summary>
    public static byte[] CharInfo(Func<string, uint> rsc, IReadOnlyList<SpellDefinition> spells)
    {
        var w = new PacketWriter((byte)GameMessage.CharInfo).U8((byte)HairXlats.Length);
        foreach (byte x in HairXlats)
            w.U8(x);
        w.U8((byte)SkinXlats.Length);
        foreach (byte x in SkinXlats)
            w.U8(x);
        foreach (int gender in new[] { Appearance.Male, Appearance.Female })
        {
            var c = Choices(gender);
            WriteIcons(c.Hair);
            w.U32(rsc(c.Head));
            WriteIcons(c.Eyes);
            WriteIcons(c.Noses);
            WriteIcons(c.Mouths);
        }
        var offered = spells.Where(Offered).ToList();
        w.U32((uint)offered.Count);
        foreach (var s in offered)
            w.U32((uint)s.Sid).U32(rsc(s.Name)).U32(rsc(s.Description ?? s.Name)).U32((uint)SpellCost(s)).U8((byte)s.School);
        return w.U32(0).ToArray();   // noch keine Fertigkeiten

        void WriteIcons(string[] icons)
        {
            w.U32((uint)icons.Length);
            foreach (string i in icons)
                w.U32(rsc(i));
        }
    }

    public static byte[] CharInfoOk(uint characterId) =>
        new PacketWriter((byte)GameMessage.CharInfoOk).U32(characterId).ToArray();

    public static byte[] CharInfoNotOk() => new PacketWriter((byte)GameMessage.CharInfoNotOk).ToArray();

    /// <summary>Daten aus BP_NEW_CHARINFO.</summary>
    public sealed record Request(uint CharacterId, string Name, string Description, int Gender, uint[] Parts,
        byte HairXlat, byte SkinXlat, int[] Stats, uint[] Spells);

    public static Request Parse(ref PacketReader r)
    {
        uint id = r.U32() & ObjectId.Mask;
        string name = r.Str(), desc = r.Str();
        int gender = r.U8();
        var parts = new uint[r.U16()];
        for (int i = 0; i < parts.Length; i++)
            parts[i] = r.U32();
        byte hair = r.U8(), skin = r.U8();
        var stats = new int[r.U16()];
        for (int i = 0; i < stats.Length; i++)
            stats[i] = (int)r.U32();
        var spells = new uint[r.U16()];
        for (int i = 0; i < spells.Length; i++)
            spells[i] = r.U32();
        int skills = r.U16();
        for (int i = 0; i < skills; i++)
            r.U32();
        return new(id, name, desc, gender, parts, hair, skin, stats, spells);
    }

    private const string NameChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz1234567890_ '!@$^&*()+=:[]{};/?|<>";
    private static readonly string[] GodNames = ["Faren", "Shal'ille", "Qor", "Kraanan", "Riija", "Jala", "You", "guild"];

    /// <summary>Namensprüfung wie system.kod (Länge, Zeichen, Götternamen); Eindeutigkeit prüft der Aufrufer.</summary>
    public static bool ValidName(string name) =>
        name.Length is >= MinNameLength and <= MaxNameLength
        && name.All(NameChars.Contains)
        && !GodNames.Any(g => string.Equals(g, name, StringComparison.OrdinalIgnoreCase))
        && !name.Contains("guardian angel", StringComparison.OrdinalIgnoreCase)
        && !name.Contains("guardianangel", StringComparison.OrdinalIgnoreCase);

    /// <summary>player.kod: Werte je 1..50, zusammen höchstens 200, sonst die „gehackte“ Belegung 3/1/4/1/5/9.</summary>
    public static Attributes Stats(int[] s) =>
        s.Length == 6 && s.All(v => v is >= 1 and <= 50) && s.Sum() <= 200
            ? new Attributes(s[0], s[1], s[2], s[3], s[4], s[5])
            : new Attributes(3, 1, 4, 1, 5, 9);

    /// <summary>Gesichtsteile [Kopf, Haare, Augen, Nase, Mund] auf die erlaubten Listen beschränken (RestrictToResourceList).</summary>
    public static Appearance Face(Request req, Func<uint, string?> name)
    {
        int gender = req.Gender == Appearance.Female ? Appearance.Female : Appearance.Male;
        var c = Choices(gender);
        string Pick(int index, string[] allowed)
        {
            string? n = index < req.Parts.Length ? name(req.Parts[index]) : null;
            return n is not null && allowed.Contains(n) ? n : allowed[0];
        }
        byte hair = HairXlats.Contains(req.HairXlat) ? req.HairXlat : (byte)0;
        byte skin = SkinXlats.Contains(req.SkinXlat) ? req.SkinXlat : SkinXlats[0];
        return new Appearance(gender, Pick(0, [c.Head]), Pick(1, c.Hair), Pick(2, c.Eyes), Pick(3, c.Noses), Pick(4, c.Mouths), hair, skin);
    }
}
