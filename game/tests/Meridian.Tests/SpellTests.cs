using Meridian.Protocol;
using Meridian.Server;

namespace Meridian.Tests;

/// <summary>Schulsprüche auf Monster (docs/magic.md): Angriff mit Resistenz, Festhalten, Erdbeben, Gift heilen.</summary>
public sealed class SpellTests : IAsyncLifetime
{
    private static string Data(params string[] parts) => Path.Combine([AppContext.BaseDirectory, "data", .. parts]);

    private readonly string _db = Path.Combine(Path.GetTempPath(), $"meridian-spells-{Guid.NewGuid():N}.db");
    private readonly CancellationTokenSource _cts = new(TimeSpan.FromSeconds(60));
    private GameServer _server = null!;
    private Task _run = null!;

    /// <summary>Zielscheibe: viele Lebenspunkte, greift nicht an.</summary>
    private static readonly MonsterKind Dummy = new("Puppe", "zombie.bgf", 500, 1, 1, 0, TimeSpan.FromHours(1), 0, 64) { Gender = "f" };

    public Task InitializeAsync()
    {
        var accounts = AccountStore.Open(_db);
        accounts.AddOrUpdate("Magier", "pw");
        _server = new GameServer(new ServerOptions
        {
            Port = 0,
            World = new WorldDefinition(1, [new RoomDefinition(1, "R", "r.roo") { Teleport = new GridPos(5, 5) }]),
            RandomSeed = 7,
            Items = ItemCatalog.Load(Data("items", "items.json")),
            Spells = SpellBook.Load(Data("spells", "spells.json")),
            Skills = SkillBook.Load(Data("skills", "skills.json")),
            Database = _db,
        }, accounts);
        _server.Start();
        _run = _server.RunAsync(_cts.Token);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _cts.Cancel();
        await _run;
        foreach (var f in new[] { _db, _db + "-wal", _db + "-shm" })
            File.Delete(f);
    }

    [Fact]
    public async Task SplashOfAcid_DamagesMonster()
    {
        var (c, events, me) = await EnterAsync(167);
        var m = _server.World.SpawnMonster(1, Dummy, new GridPos(5, 7));
        await CastUntilAsync(c, events, me, 167, m.Id, t => t.Contains("trifft die Puppe") || t.Contains("besiegt"));
        Assert.True(m.Hp < m.MaxHp);
    }

    [Fact]
    public async Task Hold_FreezesMonster()
    {
        var (c, events, me) = await EnterAsync(47);
        var m = _server.World.SpawnMonster(1, Dummy, new GridPos(5, 7));
        GiveReagents(me, ("purplemushroom", 40));
        await CastUntilAsync(c, events, me, 47, m.Id, t => t.Contains("festgehalten"));
        Assert.True(m.HeldUntil > DateTime.UtcNow);
        Assert.Same(me, m.Target);   // es merkt sich den Angreifer
    }

    [Fact]
    public async Task Earthquake_HurtsMonstersAndCaster()
    {
        var (c, events, me) = await EnterAsync(14);
        var m = _server.World.SpawnMonster(1, Dummy, new GridPos(5, 6));
        GiveReagents(me, ("redmushroom", 20), ("purplemushroom", 20));
        await CastUntilAsync(c, events, me, 14, 0, t => t.Contains("Erde bäumt sich"));
        Assert.True(m.Hp < m.MaxHp);
        Assert.True(me.Hp < me.MaxHp);
    }

    [Fact]
    public void MonsterSpells_HaveTheOriginalValues()
    {
        var book = SpellBook.Load(Data("spells", "spells.json"));
        var fireball = book["fireball"]!;
        Assert.Equal((5, 4, 4, 7), (fireball.Sid, fireball.School, fireball.Level, fireball.Mana));
        Assert.Equal("fire", fireball.AttackType);
        Assert.Equal(1, book["hold"]!.Targets);
        Assert.Equal(0, book["earthquake"]!.Targets);
        Assert.Equal(5000, book["earthquake"]!.CastTime);
    }

    private void GiveReagents(Avatar a, params (string Key, int Number)[] items)
    {
        var catalog = ItemCatalog.Load(Data("items", "items.json"));
        foreach (var (key, number) in items)
            Assert.True(catalog.TryGet(key, out _), key);
        _server.World.GiveItemsForTest(a, items);
    }

    /// <summary>Zaubern, bis die erwartete Meldung kommt (Erfolg ist ein Wurf); Mana wird jedes Mal aufgefüllt.</summary>
    private async Task CastUntilAsync(ClientSession c, List<ClientEvent> events, Avatar me, int sid, uint target, Func<string, bool> done)
    {
        for (int i = 0; i < 20; i++)
        {
            me.State.Mana = 99;
            int before = events.Count;
            await c.CastAsync(Spell.ObjectIdBase + (uint)sid, target == 0 ? [] : [target]);
            await WaitForAsync(c, events, _ => events.Skip(before).OfType<ClientEvent.Chat>().Any(ch =>
                done(ch.Text) || ch.Text.Contains("misslungen") || ch.Text.Contains("konzentrierst") || ch.Text.Contains("fehlen")));
            Assert.DoesNotContain(events.Skip(before).OfType<ClientEvent.Chat>(), ch => ch.Text.Contains("fehlen"));
            if (events.Skip(before).OfType<ClientEvent.Chat>().Any(ch => ch.Text.Contains("konzentrierst")))
                await WaitForAsync(c, events, _ => events.Skip(before).OfType<ClientEvent.Chat>().Any(ch => done(ch.Text) || ch.Text.Contains("misslungen")));
            if (events.Skip(before).OfType<ClientEvent.Chat>().Any(ch => done(ch.Text)))
                return;
        }
        Assert.Fail("Zauber hat nie gewirkt");
    }

    private async Task<(ClientSession, List<ClientEvent>, Avatar)> EnterAsync(int sid)
    {
        var c = new ClientSession();
        var events = new List<ClientEvent>();
        c.Event += events.Add;
        await c.ConnectAsync("127.0.0.1", _server.Port, "Magier", "pw");
        await WaitForAsync(c, events, e => e is ClientEvent.EnteredRoom);
        var me = _server.World.Rooms.Single().Avatars.Values.Single();
        me.State.Spells[sid] = 99;
        me.State.Attributes = me.State.Attributes with { Mysticism = 50, Intellect = 50 };
        return (c, events, me);
    }

    private async Task WaitForAsync(ClientSession s, List<ClientEvent> events, Func<ClientEvent?, bool> match)
    {
        while (!events.ToList().Any(match) && !match(null))
        {
            _cts.Token.ThrowIfCancellationRequested();
            await Task.Delay(10);
            s.ProcessEvents();
        }
    }
}
