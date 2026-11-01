using Meridian.Protocol;
using Meridian.Server;

// Aufruf:
//   Meridian.Server [--port 5959] [--accounts accounts.json]
//   Meridian.Server add-account <name> <passwort> [--admin] [--accounts accounts.json]

string accountsPath = "accounts.json";
int port = 5959;
var positional = new List<string>();
bool admin = false;

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--port": port = int.Parse(args[++i]); break;
        case "--accounts": accountsPath = args[++i]; break;
        case "--admin": admin = true; break;
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

var server = new GameServer(new ServerOptions { Port = port }, accounts);
server.Start();
await server.RunAsync(cts.Token);
Log.Info("Server beendet");
return 0;
