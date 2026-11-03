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
- Gruft 2 (marcryp2.kod, umgesetzt 2026-11-03): beim ersten Betreten 37 lebende Statuen an ihren festen Plätzen und
  die Tür zu Gruft 3a (Sektor 5) zu; fällt die letzte Statue, hebt sich die Tür, vorher ist der Durchgang verschlossen.
- Gruft 3a (marcry3a.kod, umgesetzt 2026-11-03, `WorldCrypt3a.cs`): drei Fallenbereiche mit schlafenden Dreschern
  (Falle 1: Zeilen 23–25 × Spalten 17–26, ein Drescher auf 23/19; Falle 2: 6–7×3–4, 8–10×3–9, 11–14×8–9, zwei auf 10/3
  und 10/7; Falle 3: 29×31–32 und 30–34×28–34 plus Truhennische 30–33×23–27, drei auf 32/29, 30/34, 33/34). Schläfer
  stehen still und wachen auf, wenn ein Spieler auf 7 Felder herankommt oder sie angreift. Tötet ein Spieler, der selbst
  im Bereich steht, den letzten Drescher darin, öffnet sich die Falle: Sektor 1 Boden 1824 (zu 1864) für 15 s; Sektoren
  2/3 Decke 1958/1864 (zu 1910/1816) für 30 s; Sektor 5 Decke 2022 (zu 1960) für 45 s. Solange jemand im Bereich steht,
  bleibt sie offen, danach schließt sie und neue Schläfer erscheinen. Falle 3 wirft die Endbeute aus (N = Random(1,
  Spieler im Bereich), ab sechs Random(3,5) Würfe in die Liste von ResetTreasureList: Waffen, Plattenrüstung, Reagenzien in
  großen Mengen, 25000 Schillinge …). Erdbeben ist hier verboten.
  Abweichungen: Beute liegt vor der Truhe auf dem Boden (es gibt noch keine Behälter); Opfergaben, Prisma, Laute, Rose
  und die Zusatzkräfte der Waffen (Blender, Lähmer …) fehlen; Drescher ohne Drehangriff, Zweitschlag und Zusatzwaffe;
  Sichtlinie beim Aufwachen nicht geprüft; die verbotene Tür (31/8) bestraft nicht.
- Noch nicht: Martyrs' Battleground (nur mit PvP), Totenbeschwörer-Tür (Passwort nur für Spielleiter).
- Fehlende Beute (Katalog): Nerudit-Pfeil, Zwietrachtrolle, Identifizierstab, Dunkelengelfeder, Schuppenpanzer,
  Nachtsichttrank, Berserkerring; Zusatzbeute der Drescher.
- Sprüche Ausbessern (Colhorr), Destillieren (Aftyn), Waffenstillstand (Ran) und Aftyns Tränke fehlen; ebenso
  Zimmermiete (Morrigan), Stimmungen der NPCs, Geschenke an Tova, das Rosen-Opfer am Mimosenbaum (Segen).
- Die Wälder vor der Stadt (C4, C5, Forest5) gibt es noch nicht; die Ränder führen nirgendwohin.
- Godot-Client: Gras-Gelände, Mumien und Drescher mit dem Zombie-Modell, Leichen als Knochenhaufen.
