# Neuer Client (Godot 4.7 .NET, C#)

Stand: Platzhalter-Grafik, in Godot 4.7.2 gestartet und mit dem neuen Server getestet (Tos). Anmeldung, automatische Charakterwahl (erster Charakter),
ein Raum als Fläche, Spieler als Kapseln mit Namen, Laufen, Drehen, Chat.
Die Protokoll- und Ablauflogik steckt in `game/shared/Meridian.Protocol` (`ClientSession`),
das Godot-Projekt macht nur Darstellung und Eingabe (`scripts/Main.cs`).

## Starten

1. **Godot 4.7 .NET** installieren (godotengine.org → Download → „.NET“). Die normale Version reicht nicht.
2. In Godot `game/client/project.godot` öffnen, F5.
3. Vorher den Server starten (siehe `game/server/README.md`) und einen Account anlegen.

Bauen ohne Editor (prüft nur, ob der Code kompiliert): `dotnet build game/client`.

## Automatisch starten (Tests, Screenshots)

```bash
godot --path game/client -- --login Thorin bart --port 5959 [--host 127.0.0.1] [--camera front] [--walk 3] [--screenshot bild.png]
```

`--delay s` wartet vor dem Bild (Standard 1,5 s), `--fight 1` greift automatisch das nächste Monster an,
`--create show` fotografiert den Erstellungsdialog eines neuen Accounts, `--create auto` erstellt mit Vorgaben
und betritt die Welt. `--jump 1` springt kurz vor dem Bildschirmfoto. `--cast NAME` wirkt den Spruch auf sich, bis er gelingt
(mit `--create auto` wird er bei der Erstellung gewählt). `--inventory 1` öffnet das Inventar, `--shop 1` die Ladenliste des nächsten Händlers.
`--login` meldet sich sofort an. Mit `--screenshot` wird nach dem Betreten des Raums ein Bild gespeichert
und der Client beendet; `--walk` läuft vorher n Sekunden, `--camera front` zeigt die Figur von vorne.

## Steuerung

| Taste | Aktion |
|---|---|
| W / S | vorwärts / rückwärts (rückwärts langsamer) |
| A / D | drehen; mit gedrückter rechter Maustaste seitlich |
| Q / E | seitlich gehen |
| Shift | gehen statt rennen |
| NumLock | Dauerlauf an/aus |
| Leertaste | springen |
| rechte Maustaste | Figur dreht sich in Kamerarichtung; ziehen dreht Figur und Kamera |
| linke Maustaste ziehen | Kamera kreist frei; beim Vorwärtslaufen schwenkt sie zurück |
| beide Maustasten | vorwärts laufen |
| Mausrad | Zoom (weich); ganz nah Ego-Ansicht |
| Linksklick | Tür/Hebel benutzen, Figur anschauen |
| T | nächste Figur anschauen |
| F | nächstes Monster angreifen |
| I / B / G / Z | Inventar / Laden / aufheben / Sprüche |
| Enter | Chat |

Tempo wie WoW: Rennen 7 yd/s (6,4 m/s), rückwärts 4,5 yd/s, Gehen 2,5 yd/s, Drehen 180°/s. Seitlich und schräg
dreht sich der Körper in Laufrichtung, rückwärts läuft die Animation rückwärts; im Sprung bleibt der Schwung.
Die Kamera hängt fest am Kopf, Hindernisse holen sie heran, danach fährt sie langsam zurück.

Test-Automatik: `--route "Raza,Mausoleum"` läuft mit echter Bewegung und Türklick durch die Räume.

## Koordinaten

Server-Koordinaten sind KOD-Feineinheiten (64 pro Grid-Feld, Zeile/Spalte 1-basiert).
Im Client: 1 Feld = 2,5 m, Spalte → X, Zeile → Z, Winkel 0..4095 → 0..2π.
Positionen werden wie beim Original-Client höchstens 1×/s gesendet.

## Texturen

`textures/*_albedo.png` und `*_normal.png` (Pflaster, Putz, Holzdielen, Dachziegel, Mauerstein) sind eigene,
prozedural erzeugte Texturen (`game/tools/make_textures.py`). Sie werden triplanar in Weltkoordinaten
aufgelegt, die Blöcke brauchen daher keine UVs.

## Raumgeometrie (Platzhalter)

`rooms/*.json` sind grobe Höhenraster (ein Wert pro Feld), erzeugt mit `game/tools/roo2layout.py`
aus den Original-`.roo`-Dateien. Sie übernehmen nur den Grundriss (Straße, Gebäude bis Dachhöhe,
massive Wand), keine Texturen oder Original-Geometrie. Der Client baut daraus Blöcke (`RoomLayout.cs`)
und verhindert das Laufen durch Gebäude. **Wird durch eigene Karten ersetzt.**

Neu erzeugen (im Repo-Root):

```bash
python -I game/tools/roo2layout.py game/server/data/world/tos.json resource/rooms game/client/rooms
python -I game/tools/roo2layout.py game/server/data/world/raza.json resource/rooms game/client/rooms
cd game && python3 tools/export_exits.py
```

`roo2layout.py` schreibt zusätzlich die Wassertiefe der Sektoren (`depth`, SF_DEPTH1–3) und Sektoren mit ID
(`tags`, für `BP_SECTOR_MOVE`, `scripts/RoomMechanics.cs`), `export_exits.py`
danach Ausgänge, Türen, Gelände (`terrain: "village"` → Wiese) und Bäume/Sträucher aus den Zierobjekten
(`trees`). Wasser wird glatt und leicht durchsichtig gezeichnet. Kuh, Portal, Schild und Knochenhaufen sind
einfache Formen (`scripts/PropModels.cs`), Mumien nutzen das Zombie-Modell.

Noch nicht gegen den Original-Server getestet.

## Häuser und Innenräume (`scripts/RoomDecor.cs`)

Prozedural über dem Höhenraster, ohne fremde Assets: Fassaden an der Straße mit Putzfeldern in wechselnden
Farben, Fachwerk, Steinsockel, Fenstern (nachts warm leuchtend), Türen und Dachüberständen. Innenräume mit
Holzdecke und Balken, Vertäfelung an Wänden und Einbauten und Lampen alle vier Felder. Die Kamera bleibt innen
unter der Decke. Das Licht (Sonne, Himmel, Umgebung) folgt dem Raumlicht des Servers (`docs/daynight.md`).

## Animationen

Die Bewegungen stammen aus der „Universal Animation Library“ von Quaternius (CC0, Standardversion; Spiegel
github.com/J-Ponzo/gltf-universal-animation-library). `tools/blender/retarget_animations.py` überträgt sie auf
unsere Modelle:

    blender -b -P game/tools/blender/retarget_animations.py -- AnimationLibrary_Godot_Standard.gltf game/client/models/dwarf.glb

Clips: Idle, Walk, Run, Sprint, Attack, Punch, Punch2, Die, Hit, HitHead, Cast, CastIdle, Jump_Start/Loop/Land,
Interact, PickUp, Idle_Torch, CombatIdle, Talk, Dance, SitDown/Sit/StandUp, Crouch, CrouchWalk, Roll, Swim, SwimIdle.

Rumpf, Hals und Kopf übernehmen die Drehung relativ zur Ruhelage; Gliedmaßen werden auf die Knochenrichtung der
Quelle ausgerichtet, weil Quelle (A-Pose) und Ziel (T-Pose) verschiedene Grundhaltungen haben – sonst stehen die
Arme verdreht ab und schwingen kaum. Seitwärts- und Rückwärtsclips fehlen in der Bibliothek: seitlich dreht sich der
Körper, rückwärts läuft der Gehen-Clip rückwärts.

## Springen (`scripts/JumpControl.cs`)

Leertaste: Sprung mit ca. 0,9 m Höhe (4,2 m/s Absprung, 9,8 m/s² Schwerkraft), Animationen Jump_Start/Loop/Land.
**Abweichung vom Original:** Meridian 59 kennt kein Springen und das Protokoll keine Höhe – der Sprung ist rein
optisch, andere Spieler sehen ihn nicht, über Wände oder auf Dächer springt man nicht. Die Kamera bleibt auf
Fußhöhe. Die Leertaste wird vor der GUI abgefangen (sonst löst sie den zuletzt geklickten Button aus); in
Textfeldern bleibt sie ein Leerzeichen.
