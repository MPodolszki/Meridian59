using Meridian.Server;

namespace Meridian.Tests;

/// <summary>Spieluhr, Licht und Nachtmonster wie im Original (system.kod, room.kod, tosgrave.kod).</summary>
public sealed class ClockTests
{
    [Theory]
    [InlineData("2026-11-02T05:00:00Z", 0)]    // UTC − 5 h = Mitternacht, Zyklusbeginn
    [InlineData("2026-11-02T05:04:59Z", 0)]
    [InlineData("2026-11-02T05:05:00Z", 1)]
    [InlineData("2026-11-02T06:55:00Z", 23)]
    [InlineData("2026-11-02T07:00:00Z", 0)]    // 2 echte Stunden = 1 Spieltag
    public void Hour_FiveRealMinutesPerGameHour(string utc, int hour) =>
        Assert.Equal(hour, GameClock.Hour(DateTime.Parse(utc, null, System.Globalization.DateTimeStyles.AdjustToUniversal)));

    [Theory]
    [InlineData(0, 16)]     // abs(-2 mod 24) = 2
    [InlineData(1, 15)]
    [InlineData(2, 15)]
    [InlineData(23, 25)]
    [InlineData(8, 50)]
    [InlineData(14, 75)]
    [InlineData(20, 50)]
    public void Brightness_FollowsOriginalCurve(int hour, int brightness) => Assert.Equal(brightness, GameClock.Brightness(hour));

    [Fact]
    public void RoomLight_DependsOnOutsideFactor()
    {
        var street = new RoomDefinition(50, "Tos", "tos.roo") { BaseLight = 209, OutsideFactor = 10 };
        var tavern = new RoomDefinition(76, "Taverne", "tostavern.roo") { BaseLight = 164, OutsideFactor = 0 };
        Assert.Equal(255, GameClock.RoomLight(street, 14));   // 209 + 62, begrenzt auf LIGHT_MAX
        Assert.Equal(209 - 10 * 35 / 4, GameClock.RoomLight(street, 1));
        Assert.Equal(164, GameClock.RoomLight(tavern, 0));
    }

    [Theory]
    [InlineData(12, false)]
    [InlineData(23, true)]
    public void Graveyard_SpawnsOnlyAtNight(int hour, bool spawns)
    {
        var def = new WorldDefinition(70, [new RoomDefinition(70, "Graveyard", "tosgrave.roo") { Teleport = new GridPos(14, 18) }]);
        var world = new World(def, new ResourceRegistry(), "blank.bgf", seed: 1) { FixedHour = hour };
        var now = DateTime.UtcNow;
        for (int i = 0; i < 10; i++)
            world.Tick(now.AddSeconds(3 * i), 0.25);
        Assert.Equal(spawns, world.MonstersIn(world.StartRoom).Count > 0);
    }
}
