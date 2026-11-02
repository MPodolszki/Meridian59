# Entscheidungen

Format: Datum – Entscheidung – Begründung.

## 2026-11-01 – Arbeit im Fork, Branch `game`
Der Fork https://github.com/MPodolszki/Meridian59 enthält den Original-Code.
Das neue Spiel entsteht im Branch `game` unter `game/` (server, client, docs).
`master` bleibt das unveränderte Original.

**Begründung:** Original-Code direkt als Referenz griffbereit, ein einziges
Repo für die Synchronisation über mehrere Rechner, saubere Trennung durch
eigenen Branch und eigenes Verzeichnis.

## 2026-11-01 – Client-Engine: Godot
Der neue Client wird mit Godot gebaut.

**Begründung:** Läuft auf allen gängigen Plattformen (Windows, Linux, macOS,
später ggf. mobil/Web), Open Source, gute 3D-Unterstützung für das Ziel
1st/3rd-Person-MMO.

Godot 4.x (.NET), Sprache C# – siehe Eintrag unten.

## 2026-11-01 – Protokoll zunächst kompatibel zum Original
Der neue Server und Client sprechen anfangs das Original-Protokoll
(siehe protocol.md).

**Begründung:** Gegentests möglich: neuer Server ↔ alter Client,
neuer Client ↔ alter Server.

## 2026-11-01 – Server in C# (.NET), Client in Godot mit C#
Server: C# auf .NET. Client: Godot 4 (.NET-Variante) mit C#.
Protokoll-Code (Framing, Security-Hash, Obfuskation, Nachrichtentypen) und
gemeinsame Spiellogik liegen in einer geteilten C#-Bibliothek, die Server und
Client beide nutzen. Spiellogik wird zunächst direkt in C# geschrieben
(Ersatz für Blakod/Kod); eine Skriptsprache für Inhalte (z. B. Lua) kann später folgen.

**Begründung:** Eine Sprache für beide Seiten, Protokoll nur einmal
implementiert, gute Performance und Werkzeuge, plattformübergreifend.

**Konsequenz:** Desktop (Windows/Linux/macOS) zuerst. Web-Export und Mobile
sind mit Godot-C# eingeschränkt und werden bei Bedarf neu bewertet.

## 2026-11-01 – Server-Zielplattform: Linux (auch Raspberry Pi)
Der Server soll primär unter Linux laufen, zum Testen auf einem Raspberry Pi (64-Bit, linux-arm64).
Gebaut wird self-contained per `dotnet publish -r linux-arm64`, auf dem Pi ist kein .NET nötig.
Keine Windows-spezifischen APIs im Server.

**Begründung:** Günstiges, einfaches Hosting; Pi als Testserver im Heimnetz.

## 2026-11-01 – Spielwelt startet in Tos
Neue Charaktere starten in den „Streets of Tos“ (RID 50, Ankunft 60/18). Tos-Räume, Ausgänge,
Türen, Objekte und NPCs stehen in `server/data/world/tos.json`, abgeleitet aus `kod/.../tosrm`
(siehe tos.md). Ausgänge lösen wie im Original erst mit „Gehen/Öffnen“ (BP_REQ_GO, Leertaste) aus.

**Begründung:** Wunsch, mit Tos zu beginnen. Abweichung vom Original, dort starten neue Spieler auf Raza.

## 2026-11-01 – Spielerrasse Zwerg als erste Figur
Die Spielfigur wird im neuen Client als Zwerg dargestellt (klein, gedrungen, Bart,
Startkleidung ohne Rüstung). Platzhalter aus Grundformen, bis eigene Modelle vorliegen.

## 2026-11-01 – Charaktermodelle mit MPFB2 (Blender), Assets per Skript
Spielfiguren entstehen mit MPFB2 in Blender, per Skript im Hintergrund (game/tools/blender/make_dwarf.py),
nicht von Hand. Körper aus MPFB (CC0), Haut/Augen/Haare/Kleidung aus dem MakeHuman-System-Asset-Paket
(CC0, umgefärbt), Bart als prozedurale Haarkarten. Export als .glb mit Skelett „game_engine“
(Knochennamen wie das Unreal-Standardskelett) nach game/client/models.

**Begründung:** Realistische Figuren mit Skelett, reproduzierbar, eigene bzw. CC0-Assets.
**Offen:** Animationen; ggf. Git LFS, falls die Modelldateien zahlreich/groß werden.

## 2026-11-01 – Vorerst nur eine Rasse: Zwerge
Alle Spieler sind Zwerge und werden im Client mit demselben Modell (models/dwarf.glb) dargestellt.
Weitere Rassen kommen später; dann muss die Rasse über das Protokoll übertragen werden.

## 2026-11-02 – Persistenz: SQLite
Der Spielstand der Charaktere (Raum, Position, Lebenspunkte, Inventar, Bankkonten) liegt in einer
SQLite-Datenbank (`meridian.db`, Standard neben `accounts.json`; Code `server/Meridian.Server/GameStore.cs`).
Gespeichert wird beim Ausloggen, beim Beenden des Servers und alle 5 Minuten für alle eingeloggten Charaktere,
jeweils vollständig pro Charakter in einer Transaktion. Schema-Version über `PRAGMA user_version`.

**Begründung:** Das Original speichert den ganzen Spielstand als Snapshot (blakserv `SaveAll`, alle 180 min
oder per Admin-Befehl); bei einem Absturz ist alles seit dem letzten Speichern weg, und für ein größeres MMO
skaliert das nicht. SQLite ist eine einzelne Datei ohne Datenbankserver, läuft auf dem Pi (native Bibliothek
für linux-arm64 wird mitgeliefert), schreibt transaktionssicher und lässt sich abfragen. Ein späterer Umzug auf
PostgreSQL bleibt möglich, weil der Zugriff in `GameStore` gekapselt ist.

Seit Schema 2 liegen auch die Accounts in der Datenbank (Tabelle `accounts`); eine alte `accounts.json`
wird beim ersten Start übernommen und in `accounts.json.imported` umbenannt.

**Noch nicht in der Datenbank:** Gegenstände am Boden, Monster und
freigeschaltete Händlerwaren – diese beginnen nach einem Neustart frisch.
