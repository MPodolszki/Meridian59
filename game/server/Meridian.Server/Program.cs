using Meridian.Protocol;
using Meridian.Server;

// Aufruf:
//   Meridian.Server [--port 5959] [--database meridian.db] [--world data/world/tos.json]
//                   [--client-dir <resource-ordner>] [--player-icon bta.bgf] [--start-room 52]
//                   [--save-period 5] [--accounts accounts.json] [--hour 23]
//   Meridian.Server add-account <name> <passwort> [--admin] [--database meridian.db]
// --accounts: alte Account-Datei, wird beim ersten Start in die Datenbank übernommen. Ohne --database liegt
// die Datenbank neben dieser Datei.

string accountsPath = "accounts.json";
int port = 5959;
string? clientDir = null;
string worldPath = Path.Combine(AppContext.BaseDirectory, "data", "world", "tos.json");
string playerIcon = "bta.bgf";   // Körper des männlichen Spielers im Original
int? startRoom = null;
string? database = null;
int savePeriodMinutes = 5;
int? fixedHour = null;   // feste Spielstunde zum Testen (z. B. 23 = Nacht)
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
        case "--hour": fixedHour = int.Parse(args[++i]) % 24; break;
        default: positional.Add(args[i]); break;
    }
}

database ??= Path.Combine(Path.GetDirectoryName(Path.GetFullPath(accountsPath))!, "meridian.db");
var accounts = AccountStore.Open(database, importJson: accountsPath);

if (positional is ["add-account", var name, var password])
{
    accounts.AddOrUpdate(name, password, admin ? ClientAccountType.Admin : ClientAccountType.Normal);
    Console.WriteLine($"Account '{name}' gespeichert in {Path.GetFullPath(database)}");
    return 0;
}
if (positional.Count > 0)
{
    Console.Error.WriteLine("Unbekannte Argumente. Siehe Kommentar in Program.cs bzw. game/server/README.md.");
    return 1;
}

if (accounts.Count == 0)
    Log.Warn($"Keine Accounts in {Path.GetFullPath(database)} – anlegen mit: add-account <name> <passwort>");

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
    Database = database,
    FixedHour = fixedHour,
    SavePeriod = TimeSpan.FromMinutes(savePeriodMinutes),
}, accounts);
server.Start();
await server.RunAsync(cts.Token);
Log.Info("Server beendet");
return 0;
