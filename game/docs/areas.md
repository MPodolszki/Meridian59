# Weitere Gebiete (tools/make_areas.py)

Gebiete, die vollständig automatisch aus den KOD-Raumklassen entstehen (`tools/kodroom.py`, wie die Wildnis in
`docs/wilderness.md`): Räume, Ausgänge, Ränder, Zonen-Übergänge, verschlossene Türen, Monster und Spawnzeiten,
Bossräume (`plBossTemplate`/`plHenchmenTemplate` als feste Monster), friedliche Bewohner (Klassen mit
`MOB_NOFIGHT`, Name und Grafik aus dem KOD) und Schilder.

```bash
cd game && python3 tools/make_areas.py [gebiet …] && python3 tools/export_exits.py
```

## Kanalisationen (`sewers.json`)
| RID | Raum | Monster |
|---|---|---|
| 108, 111, 112 | The Sewers of Barloque | Riesenratten, Lupoggs |
| 377–379 | The Sewers of Jasper | Riesenratten, Lupoggs |
| 380 | The Ancient Smuggler's Crossing (Versteck des Lupogg-Königs) | Lupogg-König und drei Lupoggs fest, Lupoggs |
| 110 | A shadowy corner (Assassinen-Versteck) | Roq, fünf Schilder |

Zugang: Barloque Nord (47/41) und Jasper West, die Kanalisationen beider Städte treffen sich im Versteck.

Nicht umgesetzt: Hebel, Schleusentore und Flutung im Versteck (Ertrinken), Bossschatz (Opfergabe, Langbogen,
mystisches Schwert, Jüngerrobe) und das Zurücksetzen nach 30 Minuten; Rebellentruppe (RebelTroop) in Jaspers
Kanalisation, die es als Monster noch nicht gibt; Assassinen-Gilde.
