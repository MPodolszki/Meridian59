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
