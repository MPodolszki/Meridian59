# Neuer Server (C#/.NET)

Stand:
- **Login-Modus** wie das Original (`AP_GETLOGIN` → `AP_LOGIN` → `AP_LOGINOK`, `AP_GETCHOICE`, `AP_CREDITS` → `AP_GAME`).
- **Game-Modus** mit Security-Hash-Prüfung, Epoche und Type-Byte-Verschleierung (Token/Redbook):
  Charakterwahl (`char.dll`), ein Raum, mehrere Spieler sehen sich (`BP_CREATE`/`BP_REMOVE`),
  Bewegung/Drehung (`BP_MOVE`/`BP_TURN`), Chat (`BP_SAID`), Ping/Echo, `BP_REQ_QUIT` zurück ins Menü.
- Texte und Dateinamen (Module, Raum, Namen) schickt der Server per `BP_CHANGE_RESOURCE` mit eigenen
  IDs ab `0x00F00000` – keine Abhängigkeit von den kompilierten `.rsc`-Dateien des Originals.
- Fehlt noch: Resync (Beacon), Charaktererstellung, Inventar/Gegenstände, Stats, Kampf, mehrere Räume, Persistenz.

Projekte (Solution `game/Meridian.sln`):
- `shared/Meridian.Protocol` – Protokoll (Framing, CRC, Security-Hash, Token, Login-Nachrichten), net8.0, auch für den Godot-Client gedacht
- `server/Meridian.Server` – Server, net9.0
- `tests/Meridian.Tests` – Unit- und Login-Ablauf-Tests

## Lokal (Windows/Linux)

```bash
cd game
dotnet test
dotnet run --project server/Meridian.Server -- add-account Tester geheim
dotnet run --project server/Meridian.Server -- --port 5959
```

Accounts liegen vorerst in `accounts.json` im Arbeitsverzeichnis (MD5-Digest wie im Original,
nur für Entwicklung; nicht eingecheckt).

## Raspberry Pi / Linux-Server

Pi 4/5 mit **64-Bit**-Raspberry-Pi-OS. Auf dem Entwicklungsrechner bauen (kein .NET auf dem Pi nötig):

```bash
dotnet publish game/server/Meridian.Server -c Release -r linux-arm64 --self-contained -o game/publish/pi
```

Für x64-Linux-Server `-r linux-x64`. Ordner auf den Pi kopieren, dann dort:

```bash
chmod +x Meridian.Server
./Meridian.Server add-account Tester geheim
./Meridian.Server --port 5959
```

Strg+C bzw. SIGTERM (systemd) beendet den Server sauber.

### Als Dienst auf dem Pi (systemd)

Vom Entwicklungsrechner aus (Git Bash, ssh/scp nötig):

```bash
game/server/deploy/publish-pi.sh pi@raspberrypi.local
```

Dann auf dem Pi:

```bash
sudo ~/meridian-upload/install-pi.sh
sudo -u meridian /opt/meridian/Meridian.Server add-account Tester geheim --accounts /opt/meridian/data/accounts.json
sudo systemctl restart meridian-server
journalctl -u meridian-server -f
```

Der Dienst läuft als eigener Benutzer `meridian`, Daten liegen in `/opt/meridian/data`.
Accounts werden beim Start gelesen, nach `add-account` daher neu starten.

## Optionen

| Option | Bedeutung |
|---|---|
| `--port 5959` | TCP-Port |
| `--accounts accounts.json` | Account-Datei |
| `--room tosinn.roo` | Raumdatei des Clients für den Startraum |
| `--world data/world/tos.json` | Weltbeschreibung (Dialoge aus `data/dialog/<gleicher Name>`, Händler aus `data/shops/<gleicher Name>`, Gegenstände aus `data/items/items.json`) |
| `--start-room 52` | Startraum überschreiben (zum Testen einzelner Räume) |
| `--client-dir <pfad>` | `resource`-Ordner eines Original-Clients: Raum-Security wird aus der `.roo` gelesen (sonst wird der Spieler im Original-Client „blind“) |

## Original-Client verbinden

Der Client bildet den Port aus der Servernummer (`59xx`) oder nimmt `SockPort` aus seiner INI
(siehe `docs/protocol.md` 1.1). Am einfachsten Server mit `--port 5959` starten und im Client
Host und Port fest eintragen. Noch nicht mit dem echten Client getestet.
