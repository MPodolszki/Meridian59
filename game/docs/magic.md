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

## Schulsprüche auf Monster, Heilung, Flächenzauber (`WorldSpells.cs`)

Werte (SID, Schule, Stufe, Mana, Trance, Reagenzien) maschinell aus `docs/kod/spells_catalog.json`, Formeln aus den
KOD-Dateien (Zeilenangaben im Katalog). Spruchstärke wie oben.

| Spruch | Schule/Stufe | Wirkung |
|---|---|---|
| Feuerball (5), Blitz (1) | Faren 4/5 | Random(8,12) bzw. (13,18) × (50 + Stärke/2) / 99 Feuer- bzw. Blitzschaden |
| Schockende Wut (164), Feuerstoß (165), Explosiver Frost (166) | Faren 3/4 | Random(12,20)/(15,23)/(13,25) × (50 + Stärke/2) / 99 |
| Säurespritzer (167) | Qor 4 | Random(16,25) × (50 + Stärke/2) / 99 Säure |
| Vampirischer Entzug (22) | Qor 3 | Random(12,18) × … unheilig; heilt den Wirker um den halben Schaden (beim Töten 9) |
| Große Heilung (42) | Shal'ille 5 | Random(7,13) + Stärke/5 + Karma/5, 0..50 |
| Lebensatem (148) | Shal'ille 1 | gibt (Karma des Ziels 10..80) % der eigenen LP ab, Ziel erhält (75 + Stärke/2) % davon, bis 150 % bzw. 130 |
| Kraftübertragung (158) | Kraanan 1 | min(Stärke, Ausdauer − 10, 150 − Ausdauer Ziel), Ziel erhält 5/4 |
| Gift heilen (37) | Shal'ille 2 | ganz bei Random(1,100) < Stärke oder schwachem Gift, sonst Giftstärke − Stärke % |
| Festhalten (47) | Qor 4 | Monster steht und kämpft nicht: Random(D/2, D), D = 2000 + 40 × Stärke ms |
| Blindheit (46), Gleißen (161) | Qor 5, Shal'ille 4 | Monster ohne Ziel: Random(d/2,d) mit d = 5 + Stärke/6 s (3..20) bzw. Stärke/12 + Karmaunterschied/20 s (3..15) |
| Schwächen (53), Schwindel (107) | Qor 3, Riija 3 | Lähmung: Offensive und Schaden des Monsters × 3/4 |
| Erdbeben (14) | Faren 5 | alle Monster: Random(5,9) × (1 + Stärke/25), voll bis 8 Felder, ab 20 keiner; trifft auch den Wirker |
| Letzte Riten (133) | Shal'ille 5 | alle Untoten: Random(1,5) + Stärke/20 + 2 × (Karmaunterschied/40), heilig |
| Blitzlicht (113) | Riija 1 | jedes Monster mit 33 + 2 × Stärke/3 % für 1 s festgehalten |
| Heiliges Symbol (132) | Shal'ille 1 | Untote fliehen 9 min/Schwierigkeit vor dem Wirker, bei klarem Fehlschlag greifen sie an |

Resistenzen der Monster wirken wie bei Waffen (`MonsterKind.ResistanceTo`, `spell_all` auf alle Zauber).
Ziel im Godot-Client: die zuletzt angeklickte Figur bzw. das mit F angegriffene Monster, sonst man selbst.

Abweichungen: Spieler als Ziel schädlicher Sprüche gibt es noch nicht (kein PvP); das Erdbeben verschont andere
Spieler, und der Wirker fällt nicht unter 1 LP. Geschosse (Feuerball, Blitz) treffen sofort, ohne sichtbares
Projektil. Hilfreiche Sprüche mit einem Monster als Ziel wirken auf den Zaubernden (Komfort für den neuen Client).
Lernen: bei den Tempelpriesterinnen (Shal'ille, Kraanan, Qor, Faren in den Badlands; docs/wilderness.md), jeweils die
Sprüche ihrer Liste, die es im Spiel schon gibt.

## Noch nicht umgesetzt
Weitere Verzauberungen (Entdecken, Unsichtbarkeit, Nachtsicht …), Raumzauber (Licht, Dunkelheit, Nebel), Wände, Riija/Jala,
Spruch-Murmeln für Umstehende, Karma-Änderung durch Heilen, Raumboni nach Gelände.

## Raumverzauberungen, Schutz, Rettung, Manafokus (2026-11-03)
Neu in `WorldRoomMagic.cs`, Werte aus `docs/kod/spells_catalog.json`:

| Spruch | Schule/Stufe | Wirkung |
|---|---|---|
| Licht (light.kod) | Faren 1 | Raumlicht + (25 + Stärke) für Random(D/2, D), D = 45 + 3×Stärke s |
| Dunkelheit (darkness.kod) | Qor 1 | Raumlicht − (Stärke + 25), D = 30 + 2×Stärke s; braucht Feenflügel (Beute der Feen) |
| Schlachtfeld (killing.kod) | Kraanan 5 | alle Nahkampftreffer im Raum + Random(2, 5), 50 + Stärke s |
| Kräfte des Lichts (forceslt.kod) | Shal'ille 4 | Treffer + (Stärke + 50) für Spieler mit gutem Karma, 6 × (Stärke/3 + 1) s |
| Rüstung des Gort (gort.kod) | Kraanan 5 | jeder Monstertreffer − bound(Random(0, Stärke/25), 1, 4), höchstens 15; (Stärke/8 + 1) min |
| Magie widerstehen (resmagic.kod) | Kraanan 5 | Widerstand Stärke/3 + 7 gegen alle Spruchschäden, (40 + 2×Stärke) s |
| Rettung (rescue.kod) | Shal'ille 3 | nach 15000 − Stärke×37,5 ms (halb so oft + 5–10 s) in den Raum, in dem man nach dem Tod erwacht |
| Manafokus (focus.kod) | Faren 3 | Trance: Mana im Takt × (200 − Stärke)/200 (0,5–60 s), Angriffssprüche + Stärke×5/99 + 1 |

Raumverzauberungen gibt es je Raum nur einmal; Licht und Dunkelheit schicken das neue Raumlicht (BP_LIGHT_AMBIENT).
Abweichungen: Magie widerstehen wirkt erst, wenn Monster zaubern (noch nicht); Rettung kennt Gildenhalle und
Ko'catan noch nicht; Manafokus endet bei Bewegung oder Nahkampf (im Original bei jeder Trance-brechenden Handlung
außer Angriffssprüchen); Neuankömmlinge bekommen keinen Hinweis auf die Raumverzauberung; die Schulbonus-Stärke der
Kräfte des Lichts für Shal'ille-Sprüche fehlt.

## Kara'hols Fluch, Nachtsicht, Läutern; Lähmung von Spielern (2026-11-03)
- **Kara'hols Fluch** (karahol.kod, Qor 2): fest (40 + Stärke/2) s Treffer + 100 + 2×Stärke, Schaden + 1 + Random(0,
  Stärke/20); danach gelähmt für ein Sechstel der Dauer.
- **Nachtsicht** (nightv.kod, Kraanan 3): eigenes Licht Stärke + 50 für Random(D/2, D), D = 1500 + 25×Stärke s;
  BP_LIGHT_PLAYER, im Godot-Client ein heller, weiter Lichtschein um die Figur.
- **Läutern** (purify.kod, Shal'ille 4): Gift und Lähmung verschwinden je mit Stärke % Chance.
- **Lähmung** (hold.kod für Spieler): BP_EFFECT EFFECT_PARALYZE/RELEASE wie im Original; der Godot-Client sperrt
  dann die Bewegung, der Server lässt Angriff, Zaubern und Bewegung nicht zu.

## Schirm, Unsichtbarkeit, Zwietracht, Antimagie-Aura, Winde (2026-11-03)
- **Schirm** (umbrella.kod, Shal'ille 5): Abwehr + 250, gegen Untote Schaden − Schaden × (Stärke/20)/10; Random(D/2, D),
  D = 40 + 6×Stärke s.
- **Unsichtbarkeit** (invis.kod, Qor 5): Abwehr + 25 + Stärke, Monster suchen sich den Spieler nicht als Ziel (wehren
  sich aber); D = 20 + 2×Stärke s. Braucht Federn Dunkler Engel (Beute). Für andere Spieler noch sichtbar.
- **Zwietracht** (discord.kod, Kraanan 4): jede Raumverzauberung fällt mit Stärke % Chance, ab 60 eine sicher.
- **Antimagie-Aura** (antimag.kod, Kraanan 6): jeder Spruch im Raum − (Stärke/2 + 25) Spruchstärke; D = 60 + Stärke s.
  Braucht einen polierten Seraphym.
- **Winde** (winds.kod, Faren 3): Zielen aller Spieler im Raum − Stärke/4 (Treffer − 4 × das), immer 60 s; die
  Fehlschusschance für Fernwaffen fehlt (es gibt noch keine).

## Waffenweihen; Angriffsarten der Waffen (2026-11-03)
- **Waffe verzaubern** (enchwp.kod, Kraanan 2), **Heilige Waffe** (holywp.kod, Shal'ille 2), **Unheilige Waffe**
  (unholywp.kod, Qor 2): die geführte Waffe gilt (Random(1, 4) + Stärke/3) Stunden als magisch bzw. trifft zusätzlich
  heilig/unheilig. Zauberzeit 30 s. Abweichung: Ziel ist immer die Waffe in der Hand; die Weihe wird noch nicht
  gespeichert (endet beim Ausloggen).
- **Angriffsarten**: Ein Waffentreffer trägt wie im Original seine Waffenart plus `nonmagic` (gewöhnliche Waffe) bzw.
  `magic` (verzaubert/magische Waffe), dazu ggf. `holy`/`unholy`; die Resistenz ist wie in battler.kod größter
  positiver plus kleinster negativer Wert über alle passenden Einträge. Geister widerstehen gewöhnlichen Waffen zu 90 %.

## Jala-Lieder (2026-11-03, `WorldJala.cs`)
Wie jala.kod: Ein Instrument muss getragen werden (Laute 20, edle/wahre Laute 30, Halskette der Jala 5; Platz
„Instrument“ bzw. „Hals“). Jala-Stärke: Fähigkeit/2 + Instrument (bis 40) + maximale Lebenspunkte/12 (bis 10). Das Lied
liegt auf dem Raum, nur eines; ein neues muss lauter sein (Lautstärke = Stärke × Manazahlung). Alle 5 s kostet es
Mana (viManaDrain); es endet, wenn der Barde sich bewegt, kämpft, einen anderen Spruch wirkt, den Raum verlässt, stirbt
oder weniger als das Doppelte der Zahlung an Mana hat.

| Lied | Stufe | Mana (Start/5 s) | Wirkung |
|---|---|---|---|
| Wahrheit, Höflichkeit, Versöhnung, Heilige/Unheilige Resonanz, Missklang | 1–4 | 5/3 | Erfolgschance der Riija-/Faren-/Kraanan-/Qor-/Shal'ille-/Jala-Sprüche im Raum − Random(Stärke/2, 2×Stärke/3), 5..95 |
| Frohsinn / Schwermut | 1 | 2/2 | jeder Anwesende (auch Neuankömmlinge) mit Stärke % Chance: gegen böse (Frohsinn) bzw. gute (Schwermut) Monster Treffer + 100, Schaden + Random(1, 5) |
| Belebung | 1 | 5/1 | Ausdauer-Takt × (200 − Stärke)/200 |
| Zeitverzerrung | 2 | 5/5 | Trancezeiten × (100 + Stärke)/100 |
| Zauberbann | 3 | 12/8 | schwächere Sprüche scheitern, Monster zaubern nicht (Gegenstände ausgenommen) |
| Wiederherstellung | 3 | 5/3 | Lebenspunkte-Takt × (400 − (40 + Stärke))/400, 0,67–60 s |
| Manakonvergenz | 5 | 10/9 | Spruchstärke + Stärke/2 + 25 für jeden Spruch im Raum |
| Verjüngung | 5 | 5/8 | Mana-Takt × (200 − Stärke)/200, 0,5–60 s |

Lehrer: Spielmann Parrin Aragone (wandernd, docs/wanderers.md) acht Lieder; Heilige Resonanz die Priesterin der
Shal'ille, Unheilige die der Qor. Lauten gibt es auf dem Festland nur als Beute (verkauft werden sie auf Ko'catan).
Noch nicht: Tanz (Jig), Destillieren, Mana kristallisieren; Lautstärke ohne Einfluss der Instrumentenqualität; Musik
des Liedes im Client; Stimmung der NPCs.

## Sprüche Teil 3: Schutz, Flüche, Raum (2026-11-06)
97 Sprüche. Umgesetzt in `WorldMoreSpells.cs`, Formeln aus docs/kod/spells_catalog.json:

| Spruch | Wirkung |
|---|---|
| Fluch lösen (Shal'ille 2) | legt alle verfluchten Gegenstände ab; ohne Fluch „keinen verfluchten Gegenstand“ |
| Krankheit heilen (Shal'ille 3) | Random(1,100) < Stärke: Lähmung (Schwäche) und Umnachtung weg; sonst Restdauer − Stärke % (Abw.: im Original Werte zurück) |
| Gift widerstehen (Kraanan 2) | neue Vergiftung abgewehrt bei Random(1,100) < Stärke (10..99), 450–900 s |
| Freie Bewegung (Kraanan 3) | Zustand Stärke/2 + 5 (10..50): Festhalten ausweichen bei Random(0,100) ≤ Z × 15/100, sonst Dauer − Dauer × Z/150 |
| Magischer Schild (Kraanan 3) | Abwehr + 50 + 2 × Stärke, (1 + (Stärke+5)/10) Minuten |
| Ablenken (Kraanan 4) | Angriffszauber von Monstern mit Random(0,100) < Stärke × 3/4 (10..75) zurück auf den Wirker (halbe Stärke) |
| Adleraugen (Kraanan 4) | Blendung abwehren bei Random(0,100) ≤ Z × 5/100, sonst Dauer − Dauer × Z/200 (Fernkampfbonus noch ohne Bögen) |
| Schattengestalt (Riija 1) | Abwehr + (30 + 164 − Raumlicht), 10..150; 60 + 3 × Stärke s |
| Böses/Gutes erkennen (Shal'ille/Qor 1) | Monster/Spieler mit Karma < −10 (bzw. > 10) und stärker als das eigene blinken (OF_FLASHING) |
| Unsichtbares sehen, Umhang, Namenlosigkeit | Verzauberung mit Dauer wie im Original; Wirkung fehlt noch (Unsichtbare Spieler, Jagd/Séance, Namen) |
| Verleugnung (Riija 4) | sofort volle LP; am Ende Rückzahlung Gewinn − Gewinn × ((Stärke+1)/5)/100, nie unter 1 LP |
| Köder (Riija 2) | jedes Monster greift mit Random(1,100) < 25 + Stärke/2 (mit Ziel: /4) den Wirker an |
| Umnachtung (Riija 4) | Monster zaubern (300 + 16 × Stärke) × 100 ms lang halb so oft |
| Mal der Schande (Shal'ille 4) | Monster wendet sich gegen den Wirker (auf Spieler: PvP) |
| Waffenruhe (Shal'ille 4) | Raum ohne Kampf zwischen Spielern und Monstern (auch keine Monsterzauber), Random(d/2, d), d = 20 + Stärke s |
| Hitze (Faren 3), Illusion bannen (Kraanan 4) | treffen im Original nur PK-fähige Spieler bzw. wirken nur für PK-fähige Wirker – ohne PvP nur die Meldung |
| Erkennen (Shal'ille 3) | zeigt Ladungen, Fluch, Spruchmodifikator und Spruch aller eigenen Gegenstände (Abw.: kein Einzelziel) |
| Schlitterblitz (Schule 7) | Angriffszauber Blitz 12–19, nur als Stab und für Ork-Zauberer |
| Vergessen, Spröde, Zerschmettern, Tausch | nur gegen Spieler – der Schutzengel verbietet es, bis es PvP gibt |

Lehrer nach plFor_sale: General (Erkennen, Illusion bannen, Gift widerstehen, Freie Bewegung), Druidengeist
(Ablenken), Tempel (Fluch lösen, Krankheit heilen, Böses/Gutes erkennen, Umhang, Adleraugen, Unsichtbares sehen …),
Marion-Ältester (Waffenruhe), Jasper-Ältester (Blitz, Umnachtung), Barloque-Schmied (Magischer Schild),
Barloque-Apotheker (Hitze). Riija-Sprüche lehrt nur der Mönch auf Ko'catan. Der Tos-Schmied fehlt noch.
Ausrüstung ändert die Spruchstärke (docs/trade.md).

Ergänzt (2026-11-06): **Totstellen** (Riija 1, fdeath.kod): Waffe, Schild und Helm fallen, andere Gegenstände mit
Random(1,100) < 10 − Stärke/10; falsche Meldung „### X wurde gerade von … getötet.“ an den Raum (Mörder: erstes
Monster des Raums, 1–3 % Mocker/Klavier/Häschen); Abwehr + 3 × Stärke; alle 5 s 5 Mana, unter 10 Mana Ende; endet
bei Bewegung, Angriff oder anderem Spruch (Abw.: kein Leichen-Trugbild, nur die Todesanimation; Monster lassen sich
wie im Original nicht täuschen). **Trugform** (Riija 3, illform.kod): erscheint anderen als Monster (Gewicht
S − |S − Stufe|, über S doppelt, S = Stärke × 1,3; nur CanMorphTo), Random(t/2, t), t = (90 + 2 × Stärke) s; auch
als brauner Trank (Stärke 1–80). Abw.: Gestalt nicht per Monstername wählbar.
