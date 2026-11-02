# Jasper (Stand 2026-11-02)

Quelle: `kod/object/active/holder/room/jasperrm/`, Bewohner `kod/.../monster/towns/jasprtwn/`. Daten erzeugt
`tools/make_jasper.py` → `server/data/world/jasper.json`; Läden `server/data/shops/jasper.json`, Dialoge
`server/data/dialog/jasper.json`. Alle Räume ohne eigene Musik spielen city03.mid (JasperRoom).

## Räume
| RID | Raum | .roo | Bewohner |
|---|---|---|---|
| 350 | East Jasper (Ankunft 40/54, ROOM_GUILD_PK_ONLY) | jas-east.roo | – |
| 382 | West Jasper (Ankunft 37/25) | jas-west.roo | – |
| 370 | Yonder Inn of Jasper (ROOM_HOMETOWN, ROOM_TRIPLE_HEAL) | jasinn.roo | Witwe Qesino |
| 371 | Pietro's Wicked Brews | jastavrn.roo | Pietro |
| 372 | Adventurer's Hall of Jasper | jashall.roo | – |
| 373 | General Store of Jasper | jasstore.roo | Zhieu B'hob |
| 374 | Quintor's Smithy | jassmith.roo | Quintor |
| 375 | The Home of the Wise Man | jaselder.roo | Afiera D'xor |
| 376 | The Royal Bank of Jasper | jasbank.roo | Yevitan (Bankier), Schild |
| 351–364 | verlassene Häuser (Old Granary … Warm Haven) | jasab1–14.roo | – |

Die Tore zwischen Ost und West (groß, klein, versteckt), alle Ausgänge, die 18 verschlossenen Türen (mit Texten für
Schürferhütte, Arsenal, Kornspeicher) und die Ränder (Ost → F8, Süd → Gildenhalle 4, West → D7, Nord → E7) wie im
Original. Von den verlassenen Häusern ist wie im Original nur die alte Schule (353) von der Stadt aus erreichbar.

## Bewohner
- **Yevitan** (Bankier): Bank 1 – dasselbe Konto wie in Tos (viBank = BID_TOS).
- **Witwe Qesino** (Wirtin): Eintopf ×4.
- **Quintor** (MERCHANT_BARGAIN): Streitkolben, Kurzschwert, Goldschild, Hammer, Lederrüstung; kauft Waffen und Rüstungen.
- **Zhieu B'hob** (MERCHANT_RIPOFF): Hosen in 7 Farben (PT_GRAY_TO_RED/LORANGE/LGREEN/LBGREEN/LSKY/LBLUE/LPURPLE),
  Hemden in 5 Farben, Fackel, Wasserschläuche, Saphir, Smaragde, Pilze, Holunderbeeren, Kräuter; kauft Reagenzien;
  auf Nachfrage 4 Rubine für 250.
- **Pietro** (MERCHANT_NORMAL): Brot ×4, Krug Gebräu, Fleischpasteten ×4.
- **Afiera D'xor** (Weiser): nur Dialog.

## Noch nicht umgesetzt
Kanalisation (377–379), Gildenhallen, Wildnis; Sprüche Blenden (Yevitan, ab 4999 Schillingen auf dem Konto), Fegen und
Wiederherstellen (Qesino), Blitz und Wahnsinn (Afiera, mit Aufgabe), Krummsäbelkampf (Quintor, mit Aufgabe);
Schuppenpanzer, Schach; Stimmungen; Rauchsäule, Fässer und Laterne als Zierobjekte; das Tagebuch in der alten Schule.
