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
- `game/client` – neuer Client (Godot 4.7 .NET, C#)
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
- Skriptsprache für Inhalte (vorerst Spiellogik direkt in C#)

Getroffene Entscheidungen werden in game/docs/decisions.md festgehalten.

## Arbeitsweise
- Vor größeren Änderungen einen kurzen Plan vorlegen.
- Erkenntnisse über den Original-Code in game/docs/ dokumentieren, nicht nur im Chat.
- Antworten kurz und auf Deutsch.
- Mechaniken vorerst eng am Original (kod/) umsetzen, Abweichungen in game/docs festhalten.
  Nicht jeden Schritt abfragen, sondern umsetzen, testen, committen.

## Nächster Schritt
Stand: Server mit Login, Game-Modus, Tos (18 Räume), NPCs mit Dialogen, Kampf (docs/combat.md),
Monster auf Friedhof/Krypta. Gegenstände, Inventar, Schillinge, Kaufen/Verkaufen bei Tos-Händlern, Bank,
Beute zum Aufheben (docs/trade.md, eng am KOD-Original). Client (Godot 4.7.2) mit texturiertem Tos, animierten
Figuren aus Blender (Zwerg, Zombie, Skelett via game/tools/blender/make_character.py), Lebensanzeige, Angriff (F),
Inventar (I), Laden (B), Aufheben (G). Nichts davon gegen das Original getestet.
Accounts und Spielstand der Charaktere in SQLite (meridian.db, decisions.md).
Gegenstände benutzen/ausrüsten mit Waffenschaden, Rüstung, Essen und Ausdauer (combat.md, trade.md).
Handel zwischen Spielern (trade.md).
Tag-Nacht-Wechsel mit Originaluhr und Raumlicht (daynight.md).
Monster-Wegfindung per A* auf den Raum-Höhenrastern (combat.md).
Teleport-Erkennung für Spielerbewegungen wie im Original (combat.md).
Häuser mit Fachwerkfassaden und Innenräume mit Decke/Lampen im Client (client/README.md).
Original-Client (Steam, Wine) getestet: Login, Welt, Chat, Anschauen, Kaufen, Anbieten, Bank, Türen, Kampf, Beute gehen (original-client-test.md).
Magie erster Umfang: Charakterwerte, Mana, Zaubern, 4 Sprüche, Lernen (docs/magic.md).
Charaktererstellung wie im Original, im Original-Client getestet (docs/character.md).
Kampf nach Original-Formeln mit Fertigkeiten (docs/combat.md, combat-original.md).
Godot-Client: echte Animationen (Quaternius CC0, retargetet), Türen per Linksklick, flüssige Bewegung.
Charaktererstellung auch im Godot-Client (Dialog, `--create show|auto`).
Springen mit der Leertaste (nur Client-Optik, docs: client/README.md).

Persönliche Verzauberungen: Segen, Superstärke, Eile, Widerstände (docs/magic.md).

Raza (Neulingsinsel, 8 Räume, Start neuer Charaktere, Portal nach Tos) eng am Original (docs/raza.md).

Raza vollständig: Mausoleum-Rätsel mit Hebeln/Tür/Endkampf, Museum, Kleidung (docs/raza.md).

Marion mit Gasthaus, Läden, Gruft (3 Ebenen) eng am Original; Raza-Portal führt dorthin (docs/marion.md).

Cor Noth mit Läden, Schneiderin, Waffenmeister (Fertigkeiten lernen wie Sprüche) (docs/cor.md).

Jasper (Ost/West, Bank mit Tos-Konto, Läden, 14 verlassene Häuser) (docs/jasper.md). Raza-Portal würfelt Marion/Cor/Jasper.

Barloque (Nord/Süd, Hafen, Läden, Gericht, Gefängnis, Gildenmeisterhalle) (docs/barloque.md).

Godot-Client: WoW-Steuerung/Kamera, Waldränder auf grünen Karten, Spielerlicht in dunklen Räumen, Lauf-Automatik --route.
Einrichtung der Räume: Möbel/Lichter/Zierobjekte aus dem KOD (tools/add_furniture.py) plus Wandausstattung je Raumart (client/README.md).

Godot-Client: Arme korrekt (Gliedmaßen nach Quellrichtung), 29 Clips inkl. Sitzen, Ducken, Tanzen, Schwimmen; detailliertere Texturen. Testdatenbank dauerhaft in ~/.local/share/meridian.
Monster datengetrieben: alle 62 Arten des Originals in server/data/monsters/monsters.json (tools/make_monsters.py aus docs/kod/monsters_catalog.json), Aggression/Karma, Flucht, Gift, alle Beutetabellen (docs/monsters.md).
Monster-Modelle: 46 eigene prozedurale Modelle mit Animationen (tools/blender/make_creature.py, creatures.py), Datei = Name der Original-Grafik; verschmolzene Körper per Distanzfeld (sdf.py) mit Hautrelief, Zeichnung, Gesichtstypen. Fremde Monster-Pakete verworfen (Quaternius zu niedlich, Sketchfab nicht gewünscht).
Zauberschulen Teil 1: 37 von ~150 Sprüchen (Angriff, Schwächung, Fläche, Heilung, Gift heilen; docs/magic.md). Katalog aller Sprüche: docs/kod/spells_catalog.json, docs/spells-original.md (Reihenfolge dort).
Wildnis (53 Räume, automatisch aus dem KOD: tools/kodroom.py, make_wilderness.py) und Tempel Shal'ille/Kraanan/Qor + Faren-Priesterin als Spruchlehrer (make_temples.py, docs/wilderness.md); Zonen-Übergänge und Spawnzeiten wie im Original. Alle Städte zu Fuß verbunden. Übersicht des Rests: docs/world-remaining.md.
Weitere Gebiete automatisch aus dem KOD (tools/make_areas.py, docs/areas.md): Kanalisationen mit Lupogg-König, Burgen mit Herzog/Prinzessin, Nekropolis, Spinnennest, Feenwald, Gildenhallen (ohne Gilden).
Sprüche: 60 von ~150 (Raumzauber, Schutz, Waffenweihen, Kara'hol, Nachtsicht, Manabombe …; docs/magic.md).
Offen (Magie): Wände, Riija-Illusionen, Jala-Lieder, PvP-Sprüche, Leichen/Séance, Flüche/Krankheiten.
Heimatstadt je Spieler wie im Original (Ahnenhalle Cor Noth, docs/cor.md); zaubernde Monster (docs/monsters.md).
Offen: Ko'catan, Orkhöhlen, Gruft-Rätsel in Marion, fehlende Gegenstände (Tränke, Zauberstäbe, Ringe, Rüstungen), Gilden, Mietzimmer, PvP, Rätsel der Wildnis.
