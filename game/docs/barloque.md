# Barloque (Stand 2026-11-02)

Quelle: `kod/object/active/holder/room/barlqrm/`, Schenke `monsroom/objroom/barlbar1.kod`, Bewohner
`kod/.../monster/towns/barlqtwn/`. Daten erzeugt `tools/make_barloque.py` → `server/data/world/barloque.json`; Läden
`server/data/shops/barloque.json`, Dialoge `server/data/dialog/barloque.json`.

## Räume
| RID | Raum | .roo | Bewohner |
|---|---|---|---|
| 101 | North Barloque (Ankunft 25/42, GUILD_PK_ONLY, Lampen) | barln.roo | 35 Zierobjekte (Bäume, Sträucher, Lampen, Fässer, Urnen) |
| 102 | South Barloque (Ankunft 34/43) | barls.roo | 29 Zierobjekte |
| 103 | The Bhrama & Falcon (ROOM_HOMETOWN) | barlbar1.roo | Meidei (Schankwirt) |
| 104 | Joguer's Herbs and Roots | barapoth.roo | Joguer (Kräuterkundiger) |
| 105 | Adventurer's Hall of Barloque | barhall.roo | – |
| 106 | Brownestone Inn (ROOM_HOMETOWN) | barinn.roo | Pritchett (Wirt), 10 Mietzimmertüren |
| 107 | The Ports of Barloque | barlport.roo | – (28 verschlossene Türen) |
| 109 | The Sparkling Stone Shop | barmerch.roo | Herbutte (Edelsteinhändler) |
| 113 | The Royal Blacksmith of Barloque | barsmith.roo | Fehr'loi Qan (Schmied) |
| 114 | Office of the Barloque Vaultman | barvault.roo | Obert Cair'bre (Lagerverwalter), Schild |
| 970 | Office of the Justicar | barcourt.roo | Caramo, Wache, Schild |
| 971 | Old Jailhouse | barjail.roo | 3 Wachen |
| 700 | The Guildmaster's Hall | gmhall.roo | Frular (Gildenmeister) |

Alle Ausgänge (Tore Nord/Süd, Hafen, Gericht, Gefängnis), verschlossenen Türen und Musik (corloth.mid) wie im Original.

## Bewohner und Läden
- **Joguer** (DISCOUNT): Kräuter ×5, Pilze ×5, Holunderbeeren ×5, rote/blaue Pilze ×4, Flasche; kauft Reagenzien außer Edelsteinen.
- **Meidei** (EXPENSIVE): Kelch Wein, Starkbier ×4, Äpfel ×4, Wasserschläuche ×4, Brot ×4, Fleischpasteten ×4, Käse ×2.
- **Pritchett**: Hosen dunkelgrau/tiefschwarz (PT_GRAY_TO_KGRAY/BLACK), Truthahnkeule; auf Nachfrage Streitkolben für 100.
- **Herbutte** (DISCOUNT): Diamant, Fackel, Saphir, Smaragd; kauft Edelsteine und Kleinkram.
- **Fehr'loi Qan** (EXPENSIVE): Kettenrüstung, Axt, Hammer, kleiner Rundschild; kauft Waffen/Rüstungen; auf Nachfrage Streitkolben für 100.
- Obert, Caramo, Frular und die Wachen nur mit Dialog.

## Noch nicht umgesetzt
Kanalisation (108/111/112, Ratten und Lupoggs) und Assassinen-Versteck, Gildenhallen, Tor I3 und Wildnis; Lager
(Vault), Zimmermiete, Justiziar-Wahl, Gilden; Sprüche Hitze/Verblassen (Joguer, mit Aufgabe) und Magischer Schild;
Feuersand, Schach; Wasseruhr und Flackerlichter (Sektoren), die Sperre der Schankstreppe.
