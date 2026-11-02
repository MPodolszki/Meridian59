# Test mit dem Original-Client (Stand 2026-11-02)

Ziel: Der Original-Client (Windows, `clientd3d`) verbindet sich mit dem neuen Server. Unter Linux mit Wine.

## Client besorgen
Der Client mit Grafiken ist nicht im Repo (Original-Assets). Bezugsquellen:
- Server-105-Client: https://www.meridiannext.com/play/ (Seite hinter einer Cloudflare-Prüfung, nur im Browser).
- Steam: „Meridian 59“ (kostenlos, App 893390). Der Client kennt Steam-Installationen (`/S`, `clientd3d/config.c`).
- Open Meridian (Server 112): http://openmeridian.org/patcher

Installer bzw. Ordner z. B. nach `~/Downloads` legen; Installation dann mit `wine <installer>.exe`.

## Ergebnis 2026-11-02
Steam-Client (Meridian 59, App 893390) unter Wine 11.17: **Login, Charakterwahl und Eintritt in Tos klappen.**
Der Client meldet sich als Version 7.37 und fragt danach `BP_SEND_STAT_GROUPS`, `BP_SEND_STATS`,
`BP_SEND_SPELLS`, `BP_SEND_SKILLS`, `BP_SEND_ENCHANTMENTS` sowie `UC_STAND`/`UC_SAFETY` ab – der Server antwortet
jetzt wie user.kod (Gruppen Zustand/Werte/Sprüche/Fertigkeiten, Gruppe 1 mit Lebenspunkten/Mana/Ausdauer,
leere Spruch-/Fertigkeitslisten). Ohne `/Q` bleibt der Client auf dem Startbildschirm.
Bildschirmfotos vom GNOME-Wayland-Desktop sind per Skript nicht möglich; die Darstellung muss man selbst ansehen.

Darstellung: Tos ist im Original-Client zu sehen. Damit Figuren nicht unsichtbar bleiben, schickt der Server jetzt
die Original-Grafiknamen: Spieler `bta.bgf` (Körper; Kopf/Arme/Beine als Overlays fehlen noch), NPCs ihr KOD-Icon
(`tsinnk.bgf`, `tsapoth.bgf`, `tsbanker.bgf`, `tswatch.bgf`, in `data/world/tos.json`), Zombie `zombie.bgf`,
Skelett `skelbody.bgf`; Gegenstände hatten schon die KOD-Icons. Der eigene Client bildet `skelbody` auf sein
Skelett-Modell ab, alles Unbekannte wird zum Zwerg.

Start: `game/tools/run-original-client.sh NAME PASSWORT [HOST] [PORT]`.

## Starten gegen den eigenen Server
Der Client nimmt Host und Port von der Kommandozeile (`clientd3d/config.c` ConfigOverride):

```bash
wine Meridian.exe /Q /H:127.0.0.1 /P:5959 /U:Tester /W:geheim
```

Alternativ in `meridian.ini`, Abschnitt `[Comm]`: `Hostname=127.0.0.1`, `SocketPort=5959`.

Server mit der Raum-Security aus den .roo-Dateien des Clients starten, sonst bleibt der Bildschirm schwarz
(protocol.md 5.5):

```bash
dotnet run --project game/server/Meridian.Server -- --port 5959 --client-dir <client>/resource
```

## Was zu prüfen ist
1. Login, Charakterauswahl, Eintritt in Tos (`BP_PLAYER`, Raum-Security).
2. Bewegung, Drehen, andere Spieler/NPCs/Monster sichtbar (Icons: der Server schickt eigene Namen wie
   `zombie.bgf`; im Original heißen die Grafiken anders – evtl. Platzhalter oder unsichtbar).
3. Chat, Anschauen, Angriff (Extra-Byte in `BP_REQ_ATTACK`), Lebenspunkte/Ausdauer-Anzeige.
4. Inventar, Kaufen (Ladenliste), Anbieten/Gegenangebot, Bankbefehle (`deposit 100`).
5. Tageslicht (`BP_LIGHT_AMBIENT`).

Ergebnisse und Abweichungen hier und in protocol.md festhalten.
