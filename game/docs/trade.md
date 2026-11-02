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
- Startausstattung: Streitkolben + 500 + 2000 Schillinge (player.kod `AddDefaultObjects`) + 1000 (`AddRealWorldObjects`,
  weil wir direkt in Tos statt auf Raza beginnen) = 3500 Schillinge. Reagenzien für Sprüche fehlen (noch keine Magie).

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

## Bank (`BP_USERCOMMAND` UC_DEPOSIT 35 / UC_WITHDRAW 36 / UC_BALANCE 37)
- Geht nur, wenn ein Bankier im Raum ist. Texte und Prüfungen wie monster.kod `BankDeposit`/`BankWithdraw`.
- Im Client per Chat: `einzahlen 100`, `abheben 100`, `kontostand` (auch `deposit`/`withdraw`/`balance`).

## Beute (monster.kod `CreateTreasure`, `TreasureType.GenerateTreasure`)
- Anzahl Stücke = 1 + Stufe/55 + Zufall(0, Schwierigkeit/3), höchstens 6 → Zombie (55/4) und Skelett (75/5) je 2–3.
- Tabellen aus `trestype/zombiet.kod` und `tought.kod` (Gewichte auf 100 normiert, Rest zum ersten Eintrag).
  Geld: 1 + 2 × Zufall(Stufe/2, 3·Stufe/2) Schillinge.
- Skelette lassen zusätzlich immer ein Langschwert fallen (skel.kod).
- Beute liegt auf dem Boden (OF_GETTABLE) und wird mit `BP_REQ_GET` aufgehoben, `BP_REQ_DROP` legt ab.

## Abweichungen vom Original
- Inventar und Konten werden in SQLite gespeichert (decisions.md); Gegenstände am Boden und freigeschaltete
  Händlerwaren gehen bei einem Neustart verloren.
- Kein Gewicht/Volumen (`ReqNewHold`), keine Haltbarkeit (Langschwert-Schäden), keine magischen Gegenstände,
  Token, Siegelringe oder Quest-Gegenstände in der Beute.
- Gegenstände benutzen/ausrüsten (Essen, Waffen, Fackel) fehlt noch.
- Handel zwischen Spielern fehlt noch (nur Spieler ↔ NPC).
- Aufheben nur innerhalb von 2 Feldern; höchstens 40 Gegenstände pro Raum am Boden (ältere verschwinden) –
  das Original räumt anders auf.
- Kein Schmied in Tos (nicht in der Weltbeschreibung), daher kauft niemand Waffen/Rüstungen.

## Client
- **I** Inventar (Ablegen, dem nächsten Händler anbieten), **B** Ladenliste des nächsten Händlers, **G** Gegenstand
  aufheben. Gegenangebote erscheinen als Dialog „Annehmen/Ablehnen“.
