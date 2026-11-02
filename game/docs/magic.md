# Magie (Stand 2026-11-02, erster Umfang)

Eng am Original (`kod/object/passive/spell.kod`, `player.kod`, einzelne Sprüche). Code:
`server/Meridian.Server/Magic.cs` (Daten, Formeln), `WorldMagic.cs` (Ablauf); Daten `server/data/spells/spells.json`.

## Charakterwerte
Stärke, Intellekt, Ausdauer, Geschick, Mystik, Zielen (je 1..50, 200 Punkte) und Karma (−100..100).
Neue Charaktere verteilen sie im Erstellungsassistenten des Original-Clients (docs/character.md); wer ohne
Erstellung einsteigt (Godot-Client, alte Charaktere), bekommt **35/30/35/30/40/30**, Karma 0.
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
| Zap | Faren 1 | 6 | 1 blauer Pilz | Berührungszauber 3–6 |
| Mystische Berührung (mystic touch) | Faren 1 | 3 | 1 Pilz | gibt einem anderen Spieler Stärke/2 % (10–55) des eigenen Manas, Ziel max. 1,5 × Max bzw. 75 |
| Waffe erschaffen (create weapon) | Kraanan 1 | 15 | – | Wurf Stärke/3..Stärke: Streitkolben, Kurzschwert, Hammer, Axt, Langschwert, Krummsäbel, mystisches Schwert |
| Heilige Berührung (holy touch) | Shal'ille 2 | 12 | 2 Smaragde | Berührungszauber 3–6 |
| Säureberührung (acid touch) | Qor 2 | 10 | 1 Entwurzbeere | Berührungszauber 4–8 |
| Eisige Finger (icy fingers) | Faren 2 | 6 | 1 roter Pilz | Berührungszauber 5–8 |
| Flammenberührung (touch of flame) | Faren 2 | 9 | 1 roter Pilz | Berührungszauber 4–9 |
| Nahrung erschaffen (create food) | Kraanan 1 | 10 | 2 Kräuter, 2 Holunderbeeren | Wurf Stärke/2..Stärke: Apfel, Brot, Trauben, Keule, Pastete, Käse, selten Tintling |

Startsprüche: Kleine Heilung, Zap, Nahrung erschaffen mit `InitialAbility` (Random(Req/2, Req)/2, 1..25) und
Reagenzien für je 10 Zauber (AddDefaultObjects + AddRealWorldObjects). Bestehende Charaktere bekommen sie beim
nächsten Einloggen.

Berührungszauber (touchatk.kod): Schaden Random(min, max), davon die Hälfte fest und die Hälfte nach Stärke, +1;
Dauer Random(Stärke/3, Stärke/2) × 6 s (1–7,5 min); ein neuer ersetzt den alten. `viHit_Factor` setzt das
Original, wertet ihn aber nirgends aus – Treffer daher wie ein normaler Schlag.

## Lernen
Händler mit Sprüchen (Frisconar: Hospiz) bieten sie in der Ladenliste an, wenn `PlayerCanLearn` es erlaubt
(Sprüche der Stufe davor mit genug Fähigkeit, Lernpunkte-Formel mit POINTS_SLOPE 7, MaxLearnPoints 16).
Preis 250·2^Stufe. Hospiz (Stufe 3) braucht geübte Stufe-2-Sprüche der Shal'ille (Heilige Berührung, bei der Erstellung wählbar).

## Protokoll
`BP_SPELLS`/`BP_SPELL_ADD` (object_desc, Ziele, Schule), Stat-Gruppe 3 als Liste (Fähigkeit in %),
`BP_REQ_CAST`. Godot-Client: Taste **Z** öffnet die Sprüche, Mana-Balken unter Lebenspunkten/Ausdauer.

## Persönliche Verzauberungen (`WorldEnchant.cs`, persench.kod)
Ein Ziel (sich selbst oder einen Spieler im Raum). Auf sich selbst darf man neu wirken – die alte Verzauberung
endet still (`CanRecastSelfEnchantment` = TRUE); auf andere nicht, solange sie wirkt. Dauer jeweils
`Random(d/2, d)`, Stärke = Spruchstärke.

| Spruch | Schule/Stufe | Mana | Reagenzien | Wirkung | d |
|---|---|---|---|---|---|
| Segen (bless, SID 11) | Kraanan 2 | 6 | 2 Pilze, 2 Saphire | Angriff + Stärke, Schaden + Random(0, Stärke/33) | 40 s + 6 s·Stärke |
| Superstärke (SID 17) | Kraanan 2 | 10 | 2 Pilze, 1 Orkzahn | Stärke-Attribut + Stärke/5 + 1 (Schadensbonus) | 300 s + 6 s·Stärke |
| Eile (haste, SID 116) | Kraanan 2 | 10, Trance 2 s, keine Anstrengung | 5 Snacks | Lauf-Anstrengung × (100 − Stärke) % | (5 + 15·Stärke/100) min |
| Feuer widerstehen (SID 29) | Faren 2 | 4 | 3 Pilze, 1 roter Pilz | Widerstand Stärke/2 + 25 % gegen Feuer | 180 s + 6 s·Stärke |
| Kälte widerstehen (SID 36) | Faren 2 | 9 | 3 Pilze, 1 roter Pilz | Stärke/2 + 5 % gegen Kälte | 300 s + 6 s·Stärke |
| Blitzen widerstehen (SID 28) | Faren 3 | 6 | 3 Pilze, 1 blauer Pilz | Stärke/2 + 25 % gegen Blitz | 180 s + 3 s·Stärke |
| Säure widerstehen (SID 149) | Shal'ille 2 | 9 | 2 Kräuter, 1 Entwurzelbeere | Stärke/2 + 15 % gegen Säure | 300 s + 6 s·Stärke |

Protokoll: `BP_ADD_ENCHANTMENT` (147: u8 Typ 1, object_desc des Spruchs) und `BP_REMOVE_ENCHANTMENT` (148: u8 Typ,
u32 ID); auf `BP_SEND_ENCHANTMENTS` (Typ 1) schickt der Server alle aktiven. Der Godot-Client zeigt sie als
„Verzaubert: …“ unter dem Mana-Balken. Gespeichert wird die Restdauer (`characters.magic`).

Abweichungen: Die Restdauer läuft offline nicht weiter (im Original laufen die Timer des Spielerobjekts).
Widerstände wirken nur gegen Monster mit magischer Angriffsart (`MonsterKind.AttackType`) – Zombies und Skelette
in Tos schlagen körperlich zu, dort bringen sie also noch nichts. Lehrer sind im Original die Tempelpriester
(kranprst, farnprst, shalprst) außerhalb von Tos; bis diese Gebiete existieren, gibt es die Stufe-2-Sprüche nur
bei der Charaktererstellung (OfferToNewCharacters). Das Verbot, in Sicherheitszonen andere zu verzaubern
(ROOM_NO_COMBAT), fehlt noch.

## Noch nicht umgesetzt
Weitere Verzauberungen (Entdecken, Unsichtbarkeit, Nachtsicht …), Raumzauber (Licht, Dunkelheit, Nebel), Wände, Riija/Jala,
Spruch-Murmeln für Umstehende, Karma-Änderung durch Heilen, Raumboni nach Gelände.
