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

## Burgen (`castles.json`)
| RID | Raum | Bewohner / Monster |
|---|---|---|
| 950–954 | Blackstone Keep: Hof, Burg, Gemächer des Herzogs, Festhalle, großer Ballsaal (Zugang Tos 12/18) | Herzog Akardius (Gemächer), Wachen, Wirt Rycksher (Ballsaal) |
| 850–853, 856 | Bourgeois-Burg: Bankettsaal, Burg, Ratskammer, Bibliothek, Madelias Schneiderei (Zugang J3) | Prinzessin Kateriina (Ratskammer), Wachen, Schneiderin Madelia |
| 2, 38–41 | Castle Victoria: Vorplatz, Burg, Obergeschoss, Thronsaal, Kerker (Zugang I9) | Skelette, Zombies, Hauer-Skelette; Ratsherr Drechx (Vorplatz) |

Fraktionsführer stehen wie in `factions.kod` (`viHomeroom`, `viMyRow/Col`) in ihrem Raum.

Nicht umgesetzt: Fraktionen (Herzog, Prinzessin, Rebellen), Ratsherren und Fahnen, Madelias Laden, Thron und
Kohlebecken als Zierobjekte, Sonderlogik der Bibliothek (castle2d) und des Ballsaals (duke5), Mockers- und
Bazmans-Raum (Verwaltung).

## Nekropolis, Lich-Labyrinth, Unterwelt, Spinnennest (`necropolis.json`)
| RID | Raum | Monster |
|---|---|---|
| 826–830 | Winding Caverns, Ancient Graveyard, Decaying City, Within the Walls, Ruins of Castle Brax (Zugang G8) | Dämonenskelette, Narthyl-Würmer, Hauer-Skelette |
| 831, 832 | Ancient Building (zwei Häuser in der Stadt) | wie oben |
| 833 | Goad's Grinder (Arena) | – |
| 825 | The Dungeon of Castle Brax (Lich-Labyrinth) | – |
| 35 | The Spider Nest (Zugang Forest2) | Spinnenkönigin fest, Spinnen, Spinnenjunge |
| 1 | The Underworld | – (nicht angebunden) |

Neu im Generator: Ränder mit Bedingung (`ROW/COL_IS_GREATER/LESS_THAN`, z. B. Nekropolis 5 → Labyrinth je nach
Spalte; betrifft auch 22 Wildnis-, Kanal- und Burgräume) und fest platzierte Monster aus `Create(&Monster)`
(Spinnenkönigin, Steintrolle in I9, Yeti in der Eishöhle).

Nicht umgesetzt: Lich (entsteht im Original nur in der Gildenhalle der Nekromanten), Rätsel und Fallen des Labyrinths,
Arena-Kämpfe (Goad's Grinder), Unterwelt als Totenreich (Tote erwachen hier wie bisher in ihrer Stadt),
Nachwachsen der Spinnenkönigin.

## Feenwald (`feyforest.json`)
Acht Karten nördlich der Wälder von Ileria (A1, B1, B2, C1 The Fey's Crossing, C2 Vale of Sorrows, C3 Quilicia Wood,
D1, D2), Zugang von C4 nach Norden. Monster wie `feyforst.kod TryCreateMonster`: Fee oder böse Fee (vereinfacht
je 50 %; im Original entscheidet das Karma des Waldes), Spawnzeit je Karte (`piGen_time` 50–240 s, 80 %).
Nicht umgesetzt: Waldkarma, Feenknoten (c2), Feenstaub.

## Einsiedlerhütte und Universität (`huts.json`)
The Hermit's Hut (37, Zugang I7 und Forest3) mit Alzahakar, dem Ratsherrn; Meridian University (902) – im Original
führt kein Weg hinein, sie ist auch hier nicht angebunden. Bogenbauer und Handelsposten liegen auf Ko'catan.

## Gildenhallen (`guildhalls.json`)
13 Hallen des Festlands (701–704, 706–712, 714, 715), jeweils mit dem Zugang aus ihrer Stadt bzw. Wildnis, z. B.
Mercenary Alley und Ivory Chapel (Cor Noth), Hall of Explorers (Barloque Süd), Wryn's Keep und Jaarba's Abode
(Jasper), Den of Shadows (F8), Sewer Hideout (Kanalisation Barloque), Inner Sanctum of Queen Venya (Lich-Labyrinth).
Die beiden Hallen auf Ko'catan (705, 713) fehlen. Nicht umgesetzt: Gilden, Kauf und Besitz einer Halle, Passwort-
und Gildentüren, Lich in der Nekromantenhalle (guildh15). Mietzimmer (Barloque, Marion) entstehen im Original je
Mieter (Raumnummern ab 10000) und kommen mit dem Mietsystem.
