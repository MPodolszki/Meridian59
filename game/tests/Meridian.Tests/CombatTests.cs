using Meridian.Protocol;
using Meridian.Server;

namespace Meridian.Tests;

/// <summary>Kampf: Angriff, Monster besiegen, Tod und Wiederbelebung, Rand-Ausgang zum Friedhof.</summary>
public sealed class CombatTests : IAsyncLifetime
{
    // Tos (50) und ein Monsterraum ohne automatische Spawns (Id 99), dazu der echte Rand-Ausgang Tos → Friedhof (70).
    private static readonly WorldDefinition World = new(50,
    [
        new RoomDefinition(50, "Streets", "tos.roo")
        {
            Rows = 69, Cols = 42, Teleport = new GridPos(50, 3),
            EdgeExits = [new EdgeExitDefinition("WEST", 70, 28, 32)],
        },
        new RoomDefinition(70, "Graveyard", "tosgrave.roo") { Rows = 30, Cols = 32, Teleport = new GridPos(14, 18) },
        new RoomDefinition(99, "Arena", "x.roo") { Teleport = new GridPos(10, 10) },
    ]);

    private static readonly MonsterKind Weakling = new("Zombie", "zombie.bgf", 1, 1, 1, 0.0, TimeSpan.FromHours(1), 0, 64);
    private static readonly MonsterKind Brute = new("Zombie", "zombie.bgf", 999, 30, 30, 1.0, TimeSpan.FromMilliseconds(100), 300, 20 * 64);

    private readonly string _accountsPath = Path.Combine(Path.GetTempPath(), $"meridian-accounts-{Guid.NewGuid():N}.db");
    private readonly CancellationTokenSource _cts = new(TimeSpan.FromSeconds(30));
    private GameServer _server = null!;
    private Task _serverTask = null!;

    public Task InitializeAsync()
    {
        var accounts = AccountStore.Open(_accountsPath);
        accounts.AddOrUpdate("Anna", "pw");
        _server = new GameServer(new ServerOptions { Port = 0, World = World, RandomSeed = 1 }, accounts);
        _server.Start();
        _serverTask = _server.RunAsync(_cts.Token);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _cts.Cancel();
        await _serverTask;
        foreach (var f in new[] { _accountsPath, _accountsPath + "-wal", _accountsPath + "-shm" })
            File.Delete(f);
    }

    [Fact]
    public async Task EdgeExit_West_LeadsToGraveyard()
    {
        var (anna, events) = await EnterAsync();
        await WaitForAsync(anna, events, e => e is ClientEvent.HealthChanged { Hp: 20, MaxHp: 20 });
        // Zeile 50, Spalte 0 → links aus dem Raster
        await anna.MoveAsync(Room.Fine(50), 20, running: false);
        await WaitForAsync(anna, events, e => e is ClientEvent.EnteredRoom { RoomName: "Graveyard" });
    }

    [Fact]
    public async Task Attack_KillsWeakMonster_WithDeathAnimation()
    {
        var (anna, events) = await EnterAsync();
        // Monster direkt neben den Spieler (Ankunft 50/3)
        var monster = _server.World.SpawnMonster(50, Weakling, new GridPos(50, 4));
        await WaitForAsync(anna, events, e => e is ClientEvent.ObjectAdded { Object.IsAttackable: true });

        // Treffer ist zufällig (75 %): angreifen, bis das Monster stirbt.
        while (!events.Any(e => e is ClientEvent.ObjectAnimated { Death: true }))
        {
            await anna.AttackAsync(monster.Id);
            await Task.Delay(1300, _cts.Token);
            anna.ProcessEvents();
        }
        Assert.Contains(events, e => e is ClientEvent.Chat { Text: "Du hast den Zombie besiegt!" });
        await WaitForAsync(anna, events, e => e is ClientEvent.ObjectRemoved r && r.Id == monster.Id);
    }

    [Fact]
    public async Task StrongMonster_KillsPlayer_WhoRespawnsInTos()
    {
        var (anna, events) = await EnterAsync();
        _server.World.SpawnMonster(50, Brute, new GridPos(50, 5));
        await WaitForAsync(anna, events, e => e is ClientEvent.HealthChanged { Hp: 0 });
        await WaitForAsync(anna, events, e => e is ClientEvent.Chat c && c.Text.Contains("getötet"));
        await WaitForAsync(anna, events, e => e is ClientEvent.HealthChanged { Hp: 20 });
        Assert.True(events.OfType<ClientEvent.EnteredRoom>().Count() >= 2);
    }

    [Fact]
    public async Task Attack_OutOfRange_IsRefused()
    {
        var (anna, events) = await EnterAsync();
        var monster = _server.World.SpawnMonster(50, Weakling, new GridPos(60, 30));
        await anna.AttackAsync(monster.Id);
        await WaitForAsync(anna, events, e => e is ClientEvent.Chat { Text: "Der Zombie ist zu weit weg." });
    }

    private async Task<(ClientSession, List<ClientEvent>)> EnterAsync()
    {
        var anna = new ClientSession();
        var events = new List<ClientEvent>();
        anna.Event += events.Add;
        await anna.ConnectAsync("127.0.0.1", _server.Port, "Anna", "pw");
        await WaitForAsync(anna, events, e => e is ClientEvent.EnteredRoom);
        await WaitForAsync(anna, events, e => e is ClientEvent.ObjectAdded a && a.Object.Id == anna.PlayerId);
        return (anna, events);
    }

    private async Task WaitForAsync(ClientSession s, List<ClientEvent> events, Func<ClientEvent, bool> match)
    {
        while (!events.Any(match))
        {
            _cts.Token.ThrowIfCancellationRequested();
            await Task.Delay(10);
            s.ProcessEvents();
        }
    }
}
