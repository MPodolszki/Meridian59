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
- `game/server` – neuer Server
- `game/client` – neuer Client (Godot)
- `game/docs`   – Dokumentation (Protokoll, Architektur, Entscheidungen)

Der Original-Code (blakserv/, clientd3d/, kod/, include/, doc/ …) bleibt
unverändert im Repo und dient als Referenz.

## Grundsätze
- Das Netzwerkprotokoll wird anfangs vom Original übernommen, damit der neue
  Server mit dem alten Client und der neue Client mit dem alten Server testbar bleibt.
- Es werden eigene Assets verwendet, keine Original-Assets im neuen Code.
- Client-Engine: Godot (plattformübergreifend), siehe game/docs/decisions.md.

## Offene Entscheidungen
- Sprache des Servers
- Datenbank und Persistenz
- Ersatz für die Skriptsprache des Originals (Blakod/Kod)

Getroffene Entscheidungen werden in game/docs/decisions.md festgehalten.

## Arbeitsweise
- Vor größeren Änderungen einen kurzen Plan vorlegen.
- Erkenntnisse über den Original-Code in game/docs/ dokumentieren, nicht nur im Chat.
- Antworten kurz und auf Deutsch.

## Nächster Schritt
Protokoll-Doku (game/docs/protocol.md) liegt vor (nur aus Code abgeleitet, ungetestet). Als Nächstes Server-Sprache festlegen
und einen minimalen Server bauen, der den Login des Original-Clients beantwortet.
