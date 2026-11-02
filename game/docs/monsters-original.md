# Monster-Katalog Meridian 59 (KOD) – Mechanik für die C#-Neuimplementierung

Quelle: `kod/object/active/holder/nomoveon/battler/monster.kod` (im Folgenden `monster.kod`),
`battler.kod`, `kod/object/passive/brain.kod`, `kod/object/active/holder/room/monsroom.kod`,
Konstanten aus `kod/include/blakston.khd`. Daten: `monsters_catalog.json` (172 Klassen, inkl.
Stadt-NPCs und abstrakter Basisklassen; Feld `category` unterscheidet monster/boss/event/summoned/npc/abstract/test/prop).

## 1. Klassenwerte (Defaults in monster.kod, classvars ab Zeile ~215)

| Feld | Default | Bedeutung |
|---|---|---|
| `viLevel` | 25 | 25–200 (≥150 Boss). Bestimmt HP, Treffer, Schaden, Beute |
| `viDifficulty` | 0 | 0–9 (Kommentar sagt 1–9). Treffer/Ausweichen/Sicht/Reichweite/Timing |
| `viSpeed` | 0 | SPEED_NONE 0, VERY_SLOW 4, SLOW 8, AVERAGE 12, FAST 16, VERY_FAST 20 |
| `viAttack_type` | ATCK_WEAP_HIT (0x8) | Waffen-Bitmaske des Nahkampfs |
| `viAttack_spell` | 0 | Zauber-Bitmaske (z. B. ATCK_SPELL_ACID beim Schleim) |
| `viTreasure_type` | TID_NONE (1) | Beutetabelle (`kod/object/passive/trestype/*.kod`) |
| `viKarma` | 0 | −100..100; Karma-Aggro und Spielerkarma bei Kill |
| `viWimpy` | 0 | Fluchtschwelle in % HP (0 = kämpft bis zum Tod) |
| `viDefault_behavior` | 0 | AI_*-Bits (Brain) |
| `viAttributes` | 0 | MOB_*-Bits (Dienste, Spasm/Random-Timer, ONE_TREASURE …) |

Es gibt **kein Geld-Feld (viCashmin/max)**: Münzen kommen ausschließlich über die Beutetabelle (`cashRange` ist im JSON immer `null`).
Resistenzen werden in `Constructed()` (selten `Constructor`) als `plResistances = [[prozent, typ], …]` gesetzt;
negativer Typ = Zaubertyp (`-ATCK_SPELL_*`), positiver = Waffentyp.

Einige Klassen deklarieren `vrName`, `viLevel` usw. im `properties:`-Block statt `classvars:` (z. B. Avar, Zombie, Troops) –
der Parser berücksichtigt beides (Property überschreibt Classvar).

## 2. Lebenspunkte
- `GetMaxHitPoints()` (monster.kod:4082): `level < 40 ? level : level*120/100`.
- Im `Constructor` (monster.kod:349) `piMax_hit_points = Fuzzy(GetMaxHitPoints())`.
- `Fuzzy(num, scale=4)` (monster.kod:4266): `num - num/4 + Random(0, num*2/4)` → 75 %–125 % (Ganzzahlarithmetik).
- Regeneration: `HealHitPoint()` (+1 HP, monster.kod:3967) mit 20 % Chance pro Chase-Tick (monster.kod:1110) und 25 % pro Move-Tick (monster.kod:1161).
- Schadensstufen-Meldungen bei Wechsel von `hp*5/maxhp` (`HitPointThreshold`, monster.kod:3918).

## 3. Treffen und Schaden
- Trefferchance (battler.kod:319 `TryAttack`): `chance = Offense(att) * 55 / Defense(ziel)`, begrenzt 10–95 %, Treffer wenn `chance >= Random(1,100)` (`EQUAL_CHANCE_HIT = 55`, battler.kod:19).
- Monster-Offense = Defense = `3*level + 60*difficulty`, 1..1500 (monster.kod:1436/1458); Palsy-Verzauberung: Offense ×3/4.
- Ausweichwert `difficulty*10` (monster.kod:1480).
- Schaden (monster.kod:1491): `Fuzzy(level / Random(10,15))`, mind. 1; Palsy ×3/4. Nur wenn `piMinDamage/piMaxDamage` gesetzt (nur Brambles) gilt `Random(min,max)`.
  Achtung: `CalculateDamage()` in mummy.kod/lupogg.kod wird nirgends aufgerufen (toter Code).
- Monster-Kampf-Ablauf (monster.kod:1409 `TryAttack`): kein Angriff bei AI_NOFIGHT/AI_NPC; Raum-Erlaubnis (`ReqSomethingAttack`); Monster dreht sich zum Ziel; `MonsterAttack()` (Hook für Animation/Spezialangriff); dann battler.TryAttack.
- Nach jedem nicht-tödlichen Treffer: `DidDamage` → `HitSideEffect(what)` (monster.kod:1648/3030) – hier hängen Gift, Palsy, Dementia, Ausdauerentzug.
- Erlittener Schaden (`AssessDamage`, monster.kod:1532): NPCs unverwundbar; Resistenz anwenden; +bonus; min. 1; Hass +1 wenn vom aktuellen Ziel; HP ≤ 0 → Rückgabe `$` = tot.
- Resistenz (battler.kod:189/251): über alle passenden Einträge `max(res) + min(res)` (jeweils mit 0 gestartet, geklemmt auf ±100). `ATCK_WEAP_ALL`/`ATCK_SPELL_ALL` trifft jeden Waffen-/Zauberangriff.
  Schaden: Resist > 0 → `dmg*(100-r)/100`; sonst `dmg*(-100+r)/-100` (= dmg*(100+|r|)/100, also Verwundbarkeit). Werte >100 (Xeochicatl 110) sind bei max auf 100 begrenzt.

## 4. Timing (Zustandsautomat)
Zustände: LIMBO (keine Spieler im Raum, keine Timer), WAIT, MOVE, CHASE, ATTACK (monster.kod:685–800).
- Angriff: `GetAttackTime` = `Fuzzy(1000) + 3500 − 70*(3*difficulty + speed)` ms (monster.kod:4325).
- Verfolgen: `GetChaseTime` = `Fuzzy(150) + 2000 − 40*(difficulty + 2*speed)`, diagonal ×1,414 (monster.kod:4343).
- Wandern: `Fuzzy(1500) + 300*(26 − clamp(Fuzzy(speed),0,25))`; mit Meister im Raum `Fuzzy(500) + 100*(26−…)` (monster.kod:4331).
- Neuer Raum: erste Aktion nach `MOB_START_FREEZE` = 15 s (brain.kod:70 `NewOwner`), sonst 1,5 s Wait.
- Sicht `4 + difficulty/2` Felder (monster.kod:1679), Angriffsreichweite `clamp(2 + difficulty/6, 2, 3)` (monster.kod:1685); `CanReach` = gleiche Raum + Distanz² ≤ Reichweite² + Sichtlinie (außer AI_FIGHT_THROUGH_WALLS), gecacht über VSTATE-Bits (monster.kod:1739).
- `AttackTimer` (monster.kod:1014): Brain.GetAttackBehavior (Positionieren), außer Reichweite → Chase, sonst zufällig Uhrzeigersinn-Bit und `TryAttack`, danach neuer Attack-Timer.
- `ChaseTimer` (monster.kod:1076): in Reichweite → Attack; evtl. heilen; **Zaubern** (`MonsterCanCastSpell`/`MonsterCastSpell`), sonst Brain.GetChaseBehavior (bewegen). Monster zaubern also nur während der Verfolgung (Ausnahmen: Lich, Lupogg-König und Yeti zaubern in einem eigenen `AttackTimer`; Xeochicatl zaubert zusätzlich in `MonsterAttack`, wenn kein Manaentzug erfolgt).
- Zufallswandern `DoRandomWalk` (monster.kod:1304): nur wenn `Random(1,140) >= hp` (starke Monster bummeln weniger), 1 Feld orthogonal, „Laufwille“ `piWalkingEffort` begrenzt auf 2*hp.
- Optionale Timer (monster.kod:4245): MOB_RANDOM → RandomTimer (120 s × 1..2,5), MOB_SPASM → Zuck-Animation alle 15–20 s.

## 5. Zielwahl / Aggro (brain.kod:636 `GetHatred`, 924 `AttemptTargetSwitch`)
Hass-Score für Kandidat; Wechsel wenn > aktueller Hass. Faktoren (brain.kod:30–50): Distanz 9, Spieler 10, Hyperaggressiv +60, Chaser 75, Wizard 25, Karma 25, MaxDistanz 7.
- Bereits Ziel: −10 (−15 extra wenn es uns angreift); AI_FIGHT_SINGLEMINDED wechselt nie.
- Unsichtbar −50, Schattenform −25; andere Räume → 0; Mord eines Weißnamens +75 (Wachen, AI_FIGHT_MURDERERS).
- Kandidat greift Meister an +60, greift uns an +40, greift Verbündeten an +20; Meister will ihn +50.
- Monster als Ziel nur mit AI_FIGHT_MONSTERS. AI_FIGHT_NEWBIESAFE: Spieler ohne PK-Flag bei Bewegung ignoriert.
- AI_FIGHT_KARMA_AGGRESSIVE: bei entgegengesetztem Karma-Vorzeichen `+|karma|*25/100`.
- AI_FIGHT_MURDERERS: Mörder +50 + 4*Kills; AI_FIGHT_WIZARD_KILLER: Magier +25; `SpecialHatredBehavior` je Klasse.
- Selbstverteidigung (angegriffen): +100 ohne Ziel bzw. +level/2.
- **Nur wenn Hass > 0 oder AI_FIGHT_AGGRESSIVE** geht es weiter: Nähe-Bonus ohne Ziel `difficulty*7/10*9 − 9*dist` (≥0), Monsterlevel/5 bzw. `(maxHP_Spieler−20)*10/100`, minus `Verfolgerzahl*75/100`; Hyperaggressiv +60.
- Auslöser: `SomethingMoved` (nur 1/10 der Bewegungen, brain.kod:132), `SomethingAttacked` (eigene immer, fremde 50 %, brain.kod:190), `SomethingKilled` (Wachen, Meister), erste Spielerbewegung im Raum.

## 6. Bewegung, Flucht, Gruppen (brain.kod:407 / 462 / 540)
- AI_NOMOVE: steht. AI_MOVE_RANDOM (z. B. geblendet): zufällig, im Kampf 2–3 Schritte.
- AI_MOVE_FLEE_FRIGHTENERS: flieht vor „Frightener“ (Untote vor dem Holy-Symbol-Wirker, `TurnedBy` hält `9 min / difficulty`, monster.kod:5755; Orks vor Riesenratten via `IsFrightenedOf`). Suchradius `(Sicht*3)²` (monster.kod:5303).
- **Flucht**: `HealthBelowWimpy` (monster.kod:5451) = `hp*100/maxhp < viWimpy`. Dann mit AI_MOVE_REGROUP zum nächsten Verbündeten (`GetClosestAlly`, Radius `(Sicht*5)²`, `IsAlly` = gleiche Klasse oder Klassen-Override, monster.kod:5377/5440), ohne Verbündete kämpft er weiter; ohne REGROUP läuft er weg.
- AI_MOVE_OPTIMAL_RANGE: hält Abstand `GetOptimalRange` (Schamane, Häuptling, Eiswesen, Dunkler Engel).
- AI_MOVE_FOLLOW_MASTER: Charm/Haustier folgt Meister.
- Kosmetisches Umkreisen im Nahkampf mit Chance `difficulty/20`.
- AI_MOVE_WALKTHROUGH_WALLS / AI_FIGHT_THROUGH_WALLS: Geist, Revenant, Lich, Xeochicatl.

## 7. Spezialangriffe
- **Gift** (`HitSideEffect` → `Poison.MakePoisoned`, `kod/object/passive/sickness/poison.kod`): Chance `1/POISON_CHANCE` pro Treffer; nur Spieler; Resist-Poison-Verzauberung kann abwehren; `AddPoison` (player.kod:9673): Vigor −20 sofort, alle 10 s `HP −= lossRate/1000` (nie unter 1 HP), Dauer `POISON_DURATION` ms. Werte: Spinne 1/30, 2500, 180 s; Schwarze Spinne 1/10, 5000, 180 s; Spinnenkönigin 1/15, 2500, 30 s; Dämmerratte 1/6, 10000, 30 s (+Taumel-Effekt).
- **Palsy/Dementia** (`MakeSick`, nur wenn noch nicht verzaubert): Skorpion Palsy 1/5, Kranker Baum 1/10, Zerschlagenes Skelett Palsy 1/10 + Dementia 1/10, Zombie Dementia 1/5, Thrasher Palsy 1/20.
- **Ausdauerentzug**: Narthyl-Wurm `AddExertion(random(3,7)*10000)`. **Manaentzug**: Xeochicatl 50 %/Treffer 5 % (Wasser 10 %) Max-Mana +1.
- **Zaubern** (Override von `MonsterCastSpell`, Aufruf im ChaseTimer): Muster `Random(1, AdjustedChanceBase(SPELL_CHANCE)) = 1` (monster.kod:1274: Antimagie-Aura ×1,5, Dementia ×2, Manabombe +1000), Reichweite meist Distanz² < 666, Sichtlinie, `ReqSomethingAttack`, dann `Spell.CastSpell(who=self, iSpellPower=75 oder SPELLPOWER_MAXIMUM)`. Mana-Kosten nur bei Avar-Schamane/Ork-Zauberer/Xeochicatl (`piMana`, Regeneration +1/s bis 10). Spruchtabellen je Monster im JSON unter `specials.spellcasting`.
- **Fernangriffe** über `SomethingShot` + normalen `TryAttack` (Avar-Häuptling Pfeil, Eiswesen Speer, Molluske Stein, Feen, Pilzbestie, Libellen-Spucke, Dunkler Engel).
- **Beschwörung**: Dunkler Engel erzeugt EvilTwin/Reflection (Limits 25 Monster / 10 Twins / 10 Reflections im Raum).
- **Zauberresistenz** (`SpellResist`): Dunkler Engel 50 %, Lich 60 % + Qor-immun, Xeochicatl 30 %; Statue immun gegen Blind/Dazzle/Seduce.
- **Doppelangriff**: Ork-Grubenboss, Thrasher (mit Waffe) – zweiter `TryAttack` 400 ms später. **Wirbel**: Thrasher trifft alle erreichbaren Spieler (7–15).

## 8. Tod und Beute
- `Killed` (monster.kod:3075): NPCs sterben nicht; Leiche `DeadBody(vrDead_icon, viDead_drawfx)`; Todesgeräusch; Questengine; `CreateTreasure`; Delete.
- `CreateTreasure` (monster.kod:4941): keine Beute bei Illusion/Beschworen; Anzahl `1 + level/55 + Random(0, difficulty/3)` (max 6, × ItemFactor%), bei MOB_ONE_TREASURE genau 1; plus Inventar des Monsters (Skelett: abgenutztes Langschwert; Troops: 20 % Ausrüstung; Xeochicatl: Herzstein; Grubenboss: Kopf).
- Spawn (monsroom.kod:120ff): Raum hat `plMonsters = [[&Klasse, Gewicht%], …]`, Generator-Timer `piGen_Time` (20 s) × SpawnRate% / 5 (Spielerzahl wird nur bei `vbScaleSpawnRateWithPlayers` berücksichtigt, sonst fest 5 → effektiv 4 s), `piGen_percent` Chance je Tick, max. `piMonster_count_max` 10, Start 1–5 Monster; Gewichte kumulativ gegen `Random(1,100)` (Summe < 100 = Chance auf nichts). JSON `spawns.generatorRooms` listet Raumklasse + Gewicht (max 10), `placedInRooms` explizit per `Create(&X)` platzierte.

## 9. Besonderheiten / Unsicherheiten
- Iceperson: `plResistances` verwendet `ATCK_SPELL_SHOCK/ALL/ACID/QUAKE` **ohne Minus** → Engine wertet sie als Waffentypen (WEAP_MAGIC/ALL/THRUST/SLASH). Im JSON mit `warning` markiert.
- Level/Difficulty/Speed ändern sich zur Laufzeit bei: Revenant (aus Spielerwerten), FactionTroops (Ausrüstung), LivingStatue/Thrasher (ruhend SPEED_NONE → aktiv), Thrasher-Waffe, Avar-Clan (Name/Farbe).
- Nicht in Räumen platziert (DM/ungenutzt): DarkAngel, Iceperson, EvilEnt, Guard (Wachkuh), flytrap, Thief, Priestess, GraphicTester, DragonFlyPet.
- `ladypheonix` (towns/ladyphen.kod) ist ein PassiveObject, kein Monster – nur der Vollständigkeit halber gelistet.
- Körperform/Größe/Beschreibung (`bodyShape`, `size`, `visualDe`) sind manuell aus `vrDesc` und BGF-Namen abgeleitet, nicht aus Grafikdaten.
- Abgeleitete Werte im JSON (`derived`) sind statische Bereiche aus den Classvars (Fuzzy-Grenzen), keine Laufzeitwerte.
