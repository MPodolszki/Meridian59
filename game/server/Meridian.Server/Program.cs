using Meridian.Protocol;
using Meridian.Server;

// Aufruf:
//   Meridian.Server [--port 5959] [--accounts accounts.json] [--world data/world/tos.json]
//                   [--client-dir <resource-ordner>] [--player-icon blank.bgf] [--start-room 52]
//                   [--database meridian.db] [--save-period 5]
//   Meridian.Server add-account <name> <passwort> [--admin] [--accounts accounts.json]

string accountsPath = "accounts.json";
int port = 5959;
string? clientDir = null;
string worldPath = Path.Combine(AppContext.BaseDirectory, "data", "world", "tos.json");
string playerIcon = "blank.bgf";
int? startRoom = null;
string? database = null;   // Standard: meridian.db neben der Account-Datei
int savePeriodMinutes = 5;
var positional = new List<string>();
bool admin = false;

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--port": port = int.Parse(args[++i]); break;
        case "--accounts": accountsPath = args[++i]; break;
        case "--admin": admin = true; break;
        case "--client-dir": clientDir = args[++i]; break;
        case "--world": worldPath = args[++i]; break;
        case "--player-icon": playerIcon = args[++i]; break;
        case "--start-room": startRoom = int.Parse(args[++i]); break;
        case "--database": database = args[++i]; break;
        case "--save-period": savePeriodMinutes = int.Parse(args[++i]); break;
        default: positional.Add(args[i]); break;
    }
}

var accounts = AccountStore.Load(accountsPath);

if (positional is ["add-account", var name, var password])
{
    accounts.AddOrUpdate(name, password, admin ? ClientAccountType.Admin : ClientAccountType.Normal);
    Console.WriteLine($"Account '{name}' gespeichert in {Path.GetFullPath(accountsPath)}");
    return 0;
}
if (positional.Count > 0)
{
    Console.Error.WriteLine("Unbekannte Argumente. Siehe Kommentar in Program.cs bzw. game/server/README.md.");
    return 1;
}

if (accounts.Count == 0)
    Log.Warn($"Keine Accounts in {Path.GetFullPath(accountsPath)} – anlegen mit: add-account <name> <passwort>");

using var cts = new CancellationTokenSource();
// Strg+C und SIGTERM (systemd stop) beenden den Server sauber.
using var sigint = System.Runtime.InteropServices.PosixSignalRegistration.Create(
    System.Runtime.InteropServices.PosixSignal.SIGINT, ctx => { ctx.Cancel = true; cts.Cancel(); });
using var sigterm = System.Runtime.InteropServices.PosixSignalRegistration.Create(
    System.Runtime.InteropServices.PosixSignal.SIGTERM, ctx => { ctx.Cancel = true; cts.Cancel(); });

var world = File.Exists(worldPath) ? WorldDefinition.Load(worldPath) : WorldDefinition.SingleRoom();
if (startRoom is { } sr)
    world = world with { StartRoom = sr };   // zum Testen einzelner Räume
Log.Info(File.Exists(worldPath)
    ? $"Welt: {world.Rooms.Count} Räume aus {worldPath}"
    : $"Keine Weltdatei unter {worldPath} – nur ein Testraum");

// Dialoge liegen neben der Weltdatei: data/world/tos.json → data/dialog/tos.json
string dialogPath = Path.Combine(Path.GetDirectoryName(worldPath)!, "..", "dialog", Path.GetFileName(worldPath));
var dialogs = File.Exists(dialogPath) ? DialogFile.Load(dialogPath) : DialogFile.Empty;
// Läden: data/shops/tos.json, Gegenstände: data/items/items.json
string shopPath = Path.Combine(Path.GetDirectoryName(worldPath)!, "..", "shops", Path.GetFileName(worldPath));
var shops = File.Exists(shopPath) ? ShopFile.Load(shopPath) : ShopFile.Empty;
string itemPath = Path.Combine(Path.GetDirectoryName(worldPath)!, "..", "items", "items.json");
var items = File.Exists(itemPath) ? ItemCatalog.Load(itemPath) : ItemCatalog.Empty;

var server = new GameServer(new ServerOptions
{
    Dialogs = dialogs,
    Items = items,
    Shops = shops,
    Port = port,
    World = world,
    PlayerIcon = playerIcon,
    ClientResourceDir = clientDir,
    Database = database ?? Path.Combine(Path.GetDirectoryName(Path.GetFullPath(accountsPath))!, "meridian.db"),
    SavePeriod = TimeSpan.FromMinutes(savePeriodMinutes),
}, accounts);
server.Start();
await server.RunAsync(cts.Token);
Log.Info("Server beendet");
return 0;
