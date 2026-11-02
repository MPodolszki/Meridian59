namespace Meridian.Server;

/// <summary>
/// Spielzeit wie im Original (util/system.kod RecalcLightAndWeather/NewGameHour): ein Spieltag dauert 2 echte
/// Stunden, eine Spielstunde 5 Minuten, gerechnet aus der UTC-Zeit minus 5 Stunden. Daraus ergeben sich
/// Helligkeit (15–75 %) und Raumlicht (room.kod GetRoomLight).
/// </summary>
public static class GameClock
{
    /// <summary>Spielstunde 0..23 zur Zeit <paramref name="utc"/>.</summary>
    public static int Hour(DateTime utc)
    {
        long seconds = (long)(utc - DateTime.UnixEpoch).TotalSeconds - 5 * 3600;
        long minutes = (seconds % (2 * 3600) + 2 * 3600) % (2 * 3600) / 60;
        return (int)(minutes / 5);
    }

    /// <summary>Helligkeit in Prozent: um 2 Stunden verschoben, linear bis Mittag (14 Uhr), begrenzt auf 15..75.
    /// Blakods „mod“ liefert abs(a % b) (sendmsg.c), daher ergibt Stunde 0 die Lichtstunde 2.</summary>
    public static int Brightness(int hour)
    {
        int lightHour = Math.Abs((hour - 2) % 24);
        int b = lightHour <= 12 ? lightHour * 100 / 12 : (24 - lightHour) * 100 / 12;
        return Math.Clamp(b, 15, 75);
    }

    /// <summary>GetRoomLight: Grundlicht + Außenanteil × (Helligkeit − 50) / 4, begrenzt auf 0..255.</summary>
    public static byte RoomLight(RoomDefinition room, int hour) =>
        (byte)Math.Clamp((room.BaseLight ?? 200) + room.OutsideFactor * (Brightness(hour) - 50) / 4, 0, 255);

    /// <summary>Nacht für die Monsterräume von Tos (tosgrave.kod/toscrypt.kod TryCreateMonster).</summary>
    public static bool IsNight(int hour) => hour < 5 || hour > 21;
}
