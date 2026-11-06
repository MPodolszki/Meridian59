using Meridian.Protocol;
using Meridian.Server;

namespace Meridian.Tests;

/// <summary>Gegenstände und Sprüche aus den Beutetabellen (docs/trade.md, docs/magic.md): Spruchmodifikator der
/// Ausrüstung, Fluch des Lethargie-Rings und Fluch lösen, Nerudit gegen Trolle, Sonderstäbe, Schutzsprüche, Waffenruhe.</summary>
public sealed class MoreItemsTests : IAsyncLifetime
{
    private static string Data(params string[] parts) => Path.Combine([AppContext.BaseDirectory, "data", .. parts]);

    private readonly string _db = Path.Combine(Path.GetTempPath(), $"meridian-items2-{Guid.NewGuid():N}.db");
    private readonly CancellationTokenSource _cts = new(TimeSpan.FromSeconds(60));
    private GameServer _server = null!;
    private Task _run = null!;

    private static readonly MonsterKind Dummy = new("Puppe", "zombie.bgf", 500, 1, 1, 0, TimeSpan.FromHours(1), 0, 64) { Gender = "f" };

    public Task InitializeAsync()
    {
        var accounts = AccountStore.Open(_db);
        accounts.AddOrUpdate("Held", "pw");
        _server = new GameServer(new ServerOptions
        {
            Port = 0,
            World = new WorldDefinition(1, [new RoomDefinition(1, "R", "r.roo") { Teleport = new GridPos(5, 5) }]),
            RandomSeed = 11,
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
    public async Task PlateArmor_LowersSpellPower_DiscipleRobeRaisesItsSchool()
    {
        var (c, events, me) = await EnterAsync();
        var spells = SpellBook.Load(Data("spells", "spells.json"));
        var heal = spells["minorheal"]!;     // Shal'ille Stufe 1
        me.State.Spells[heal.Sid] = 99;
        int before = _server.World.SpellPower(me, heal);
        var plate = Give(me, "platearmor");
        await UseAsync(c, events, plate);
        Assert.Equal(before - 30, _server.World.SpellPower(me, heal));   // plate.kod viSpell_modifier = −30

        var robe = Give(me, "disciplerobeshalille");
        await UseAsync(c, events, robe);                                   // ersetzt die Plattenrüstung
        Assert.Equal(before + 4, _server.World.SpellPower(me, heal));     // 4 × höchste Shal'ille-Stufe (1)
    }

    [Fact]
    public async Task RingOfLethargy_IsCursed_UntilRemoveCurse()
    {
        var (c, events, me) = await EnterAsync();
        var ring = Give(me, "ringoflethargy");
        await UseAsync(c, events, ring);
        Assert.True(ring.Used);

        int before = events.Count;
        await c.UnuseAsync(ring.Id);
        await WaitForAsync(c, events, _ => Chats(events, before).Any(t => t.Contains("gebunden")));
        Assert.True(ring.Used);
        await c.DropAsync([new ObjectRef(ring.Id, null)]);
        await WaitForAsync(c, events, _ => Chats(events, before).Count(t => t.Contains("gebunden")) >= 2);
        Assert.Contains(ring, me.State.Inventory);

        var potion = Give(me, "removecursepotion");
        before = events.Count;
        await c.UseAsync(potion.Id);
        await WaitForAsync(c, events, _ => Chats(events, before).Any(t => t.Contains("frei atmen")));
        Assert.False(ring.Used);
    }

    [Fact]
    public void NeruditeSword_HurtsTrolls()
    {
        var troll = MonsterKind.ByKey("troll");
        Assert.Equal(80, troll.ResistanceTo(["thrust", "nonmagic"]));   // gewöhnliches Schwert
        Assert.Equal(-80, troll.ResistanceTo(["thrust", "nerudite"]));   // Nerudit zählt nicht als „nicht magisch“
        var items = ItemCatalog.Load(Data("items", "items.json"));
        Assert.Equal((4, 9), CombatRules.WeaponDamage(items["neruditesword"]));   // Stich 3–8, Nerudit + 1
    }

    [Fact]
    public async Task FireWand_BurnsMonster_AndUsesCharges()
    {
        var (c, events, me) = await EnterAsync();
        me.State.Attributes = me.State.Attributes with { Mysticism = 50, Aim = 50 };   // Treffer bei Wurf < 90
        var m = _server.World.SpawnMonster(1, Dummy, new GridPos(5, 7));
        var wand = Give(me, "firewand");
        for (int i = 0; i < 4 && m.Hp == m.MaxHp; i++)
        {
            int before = events.Count;
            await c.UseAsync(wand.Id);
            await WaitForAsync(c, events, _ => Chats(events, before).Any(t => t.Contains("pulsiert")));
        }
        Assert.True(m.Hp < m.MaxHp);
        Assert.True(wand.Charges < 12);
    }

    [Fact]
    public async Task DiscipleRobe_FromTreasure_GetsASchool()
    {
        var (_, _, me) = await EnterAsync();
        var items = ItemCatalog.Load(Data("items", "items.json"));
        Assert.Equal(6, items["disciplerobe"].Variants!.Count);
        _server.World.GiveItemsForTest(me, [("disciplerobe", 1)]);
        var robe = me.State.Inventory.Single(i => i.Kind.Key.StartsWith("disciplerobe"));
        Assert.InRange(robe.Kind.DiscipleSchool, 1, 6);
    }

    [Fact]
    public async Task MagicShieldPotion_RaisesDefense()
    {
        var (c, events, me) = await EnterAsync();
        int before = _server.World.PlayerDefense(me);
        var potion = Give(me, "magicshieldpotion");
        int n = events.Count;
        await c.UseAsync(potion.Id);
        await WaitForAsync(c, events, _ => Chats(events, n).Any(t => t.Contains("schützender Schild")));
        Assert.True(_server.World.PlayerDefense(me) >= before + 90);   // 50 + 2 × Stärke (20..60)
    }

    [Fact]
    public async Task TruceScroll_StopsCombat()
    {
        var (c, events, me) = await EnterAsync();
        var m = _server.World.SpawnMonster(1, Dummy, new GridPos(5, 6));
        var scroll = Give(me, "trucescroll");
        int n = events.Count;
        await c.UseAsync(scroll.Id);
        await WaitForAsync(c, events, _ => Chats(events, n).Any(t => t.Contains("betäubende Ruhe")));
        n = events.Count;
        await c.AttackAsync(m.Id);
        await WaitForAsync(c, events, _ => Chats(events, n).Any(t => t.Contains("gegen die Natur")));
        Assert.Equal(m.MaxHp, m.Hp);
    }

    [Fact]
    public async Task PvpWand_IsRefusedByGuardianAngel()
    {
        var (c, events, me) = await EnterAsync();
        var wand = Give(me, "shatterwand");
        int n = events.Count;
        await c.UseAsync(wand.Id);
        await WaitForAsync(c, events, _ => Chats(events, n).Any(t => t.Contains("Schutzengel")));
        Assert.Contains(wand, me.State.Inventory);
    }

    [Fact]
    public async Task Longbow_ShootsAcrossTheRoom_AndUsesArrows()
    {
        var (c, events, me) = await EnterAsync();
        me.State.Attributes = me.State.Attributes with { Aim = 50 };
        var shield = Give(me, "metalshield");
        await UseAsync(c, events, shield);
        var bow = Give(me, "longbow");
        await UseAsync(c, events, bow);
        Assert.False(shield.Used);                       // Bögen brauchen beide Hände
        var m = _server.World.SpawnMonster(1, Dummy, new GridPos(5, 15));   // 10 Felder entfernt

        int n = events.Count;
        await c.AttackAsync(m.Id);
        await WaitForAsync(c, events, _ => Chats(events, n).Any(t => t.Contains("keine Munition")));

        _server.World.GiveItemsForTest(me, [("arrow", 3)]);
        var arrows = me.State.Inventory.Single(i => i.Kind.Key == "arrow");
        for (int i = 0; i < 3; i++)
        {
            n = events.Count;
            await Task.Delay(1100);                       // Angriffstakt
            await c.AttackAsync(m.Id);
            await WaitForAsync(c, events, _ => Chats(events, n).Any(t => t.Contains("Pfeil auf")));
        }
        Assert.DoesNotContain(arrows, me.State.Inventory);   // alle drei verschossen
        Assert.True(m.Hp < m.MaxHp);
    }

    [Fact]
    public void ArrowsFromTreasure_ComeInBundles()
    {
        var items = ItemCatalog.Load(Data("items", "items.json"));
        Assert.Equal(20, items["arrow"].DefaultNumber);    // ammo piNumber
        Assert.Equal(5, items["neruditearrow"].DefaultNumber);
        Assert.Equal(10, items["silverarrow"].DefaultNumber);
        Assert.Equal(-20, items["longbow"].SpellModifier);
        Assert.Equal(17, items["battlebow"].Range);
    }

    [Fact]
    public async Task IllusionaryFormPotion_ShowsPlayerAsMonster()
    {
        var (c, events, me) = await EnterAsync();
        var potion = Give(me, "illusionaryformpotion");
        int n = events.Count;
        await c.UseAsync(potion.Id);
        await WaitForAsync(c, events, _ => Chats(events, n).Any(t => t.Contains("siehst du nun aus wie")));
        Assert.NotNull(me.Illusion);
        Assert.True(me.Illusion!.CanMorphTo);
        Assert.False(MonsterKind.ByKey("darkangel").CanMorphTo);
    }

    [Fact]
    public async Task FeignDeath_DropsWeapon_RaisesDefense_EndsOnMove()
    {
        var (c, events, me) = await EnterAsync();
        var spells = SpellBook.Load(Data("spells", "spells.json"));
        var feign = spells["feigndeath"]!;
        me.State.Spells[feign.Sid] = 99;
        me.State.Mana = 99;
        _server.World.GiveItemsForTest(me, [("solagh", 5), ("rainbowfern", 5)]);
        var sword = Give(me, "longsword");
        await UseAsync(c, events, sword);
        int before = _server.World.PlayerDefense(me);
        for (int i = 0; i < 10 && me.FeignDrainAt is null; i++)
        {
            int n = events.Count;
            me.State.Mana = 99;
            await c.CastAsync(Spell.ObjectIdBase + (uint)feign.Sid, []);
            await WaitForAsync(c, events, _ => Chats(events, n).Any(t => t.Contains("stellst dich tot") || t.Contains("misslungen")));
        }
        Assert.NotNull(me.FeignDrainAt);
        Assert.DoesNotContain(sword, me.State.Inventory);         // Waffe fällt immer
        Assert.True(_server.World.PlayerDefense(me) > before);
        await c.MoveAsync((ushort)(me.Row + 64), me.Col, false);
        await WaitForAsync(c, events, _ => me.FeignDrainAt is null);
    }

    private Item Give(Avatar me, string key)
    {
        _server.World.GiveItemsForTest(me, [(key, 1)]);
        return me.State.Inventory.Last(i => i.Kind.Key == key);
    }

    private async Task UseAsync(ClientSession c, List<ClientEvent> events, Item item)
    {
        await c.UseAsync(item.Id);
        await WaitForAsync(c, events, _ => item.Used);
    }

    private static IEnumerable<string> Chats(List<ClientEvent> events, int from) =>
        events.ToList().Skip(from).OfType<ClientEvent.Chat>().Select(ch => ch.Text);

    private async Task<(ClientSession, List<ClientEvent>, Avatar)> EnterAsync()
    {
        var c = new ClientSession();
        var events = new List<ClientEvent>();
        c.Event += events.Add;
        await c.ConnectAsync("127.0.0.1", _server.Port, "Held", "pw");
        await WaitForAsync(c, events, e => e is ClientEvent.EnteredRoom);
        var me = _server.World.Rooms.Single().Avatars.Values.Single();
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
