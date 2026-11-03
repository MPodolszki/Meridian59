# Cor Noth (Stand 2026-11-02)

Quelle: `kod/object/active/holder/room/crnthrm/`, Bewohner `kod/.../monster/towns/crnthtwn/`. Daten erzeugt
`tools/make_cor.py` → `server/data/world/cor.json`; Läden `server/data/shops/cor.json`, Dialoge `server/data/dialog/cor.json`.

## Räume
| RID | Raum | .roo | Bewohner |
|---|---|---|---|
| 150 | Cor Noth (Ankunft 20/20, ROOM_GUILD_PK_ONLY) | cornoth.roo | 14 Bäume, 1 Strauch |
| 151 | Solomon's Edibles | corgroc.roo | Solomon (Krämer) |
| 152 | The Adventurer's Hall of Cor Noth | corhall.roo | – |
| 153 | Cibilo Creek Inn (ROOM_HOMETOWN) | corinn.roo | D'Franco (Wirt) |
| 154 | The Weapon Master's Abode | weaponsm.roo | Rook (Waffenmeister) |
| 155 | The Spindle and the Spinster | cortail.roo | Hester Gilk (Schneiderin) |
| 156 | The Hall of Genealogy (Universität) | coruniv.roo | – |

Alle Ausgänge (auch Gildenhallen 701/711 und Wildnis G4 574/G5 575, die es noch nicht gibt – „noch nicht begehbar“),
verschlossenen Türen und der Westrand nach G4 wie im Original; Drehungen ROTATE_180/225/270 als Grad.

## Bewohner
- **Solomon** (MERCHANT_EXPENSIVE 4): je 4 Fackeln, Holunderbeeren, blaue/rote Pilze, Wasserschläuche, Brot, Äpfel;
  kauft Reagenzien und Kleinkram.
- **Rook** (MERCHANT_RIPOFF 5): Langschwert, Kurzschwert, Streitkolben, Lederrüstung; **lehrt Fertigkeiten**
  Ausweichen, Faustschlag, Hieb, Axt-, Hammer-, Kurzschwert-, Streitkolben-, Schwertkampf, Raufen, Parieren.
- **Hester Gilk** (MERCHANT_DISCOUNT 2): 27 Kleidungsstücke – Hosen C mit PT_GRAY_TO_* (rot, hellgrün, himmelblau,
  dunkelblau, lila, gold, leuchtend blau, drei Haartöne), Hosen A in Hauttönen/Rot/Grau/Schwarz, Röcke in acht Farben,
  Hemden rot/schwarz/grau.
- **D'Franco** (Wirt): nur Dialog.

## Fertigkeiten lernen
Fertigkeiten werden wie Sprüche gelernt (monster.kod Buy, player.kod PlayerCanLearn): gleiche Regel, Schule
SKS_FENCING (10), Preis 250 × 2^Stufe (skill.kod GetValue), Anfangsfähigkeit Random(Req/2, Req)/2 (1..25). Die
Lernpunkte zählen jetzt Sprüche und Fertigkeiten gemeinsam (wie im Original beide Listen).

## Noch nicht umgesetzt
- Universität: Heimatstadt wählen per Satz (es gibt noch keine Heimatstadt je Spieler), die fünf Bücher.
- D'Franco: Spruch Beleben (Jala) und Ehering; Solomon: Feenflügel; Rook: Vergessenstrank für Kraanan, Monster-Ratschläge.
- Stimmungen der NPCs, Gerüchte über das Token, Uhr an der Wand, Gildenhallen und Wildnis um Cor Noth.

## Heimatstadt und Ahnenhalle (2026-11-03)
Wie user.kod `piHomeroom`: Wer durch das Raza-Portal geht, bekommt das Gasthaus, in dem er ankommt, als Heimat
(Marion 3, Cor Noth 4, Jasper 3 von 10). Nach dem Tod und mit dem Spruch Rettung erwacht man im Heimatgasthaus
(Raza bleibt Raza). Ohne gültige Heimat wird eine ausgewürfelt (AdminGoToSafety). In der Ahnenhalle von Cor Noth
(Coruniv.kod) legt man sie einmal fest, indem man den Schwur der Stadt spricht:

| Stadt | Schwur (Original sinngemäß) |
|---|---|
| Tos | „Ich bin in den alten Straßen von Tos aufgewachsen.“ |
| Barloque | „Gutes Barloque, deine königlichen Straßen seien meine Heimat.“ |
| Cor Noth | „Ich vertraue mein Wissen der freien Stadt Cor Noth an.“ |
| Jasper | „In Einigkeit stehen wir in Jasper.“ |
| Marion | „Marion, trautes Heim.“ |

Gespeichert im Charakter (magic-JSON, `Hometown`, negativ = fest). Abweichung: kein Umweg über die Unterwelt nach dem
Tod; die Herkunft in der Beschreibung („hat Jasper seit acht Jahren zur Heimat“) fehlt.
