# Marion (Stand 2026-11-02)

Quelle: `kod/object/active/holder/room/marnrm/` (Stadt, Halle, Heilerin), `monsroom/objroom/mar*.kod` (Schmiede, Gasthaus,
Hütte des Ältesten), `monsroom/marcryp1.kod`, `marcryp2.kod`, `marcry3a.kod` (Gruft), Bewohner in
`kod/.../monster/towns/marntwn/`. Daten erzeugt `tools/make_marion.py` (Bausteine `tools/kodarea.py`) →
`server/data/world/marion.json`; Läden `server/data/shops/marion.json`, Dialoge `server/data/dialog/marion.json`.

## Räume
| RID | Raum | .roo | Bewohner / Monster |
|---|---|---|---|
| 200 | Marion (Ankunft 56/39, kein Kampf) | marion.roo | Ran er'Hoth (Ältester, draußen auf 80/37), Mimosenbaum (Gedenkschild) |
| 201 | Ye Olde Slasher Salesman | marsmith.roo | Colhorr (Schmied) |
| 202 | The Limping Toad Inn and Tavern (ROOM_HOMETOWN) | marinn.roo | Morrigan (Wirt), Tova (Schankwirt) |
| 203 | The home of the elder | marelder.roo | – (auch im Original ohne Eingang) |
| 204 | The Adventurer's Hall of Marion | marhall.roo | – |
| 205 | Lady Aftyn's Sanctuary | marheal.roo | Lady Aftyn (Heilerin) |
| 2600 | The crypt in Marion | MarDun01.roo | Gespenstermumien (9 Spawnpunkte, höchstens 10), 13 mumifizierte Leichen |
| 2601 | Resting place of Marion's ancestors | MarDun02.roo | Skelette 80 %, verwitterte Skelette 20 % (35 Spawnpunkte, höchstens 25), Warnschild |
| 2602 | Affirmation of the Forsaken | res00001.roo | Drescher (7 Spawnpunkte, höchstens 15), 10 verschlossene Gitter |

Ausgänge, verschlossene Türen (Gruft-Tor 70–71/13–14, Mietzimmer im Gasthaus), Licht (LIGHT_NICE 164, Gruft 124/84) und
Musik wie im Original. Der Eingang zur Gruft liegt bei 80/15 südlich des Tors.

## Bewohner
- **Tova** (MERCHANT_RIPOFF): Starkbier ×4, Brot, Fleischpastete; auf Nachfrage („Entwurzel“) 4 Entwurzelbeeren für 200.
- **Morrigan** (MERCHANT_RIPOFF): Hosen hellrot/dunkelorange (PT_GRAY_TO_LRED 0x21 / KORANGE 0x22), Kräuter ×5,
  Holunderbeeren ×4, Fackel; kauft Reagenzien, Kleidung/Rüstung, Lichter.
- **Colhorr** (MERCHANT_NORMAL): Streitkolben, Kurzschwert, kleiner Rundschild, Lederrüstung, Axt; kauft Waffen und Schilde.
- **Lady Aftyn** (MERCHANT_DISCOUNT): Flasche.
- **Ran er'Hoth**: auf Nachfrage („Orkzähne“) 4 Orkzähne für 350.

## Monster
| Art | Stufe/Schwierigkeit | Tempo | Beute | Besonderes |
|---|---|---|---|---|
| Gespenstermumie (specmum.kod) | 40/5, 48 LP | SPEED_SLOW | TID_MUMMY | 50 % durchscheinend, Resistenzen |
| Skelett (skel.kod) | 75/5 | SPEED_AVERAGE | TID_TOUGH | wie in Tos, Langschwert |
| verwittertes Skelett (batrskel.kod) | 60/4, 72 LP | SPEED_AVERAGE | TID_SKELETON2 | Langschwert |
| Drescher (thrasher.kod) | 150/7, 180 LP | SPEED_FAST | TID_THRASHER | Resistenzen (Blitz 90 %) |

## Raza-Portal
Das Portal würfelt wie `SetRandomHomeroom` (Marion 3, Cor 4, Jasper 3 von 10) über die vorhandenen Heimatstädte –
derzeit führt es also immer ins Gasthaus von Marion (Ankunft 11/6).

## Noch nicht umgesetzt / Abweichungen
- Gruft 1 (marcryp1.kod, umgesetzt 2026-11-03, gleiche Mechanik wie das Mausoleum in Raza): zwei Hebel (27/4, 27/9);
  einer hebt die Tür halb und sie schlägt nach 2 s zu, beide öffnen den Endbereich und schließen den Weg nach unten.
  Ist der Endbereich leer, wird er beim Umlegen mit einer lebenden Statue (32/7) und fünf Gespenstermumien besetzt.
  Sind alle besiegt und steht ein Spieler darin, öffnet sich der Weg zur zweiten Gruft (Sektoren 6/7); vorher ist der
  Durchgang verschlossen. Ist niemand mehr im Endbereich, setzt sich alles zurück.
- Noch nicht: lebende Statuen in Gruft 2 und die Tür zu Gruft 3a, Fallen, schlafende Drescher und die Truhe in Gruft
  3a, Martyrs' Battleground (nur mit PvP), Totenbeschwörer-Tür (Passwort nur für Spielleiter).
- Fehlende Beute (Katalog): Nerudit-Pfeil, Zwietrachtrolle, Identifizierstab, Dunkelengelfeder, Schuppenpanzer,
  Nachtsichttrank, Berserkerring; Zusatzbeute der Drescher.
- Sprüche Ausbessern (Colhorr), Destillieren (Aftyn), Waffenstillstand (Ran) und Aftyns Tränke fehlen; ebenso
  Zimmermiete (Morrigan), Stimmungen der NPCs, Geschenke an Tova, das Rosen-Opfer am Mimosenbaum (Segen).
- Die Wälder vor der Stadt (C4, C5, Forest5) gibt es noch nicht; die Ränder führen nirgendwohin.
- Godot-Client: Gras-Gelände, Mumien und Drescher mit dem Zombie-Modell, Leichen als Knochenhaufen.
