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
- Beute: alle 38 Beutetabellen aus `kod/object/passive/trestype` (`tools/make_treasure.py`, items.json "treasure"),
  jedes Monster mit Beute verweist auf eine vorhandene Tabelle. Ablauf wie monster.kod CreateTreasure /
  TreasureType: Anzahl 1 + Stufe/55 + Random(0, Schwierigkeit/3), höchstens 6 (MOB_ONE_TREASURE, Geist: genau 1);
  Gewichte auf 100 normiert (Rest auf den ersten Eintrag), Wurf 0..100; Geld 1 + 2 × Random(Stufe/2, 3·Stufe/2).
  Abweichungen: Gegenstände, die es noch nicht gibt, fehlen (Stand siehe `python3 tools/make_treasure.py`);
  die übrigen Gewichte werden dadurch etwas größer. Es fehlen außerdem Waffen mit Zusatzkräften (piDiff_seed/piItem_Att_chance, 24 Tabellen),
  Marken des Ratsspiels, der Siegelring für Neulinge und das Neuwürfeln unzugänglicher Spruchgegenstände.

## Noch nicht umgesetzt

Monsterzauber (Tabellen stehen unter `special.spellcasting`), Lähmung/Demenz/Ausdauerentzug beim Treffer,
Fernangriffe, Eingraben der Erdwürmer, Dunkelheit, Doppel- und Wirbelangriff, Beschwörung, Zauberresistenz,
Hass-Werte mit Zielwechsel, Sammeln bei Verbündeten, Karma des Siegers.

## Zaubernde Monster (2026-11-03)
`monsters.json` "casting" (tools/make_monsters.py CASTING, aus special.spellcasting): Chance 1/n je Angriffsgelegenheit
statt eines Schlags, Spruch nach Wurf 1..100, Mana (Start = Maximum, +1/s), Reichweite. Umgesetzt in
`WorldMonsterMagic.cs`:

| Monster | Chance | Sprüche (im Spiel) |
|---|---|---|
| Avar-Schamane | 1/12, Stärke 75, Mana 10 | Festhalten, Manabombe, Erdbeben (Sandsturm, Zersplittern fehlen) |
| Ork-Zauberer | 1/10, Stärke 75, Mana 10 | Schlachtfeld, Manabombe (Schlitterblitz, Läuterung, Vergessen fehlen) |
| Dunkler Engel | 1/3 außerhalb der Nahkampfreichweite bis 10 Felder | Blindheit, Festhalten, Zwietracht, Schwächen (Böser Zwilling, Spiegelbild, Vergessen fehlen) |
| Geist | 1/4, Stärke 75 | Feuerball |
| Ent, böser Ent | 1/10 bis 25 Felder | Blitz |
| Yeti | 1/2, nur wenn der Raum verzaubert ist | Zwietracht |

Fehlt ein Spruch, schlägt das Monster in dieser Runde normal zu. Wirkungen auf Spieler: Angriffssprüche mit Widerstand
(Art bzw. Magie widerstehen), Festhalten (EFFECT_PARALYZE), Blindheit (EFFECT_BLIND/SEE, im Godot-Client ein
schwarzer Schleier), Schwächen (Treffer und Schaden × 3/4, Abweichung: im Original Stärkeverlust), Erdbeben (alle Spieler
nach Abstand), Manabombe (Manaverlust). Die Manabombe eines Spielers hindert Monster 10 s am Zaubern. Läutern heilt
auch Blindheit und Schwäche.

Eisgestalt (iceper.kod): wirft außerhalb der Nahkampfreichweite mit Chance 1/5 einen Eisspeer bis Abstand² < 666
(etwa 25 Felder), danach 10 s Pause; der Wurf trifft wie ein Schlag (Kälte). Feen schießen im Original nur optisch ein
Geschoss bei normaler Angriffsreichweite – hier ein normaler Schockangriff.

Erdwürmer (Larve, Wurm, Königin; groundwm.kod pbSurfaced): entstehen unter der Erde und sind für Spieler unsichtbar;
haben sie ein Ziel in Angriffsreichweite, brechen sie aus dem Boden hervor (Create + Meldung) und kämpfen; ohne Ziel
tauchen sie nach 5 s wieder ab. Abweichung: keine Auftauch-Animation und keine Schleimspur.
