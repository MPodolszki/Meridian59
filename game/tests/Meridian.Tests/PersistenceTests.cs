using Meridian.Protocol;
using Meridian.Server;

namespace Meridian.Tests;

/// <summary>Spielstand in SQLite: Inventar, Bankkonto, Position und Lebenspunkte überstehen einen Serverneustart.</summary>
public sealed class PersistenceTests : IAsyncLifetime
{
    private static readonly WorldDefinition Bank = new(1,
    [
        new RoomDefinition(1, "Bank", "tosbank.roo")
        {
            Teleport = new GridPos(10, 10),
            Npcs = [new NpcDefinition("TosBanker", 9, 10) { Name = "Skivlat" }],
        },
    ]);

    private static string Data(params string[] parts) => Path.Combine([AppContext.BaseDirectory, "data", .. parts]);

    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"meridian-persist-{Guid.NewGuid():N}");
    private string DatabasePath => Path.Combine(_dir, "meridian.db");
    private readonly CancellationTokenSource _timeout = new(TimeSpan.FromSeconds(30));

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(_dir);
        AccountStore.Open(DatabasePath).AddOrUpdate("Anna", "pw");
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        Directory.Delete(_dir, recursive: true);
        return Task.CompletedTask;
    }

    private (GameServer Server, Task Run, CancellationTokenSource Cts) StartServer()
    {
        var server = new GameServer(new ServerOptions
        {
            Port = 0,
            World = Bank,
            Items = ItemCatalog.Load(Data("items", "items.json")),
            Shops = ShopFile.Load(Data("shops", "tos.json")),
            Database = DatabasePath,
        }, AccountStore.Open(DatabasePath));
        server.Start();
        var cts = CancellationTokenSource.CreateLinkedTokenSource(_timeout.Token);
        return (server, server.RunAsync(cts.Token), cts);
    }

    [Fact]
    public async Task CharacterState_SurvivesServerRestart()
    {
        var (server, run, cts) = StartServer();
        using (var anna = new ClientSession())
        {
            var events = new List<ClientEvent>();
            anna.Event += events.Add;
            await anna.ConnectAsync("127.0.0.1", server.Port, "Anna", "pw");
            await WaitForAsync(anna, events, _ => Money(anna) == 2500);

            await anna.DepositAsync(1200);
            await WaitForAsync(anna, events, _ => Money(anna) == 1300);
            var mace = anna.Inventory.Single(o => anna.Resolve(o.NameRsc) == "Streitkolben");
            await anna.UseAsync(mace.Id);
            await WaitForAsync(anna, events, _ => anna.Inventory.Any(o => o.Used));
            await anna.MoveAsync(Room.Fine(3), Room.Fine(7), running: false);
            await anna.QuitAsync();
            await WaitForAsync(anna, events, e => e is ClientEvent.Status { Text: "Zurück im Menü" });
        }
        // Serverende speichert ebenfalls; der Stand wurde schon beim Ausloggen geschrieben.
        await cts.CancelAsync();
        await run;

        (server, run, cts) = StartServer();
        using (var anna = new ClientSession())
        {
            var events = new List<ClientEvent>();
            anna.Event += events.Add;
            await anna.ConnectAsync("127.0.0.1", server.Port, "Anna", "pw");
            await WaitForAsync(anna, events, _ => Money(anna) == 1300);
            await WaitForAsync(anna, events, _ => anna.Inventory.Any(o => anna.Resolve(o.NameRsc) == "Streitkolben" && o.Used));

            var self = events.OfType<ClientEvent.ObjectAdded>().Last(e => e.Object.Id == anna.PlayerId).Object;
            Assert.Equal((Room.Fine(3), Room.Fine(7)), (self.Row, self.Col));

            await anna.BalanceAsync();
            await WaitForAsync(anna, events, e => e is ClientEvent.Chat c && c.Text.Contains("1200 Schillinge auf deinem Konto"));
        }
        await cts.CancelAsync();
        await run;
    }

    [Fact]
    public void Store_WritesAndReadsCharacters()
    {
        var store = new GameStore(DatabasePath);
        store.Save(new Dictionary<uint, SavedCharacter>
        {
            [1001] = new() { Room = 50, Row = 100, Col = 200, Angle = 1024, Hp = 7, Inventory = [new("money", 42), new("mace")], Bank = { [1] = 5 } },
        }, DateTime.UtcNow);
        // Erneut speichern ersetzt das Inventar vollständig.
        store.Save(new Dictionary<uint, SavedCharacter> { [1001] = new() { Room = 50, Inventory = [new("money", 40)] } }, DateTime.UtcNow);

        var loaded = new GameStore(DatabasePath).LoadAll()[1001];
        Assert.Equal(50, loaded.Room);
        Assert.Equal([new SavedItem("money", 40)], loaded.Inventory);
        Assert.Empty(loaded.Bank);
    }

    [Fact]
    public void OldAccountFile_IsImportedOnce()
    {
        string json = Path.Combine(_dir, "old-accounts.json");
        string db = Path.Combine(_dir, "import.db");
        File.WriteAllText(json, """[{"Name":"Bob","PasswordDigestHex":"00112233445566778899AABBCCDDEEFF","Type":0,"CharacterId":1005}]""");
        var accounts = AccountStore.Open(db, importJson: json);
        Assert.Equal(1, accounts.Count);
        Assert.True(File.Exists(json + ".imported"));
        Assert.NotNull(AccountStore.Open(db).Verify("bob", Convert.FromHexString("00112233445566778899AABBCCDDEEFF")));
    }

    private static int Money(ClientSession s) => (int)(s.Inventory.FirstOrDefault(o => o.Icon == "coin.bgf")?.Amount ?? 0);

    private async Task WaitForAsync(ClientSession s, List<ClientEvent> events, Func<ClientEvent?, bool> match)
    {
        while (!events.Any(match) && !match(null))
        {
            _timeout.Token.ThrowIfCancellationRequested();
            await Task.Delay(10);
            s.ProcessEvents();
        }
    }
}
