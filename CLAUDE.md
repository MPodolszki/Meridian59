# CLAUDE.md – Meridian-Projekt

## Projektziel
Eigenes MMO auf Basis von Meridian 59, vorerst privat. Langfristig ein
1st/3rd-Person-MMO im Stil von EverQuest/WoW.

Vorgehen: Zuerst eine funktionierende Meridian-59-Kopie (neuer Server, neuer
Client) bauen, dann daraus schrittweise das eigene Spiel entwickeln
(Assets, Inhalte, Mechaniken austauschen).

## Repository und Branches
- Repo: https://github.com/MPodolszki/Meridian59 (Fork des Originals)
- `master`: Original-Code von Meridian 59 – wird nicht weiterentwickelt,
  nur gelesen und analysiert. Updates vom Upstream dürfen hineingemergt werden.
- `game`: Arbeits-Branch für das neue Spiel. Alle neuen Dateien entstehen hier.
- GitHub wird zur Synchronisation zwischen mehreren Rechnern genutzt:
  regelmäßig committen und pushen, vor Arbeitsbeginn `git pull`.

## Struktur des neuen Codes (im Branch `game`)
- `game/Meridian.sln` – Solution (dotnet test im Ordner game)
- `game/shared` – gemeinsame C#-Bibliothek (Protokoll), auch für den Client
- `game/server` – neuer Server
- `game/tests`  – Tests
- `game/client` – neuer Client (Godot)
- `game/docs`   – Dokumentation (Protokoll, Architektur, Entscheidungen)

Der Original-Code (blakserv/, clientd3d/, kod/, include/, doc/ …) bleibt
unverändert im Repo und dient als Referenz.

## Grundsätze
- Das Netzwerkprotokoll wird anfangs vom Original übernommen, damit der neue
  Server mit dem alten Client und der neue Client mit dem alten Server testbar bleibt.
- Es werden eigene Assets verwendet, keine Original-Assets im neuen Code.
- Server: C# (.NET), läuft unter Linux (auch Raspberry Pi, linux-arm64).
  Client: Godot 4 (.NET) mit C#. Gemeinsame C#-Bibliothek
  für Protokoll und Spiellogik. Siehe game/docs/decisions.md.

## Offene Entscheidungen
- Datenbank und Persistenz
- Skriptsprache für Inhalte (vorerst Spiellogik direkt in C#)

Getroffene Entscheidungen werden in game/docs/decisions.md festgehalten.

## Arbeitsweise
- Vor größeren Änderungen einen kurzen Plan vorlegen.
- Erkenntnisse über den Original-Code in game/docs/ dokumentieren, nicht nur im Chat.
- Antworten kurz und auf Deutsch.

## Nächster Schritt
Server kann Login und einen einfachen Game-Modus (ein Raum, Bewegung, Chat, mehrere Spieler),
noch nicht mit dem Original-Client getestet. Als Nächstes: Godot-Client-Grundgerüst
(nutzt game/shared), dann Test gegen den Original-Client.
