# Raza – die Neulingsinsel (Stand 2026-11-02)

Quelle: `kod/object/active/holder/room/guestN.kod` + `guestN/newbN.kod` (Raza-Fassung der Hazar-Räume),
NPCs aus `kod/object/active/holder/nomoveon/battler/monster/towns/hazartwn/`, Portal `kod/object/active/portal/newbport.kod`.
Die Daten erzeugt `tools/make_raza.py` → `server/data/world/raza.json`; Läden `server/data/shops/raza.json`,
Dialoge `server/data/dialog/raza.json` (sinngemäß übersetzt). Grundrisse aus `guest1–8.roo` (`tools/roo2layout.py`).

## Gebietsdateien
Der Server lädt alle Dateien in `data/world/` (Option `--world` nimmt einen Ordner oder eine Datei) und führt sie zusammen;
Dialoge und Läden gleichnamig aus `data/dialog/`, `data/shops/`. Die Datei mit `"start": true` (raza.json) bestimmt den
Startraum neuer Charaktere, `respawnRoom` (tos.json: 50) den Wiederbelebungsraum, falls ein Raum keinen eigenen hat.

## Räume (RID_NEWB_BASE 1010 + n)
| RID | Raum | .roo | Bewohner |
|---|---|---|---|
| 1011 | Raza Inn (Start, Ankunft 7/4) | guest1 | Marcus (Wirt, verkauft nichts), Schild „Willkommen“ |
| 1012 | Raza (Dorf, Ankunft 23/41) | guest2 | 3 Kühe, Schild „Unterwegs“, 76 Zierobjekte (Bäume, Heu, Lampen) |
| 1013 | Raza Blacksmith | guest3 | Tomas (Schmied) |
| 1014 | Ravi's Magicks of Raza | guest4 | Ravi (Apotheker) |
| 1015 | Raza Hut | guest5 | Rodric d'Stane (Ältester) |
| 1016 | Mausoleum | guest6 | Mumien (Kampfraum) |
| 1017 | Raza Pub | guest7 | Eric d'Jorn (Schenke) |
| 1018 | The Grand Museum of Raza | guest8 | Portal aufs Festland |

Alle Ausgänge, verschlossenen Türen (mit Texten), Ankunftspunkte, Licht (LIGHT_NICE 164 / OUTDOORS_SOME 5,
Mausoleum LIGHT_DARK 124 / 0) und Musik wie im Original. Ausgang ins Mausoleum mit SET_ANGLE_WEST (Blick nach Westen).

## Regeln
- **Kein Kampf** außer im Mausoleum (ROOM_NO_COMBAT): „Hier ist kein Kampf erlaubt.“ Die Kühe (cow.kod, Stufe 200,
  MOB_NOFIGHT) stehen nur herum und sind nicht angreifbar.
- **Tod in Raza:** man erwacht im Gasthaus von Raza (user.kod), sonst wie bisher in Tos.
- **Üben** (newbN ModifyChanceToImprove): Sprüche und Fertigkeiten steigen in Raza höchstens bis 25 %, die Chance ist × 3/2.
- **Mausoleum:** Mumien (mummy.kod: Stufe 25 → 25 LP, Schwierigkeit 2, langsam, aggressiv, Beute TID_NEWBIE_AREA:
  roter Pilz 30, Entwurzelbeere 20, Rubin 15, Snack 15, Orkzahn 10, Streitkolben 7, Kräuter 3) und Mumien ohne Beute,
  je 50 %, an 10 Spawnpunkten, höchstens 20.
- **Läden:** Tomas verkauft Kettenrüstung, Lederrüstung, kleinen Rundschild, Kurzschwert, Streitkolben (MERCHANT_BARGAIN)
  und kauft Waffen/Rüstungen/Schilde; Ravi verkauft Pilze, Kräuter, Holunderbeeren, Smaragd, Flasche, Fackel und kauft
  Reagenzien und Edelsteine; Eric verkauft Brot, Äpfel, Fleischpasteten, Starkbier und gibt für **mindestens 10
  Entwurzelbeeren einen Ritterschild** (hzbart.kod).
- **Mausoleum-Rätsel** (monsroom/guest6.kod, newb6.kod): Zwei Hebel (27/4, 27/9, `BP_REQ_ACTIVATE`, Reichweite 1 Feld,
  Bildgruppe 1 oben / 2 unten). Ein Hebel hebt die Tür (Sektor 3, `BP_SECTOR_MOVE` Decke 84 → 128), nach 2 s schlägt sie
  zu (84, Tempo 128) und die Hebel springen zurück. Beide Hebel öffnen sie ganz (172); dann prüft alle 10 s ein
  Zeitgeber, ob noch ein Spieler im Endbereich ist (Zeilen 28–40 × Spalten 1–9, Gang 16–28 × 1–2, Spalte < 3 ab Zeile
  16) – sonst Rücksetzen. Betritt der erste Spieler den Raum, kommt eine Mumie ohne Beute in die Grube (11/4, höchstens
  zwei, andere Mumien nie dort) und der Endbereich wird mit vier Mumien und einer **Schattenmumie** (Schwierigkeit 4,
  schwarz) besetzt. Sind sie besiegt und ist ein Spieler im Endbereich, senkt sich die Säule (Sektoren 4/5 Boden
  500/519 → 105/124), die Tür schließt, und es liegen 5–10 Holunderbeeren, 10–15 Kräuter, 10–15 Pilze und
  450–550 Schillinge bereit. Neu Eintretende bekommen den aktuellen Sektorzustand. Der Server sperrt die Türfelder im
  Raster, solange die Tür zu ist (Mumien kommen nicht heraus).
- **Museum:** sieben Ausstellungsschilder (Willkommen, Arena von Tos, Tos, Tal der Trauer, Pyramide von Ko'catan,
  Handelsposten, Wandbilder) an den Originalplätzen.
- **Kleidung bei Eric:** Rock (orange), Hose (dunkelgrau, PantsC), Hose (schwarz, PantsA), Hemd (orange), Hemd (grün).
  Ein Hemd färbt Körper und Arme (`ANIMATE_TRANSLATION` 0x87 + 11 × Farbe + Hautton, util.kod EncodeTwoColorXLAT),
  Hose/Rock ersetzen die Beine (bfa/bfb, bfe/bff, bfc/bfd) mit Farbe; die dunkelgraue Hose nutzt PT_GRAY_TO_KGRAY (0x2B).
- **Portal** im Museum (2/11): beim ersten Betreten die Warnung, nach Verlassen und erneutem Betreten aufs Festland mit
  voller Ausdauer und `AddRealWorldObjects` (1000 Schillinge, Streitkolben, 5 Äpfel, Reagenzien für je 5 Zauber).
- Neue Charaktere beginnen im Gasthaus von Raza; bestehende Spielstände bleiben, wo sie ausgeloggt haben.

## Abweichungen
- Das Portal führt ins Gasthaus von Tos (52). Im Original würfelt es Marion, Jasper oder Cor; diese Städte fehlen noch.
- Zwei Kühe stehen im KOD außerhalb der 30-zeiligen Karte (Zeile 31 und 35) – sie stehen auf den nächsten Feldern am Südrand.
- Kühe laufen nicht umher und machen keinen Dung (Bonked).
- Es fehlen noch: die Zierobjekte des Museums (Nachbildungen, ModelNode), der Schädel im Mausoleum, die Neulingspost,
  Geräusche (down2.wav beim Hebel). Die Kleidungsfarbe nutzt den Hautton des Trägers (im Original der des Gegenstands,
  SKIN1). Der Endkampf mit Belohnung ist im Server umgesetzt, aber nicht automatisch getestet.
- Die Schilder sind Objekte zum Anschauen (wie Rechtsklick im Original); ihre Texte nennen die Tasten des Original-Clients.
- Godot-Client: Sektorbewegungen wirken sofort (ohne Animation), die Raumgeometrie wird neu gebaut; geschlossene Türen
  sind Wände. Begehbar ist ein Nachbarfeld höchstens eine Stufe (30) höher als das eigene – so kommt man Rampen hinauf
  (auch Server-Wegfindung). Hebel per Linksklick. Kleidung ist im Godot-Client nicht sichtbar (Zwergenmodell).
- Godot-Client: Wiese/Wasser aus den Sektor-Tiefen der .roo (SF_DEPTH), Bäume aus den Zierobjekten, Kuh/Portal/Schild
  als einfache Formen, Mumien mit dem Zombie-Modell. Kleine Innenräume (Gasthaus) sind teils zum Himmel offen.
