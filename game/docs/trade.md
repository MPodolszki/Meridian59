# Gegenstände, Inventar, Geld und Handel (Stand 2026-11-02)

Eng am Original (KOD) umgesetzt. Code: `server/Meridian.Server/WorldTrade.cs`, `Items.cs`, `Shops.cs`;
Daten: `server/data/items/items.json`, `server/data/shops/tos.json`; Client: `client/scripts/TradeUi.cs`.

## Gegenstände und Geld
- Werte, Icons, Typen und „Mengen-Gegenstand“ (NumberItem) aus den KOD-Klassen (`viValue_average`, `viItem_type`).
  Namen und Beschreibungen sind eigener deutscher Text.
- Geld sind Schillinge (`Money`), ein Mengen-Gegenstand im Inventar wie im Original.
- Mengen-Gegenstände werden mit Tag 1 in der Objekt-ID und `u32 amount` übertragen (protocol.md 4.5) und im Inventar
  zusammengelegt. Mengenänderungen gehen per `BP_CHANGE` an den Client (KOD `SomethingChanged`),
  neue/entfernte Gegenstände per `BP_INVENTORY_ADD`/`_REMOVE`.
- Startausstattung: Streitkolben + 500 + 2000 Schillinge (player.kod `AddDefaultObjects`) = 2500 Schillinge; dazu
  Reagenzien für die Startsprüche. `AddRealWorldObjects` (1000 Schillinge, Streitkolben, 5 Äpfel, Reagenzien für je
  5 Zauber) gibt es wie im Original erst am Portal von Raza (docs/raza.md).

## Kaufen (`BP_REQ_BUY` → `BP_BUY_LIST`, `BP_REQ_BUY_ITEMS`)
- Preis pro Stück = Wert × (100 + 20 × Aufschlag) / 100, mindestens 1 (monster.kod `GetPrice`, Fraktionsbonus 100 %).
  Aufschlag `viMerchant_markup`: Paddock und Frisconar DISCOUNT (2, ×1,4), Skivlat BARGAIN (1, ×1,2).
- Mengen 1–500 (`MAX_BUY_AMOUNT`); Händler haben unbegrenzten Vorrat (`vbSellFromInventory = FALSE`).
- Reihenfolge der Liste wie KOD `GetForSale` (per `cons` aufgebaut → umgekehrt, bedingte Waren am Ende).
- **Bedingte Waren** (`InitCondSale`, LIBACT_CONDITIONAL): Fällt im Gespräch das Stichwort (z. B. „Purpurpilz“),
  bietet der Händler die Ware zum Festpreis an, bis jemand sie kauft (Paddock: 4 Purpurpilze à 250, 2 Orkzähne à 650;
  Frisconar: 4 Phiolen Solagh à 250).

| Händler | verkauft |
|---|---|
| Paddock (Wirt) | Krug Starkbier, Käse |
| Frisconar (Apotheker) | Fackel, Kräuter, Pilze, Holunderbeeren, blaue/rote Pilze, Flasche |
| Skivlat (Bankier) | Smaragde, Saphire, Diamanten |

## Verkaufen / Anbieten (`BP_REQ_OFFER` → `BP_OFFERED` + `BP_COUNTEROFFER`, `BP_ACCEPT_OFFER`/`BP_CANCEL_OFFER`)
- Nur Käufer (MOB_BUYER) kaufen: in Tos **Frisconar**, und nur Reagenzien, die keine Edelsteine sind (`ObjectDesired`).
- Gegenangebot in Schillingen: je Gegenstand Wert × (100 − 10 × Aufschlag) / 100, mindestens 1 (monster.kod `Offer`).
- Ein Händler bedient nur einen Kunden gleichzeitig; nach 30 s zieht er das Gegenangebot zurück (`viCancel_offer_time`).
- Empfänger (MOB_RECEIVE) nehmen gewollte Gegenstände direkt an:
  - **Skivlat**: Schillinge → Einzahlung aufs Konto; Edelsteine (mind. 4 gleiche) → Geld zum Ankaufspreis,
    mit 5 % Chance „schlechte Qualität“ (Steine weg, kein Geld) – TsBanker `CheckWhyWanted`.
  - **Paddock** nimmt nichts an („Was soll ich damit?“).

## Handel zwischen Spielern (user.kod UserOffer/UserCounterOffer/UserAcceptOffer)
- A bietet B Gegenstände an (`BP_REQ_OFFER` mit Spieler-ID) → A bekommt `BP_OFFERED`, B eine Systemmeldung und
  `BP_OFFER` (Anbieter + Gegenstände). Wer schon handelt, kann kein weiteres Angebot annehmen.
- B antwortet mit `BP_REQ_COUNTEROFFER` (auch leer = Geschenk) → B bekommt `BP_COUNTEROFFERED`, A `BP_COUNTEROFFER`.
- Nur A nimmt an (`BP_ACCEPT_OFFER`); dann wird geprüft, ob beide noch alles besitzen, und getauscht. B bekommt
  `BP_OFFER_CANCELED`, damit sein Dialog schließt.
- Abbruch durch einen (`BP_CANCEL_OFFER`), Raumwechsel oder Ausloggen beendet den Handel für beide.
- Client: Im Inventar „Anbieten“ wählt den nächsten Händler, sonst den nächsten Spieler. Ein Angebot öffnet einen
  Dialog: „Gegenangebot“ (ausgewählter Inventar-Gegenstand), „Ohne Gegenleistung“, „Ablehnen“.

## Bank (`BP_USERCOMMAND` UC_DEPOSIT 35 / UC_WITHDRAW 36 / UC_BALANCE 37)
- Geht nur, wenn ein Bankier im Raum ist. Texte und Prüfungen wie monster.kod `BankDeposit`/`BankWithdraw`.
- Im Client per Chat: `einzahlen 100`, `abheben 100`, `kontostand` (auch `deposit`/`withdraw`/`balance`).

## Beute (monster.kod `CreateTreasure`, `TreasureType.GenerateTreasure`)
- Anzahl Stücke = 1 + Stufe/55 + Zufall(0, Schwierigkeit/3), höchstens 6 → Zombie (55/4) und Skelett (75/5) je 2–3.
- Tabellen aus `trestype/zombiet.kod` und `tought.kod` (Gewichte auf 100 normiert, Rest zum ersten Eintrag).
  Geld: 1 + 2 × Zufall(Stufe/2, 3·Stufe/2) Schillinge.
- Skelette lassen zusätzlich immer ein Langschwert fallen (skel.kod).
- Beute liegt auf dem Boden (OF_GETTABLE) und wird mit `BP_REQ_GET` aufgehoben, `BP_REQ_DROP` legt ab.

## Benutzen (`BP_REQ_USE`/`BP_REQ_UNUSE` → `BP_USE`/`BP_UNUSE`, `BP_USE_LIST` nach `BP_INVENTORY`)
- Waffen, Schilde (auch die Fackel), Rüstungen werden angelegt, je Platz einer; ein bereits angelegter wird abgelegt.
  Werte siehe combat.md. Die benutzte Fackel leuchtet im Client.
- Essen/Trinken (food.kod): Sättigung kommt in den Magen (max. 100, leert sich um 0,12/s), der Nährwert wird zu
  Ausdauer; Meldungen bei halb/fast satt. Werte: Krug Starkbier 6/8, Käse 30/40, Brot 20/40, Wasserschlauch 3/6,
  essbarer Pilz 5/15, Tintling 50/25 (Nährwert/Sättigung).
- Benutzte Gegenstände, Ausdauer und Magen werden mitgespeichert.

## Abweichungen vom Original
- Inventar und Konten werden in SQLite gespeichert (decisions.md); Gegenstände am Boden und freigeschaltete
  Händlerwaren gehen bei einem Neustart verloren.
- Kein Gewicht/Volumen (`ReqNewHold`), keine Haltbarkeit (Langschwert-Schäden), keine magischen Gegenstände,
  Token, Siegelringe oder Quest-Gegenstände in der Beute.
- Ausrüsten tauscht automatisch; das Original verlangt teils erst das Ablegen. Keine Haltbarkeit/Abnutzung,
  keine Alkoholwirkung, Fackel brennt nicht ab.
- Spielerhandel: kein Gewichtslimit, im Client nur ein Gegenstand pro Angebot/Gegenangebot (Protokoll kann mehrere).
- Aufheben nur innerhalb von 2 Feldern; höchstens 40 Gegenstände pro Raum am Boden (ältere verschwinden) –
  das Original räumt anders auf.
- Kein Schmied in Tos (nicht in der Weltbeschreibung), daher kauft niemand Waffen/Rüstungen.

## Client
- **I** Inventar (Ablegen, dem nächsten Händler anbieten), **B** Ladenliste des nächsten Händlers, **G** Gegenstand
  aufheben. Gegenangebote erscheinen als Dialog „Annehmen/Ablehnen“.

## Tränke, Schriftrollen, Zauberstäbe (2026-11-03)
`tools/make_spellitems.py` übernimmt aus `kod/object/item/passitem/spelitem` alle Spruchgegenstände, deren Spruch es
schon gibt (27: 12 Tränke, 9 Schriftrollen, 6 Stäbe), Schlüssel = Klassenname wie in den Beutetabellen. Benutzen
(Doppelklick/BP_REQ_USE) wirkt den Spruch wie spelitem.kod mit Spruchstärke Random(20 bzw. 30, 60), ohne Mana und
Reagenzien: Tränke auf den Trinkenden, Schriftrollen wie gezaubert, Zauberstäbe auf das nächste Monster, das einen
angreift (sonst das nächste in 10 Feldern) bzw. bei hilfreichen Sprüchen auf einen selbst. Stäbe haben
viHits_init_min/max Ladungen (gespeichert, Schema v6) und zerfallen leer.
Abweichungen: Tränke sind immer beschriftet (im Original oft unbekannt) und verderben nicht; Zauberstäbe kann man
noch nicht auf ein bestimmtes Ziel richten.

## Rüstungen, Helme, Handschuhe, Ringe, Reagenzien (2026-11-03)
Neu aus `kod/object/item/passitem/defmod`, `attmod`, `ring`, `numbitem` (Werte viDefense_base, viDamage_base,
viValue_average):

| Gegenstand | Platz | Wirkung |
|---|---|---|
| Plattenrüstung / Schuppenrüstung / Neruditrüstung | Rüstung | Schaden −6/−4/−5, Abwehr −200/−100/−150 |
| Helm / Reif / Efeukranz | Kopf | Abwehr +20 (Schaden −1) / +5 / +10 |
| Panzerhandschuhe | Hände | Abwehr +15, Treffer +20, Schaden −1 |
| Orkschild | Schild | Abwehr +20, Schaden −2 |
| Ringe des Feuer-/Kälte-/Blitz-/Säureschutzes | Ring | Widerstand 50 gegen die Art |
| Berserkerring | Ring | Treffer − Random(15,25)×10, Schaden + Random(1,4); 80–120 Schläge, dann zerspringt er |
| Ring der Unsichtbarkeit | Ring | unsichtbar für Monster, solange getragen; 4–7 Minuten |
| ungeschliffener Seraphym, Yrx'l-Saft, Netzmoos, Regenbogenfarn, Kriipa-Klaue, Schamanenblut | Reagenz | (für spätere Sprüche) |

Zwei Ringe gleichzeitig (linke und rechte Hand). Abweichungen: Haltbarkeit (piHits) von Rüstungen, Helmen und
Handschuhen fehlt; Ringe sind sofort erkennbar (im Original zeigen Widerstandsringe ihre Art erst später); Hitze
in Metallrüstungen (GetHeatDamage) fehlt.

## Gegenstände aus den Beutetabellen (2026-11-06)
`tools/make_misc_items.py` (danach `make_spellitems.py`, `make_treasure.py`):

- **Spruchmodifikator** (item.kod GetBaseSpellModifier → spell.kod GetSpellPower): jeder benutzte Gegenstand ändert
  die Spruchstärke. Werte aus viSpell_modifier der Klasse oder Elternklasse (Kette −15, Schuppe −20, Platte −30,
  Nerudit −20, einfacher Helm −5, Robe +10), Waffen nach weapon.kod (Hieb −15, Stich −10, Schlag 0; niedrige
  Qualität +5, hohe −5, Nerudit +5). Schulabhängig: Fackel Faren +10, Orkschild Shal'ille −15/Qor +15, Efeukranz
  Shal'ille +15/Qor −15, wahre Laute Jala +10.
- **Jüngerrobe** (robedisc.kod): Rüstung mit Abwehr +20; beim Erzeugen eine von sechs Schulen (Random(1,6)), Sprüche
  dieser Schule + 4 × höchste gelernte Stufe (bis 20), andere + 5. Abweichung: belegt nur den Rüstungsplatz (im
  Original auch die Beine), keine Schulfarben im Client.
- **Neruditschwert** (neruswd.kod): Stich, Treffer + 25, Schaden + 1, Angriffsart Stich + Nerudit (nicht
  „nicht magisch“): Trolle −80 %, Lupoggs −25 %, Dunkle Engel +75 %. Abweichung: ohne Haltbarkeit zersplittert es nie.
- **Ring der Lethargie** (lethring.kod): verflucht – Ablegen, Fallenlassen, Anbieten scheitern und kosten
  400 000 Anstrengung (≈ 40 Ausdauer); senkt die Erholungsschwelle um 20 (mind. 10). Nur „Fluch lösen“ nimmt ihn ab.
- **Sonderstäbe** (specwand/*.kod): Feuerstab (4–12 Ladungen, Feuer 4–10), Vampirstab (10–18, unheilig/Blitz 2–6,
  heilt Schaden − 1), Treffer bei Random(1,100) < Mystik + Zielen (10..90); Heilstab (5, heilt 2–10, hier auf sich
  selbst); Stab der Erschütterung (25–35, trifft nur Spieler – ohne PvP nur die Meldung). Abweichung: Ziel ist das
  nächste Monster (wie bei Spruchstäben), Karmaverlust des Vampirstabs fehlt.
- Ohne Funktion (wie im Original reine Gegenstände): Erzbrocken, Neruditbrocken, Spinnenei, Schlüssel, Reliquie des
  Qor (für das Steinkreis-Rätsel in I9, noch nicht umgesetzt), Feuersand (Reagenz).
- 26 neue Tränke, Schriftrollen und Stäbe zu den neuen Sprüchen (docs/magic.md). Stäbe mit PvP-Sprüchen (Vergessen,
  Spröde, Zerschmettern, Tausch) verweigert der Schutzengel.
