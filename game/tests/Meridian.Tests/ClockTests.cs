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

/// <summary>Wegfindung der Monster auf dem Höhenraster (A*).</summary>
public sealed class RoomGridTests
{
    // 5×5, Wand in Spalte 3 mit Lücke in Zeile 5:  . . # . .
    private static RoomGrid Grid()
    {
        var floor = new int[5, 5];
        for (int r = 0; r < 4; r++)
            floor[r, 2] = -1;
        return new RoomGrid(floor);
    }

    [Fact]
    public void FindPath_GoesAroundWall()
    {
        var path = Grid().FindPath(new GridPos(1, 1), new GridPos(1, 5));
        Assert.NotNull(path);
        Assert.Equal(new GridPos(1, 5), path![^1]);
        Assert.Contains(new GridPos(5, 3), path);   // durch die Lücke
        Assert.All(path, p => Assert.True(Grid().IsWalkable(p.Row, p.Col)));
    }

    [Fact]
    public void FindPath_Unreachable_ReturnsNull()
    {
        var floor = new int[3, 3];
        for (int r = 0; r < 3; r++)
            floor[r, 1] = -1;
        Assert.Null(new RoomGrid(floor).FindPath(new GridPos(1, 1), new GridPos(1, 3)));
    }

    [Fact]
    public void Graveyard_LayoutIsLoaded_AndGeneratorsAreWalkable()
    {
        var grid = RoomGrid.Load(Path.Combine(AppContext.BaseDirectory, "data", "rooms", "tosgrave.json"), new GridPos(14, 18));
        foreach (var g in new[] { new GridPos(9, 31), new GridPos(7, 24), new GridPos(12, 6), new GridPos(23, 16) })
            Assert.True(grid.IsWalkable(g.Row, g.Col), $"Generator {g} nicht begehbar");
        Assert.NotNull(grid.FindPath(new GridPos(9, 31), new GridPos(14, 18)));
    }

    [Fact]
    public void Monster_WalksAroundWall_BackHome()
    {
        var def = new WorldDefinition(1, [new RoomDefinition(1, "Test", "test.roo") { Teleport = new GridPos(1, 5) }]);
        var world = new World(def, new ResourceRegistry(), "blank.bgf", seed: 1);
        var grid = Grid();
        world.StartRoom.Grid = grid;
        var kind = new MonsterKind("Zombie", "zombie.bgf", 10, 1, 1, 0, TimeSpan.FromHours(1), 200, 64);
        // Heim rechts der Wand, Monster steht links davon.
        var monster = world.SpawnMonster(1, kind, new GridPos(1, 5));
        monster.Row = Room.Fine(1);
        monster.Col = Room.Fine(1);

        var now = DateTime.UtcNow;
        for (int i = 0; i < 200 && (monster.Row / 64, monster.Col / 64) != (1, 5); i++)
        {
            world.Tick(now.AddMilliseconds(250 * i), 0.25);
            Assert.True(grid.IsWalkable(monster.Row / 64, monster.Col / 64), $"Monster in der Wand bei {monster.Row / 64}/{monster.Col / 64}");
        }
        Assert.Equal((1, 5), (monster.Row / 64, monster.Col / 64));
    }
}
