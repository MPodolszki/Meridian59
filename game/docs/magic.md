# Magie (Stand 2026-11-02, erster Umfang)

Eng am Original (`kod/object/passive/spell.kod`, `player.kod`, einzelne Sprüche). Code:
`server/Meridian.Server/Magic.cs` (Daten, Formeln), `WorldMagic.cs` (Ablauf); Daten `server/data/spells/spells.json`.

## Charakterwerte
Stärke, Intellekt, Ausdauer, Geschick, Mystik, Zielen (je 1..50, 200 Punkte) und Karma (−100..100).
Ohne Charaktererstellung bekommen alle **35/30/35/30/40/30**, Karma 0 (Abweichung: im Original frei verteilt).
Im Protokoll Stat-Gruppe 2 (`BP_STAT_GROUP`).

## Mana
- Höchstwert `GetInitialMaxMana` = 15 + Mystik/5 (Startwerte: 23).
- Regeneration `CalculateManaTime`: (150000 + (25 − Mystik)·1000) · 200 / Ausdauer / MaxMana ms pro Punkt,
  begrenzt 1–60 s; über dem Maximum sinkt Mana wieder. Anzeige `BP_STAT` Gruppe 1, Nr. 2.

## Zaubern (`BP_REQ_CAST`: Spruch-ID, Ziele)
1. Prüfen (`CanPayCosts`): spruchspezifisch (Heilung nur bei Verletzten, Berührungszauber nicht doppelt),
   Ausdauer ≥ viSpellExertion, Mana ≥ Kosten, Reagenzien vorhanden.
2. Spruchstärke `GetSpellPower` = Fähigkeit/2 + Schulbonus (0..30) + Nebenbonus (0..10), 1..99.
   Shal'ille/Qor: Karma/10; Qor zusätzlich 2 + |Stunde−12|·2; Kraanan: Objekte im Raum, Gesundheit·10/Max;
   Faren: Ausdauer/20. Raumboni nach Gelände sind in Tos 0.
3. Trance `GetTranceTime` = viCast_time · (150 − Stärke)/100 ms; Bewegen bricht sie ab.
4. Erfolg `SuccessChance` = ((100 − Req)·Stärke/100) + Req, 5..95 (Req: Mystik für Shal'ille/Qor/Faren,
   Ausdauer für Kraanan, Intellekt für Riija/Jala). Misserfolg kostet halbes Mana und halbe Anstrengung.
5. Erfolg: Mana (`GetManaCost`, über Stärke 40/80 je −1), Anstrengung 10000·viSpellExertion, Reagenzien, Wirkung.
6. Üben `ImproveAbility` mit `GetInitialChance`/`GetSecondaryChance` (Lernpunkte anderer Schulen, Softcap
   2·Req, Bonus je 7 Zauber der Schule).

## Sprüche
| Spruch | Schule/Stufe | Mana | Reagenzien | Wirkung |
|---|---|---|---|---|
| Kleine Heilung (minor heal) | Shal'ille 1 | 3 | 1 Kraut | 1–5 + (Stärke+1)/20 (+Karma/20), max. 10, +2–5 |
| Hospiz (hospice) | Shal'ille 3 | 10 | 3 Kräuter | −3..3 + Stärke/5 (+Karma/10), 0..25 |
| Zap | Faren 1 | 6 | 1 blauer Pilz | Berührungszauber: Schläge machen 3–6 Schaden (nach Stärke), Treffer seltener (Faktor 50) |
| Nahrung erschaffen (create food) | Kraanan 1 | 10 | 2 Kräuter, 2 Holunderbeeren | Wurf Stärke/2..Stärke: Apfel, Brot, Trauben, Keule, Pastete, Käse, selten Tintling |

Startsprüche: Kleine Heilung, Zap, Nahrung erschaffen mit `InitialAbility` (Random(Req/2, Req)/2, 1..25) und
Reagenzien für je 10 Zauber (AddDefaultObjects + AddRealWorldObjects). Bestehende Charaktere bekommen sie beim
nächsten Einloggen.

## Lernen
Händler mit Sprüchen (Frisconar: Hospiz) bieten sie in der Ladenliste an, wenn `PlayerCanLearn` es erlaubt
(Sprüche der Stufe davor mit genug Fähigkeit, Lernpunkte-Formel mit POINTS_SLOPE 7, MaxLearnPoints 16).
Preis 250·2^Stufe. Hospiz (Stufe 3) braucht Stufe-2-Sprüche der Shal'ille – die gibt es noch nicht.

## Protokoll
`BP_SPELLS`/`BP_SPELL_ADD` (object_desc, Ziele, Schule), Stat-Gruppe 3 als Liste (Fähigkeit in %),
`BP_REQ_CAST`. Godot-Client: Taste **Z** öffnet die Sprüche, Mana-Balken unter Lebenspunkten/Ausdauer.

## Noch nicht umgesetzt
Weitere Sprüche (Stufe 2 aufwärts, Riija/Jala), Verzauberungen anderer Art (Segen, Widerstände),
Spruch-Murmeln für Umstehende, Karma-Änderung durch Heilen, Raumboni nach Gelände.
