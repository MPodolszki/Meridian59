using Meridian.Protocol;
using Meridian.Server;

namespace Meridian.Tests;

/// <summary>Persönliche Verzauberungen (persench.kod): Segen wirken, Symbol im Client, Neuwirken auf sich selbst,
/// Wiederherstellung nach erneutem Anmelden.</summary>
public sealed class EnchantmentTests : IAsyncLifetime
{
    private static string Data(params string[] parts) => Path.Combine([AppContext.BaseDirectory, "data", .. parts]);

    private readonly string _db = Path.Combine(Path.GetTempPath(), $"meridian-ench-{Guid.NewGuid():N}.db");
    private readonly CancellationTokenSource _cts = new(TimeSpan.FromSeconds(30));
    private GameServer _server = null!;
    private Task _run = null!;

    public Task InitializeAsync()
    {
        var accounts = AccountStore.Open(_db);
        accounts.AddOrUpdate("Krieger", "pw");
        _server = new GameServer(new ServerOptions
        {
            Port = 0,
            World = new WorldDefinition(1, [new RoomDefinition(1, "R", "r.roo") { Teleport = new GridPos(5, 5) }]),
            RandomSeed = 5,
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
    public void EnchantmentSpells_AreOfferedToNewCharacters_LikeTheOriginal()
    {
        var book = SpellBook.Load(Data("spells", "spells.json"));
        // Stufe 2 der Schulen 1–4 (OfferToNewCharacters); Blitzen widerstehen ist Stufe 3.
        foreach (var key in new[] { "bless", "superstrength", "haste", "resistfire", "resistcold", "resistacid" })
            Assert.True(CharacterCreation.Offered(book[key]!), key);
        Assert.False(CharacterCreation.Offered(book["resistshock"]!));
        Assert.Equal(25, CharacterCreation.SpellCost(book["bless"]!));
    }

    [Fact]
    public async Task Bless_ShowsIcon_CanBeRecastOnSelf_AndSurvivesRelog()
    {
        var (c, events) = await CreateAsync();
        var bless = c.KnownSpells.Single(s => c.Resolve(s.Spell.NameRsc) == "Segen");
        Assert.Equal(1, bless.Targets);

        // Erfolg ist ein Wurf: so lange wirken, bis der Segen sitzt (Mana 23, Kosten 6 bzw. 3 bei Misserfolg).
        for (int i = 0; i < 5 && !events.Any(e => e is ClientEvent.EnchantmentsChanged { Enchantments.Count: 1 }); i++)
        {
            int before = events.Count;
            await c.CastAsync(bless.Spell.Id, [c.PlayerId]);
            await WaitForAsync(c, events, e => events.IndexOf(e!) >= before && e is ClientEvent.Chat ch
                && (ch.Text.StartsWith("Kraanan erfüllt dich") || ch.Text.Contains("misslungen")));
        }
        var shown = events.OfType<ClientEvent.EnchantmentsChanged>().Last().Enchantments;
        Assert.Equal("Segen", c.Resolve(Assert.Single(shown).NameRsc));
        Assert.Equal("blessico.bgf", c.Resolve(shown[0].IconRsc));

        // Erneut auf sich selbst: erlaubt (CanRecastSelfEnchantment), kein „bereits“-Hinweis.
        int mark = events.Count;
        await c.CastAsync(bless.Spell.Id, [c.PlayerId]);
        await WaitForAsync(c, events, e => events.IndexOf(e!) >= mark && e is ClientEvent.Chat ch
            && (ch.Text.StartsWith("Kraanan erfüllt dich") || ch.Text.Contains("misslungen") || ch.Text.Contains("nicht genug Mana")));
        Assert.DoesNotContain(events.Skip(mark), e => e is ClientEvent.Chat ch && ch.Text.Contains("bereits"));

        // Abmelden und wieder anmelden: Verzauberung bleibt (Restdauer gespeichert).
        c.Dispose();
        using var again = new ClientSession();
        var events2 = new List<ClientEvent>();
        again.Event += events2.Add;
        await again.ConnectAsync("127.0.0.1", _server.Port, "Krieger", "pw");
        await WaitForAsync(again, events2, e => e is ClientEvent.EnteredRoom);
        await again.RequestEnchantmentsAsync();
        await WaitForAsync(again, events2, e => e is ClientEvent.EnchantmentsChanged { Enchantments.Count: 1 });
    }

    private async Task<(ClientSession, List<ClientEvent>)> CreateAsync()
    {
        var c = new ClientSession { CreateNewCharacters = true };
        var events = new List<ClientEvent>();
        c.Event += events.Add;
        await c.ConnectAsync("127.0.0.1", _server.Port, "Krieger", "pw");
        await WaitForAsync(c, events, e => e is ClientEvent.CharacterCreation);
        var choices = events.OfType<ClientEvent.CharacterCreation>().Single().Choices;
        var m = choices.Male;
        await c.CreateCharacterAsync(choices.CharacterId, "Kraanos", "", 1,
            [m.Head, m.Hair[0], m.Eyes[0], m.Noses[0], m.Mouths[0]], choices.HairColors[0], choices.SkinColors[0],
            [35, 30, 35, 30, 40, 30], [choices.Spells.Single(s => s.Name == "Segen").Id], []);
        await WaitForAsync(c, events, e => e is ClientEvent.EnteredRoom);
        await c.RequestSpellsAsync();
        await WaitForAsync(c, events, _ => c.KnownSpells.Count == 1);
        await WaitForAsync(c, events, e => e is ClientEvent.ManaChanged { Mana: 23 });
        return (c, events);
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
