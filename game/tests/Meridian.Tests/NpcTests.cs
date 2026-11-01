using Meridian.Protocol;
using Meridian.Server;

namespace Meridian.Tests;

/// <summary>NPCs: erscheinen im Raum, antworten auf Begrüßung/Stichwörter, lassen sich anschauen.</summary>
public sealed class NpcTests : IAsyncLifetime
{
    private static readonly WorldDefinition Inn = new(52,
    [
        new RoomDefinition(52, "Familiars", "tosinn.roo")
        {
            Teleport = new GridPos(7, 8),
            Npcs = [new NpcDefinition("TosInnKeeper", 7, 9) { Name = "Paddock" }],
        },
    ]);

    private static readonly DialogFile Dialogs = new(new Dictionary<string, NpcDialog>
    {
        ["TosInnKeeper"] = new("Wirt", "Ein kräftiger Wirt.", "Willkommen!",
            [new NpcTopic(["bier"], "Frisches Bier!")]),
    });

    private readonly string _accountsPath = Path.Combine(Path.GetTempPath(), $"meridian-accounts-{Guid.NewGuid():N}.json");
    private readonly CancellationTokenSource _cts = new(TimeSpan.FromSeconds(30));
    private GameServer _server = null!;
    private Task _serverTask = null!;

    public Task InitializeAsync()
    {
        var accounts = AccountStore.Load(_accountsPath);
        accounts.AddOrUpdate("Anna", "pw");
        _server = new GameServer(new ServerOptions { Port = 0, World = Inn, Dialogs = Dialogs }, accounts);
        _server.Start();
        _serverTask = _server.RunAsync(_cts.Token);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _cts.Cancel();
        await _serverTask;
        File.Delete(_accountsPath);
    }

    [Fact]
    public async Task Npc_IsVisible_AnswersAndCanBeLookedAt()
    {
        using var anna = new ClientSession();
        var events = new List<ClientEvent>();
        anna.Event += events.Add;
        await anna.ConnectAsync("127.0.0.1", _server.Port, "Anna", "pw");

        await WaitForAsync(anna, events, e => e is ClientEvent.ObjectAdded { Object.IsNpc: true });
        var npc = events.OfType<ClientEvent.ObjectAdded>().Single(e => e.Object.IsNpc).Object;
        Assert.Equal("Paddock (Wirt)", anna.Resolve(npc.NameRsc));
        Assert.False(events.OfType<ClientEvent.ObjectAdded>().Single(e => e.Object.Id == anna.PlayerId).Object.IsNpc);

        await anna.SayAsync("Hallo zusammen");
        await WaitForAsync(anna, events, e => e is ClientEvent.Chat { Text: "Paddock: Willkommen!" });

        await anna.SayAsync("Ein BIER bitte");
        await WaitForAsync(anna, events, e => e is ClientEvent.Chat { Text: "Paddock: Frisches Bier!" });

        await anna.LookAsync(npc.Id);
        await WaitForAsync(anna, events, e => e is ClientEvent.Chat { Text: "Ein kräftiger Wirt." });
    }

    [Fact]
    public void Npc_IgnoresUnrelatedText()
    {
        var npc = new Npc(1, new NpcDefinition("TosInnKeeper", 1, 1) { Name = "Paddock" }, Dialogs.Npcs["TosInnKeeper"]);
        Assert.Null(npc.Reply("Wie spät ist es?"));
        Assert.Equal("Willkommen!", npc.Reply("paddock?"));
    }

    [Fact]
    public void TosDialogs_CoverAllTosNpcs()
    {
        string data = Path.Combine(AppContext.BaseDirectory, "data");
        var world = WorldDefinition.Load(Path.Combine(data, "world", "tos.json"));
        var dialogs = DialogFile.Load(Path.Combine(data, "dialog", "tos.json"));
        foreach (var npc in world.Rooms.SelectMany(r => r.Npcs))
            Assert.True(dialogs.Npcs.ContainsKey(npc.KodClass), $"Dialog fehlt für {npc.KodClass}");
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
