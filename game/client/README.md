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

`--login` meldet sich sofort an. Mit `--screenshot` wird nach dem Betreten des Raums ein Bild gespeichert
und der Client beendet; `--walk` läuft vorher n Sekunden, `--camera front` zeigt die Figur von vorne.

## Steuerung

| Taste | Aktion |
|---|---|
| W/A/S/D | laufen |
| Shift | rennen |
| Q/E oder rechte Maustaste + Maus | drehen |
| Leertaste | Tür/Ausgang benutzen (wie im Original) |
| Enter | Chat |

## Koordinaten

Server-Koordinaten sind KOD-Feineinheiten (64 pro Grid-Feld, Zeile/Spalte 1-basiert).
Im Client: 1 Feld = 2,5 m, Spalte → X, Zeile → Z, Winkel 0..4095 → 0..2π.
Positionen werden wie beim Original-Client höchstens 1×/s gesendet.

Noch nicht gegen den Original-Server getestet. Die Raumgeometrie (.roo) wird noch nicht geladen.
