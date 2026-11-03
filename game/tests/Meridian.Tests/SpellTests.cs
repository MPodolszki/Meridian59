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
    public async Task Light_BrightensTheRoom_KillingFieldsEnchantsIt()
    {
        var (c, events, me) = await EnterAsync(30);
        var room = _server.World.Rooms.Single();
        byte before = _server.World.RoomLight(room);
        GiveReagents(me, ("elderberry", 40), ("orctooth", 20));
        await CastUntilAsync(c, events, me, 30, 0, t => t.Contains("magisches Leuchten"));
        Assert.True(_server.World.RoomLight(room) > before || before == 255);
        me.State.Spells[39] = 99;
        await CastUntilAsync(c, events, me, 39, 0, t => t.Contains("Kampfeswut"));
        Assert.Contains(39, room.Enchantments.Keys);
    }

    [Fact]
    public async Task ArmorOfGort_AndManaFocus_Start()
    {
        var (c, events, me) = await EnterAsync(74);
        GiveReagents(me, ("bluedragonscale", 40));
        await CastUntilAsync(c, events, me, 74, me.Id, t => t.Contains("Rüstung des Gort um dich"));
        Assert.Contains(74, me.State.Enchantments.Keys);
        me.State.Spells[67] = 99;
        await CastUntilAsync(c, events, me, 67, 0, t => t.Contains("gebündelte Magie"));
        Assert.NotNull(me.ManaFocus);
    }

    [Fact]
    public async Task KaraholsCurse_EndsInParalysis_NightVisionLightsThePlayer()
    {
        var (c, events, me) = await EnterAsync(66);
        GiveReagents(me, ("entrootberry", 40), ("elderberry", 40));
        await CastUntilAsync(c, events, me, 66, 0, t => t.Contains("Kara'hols kommt über dich"));
        Assert.True(_server.World.PlayerOffense(me) > 100);
        me.State.Enchantments[66] = me.State.Enchantments[66] with { Until = DateTime.UtcNow };
        await WaitForAsync(c, events, e => e is ClientEvent.Effect { Code: GameReplies.EffectParalyze });
        Assert.NotNull(me.HeldUntil);
        me.State.Spells[4] = 99;
        me.HeldUntil = null;
        await CastUntilAsync(c, events, me, 4, me.Id, t => t.Contains("Augen brennen"));
        await WaitForAsync(c, events, e => e is ClientEvent.PlayerLight { Light: > 50 });
    }

    [Fact]
    public async Task Umbrella_RaisesDefense_DiscordanceEndsRoomEnchantments()
    {
        var (c, events, me) = await EnterAsync(50);
        int before = _server.World.PlayerDefense(me);
        await CastUntilAsync(c, events, me, 50, 0, t => t.Contains("Kuppel aus magischer Energie umschließt"));
        Assert.Equal(before + 250, _server.World.PlayerDefense(me));
        // Winde auf den Raum, dann Zwietracht mit voller Stärke: ab 60 % fällt eine Verzauberung sicher.
        me.State.Spells[25] = 99;
        me.State.Spells[43] = 99;
        GiveReagents(me, ("elderberry", 40), ("purplemushroom", 20), ("sapphire", 40));
        await CastUntilAsync(c, events, me, 25, 0, t => t.Contains("Heftige Winde"));
        var room = _server.World.Rooms.Single();
        Assert.Contains(25, room.Enchantments.Keys);
        // Jede Raumverzauberung fällt mit Stärke % Chance; notfalls mehrmals wirken.
        for (int i = 0; i < 10 && room.Enchantments.ContainsKey(25); i++)
            await CastUntilAsync(c, events, me, 43, 0, t => t.Contains("Manafeld zu beschädigen"));
        Assert.DoesNotContain(25, room.Enchantments.Keys);
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
