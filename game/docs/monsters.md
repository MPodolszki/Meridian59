# Monster

Alle Kampfmonster, Bosse und beschworenen Wesen des Originals stehen in `server/data/monsters/monsters.json`
(62 Arten), erzeugt mit `tools/make_monsters.py` aus dem KOD-Katalog `docs/kod/monsters_catalog.json`
(Mechanik des Originals: `docs/monsters-original.md`). Schlüssel = KOD-Klasse in Kleinbuchstaben; die Gebietsdateien
verweisen darauf (`"kind": "giantrat"`). Namen und Beschreibungen sind eigene Übersetzungen.

## Umgesetzt (eng am Original)

- Werte aus Stufe, Schwierigkeit, Tempo (monster.kod): Lebenspunkte (Stufe < 40: Stufe, sonst ×1,2, ±25 %),
  Offensive/Defensive 3 × Stufe + 60 × Schwierigkeit, Schaden Fuzzy(Stufe / 10..15), Angriffstakt,
  Sichtweite 4 + Schwierigkeit/2 Felder.
- Resistenzen je Angriffsart wie battler.kod: größter positiver plus kleinster negativer passender Wert,
  `weapon_all`/`spell_all` passen auf jede Waffen- bzw. Zauberart.
- Zielwahl (brain.kod, vereinfacht): `fight_aggressive`/`fight_hyperaggressive` greifen jeden Spieler in Sichtweite
  an, `fight_karma_aggressive` (Zombie, Bäume, Feen, Froschmann) nur Spieler mit entgegengesetztem Karma; jedes
  Monster wehrt sich, wenn man es angreift. **Folge:** Zombies lassen neue Charaktere (Karma 0) in Ruhe – wie im
  Original.
- Unbewegliche Monster (AI_NOMOVE, Tempo 0: lebender Baum, Spinnenkönigin, Dornen) bleiben stehen.
- Flucht unter viWimpy Prozent Lebenspunkten (ohne Sammeln bei Verbündeten).
- Gift (Spinnen, Dämmerratte, Spinnenkönigin): Chance 1/n je Treffer, sofort −20 Ausdauer, alle 10 s
  lossRate/1000 Lebenspunkte (nie unter 1) bis zum Ende der Dauer.
- Beute: alle Beutetabellen aus `kod/object/passive/trestype` (`tools/make_treasure.py`); Gegenstände, die es noch
  nicht gibt (Tränke, Zauberstäbe, Ringe, Rüstungen …), fehlen vorerst in den Tabellen.

## Noch nicht umgesetzt

Monsterzauber (Tabellen stehen unter `special.spellcasting`), Lähmung/Demenz/Ausdauerentzug beim Treffer,
Fernangriffe, Eingraben der Erdwürmer, Dunkelheit, Doppel- und Wirbelangriff, Beschwörung, Zauberresistenz,
Hass-Werte mit Zielwechsel, Sammeln bei Verbündeten, Karma des Siegers.
