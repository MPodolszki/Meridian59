# Zauberkatalog Meridian 59 (Original-KOD)

Maschinenlesbar: `spells_catalog.json` (gleiches Verzeichnis, 187 Einträge inkl. 13 Basisklassen). Quelle: `kod/object/passive/spell/**`, Konstanten `kod/include/blakston.khd`, Lehrer aus `plFor_sale` der NPCs, Instanziierung `kod/util/system.kod:2866-3070` (CreateOneSpellIfNew).

Spalten: SID, Name, Stufe, Mana, Kategorie, Lehrer, Kurzeffekt (englisch aus dem Katalog). Details/Formeln mit Zeilenangaben stehen im JSON (`formulas`).


## Shal'ille (29 Zauber)

| SID | Name (Klasse) | St. | Mana | Kategorie | Lehrer | Effekt |
|---|---|---|---|---|---|---|
| 2 | minor heal (Heal) | 1 | 3 | heal | ShalillePriestess | Minor Heal: restores a few HP (GainHealthNormal, never above max), with a bonus for non-PK targets. The caster gains karma when healing someone with higher karma. |
| 125 | detect evil (DetectEvil) | 1 | 10 | divination | ShalillePriestess | Sets PFLAG2_DETECT_EVIL: while the viewer has karma > 0, players with karma < -10 and monsters with karma < -10 and /karma/ > viewer karma are drawn flashing. |
| 132 | holy symbol (HolySymbol) | 1 | 8 | utility/travel | ShalillePriestess | Each undead monster in the room rolls to be turned: on success it flees (TurnedBy) and targets the caster. On a bad failure (roll > chance+25) it chases the caster. |
| 148 | breath of life (BreathOfLife) | 1 | 3 | heal | ShalillePriestess | Transfers a karma-dependent share of the caster's current health to another player. The target may exceed max health up to 150% (hard cap 130). |
| 154 | seance (Seance) | 1 | 10 | divination | ShalillePriestess | Tells the caster the room where the last murderer is; on failure or if the murderer is cloaked, gives a false room and keeps repeating it for a while. |
| 21 | holy touch (HolyTouch) | 2 | 12 | attack | ShalillePriestess | Turns the caster's melee/punch attacks into holy touch strokes (range 2) whose damage depends on spellpower, doubled against undead and scaled by the victim's karma. |
| 31 | remove curse (RemoveCurse) | 2 | 9 | cure | ShalillePriestess | Unwields/removes all cursed items the target is using (they stay in inventory). |
| 37 | cure poison (CurePoison) | 2 | 10 | cure | ShalillePriestess | Either removes the poison enchantment entirely or reduces its strength (loss rate) by spellpower percent and re-applies it with the remaining time. |
| 60 | holy weapon (HolyWeapon) | 2 | 17 | buff | ShalillePriestess | Adds a timed weapon attribute WA_ATTACKSPELLTYPE with ATCK_SPELL_HOLY to the weapon so its hits count as holy damage (extra vs. holy-vulnerable/undead monsters per their resistance tables). |
| 149 | resist acid (ResistAcid) | 2 | 9 | buff | ShalillePriestess | Adds acid resistance (AddResistance(-ATCK_SPELL_ACID, value)) for several minutes and removes it on expiry. |
| 7 | rescue (Rescue) | 3 | 16 | utility/travel | ShalillePriestess | After a delay, teleports the caster to safety: own guild hall (if same region), Ko'catan inn (in Ko'catan region / Pool of Vigor), Pool of Vigor (orc caves), else hometown (AdminGoToSafety). |
| 40 | cure disease (CureDisease) | 3 | 9 | cure | ShalillePriestess | Chance to remove all diseases; otherwise weakens Palsy and Dementia by restoring a spellpower-percentage of their stat loss. |
| 41 | hospice (Hospice) | 3 | 10 | heal | TosApothecary | Heals the target; the caster gains karma if he heals someone with higher karma. |
| 62 | holy resolve (ResistEvil) | 3 | 3 | buff | KocatanBonePriestess | Holy Resolve: adds resistance against unholy spell damage (resistance type -ATCK_SPELL_UNHOLY). |
| 80 | identify (Identify) | 3 | 10 | divination | JaliGeneral, Rebel | Shows the caster a full look description of the item including hidden item attributes (temporarily reveals then re-hides them if not yet identified). |
| 100 | spiritual hammer (CreateSpiritualHammer) | 3 | 15 | create-item | ShalillePriestess | Creates a temporary SpiritualHammer weapon (bludgeon, high quality, holy attack flag, never takes wear) in the caster's inventory with a to-hit bonus; it deletes itself when its timer expires. |
| 26 | truce (Truce) | 4 | 20 | enchant-room | MarionElder | Sets ROOM_NO_MOB_COMBAT: no attacks between players and monsters in the room (monsters can't cast either) for the duration. |
| 27 | forces of light (ForcesOfLight) | 4 | 12 | enchant-room | ShalillePriestess | Room enchantment: every occupant with positive karma (checked on start/entry) gets an attack modifier (+hit roll); Shal'ille spells cast in the room get extra spellpower. |
| 44 | mark of dishonor (MarkOfDishonor) | 4 | 12 | debuff | ShalillePriestess | On an evil (negative karma) player: drains 20 vigor and lowers their vigor rest threshold proportionally to how evil they are, for the duration. On a monster: forces it to target and attack the caster. |
| 72 | purify (Purify) | 4 | 10 | cure | ShalillePriestess | Removes harmful personal enchantments from the caster, each with sp% chance; with 2+ such enchantments and sp>=60 one random one is removed guaranteed first. |
| 119 | Qorbane (QorBane) | 4 | 15 | enchant-room | KocatanBonePriestess | Room enchantment: anyone casting a Qor-school spell in the room takes holy damage (can die) if they are a monster, PK-enabled, or in a kill zone/arena. |
| 161 | dazzle (Dazzle) | 4 | 12 | debuff | ShalillePriestess | Blinds the target: a player gets a full white-out screen effect and NO_FIGHT/NO_MAGIC flags for the duration (then an 8 s 'pain' blur); a monster gets AI_MOVE_RANDOM+AI_NOFIGHT (wanders, won't fight). |
| 42 | major heal (MajorHeal) | 5 | 20 | heal | ShalillePriestess | Heals the target; healing others nudges the caster's karma toward the target's karma. |
| 50 | umbrella (Umbrella) | 5 | 15 | buff | ShalillePriestess | Adds a defense modifier: +250 defense power against all attackers, and reduces damage from undead attackers. |
| 81 | reveal (Reveal) | 5 | 30 | divination | JaliGeneral, Rebel | Reveals all hidden item attributes (RevealHiddenAttributes) and shows the full look. It also strips the Misdirection attribute if its power*2 < spellpower. |
| 133 | final rites (FinalRites) | 5 | 20 | radius | ShalillePriestess | Deals holy damage to all undead in the room, scaled by spellpower and by the karma difference between caster and victim. |
| 168 | purge (Purge) | 5 | 15 | debuff | FiveCouncilor | Strips beneficial personal enchantments from the target player, each with a chance equal to spellpower; an immortal DM strips all personal enchantments. |
| 110 | portal of life (PortalOfLife) | 6 | 30 | utility/travel | ShalillePriestess | Creates a CorpsePortal in the Underworld (row 16, col 16) that teleports the dead player back to their corpse (ZapToCorpse) when they step in, and lowers that player's death cost (chance to lose stats on death). |
| 145 | bond (Bond) | 6 | 50 | create-item | Heretic | Multicast via prism; on completion the item gets IA_TRANSCENDANT (it stays in the owner's inventory on death). |

## Qor (26 Zauber)

| SID | Name (Klasse) | St. | Mana | Kategorie | Lehrer | Effekt |
|---|---|---|---|---|---|---|
| 35 | unholy resolve (ResistGood) | 1 | 3 | buff | KocatanBonePriestess | 'Unholy resolve': adds holy-damage resistance (AddResistance(-ATCK_SPELL_HOLY, value)) for several minutes. |
| 38 | darkness (Darkness) | 1 | 8 | enchant-room | QorPriestess | Reduces the room's base light level for the duration. |
| 124 | detect good (DetectGood) | 1 | 10 | divination | QorPriestess | Sets PFLAG2_DETECT_GOOD: if the enchanted player has negative karma, good players (karma>10) and good monsters (karma > /own karma/) are drawn flashing. |
| 152 | cloak (Cloak) | 1 | 5 | buff | QorPriestess | Hides the target from location magic: Hunt notifications and Seance results show a random wrong room with a chance equal to the enchantment state. |
| 45 | acid touch (AcidTouch) | 2 | 10 | attack | QorPriestess | Enchants the caster's hands: while active the caster's melee attacks use the Acid Touch stroke (acid damage, range 2) instead of the weapon. |
| 59 | unholy weapon (UnholyWeapon) | 2 | 17 | buff | QorPriestess | Adds the WA_ATTACKSPELLTYPE attribute making the weapon's hits deal unholy-type damage, labelled with the maker. |
| 65 | fade (Fade) | 2 | 12 | illusion | BarloqueApothecary | Makes the caster invisible (PFLAG_INVISIBLE) while in a trance; ends on timer or when the trance is broken. |
| 66 | Kara'hol's curse (KaraholsCurse) | 2 | 9 | buff | QorPriestess | Berserk buff: large to-hit bonus and slight damage bonus on melee. When it expires the caster is held (paralysed) for 1/6 of the duration, with no free-action protection. |
| 22 | vampiric drain (VampiricDrain) | 3 | 10 | attack | DarkWizard | Unholy damage (ATCK_SPELL_ALL+ATCK_SPELL_UNHOLY) to the target, and the caster heals by half of the damage dealt. A kill heals a fixed bonus instead. |
| 49 | defile (Defile) | 3 | 0 | utility/travel | QorPriestess | Consumes a good player's corpse and gives the caster extra mana (may exceed max), at the cost of karma unless that corpse name was among the last 5 defiled. |
| 53 | enfeeble (Enfeeble) | 3 | 5 | debuff | QorPriestess | Applies the Palsy sickness: removes Super Strength, players lose Might for the duration; monsters get ENCH_PALSY (offense and damage *3/4). |
| 144 | poison fog (SummonPoisonFog) | 3 | 20 | wall/field | QorPriestess | Creates 9 poison fog clouds; players in a cloud may be poisoned (HP drain), monsters are not poisoned. |
| 163 | silence (Silence) | 3 | 10 | enchant-room | Heretic | While the caster stays in trance and has mana, nobody (except DMs) in the room can speak or cast spells; monsters that CanBeSilenced cannot cast. On cast it may also end a running Jala song. |
| 47 | hold (Hold) | 4 | 15 | debuff | QorPriestess | Paralyses the target: players get PFLAG_NO_MOVE, NO_FIGHT, NO_MAGIC and paralyze client effect; monsters get AI_NOMOVE+AI_NOFIGHT. |
| 85 | curse weapon (CurseWeapon) | 4 | 25 | debuff | DarkWizard | Adds the WA_CURSED weapon attribute: wielder suffers damage and hit-roll penalties and cannot unwield or drop the weapon once used, until the timer expires or Remove Curse. |
| 114 | animate (Animate) | 4 | 20 | summon | DarkWizard | Deletes the corpse and creates an undead monster (no treasure) at the corpse position whose type/level depends on spellpower; it becomes the caster's summoned pet (follows master, not aggressive, fights monsters, newb… |
| 118 | Shal'illebane (ShalilleBane) | 4 | 15 | enchant-room | KocatanBonePriestess | Any Shal'ille-school spell cast in the room hurts its caster with unholy damage (can kill) if the caster is a monster, PK-enabled, or in a kill zone. |
| 147 | shadow rift (DeathsDoor) | 4 | 30 | utility/travel | DarkWizard | Shadow Rift: several casters charge a prism. The first completed cast binds the prism's location; a second completed cast elsewhere opens a NecropolisPortal there leading to the bound spot. |
| 167 | splash of acid (SplashOfAcid) | 4 | 10 | attack | DarkWizard | Instant acid damage (ATCK_SPELL_ALL+ATCK_SPELL_ACID) to one target at short range via AssessDamage, so resistances apply (for example Resist Acid). |
| 46 | blind (Blind) | 5 | 15 | debuff | JasperBanker | Blinds the target: a player gets EFFECT_BLIND_ON (black screen) and PFLAG_NO_FIGHT + PFLAG_NO_MAGIC (cannot attack or cast); a monster gets AI_MOVE_RANDOM + AI_NOFIGHT (wanders, does not fight). |
| 51 | invisibility (Invisibility) | 5 | 12 | buff | QorPriestess | Makes the caster invisible (PFLAG_INVISIBLE) and adds a defense bonus; monsters don't react to the player entering; counts as an illusion (removed by dispel illusion). |
| 55 | swap (Swap) | 5 | 9 | debuff | QorPriestess | Exchanges a random item from the caster's inventory with a random (biased toward valuable) item from the target's inventory. |
| 73 | nodeburst (Nodeburst) | 5 | 1 | utility/travel | QorPriestess | Strips one of the caster's mana nodes and gives a burst of mana (up to a total of 250) and health. Once per game day. |
| 111 | death link (DeathLink) | 5 | 20 | enchant-room | DarkWizard | Room enchantment: whenever anything is killed in the room, the caster gains a little health and mana, four times as much for player victims. |
| 146 | blood inheritance (BloodInheritance) | 5 | 50 | create-item | kocatanTradeMaster | Several casters channel spellpower into a prism; when the total reaches 3000 the weapon receives a random Qor weapon attribute. |
| 127 | gaze of the basilisk (Paralyze) | 6 | 15 | buff | DarkWizard | Gaze of the Basilisk: adds a defense modifier; every time the caster is attacked (ModifyDefensePower is called), the attacker is held (Hold spell DoHold) for a short time; Revenants are immune, ranged-weapon player at… |

## Kraanan (27 Zauber)

| SID | Name (Klasse) | St. | Mana | Kategorie | Lehrer | Effekt |
|---|---|---|---|---|---|---|
| 56 | create weapon (CreateWeapon) | 1 | 15 | create-item | KraananPriestess | Conjures a random weapon into the caster's inventory (better weapons at higher spellpower) with reduced durability; it is marked 'made' and disappears after a timer. |
| 63 | create food (CreateFood) | 1 | 10 | create-item | KraananPriestess | Creates one random food item (by spellpower roll) in the caster's inventory; if inventory full it vanishes. |
| 153 | glow (GlowWeapon) | 1 | 15 | buff | JaliGeneral, Rebel | Adds a timed WA_GLOWING weapon attribute so the weapon emits light like a torch (replacing a weaker glow). |
| 158 | relay (Relay) | 1 | 5 | heal | KraananPriestess | Transfers vigor from caster to target, target receives 25% more than the caster loses. |
| 11 | bless (Bless) | 2 | 6 | buff | KraananPriestess | Attack modifier: increases hit roll by spellpower and adds a small random damage bonus. |
| 17 | super strength (SuperStrength) | 2 | 10 | buff | KraananPriestess | Raises Might for several minutes. |
| 61 | enchant weapon (EnchantWeapon) | 2 | 17 | buff | KraananPriestess | Adds the WA_ENCHANTED weapon attribute for some hours, which sets ATCK_WEAP_MAGIC (and clears ATCK_WEAP_NONMAGIC) so the weapon can hit monsters only vulnerable to magic weapons. |
| 116 | haste (Haste) | 2 | 10 | buff | JaliGeneral, Rebel | Reduces vigor (exertion) lost while running. |
| 130 | resist poison (ResistPoison) | 2 | 10 | buff | JaliGeneral, Rebel | Gives a chance to completely ignore new poisonings while active. |
| 4 | night vision (NightVision) | 3 | 6 | buff | KraananPriestess | Increases the target's personal light level so dark rooms are visible. |
| 12 | magic shield (MagicShield) | 3 | 9 | buff | BarloqueBlacksmith, TosBlacksmith | Raises the target's defense power (chance to be missed) for several minutes. |
| 82 | shroud (shroud) | 3 | 10 | buff | JaliGeneral, Rebel | Puts the Shroud item attribute (IA_SHROUD) on an item for hours, making it immune to most item spells (swap, shatter, brittle/weaken, enchant) while still mendable. |
| 123 | detect invisible (DetectInvisible) | 3 | 15 | buff | KocatanBonePriestess | Sets PFLAG2_DETECT_INVIS so the target sees invisible players; room contents are resent on start and end. |
| 126 | martyr's battleground (MartyrsBattleground) | 3 | 10 | enchant-room | Heretic | Room enchantment that raises the chance that a player killed in the room spawns a revenant against the killer. |
| 151 | free action (FreeAction) | 3 | 10 | buff | JaliGeneral, Rebel | Protects against Hold: chance to evade a Hold completely, otherwise reduces the hold duration. |
| 13 | mend (Mend) | 4 | 9 | utility/travel | MarionBlacksmith | Repairs an item to full current hits but permanently lowers its max hits to a percentage of the old max. |
| 43 | discordance (Discordance) | 4 | 8 | enchant-room | KraananPriestess | Tries to remove each room enchantment with a chance equal to the spellpower (half for Jala songs). From 60% chance one random enchantment is removed outright first. Immortal DMs or chance >= 100 or nil remove everything. |
| 68 | mana bomb (ManaBomb) | 4 | 0 | radius | KraananPriestess | Caster loses all his mana; every player in range loses a share of it, monsters in range get a 10 s debuff that makes their spellcasting nearly impossible. |
| 89 | dispel illusion (DispelIllusion) | 4 | 20 | utility/travel | JaliGeneral, Rebel | Each illusion object (monster/item with IsIllusion) in the room and each illusion enchantment on the room and on battlers in it is removed with a chance equal to the spellpower - but only if the caster is PK-enabled (… |
| 138 | deflect (Deflect) | 4 | 20 | buff | HuntGhost | Gives a chance to reflect incoming harmful single-target AttackSpells back onto their caster (recast on caster at half spellpower). |
| 162 | eagle eyes (EagleEyes) | 4 | 5 | buff | KraananPriestess | Adds an attack modifier improving ranged-weapon hit and damage, and protects against Blind and Dazzle: chance to resist completely, otherwise shortens the blinding duration. |
| 39 | killing fields (KillingFields) | 5 | 9 | enchant-room | KraananPriestess | All players in the room deal extra melee damage on every hit for the duration. |
| 54 | resist magic (ResistMagic) | 5 | 15 | buff | KraananPriestess | Adds resistance against all spell damage types (-ATCK_SPELL_ALL). |
| 74 | armor of Gort (ArmorOfGort) | 5 | 10 | buff | KraananPriestess | Defense modifier that absorbs a little damage per hit and caps the damage of any single hit. |
| 134 | hunt (Hunt) | 5 | 50 | divination | HuntGhost | Once casters have focused enough spellpower into a prism, the target gets a 30-minute enchantment; every time the hunted player changes room each hunter is told the room name. |
| 52 | anti-magic aura (AntiMagicAura) | 6 | 15 | enchant-room | KraananPriestess | Room enchantment that lowers the spellpower of every spell cast in the room (except Mana Convergence). |
| 139 | shatterlock (ShatterLock) | 6 | 50 | utility/travel | JaliGeneral, Rebel | Casters channel spellpower into the prism; when the required total is reached the guild hall's entrance door opens (if prism is in the foyer). |

## Faren (28 Zauber)

| SID | Name (Klasse) | St. | Mana | Kategorie | Lehrer | Effekt |
|---|---|---|---|---|---|---|
| 23 | zap (Zap) | 1 | 6 | attack | FarenPriestess | Charges the caster's hands: while no melee weapon is wielded, his normal attacks become shock touch attacks (range 3) using the Zap spell ability as stroke skill. |
| 30 | light (Light) | 1 | 5 | enchant-room | FarenPriestess | Increases the room's base light level for a while. |
| 150 | fog (SummonFog) | 1 | 6 | wall/field | FarenPriestess | Creates a 3x3 block of 9 FogCloud objects (translucent, purely visual, no damage) centered 2 squares in front of the caster in the facing direction (8 directions). |
| 156 | sweep (Sweep) | 1 | 8 | utility/travel | JasperInnKeeper | Pulls a number of random loose items from the room into the caster's inventory. Non-PK casters earn a RewardSweep from town NPCs present (Marion elder rants if near). |
| 157 | mystic touch (MysticTouch) | 1 | 3 | utility/travel | FarenPriestess | Transfers a percentage of the caster's current mana to the target, capped so the target ends at most at 1.5x max mana and 75. |
| 20 | touch of flame (TouchOfFlame) | 2 | 9 | attack | FarenPriestess | Enchants the caster's hands: melee attacks become Touch of Flame strokes (fire damage, range 2). |
| 24 | icy fingers (IcyFingers) | 2 | 6 | attack | FarenPriestess | Enchants the caster's hands so that melee attacks become icy touch strokes (cold spell type, unarmed/punch attack type, range 2) with spell-based damage for the duration. |
| 29 | withstand fire (ResistFire) | 2 | 4 | buff | FarenPriestess | Adds fire resistance (percentage damage reduction against ATCK_SPELL_FIRE) for several minutes. |
| 36 | resist cold (ResistCold) | 2 | 9 | buff | FarenPriestess | Adds cold resistance to the target for the duration. |
| 141 | bramble wall (BrambleWall) | 2 | 10 | wall/field | Heretic | Creates a line of 5 Bramble monsters (attackable, immobile) that scratch anyone moving through their square. |
| 25 | winds (Winds) | 3 | 10 | enchant-room | FarenPriestess | Room enchantment: lowers aim of all players present/entering and makes ranged attacks in the room miss often. |
| 28 | resist shock (ResistShock) | 3 | 6 | buff | FarenPriestess | Adds shock resistance (-ATCK_SPELL_SHOCK) to the target for the duration. |
| 33 | heat (Heat) | 3 | 8 | enchant-room | BarloqueApothecary | For 5 minutes, periodically burns every room occupant wearing metal or leather armor with fire damage, according to the armor type. Only PK-enabled players are hit (or anyone in kill zones or valid arena targets). |
| 67 | mana focus (ManaFocus) | 3 | 1 | buff | FarenPriestess | Puts the caster in a trance (indefinite) that speeds mana regeneration and adds bonus damage to attack spells; ends on any trance-breaking action except casting an AttackSpell. |
| 136 | spider web (SummonWeb) | 3 | 15 | wall/field | HuntGhost | Creates 9 web elements; a battler stepping onto a web square is stuck (cannot move) for a few seconds. Webs can catch the caster too. |
| 164 | shocking fury (ShockingFury) | 3 | 8 | attack | DarkWizard | Instant shock damage to one target at short range; caster and target get a brief screen invert effect. |
| 5 | fireball (Fireball) | 4 | 7 | attack | FarenPriestess | Visible fire projectile (orange light, speed 10) that deals fire+spell damage to one target via AssessDamage; caster gets kill target/PFLAG_DID_DAMAGE. |
| 58 | brittle (Brittle) | 4 | 8 | debuff | FarenPriestess | Damages the durability (hits) of the target player's wielded weapon. Sword of the Hunt is immune. |
| 128 | firewall (FireWall) | 4 | 15 | wall/field | Heretic | Creates a line of 5 damaging WallOfFire elements (plus 4 visual PassiveWallOfFire half-steps) in front of the caster; battlers on an element square take fire damage every ~1.5 s or on moving in. |
| 165 | blast of fire (BlastOfFire) | 4 | 8 | attack | DarkWizard | Instant fire damage to a single target at short range. |
| 166 | explosive frost (ExplosiveFrost) | 4 | 8 | attack | DarkWizard | Instant cold damage (ATCK_SPELL_ALL+ATCK_SPELL_COLD) to one target at short range, applied through AssessDamage, so resistances apply. |
| 1 | lightning bolt (Lightning) | 5 | 10 | attack | JasperElder | Fires a lightning projectile (BoltSpell, speed 12, light) dealing shock damage to the target via AssessDamage (ATCK_SPELL_ALL+ATCK_SPELL_SHOCK, resistances apply); caster and target get a 500 ms screen-invert effect. |
| 14 | earthquake (Earthquake) | 5 | 12 | radius | FarenPriestess | Damages every battler in the room with distance falloff (full within 8, zero beyond 20) and also hurts the player caster. Causes a room Rumble (3 s, 100% trance disruption). NPCs (no BATTLER_YES flag) are spared. |
| 57 | shatter (Shatter) | 5 | 12 | attack | FarenPriestess | Picks the most valuable of a few random shatterable items in the target's inventory and destroys it (or a percentage of a stack). |
| 75 | sandstorm (SandStorm) | 5 | 14 | enchant-room | FarenPriestess | Room storm: reduces aim of all users present, ruins ranged shots, and every 10 s drains vigor and deals small damage to PK-enabled players (or anyone in kill zones), never killing. |
| 160 | ring of flames (RingOfFlames) | 5 | 15 | wall/field | Heretic | Surrounds the target's square with 8 burning fire elements on the neighbouring squares (plus 4 passive visual flames on the target square's corners), damaging anyone in or entering those squares. |
| 87 | spore burst (SporeBurst) | 6 | 20 | wall/field | DarkWizard | Creates a spore cloud around the caster (1 active element + passive visuals); any battler (not the caster) moving within the radius is held (paralysed) via Hold.DoHold. |
| 140 | lightning wall (LightningWall) | 6 | 20 | wall/field | HuntGhost | Creates a 5-square wall of lightning (5 damaging elements plus 4 passive visual elements in between) that shocks anyone in or moving into those squares. |

## Riija (23 Zauber)

| SID | Name (Klasse) | St. | Mana | Kategorie | Lehrer | Effekt |
|---|---|---|---|---|---|---|
| 32 | blink (Blink) | 1 | 15 | utility/travel | Startzauber | Teleports caster to the current room's teleport point (Room.Teleport -> UtilGoNearSquare to viTeleport_row/col/angle). Special cases: in KOC Hall of Heroes -> RID_TOS_FORGET; in Bazman's room -> RID_FORGOTTEN_TOO. |
| 76 | shadow form (ShadowForm) | 1 | 5 | buff | RiijaMonk, OneCouncilor | Target is drawn as a black silhouette and gets a defense bonus that grows the darker the room (and the attacker's own light) is. |
| 86 | anonymity (Anonymity) | 1 | 15 | buff | RiijaMonk | Timed self-enchantment that sets PFLAG_ANONYMOUS on the caster, so other players cannot recognise them by name. When it ends the flag list is reset and the room is told the caster changed. |
| 113 | flash (Flash) | 1 | 15 | radius | RiijaMonk | Bright flash: each affected player has trance broken (EVENT_DISRUPT) and a 0.75 s white-out; each affected monster is held for 1 s (Free Action ignored). |
| 142 | feign death (FeignDeath) | 1 | 5 | illusion | RiijaMonk | Caster looks like a corpse (player illusion = a non-decaying corpse of himself or of his morph monster), drops wielded hand/head items and some inventory, gets a big defense bonus, and a fake '### X was just killed by… |
| 102 | forget (ForgetSpell) | 2 | 20 | debuff | RiijaMonk | Target temporarily loses access to several randomly chosen spells/skills (Player.HasForgetEnchantment blocks them). |
| 112 | foresight (Foresight) | 2 | 5 | divination | RiijaMonk | Shows the caster a random subset of the target's spells and skills with a proficiency word (knows of / adequate / mediocre / good / practitioner / great / master). |
| 115 | bait (Bait) | 2 | 8 | utility/travel | RiijaMonk | Each monster in the room may switch its target to the caster (hatred 100) and immediately chase him. |
| 155 | eavesdrop (Eavesdrop) | 2 | 8 | divination | Assassin | Places an invisible Listener in a random room of the yell zone; the caster hears says/emotes and player arrivals/departures there while in a trance, paying mana periodically. |
| 77 | illusionary form (IllusionaryForm) | 3 | 15 | illusion | RiijaMonk | Caster appears as a random monster template (name, icon, animations, lighting) for a duration; during the 6s cast trance the caster may say a monster's KOC name to choose the form (if its level < sp - random(0,10) and… |
| 99 | reflection (SummonReflection) | 3 | 15 | illusion | RiijaMonk | Creates a Reflection monster at the caster's position that copies the caster's appearance, name, defense and damage; it drains 1 mana from the caster every 8 s and vanishes when mana runs out or the caster logs off. |
| 106 | illusionary wounds (IllusionaryWounds) | 3 | 10 | attack | RiijaMonk | Deals 'fake' damage that disguises itself as Blast of Fire, Shocking Fury, Explosive Frost or Splash of Acid (name and sound), ignoring resistances. When the enchantment expires the damage is healed again if the victi… |
| 107 | vertigo (Vertigo) | 3 | 12 | debuff | RiijaMonk | On a player: -X agility, -X might, -X aim for the duration plus a client 'waver' screen effect; stats restored on end. On a monster: applies the Palsy disease (monster offense and damage x3/4) for the same duration. |
| 129 | illusionary firewall (IllusionaryFireWall) | 3 | 15 | wall/field | RiijaMonk | Creates an illusionary wall of fire (5 active + 4 passive elements in a line) that gives no light; stepping in it inflicts temporary Illusionary Wounds (HP loss restored when the wounds expire) and never kills. |
| 64 | dement (Dement) | 4 | 6 | debuff | JasperElder | Infects the target with the Dementia disease: player loses Intellect and suffers a spellpower penalty while it lasts; monster gets ENCH_DEMENTIA (its spell-cast chance halved). |
| 101 | apparition (SummonApparition) | 4 | 30 | illusion | RiijaMonk | Creates an illusionary, summoned monster at the target's location that immediately attacks the target (hatred 1000); its max HP is cut to 1/5. In a MonsterRoom a random monster type of that room's spawn list is used, … |
| 105 | denial (denial) | 4 | 15 | heal | RiijaMonk | Instantly restores the caster to full health; when the enchantment ends most of that healing is taken back (but never below 1 HP). |
| 84 | artifice (Artifice) | 5 | 10 | illusion | RiijaMonk | Adds a temporary Misdirection item attribute that makes the item display a random fake ('spoofable') item attribute in its name/description (and raises its apparent rarity), hiding its true powers until the timer expi… |
| 104 | morph (Morph) | 5 | 20 | illusion | RiijaMonk | Transforms the caster into a random (or chosen) monster template: removes Illusionary Form, unequips all items except soldier shields, changes max HP toward the monster's level and shows the monster's icon/name; playe… |
| 143 | evil twin (SummonEvilTwin) | 5 | 10 | summon | RiijaMonk | Creates an EvilTwin monster at the target's position that looks like the target (name/icon) and attacks it using the target's own offense, defense, damage and weapon; follows the target between rooms. |
| 103 | seduce (Seduce) | 6 | 15 | summon | – | On a monster: if the caster's roll beats the monster's max HP it becomes the caster's pet (follows master, fights monsters, not aggressive) while the caster pays mana; a badly failed roll enrages it. On a player it is… |
| 135 | summon bovine (SummonCow) | 6 | 5 | summon | – | Several casters charge a prism; once all sides are occupied and the accumulated spellpower reaches the requirement, a summoned pygmy Cow appears at the first caster's position. |
| 169 | elusion (Elusion) | 6 | 20 | utility/travel | RiijaMonk | Replaces the normal cast trance with a 30s trance that lists randomly chosen destination towns; if the caster says (part of) a listed room name during the trance, they are teleported to that room's teleport point. |

## Jala (17 Zauber)

| SID | Name (Klasse) | St. | Mana | Kategorie | Lehrer | Effekt |
|---|---|---|---|---|---|---|
| 88 | truth (Truth) | 1 | 5 | enchant-room | Minstrel | While sung, every Riija-school spell cast in the room has its success chance reduced. |
| 175 | invigorate (Invigorate) | 1 | 5 | enchant-room | CorNothInnkeeper | While sung, all players in the room regenerate vigor faster (shorter rest timer). |
| 179 | mirth (Mirth) | 1 | 2 | enchant-room | Minstrel | Bard song: each occupant (on start/entry) has sp% chance to become happy and gain an attack modifier vs negative-karma targets; NPCs' mood rises each drain tick. |
| 180 | melancholy (Melancholy) | 1 | 2 | enchant-room | Minstrel | Each player in/entering the room gets, with sp% chance, a sad action and an attack modifier that boosts hit and damage against good-karma targets; NPCs (AI_NPC) in the room lose 1 mood each drain tick. |
| 122 | warp time (WarpTime) | 2 | 5 | enchant-room | Minstrel | Stretches all spellcasting trance times in the room (including trances already in progress when a player enters/song starts; restored when leaving/song ends). |
| 170 | civility (Civility) | 2 | 5 | enchant-room | Duke | Room song that lowers the success chance of every Faren-school spell cast in the room while the bard keeps singing (trance). Only one Jala song per room; a louder song is required to replace one. |
| 171 | conciliation (Conciliation) | 2 | 5 | enchant-room | Princess | Room song that lowers everyone's success chance for Kraanan spells cast in the room while the bard keeps singing (trance). |
| 120 | spellbane (Spellbane) | 3 | 12 | enchant-room | Minstrel | While sung, any (non-item) spell cast in the room whose spellpower is below the song's spellpower fails, and silenceable monsters cannot cast at all. |
| 159 | distill (Distill) | 3 | 20 | create-item | MarionHealer | Brews a potion of the named spell, consuming a multiple of that spell's reagents from the caster's inventory; the potion's power depends on the caster's ability in the target spell and in Distill. |
| 172 | sacred resonance (SacredResonance) | 3 | 5 | enchant-room | ShalillePriestess | While sung, every Qor-school spell cast in the room has its success chance reduced. |
| 173 | profane resonance (ProfaneResonance) | 3 | 5 | enchant-room | QorPriestess | While the bard keeps singing (trance), every Shal'ille spell cast in the room has its success chance reduced. |
| 176 | restorate (Restorate) | 3 | 5 | enchant-room | JasperInnKeeper | Speeds up HP regeneration of all players in the room while sung. |
| 137 | jig (Jig) | 4 | 10 | enchant-room | Minstrel | Everyone in the room except the bard drops hand-held items and dances; room gets ROOM_JIG (monsters cannot enter, no item use/apply) and ROOM_NO_MOB_COMBAT for the song's duration. |
| 174 | disharmony (Disharmony) | 4 | 5 | enchant-room | Minstrel | While sung, anyone in the room casting a Jala-school spell has their success chance reduced. |
| 131 | mana convergence (ManaConvergence) | 5 | 10 | enchant-room | Minstrel | Room song that raises the spellpower of every spell cast in the room (except Anti-Magic Aura) while it lasts. |
| 177 | rejuvenate (Rejuvenate) | 5 | 5 | enchant-room | Minstrel | Everyone in the room regenerates mana faster while the song lasts. |
| 181 | crystalize mana (CrystalizeMana) | 6 | 20 | create-item | FiveCouncilor | While the bard keeps singing, each second adds 1-2 mana to a personal Mana Crystal item in the bard's inventory (created on first tick); using the crystal later restores the stored mana. |

## DM / Sonder (Schule 7) (24 Zauber)

| SID | Name (Klasse) | St. | Mana | Kategorie | Lehrer | Effekt |
|---|---|---|---|---|---|---|
| 117 | slitherbolt (Slitherbolt) | 5 | 10 | attack | – | Fires a ground-following projectile bolt that does shock+unholy damage to one target. |
| 6 | deliverance (DMRescue) | 50 | 0 | dm/admin-only | – | Sends the player to safety (AdminGoToSafety); for logoff ghosts moves the ghost to the player's saved room/row/col. Also clears a 'damn_last_annoyed' honor string. |
| 8 | smite (Smite) | 50 | 0 | dm/admin-only | – | DM-only bolt that deals 500000 absolute damage (kills anything). |
| 9 | damn (Damn) | 50 | 0 | dm/admin-only | – | Moves the target to the prison room RID_OUTOFGRACE at row 4/col 3 and sets the player's honor string to '<He/She> was sent to hell by <DM>.'. For logged-off ghosts also sets the saved location. |
| 10 | pardon (Pardon) | 50 | 0 | dm/admin-only | – | Clears PFLAG_MURDERER and PFLAG_OUTLAW from the target and re-evaluates PK status; fails if target is neither. |
| 34 | page (Page) | 50 | 0 | dm/admin-only | – | Plays a page sound (god2god.wav) to DM and target and sends text messages. |
| 70 | bonk (Bonk) | 50 | 0 | dm/admin-only | – | Cosmetic: shoots a random-coloured projectile at the target, inverts the target's screen briefly (EFFECT_INVERT 10000) and sends bonk messages; cows react via @Bonked. |
| 71 | scry (Scry) | 50 | 0 | dm/admin-only | – | Reports to the DM the target's mastery title per school (apprentice..archmage = highest known spell level 1..6) for Shal'ille, Faren, Riija, Kraanan, Qor, Jala and weaponcraft; if DM's scry ability >= 50 also triggers… |
| 78 | armageddon (Armageddon) | 50 | 0 | dm/admin-only | – | Fires a light projectile at every monster in the room that can fight the caster and instantly deletes (vaporizes) those that allow DMCanArmageddon. |
| 79 | vilify (Villify) | 50 | 0 | dm/admin-only | – | Admin tool to escalate PK status: lawful becomes outlaw, outlaw becomes murderer. Already murderers are refused. |
| 108 | insight (Insight) | 50 | 0 | dm/admin-only | – | Sends the DM, for each spell and skill of the target player, its ability and the improvement chance (initial*secondary/100 bounded 1..100, plus the two components). |
| 109 | evaluate (Evaluate) | 50 | 0 | dm/admin-only | – | Admin tool: shows the DM the target's chance to gain a hit point per kill, suggests suitable monster templates, and lists combat stats (offense, defense, damage, damage types). The target gets a screen-invert effect f… |
| 121 | superior hold (DMHold) | 50 | 0 | dm/admin-only | – | Toggle: freezes the target for 10 minutes (player: paralyze effect + NO_MOVE/NO_FIGHT/NO_MAGIC; monster: AI_NOMOVE/AI_NOFIGHT); casting again on a held target releases it. |
| 178 | squelch (Squelch) | 50 | 0 | dm/admin-only | – | Toggles PFLAG_SQUELCHED on the target player (removes or restores the ability to broadcast). |
| 190 | agility effect (AgilityBoon) | 50 | 0 | buff | – | Temporarily adds (or removes, if negative) Agility points via AddAgility; reverted when the boon ends. |
| 191 | aim effect (AimBoon) | 50 | 0 | buff | – | Adds 'amount' points of Aim for a number of logged-on hours, re-arming a 1-hour timer each tick; removes the points at the end. |
| 192 | stamina effect (StaminaBoon) | 50 | 0 | buff | – | Long-term (online-hours) boon/bane: AddStamina(points=amount) (the Stamina attribute); reverted when the boon expires. Amount may be negative (bane). |
| 193 | strength effect (StrengthBoon) | 50 | 0 | buff | – | Adds 'amount' points of Might to the target for a number of logged-on hours, then removes them. Mainly granted by quest nodes (questnode.kod:1298 AddBoon) and DMs. |
| 194 | mysticism effect (MysticismBoon) | 50 | 0 | buff | – | Long-term enchantment adding `amount` Mysticism, removed after N enchantment hours. |
| 195 | intellect effect (IntellectBoon) | 50 | 0 | buff | – | Long-term enchantment that adds `amount` Intellect (can be negative) to the target and removes it after N enchantment hours. |
| 196 | mana effect (ManaBoon) | 50 | 0 | buff | – | Temporarily raises (or lowers) the player's maximum mana by the boon amount (AffectsMaxMana; player.ComputeMaxMana adds GetManaBonus). |
| 197 | health effect (HPBoon) | 50 | 0 | buff | – | Long-term (online-hours) boon/bane: GainMaxHealth(amount) on start; reverted when the boon expires. Amount may be negative (bane). |
| 198 | vigor effect (VigorBoon) | 50 | 0 | buff | – | Long-term (online-hours) boon/bane: vigor rest threshold += amount; reverted when the boon expires. Amount may be negative (bane). |
| 83 | engrave (Engrave) | 100 | 0 | dm/admin-only | – | Writes the text the caster says during the (fixed 30 s) trance onto the item as an IA_ENGRAVED attribute; if the trance completes without speech it fizzles. |

## Zählung Schule × Kategorie

| Schule | attack | buff | create-item | cure | debuff | divination | dm/admin-only | enchant-room | heal | illusion | radius | summon | utility/travel | wall/field | Σ |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Shal'ille | 1 | 4 | 2 | 4 | 3 | 4 |  | 3 | 4 |  | 1 |  | 3 |  | 29 |
| Qor | 3 | 6 | 1 |  | 5 | 1 |  | 4 |  | 1 |  | 1 | 3 | 1 | 26 |
| Kraanan |  | 15 | 2 |  |  | 1 |  | 4 | 1 |  | 1 |  | 3 |  | 27 |
| Faren | 9 | 4 |  |  | 1 |  |  | 4 |  |  | 1 |  | 2 | 7 | 28 |
| Riija | 1 | 2 |  |  | 3 | 2 |  |  | 1 | 6 | 1 | 3 | 3 | 1 | 23 |
| Jala |  |  | 2 |  |  |  |  | 15 |  |  |  |  |  |  | 17 |
| DM / Sonder (Schule 7) | 1 | 9 |  |  |  |  | 14 |  |  |  |  |  |  |  | 24 |

## Nicht (regulär) erlernbar / Sonderfälle

- **seduce** (Seduce, SID 103, Schule 5): unclear - no NPC teaches it; exists as wand (item/passitem/spelitem/wand/seducew.kod)
- **summon bovine** (SummonCow, SID 135, Schule 5): unclear - Riija level 6, created by system.kod but no NPC sells it
- **slitherbolt** (Slitherbolt, SID 117, Schule 7): false for players to learn - school SS_DM_COMMAND (7); used by orc wizard monsters (orcwiza.kod) and the slitherbolt wand item (slitherw.kod)
- **deliverance** (DMRescue, SID 6, Schule 7): false - school 7 DM command
- **smite** (Smite, SID 8, Schule 7): false - SS_DM_COMMAND (school 7), DM class check
- **damn** (Damn, SID 9, Schule 7): false - DM command (school 7, DMSpell)
- **pardon** (Pardon, SID 10, Schule 7): false - school 7 DM command
- **page** (Page, SID 34, Schule 7): false - SS_DM_COMMAND
- **bonk** (Bonk, SID 70, Schule 7): false - school 7 DM command
- **scry** (Scry, SID 71, Schule 7): false - DMSpell, school 7
- **armageddon** (Armageddon, SID 78, Schule 7): false - school 7 DM command
- **vilify** (Villify, SID 79, Schule 7): false: DM command (school 7).
- **insight** (Insight, SID 108, Schule 7): false - DMSpell, school 7
- **evaluate** (Evaluate, SID 109, Schule 7): false: school SS_DM_COMMAND (7), DM only.
- **superior hold** (DMHold, SID 121, Schule 7): false - DMSpell, school 7
- **squelch** (Squelch, SID 178, Schule 7): false: DM command (school 7).
- **agility effect** (AgilityBoon, SID 190, Schule 7): quest-reward only (not learnable; school 7, granted via questnode.kod / AddBoon). false as learnable spell (SS_DM_COMMAND, level 50); granted as quest reward via questnode.kod:1286-1323
- **aim effect** (AimBoon, SID 191, Schule 7): quest-reward only (not learnable; school 7, granted via questnode.kod / AddBoon). false for players - school 7 (SS_DM_COMMAND), CanPayCosts rejects non-DM/non-monster; granted as quest reward (questnode.kod:1278,1322)
- **stamina effect** (StaminaBoon, SID 192, Schule 7): quest-reward only (not learnable; school 7, granted via questnode.kod / AddBoon). false - school 7 (SS_DM_COMMAND), level 50; only DMs/NPC quest rewards apply it
- **strength effect** (StrengthBoon, SID 193, Schule 7): quest-reward only (not learnable; school 7, granted via questnode.kod / AddBoon). false - school SS_DM_COMMAND (7), level 50, CanPayCosts restricts to DM/Monster; only received as quest reward boon
- **mysticism effect** (MysticismBoon, SID 194, Schule 7): quest-reward only (not learnable; school 7, granted via questnode.kod / AddBoon). false as a spell (DM school); granted by Marion rose offering / quest prize
- **intellect effect** (IntellectBoon, SID 195, Schule 7): quest-reward only (not learnable; school 7, granted via questnode.kod / AddBoon). false as a learnable spell (school 7 DM, level 50); granted as quest prize via questnode.kod:1271-1273
- **mana effect** (ManaBoon, SID 196, Schule 7): quest-reward only (not learnable; school 7, granted via questnode.kod / AddBoon). false as spell (school 7); granted via quest reward (questnode.kod:1306-1323)
- **health effect** (HPBoon, SID 197, Schule 7): quest-reward only (not learnable; school 7, granted via questnode.kod / AddBoon). false - school 7 (SS_DM_COMMAND), level 50; only DMs/NPC quest rewards apply it
- **vigor effect** (VigorBoon, SID 198, Schule 7): quest-reward only (not learnable; school 7, granted via questnode.kod / AddBoon). false - school 7 (SS_DM_COMMAND), level 50; only DMs/NPC quest rewards apply it
- **engrave** (Engrave, SID 83, Schule 7): false - school SS_DM_COMMAND, level 100, desc says DM only

## Abhängigkeiten → Zauber

- `personal-enchantment-framework` (50): Kara'hol's curse, acid touch, animate, anonymity, armor of Gort, bless, blind, cloak, cure poison, dazzle, deflect, denial, detect evil, detect good, detect invisible, eagle eyes, eavesdrop, enfeeble, fade, free action, gaze of the basilisk, haste, holy resolve, holy touch, hunt, icy fingers, illusionary form, illusionary wounds, invisibility, magic shield, mana focus, mark of dishonor, morph, night vision, purge, purify, resist acid, resist cold, resist magic, resist poison, resist shock, shadow form, super strength, touch of flame, umbrella, unholy resolve, vertigo, winds, withstand fire, zap
- `outlaw/PK-flags` (49): Qorbane, Shal'illebane, apparition, blast of fire, blind, bramble wall, brittle, dazzle, dement, dispel illusion, earthquake, enfeeble, evil twin, explosive frost, final rites, fireball, firewall, flash, foresight, forget, heat, hold, hunt, illusionary wounds, lightning bolt, lightning wall, mana bomb, mark of dishonor, martyr's battleground, minor heal, morph, poison fog, profane resonance, purge, reflection, ring of flames, sandstorm, seance, seduce, shatter, shatterlock, shocking fury, silence, spider web, splash of acid, spore burst, swap, vampiric drain, vertigo
- `room-enchantment-framework` (31): Qorbane, Shal'illebane, anti-magic aura, civility, conciliation, crystalize mana, darkness, death link, discordance, disharmony, forces of light, heat, invigorate, jig, killing fields, light, mana convergence, martyr's battleground, melancholy, mirth, profane resonance, rejuvenate, restorate, sacred resonance, sandstorm, silence, spellbane, truce, truth, warp time, winds
- `attack-spell/resistances` (21): apparition, blast of fire, deflect, earthquake, explosive frost, final rites, fireball, firewall, heat, holy touch, holy weapon, icy fingers, illusionary wounds, lightning bolt, lightning wall, mana focus, ring of flames, shocking fury, splash of acid, vampiric drain, zap
- `trance` (19): blood inheritance, civility, conciliation, crystalize mana, disharmony, eavesdrop, fade, feign death, flash, jig, mana convergence, mana focus, mirth, restorate, shadow rift, shatterlock, spellbane, summon bovine, warp time
- `karma` (18): anonymity, breath of life, dazzle, defile, detect evil, detect good, final rites, forces of light, holy symbol, holy touch, hospice, major heal, mark of dishonor, martyr's battleground, melancholy, minor heal, mirth, seance
- `jala-songs` (18): civility, conciliation, crystalize mana, discordance, disharmony, invigorate, jig, mana convergence, melancholy, mirth, profane resonance, rejuvenate, restorate, sacred resonance, silence, spellbane, truth, warp time
- `weapon-attributes` (12): blood inheritance, brittle, curse weapon, eagle eyes, enchant weapon, glow, holy weapon, remove curse, reveal, shroud, spiritual hammer, unholy weapon
- `resistances` (11): Shal'illebane, acid touch, holy resolve, resist acid, resist cold, resist magic, resist shock, touch of flame, unholy resolve, unholy weapon, withstand fire
- `item-enchantment` (9): artifice, curse weapon, enchant weapon, glow, holy weapon, remove curse, shroud, spiritual hammer, unholy weapon
- `wall-elements` (9): bramble wall, firewall, fog, illusionary firewall, lightning wall, poison fog, ring of flames, spider web, spore burst
- `summoned-monsters` (9): animate, apparition, bait, bramble wall, evil twin, fog, reflection, seduce, summon bovine
- `room-light` (8): darkness, firewall, glow, light, lightning wall, night vision, ring of flames, shadow form
- `skills` (7): acid touch, foresight, forget, holy touch, icy fingers, touch of flame, zap
- `room-flags (safe/no-magic)` (7): Qorbane, apparition, jig, sandstorm, silence, spore burst, truce
- `hold/paralysis` (7): Kara'hol's curse, flash, free action, gaze of the basilisk, hold, spider web, spore burst
- `chat/messaging` (7): eavesdrop, elusion, hunt, illusionary form, melancholy, mystic touch, silence
- `inventory` (6): create food, create weapon, remove curse, shatter, swap, sweep
- `guilds` (6): earthquake, elusion, rescue, shatterlock, spellbane, truce
- `newbie-guardian-angel` (6): blind, dement, reflection, seduce, truce, vertigo
- `death/respawn` (6): Qorbane, Shal'illebane, animate, bond, death link, portal of life
- `multicaster` (6): blood inheritance, bond, hunt, shadow rift, shatterlock, summon bovine
- `teleport/rift` (5): blink, elusion, portal of life, rescue, shadow rift
- `stats-boost` (5): cure disease, dement, enfeeble, super strength, vertigo
- `vigor/exertion` (5): haste, invigorate, mark of dishonor, relay, sandstorm
- `illusion-monsters` (5): apparition, dispel illusion, evil twin, illusionary form, morph
- `mana-focus` (5): blast of fire, explosive frost, fireball, lightning bolt, shocking fury
- `sickness` (4): cure disease, dement, enfeeble, vertigo
- `combat-modifiers` (4): armor of Gort, bless, forces of light, umbrella
- `prism` (4): blood inheritance, bond, shatterlock, summon bovine
- `illusion` (4): forget, illusionary firewall, illusionary wounds, invisibility
- `instruments` (4): conciliation, invigorate, sacred resonance, truth
- `undead` (3): final rites, holy symbol, holy touch
- `poison` (3): cure poison, poison fog, resist poison
- `item-attributes` (3): bond, identify, reveal
- `blindness` (3): blind, dazzle, eagle eyes
- `invisibility` (3): detect invisible, fade, invisibility
- `mana` (3): crystalize mana, defile, mana bomb
- `monster-ai` (2): holy symbol, seduce
- `disease` (2): cure disease, dement
- `health` (2): denial, hospice
- `melee-combat/strokes` (2): acid touch, touch of flame
- `attack-modifiers` (2): Kara'hol's curse, killing fields
- `free-action` (2): hold, spore burst
- `item-durability` (2): brittle, mend
- `facing/angles` (2): bramble wall, lightning wall
- `ranged-combat` (2): sandstorm, winds
- `mana-regen` (2): mana focus, rejuvenate
- `projectile` (2): fireball, lightning bolt
- `mana-drain` (2): feign death, summon bovine
- `reflections` (2): evil twin, reflection
- `client draw flags` (1): detect evil
- `murderer-tracking` (1): seance
- `cloak` (1): seance
- `tokens` (1): rescue
- `home-town` (1): rescue
- `monster-AI/aggro` (1): mark of dishonor
- `monster-ai-flags` (1): dazzle
- `eagle-eyes` (1): dazzle
- `player-flags/drawfx` (1): detect good
- `hunt` (1): cloak
- `seance` (1): cloak
- `corpses/death` (1): defile
- `prism-item` (1): shadow rift
- `mana-nodes` (1): nodeburst
- `day/night` (1): nodeburst
- `item-attributes (IA_MADE)` (1): create weapon
- `weapons` (1): create weapon
- `items/food` (1): create food
- `defense-modifiers` (1): magic shield
- `revenants` (1): martyr's battleground
- `monster-spellcasting` (1): mana bomb
- `illusion enchantments` (1): dispel illusion
- `combat` (1): killing fields
- `spellpower` (1): anti-magic aura
- `room-flags (ROOM_SPELL_POWER_MODIFIED)` (1): anti-magic aura
- `guild-halls` (1): shatterlock
- `melee-combat` (1): zap
- `npc-rewards` (1): sweep
- `touch-attack/combat-stroke` (1): icy fingers
- `armor` (1): heat
- `room-rumble/trance-disruption` (1): earthquake
- `items (CanShatter/DoShatter)` (1): shatter
- `client-effects (EFFECT_SAND)` (1): sandstorm
- `draw effects` (1): shadow form
- `defense modifiers` (1): shadow form
- `player-flags` (1): anonymity
- `blindness (whiteout effect)` (1): flash
- `illusion/morph` (1): feign death
- `death/respawn (fake)` (1): feign death
- `player-illusion` (1): feign death
- `monster-ai/aggro` (1): bait
- `factions` (1): bait
- `yell-zones` (1): eavesdrop
- `illusionary-wounds` (1): illusionary firewall
- `summoned-objects-limit` (1): illusionary firewall
- `item-attributes (IA_MISDIRECTION)` (1): artifice
- `timers` (1): artifice
- `monster-templates` (1): morph
- `npc-mood` (1): mirth
- `spellbane` (1): spellbane
- `trance/speech` (1): distill
- `potions` (1): distill
- `reagents/inventory` (1): distill
- `spell-abilities` (1): distill
- `health-regen` (1): restorate

## Empfohlene Implementierungsreihenfolge

Grundlage: Abhängigkeitsliste oben, gemeinsame Mechanik unten (Abschnittsnummern beziehen sich auf den Mechanik-Teil).

1. **Kern-Pipeline** (Mechanik §1–3): `UserCast` → `CanPayCosts` in Original-Reihenfolge, `PayCosts` (Erfolgswurf vor der Trance, Fehlschlag kostet halbes Mana/Erschöpfung), Spellpower 1–99, Castzeit `viCast_time*(150−SP)/100`, Post-Cast-Sperre, Fähigkeitssteigerung. Ohne Trance zunächst mit sofortigem Cast testbar.
2. **Lernen bei Lehrern** (§4): `plFor_sale`-Spruchlisten (Lehrer-Spalte in den Tabellen), `PlayerCanLearn`, Karma-Grenzen Shal'ille/Qor.
3. **Einfache Direktzauber ohne Folgesysteme**: minor/major heal, hospice, breath of life, relay, mystic touch, create food/weapon, blink, cure poison/disease (sobald Gift/Krankheit existieren).
4. **Personal-Enchantment-Framework** (§5, 50 Zauber): Timer, kein Stacking, Speichern/Einfrieren beim Logout, Icons. Danach Buffs mit reinen Wertänderungen: bless, super strength, magic shield, armor of Gort, haste, resist-*-Familie (resist.kod), night vision, eagle eyes, mana focus, deflect, umbrella, free action, detect evil/good/invisible.
5. **Angriffszauber** (§6): Schaden `random(min,max)*(50+SP/2)/99`, Resistenzen/Schwächen, Spieler-Kappung (30 bzw. ⅓ MaxHP), Projektile (Bolt). Dann Touch-Attacks (§7: zap, touch of flame, icy fingers, holy touch, acid touch).
6. **Debuffs mit Statusflags**: hold (+ free action, gaze, Kara'hol), blind/dazzle, enfeeble, vertigo, dement, forget, mark of dishonor, purge – benötigt Hold/Blind-Status und Sickness-System.
7. **Room-Enchantment-Framework** (§8): light/darkness (Raumlicht, passt zum vorhandenen Tag-Nacht-System), winds, heat, sandstorm, killing fields, forces of light, truce, silence, anti-magic aura, Qorbane/Shal'illebane, martyr's battleground, death link, discordance.
8. **Radiuszauber** (§10): mana bomb, earthquake, final rites, flash.
9. **Wände/Felder** (§9, wallelem-Objekte): fog, firewall, lightning wall, bramble wall, ring of flames, spider web, spore burst, poison fog, illusionary firewall.
10. **Gegenstandsverzauberung**: holy/unholy/enchant/curse weapon, glow, brittle, shatter, mend, remove curse, identify, reveal, shroud, spiritual hammer.
11. **Jala-Lieder** (§12): ein Lied pro Raum (lautestes gewinnt), Mana-Takt 3/5 s, Trance-Bindung; danach Hinder-Lieder (Truth, Civility, …) und Spellbane.
12. **Illusion/Riija**: invisibility, fade, shadow form, anonymity, illusionary form, morph, apparition, reflection, evil twin, feign death, bait, dispel illusion, artifice, elusion – braucht Illusionsmonster, Overlays, Zeichenflags.
13. **Gilden/PK/Multicaster** (§11): shatterlock, hunt, bond, blood inheritance, shadow rift, summon bovine; dazu Outlaw/Safety (§14), Seance, Eavesdrop, Nodeburst (Manaknoten, Tageswechsel).
14. **Zuletzt/optional**: Boons (Questbelohnungen), DM-Befehle (Schule 7), Seduce (nur Zauberstab), Slitherbolt (nur Monster/Zauberstab).


---

## Gemeinsame Mechanik: Zauber-Mechanik im Original (KOD) – gemeinsame Basis aller Sprüche

Referenz für die C#-Neuimplementierung. Alle Angaben aus `kod/` (Stand Repo 2026-11-02).
Ganzzahl-Arithmetik wie in KOD (Division schneidet ab), `random(a,b)` inklusive beider Grenzen,
`bound(x,lo,hi)` = Clamp (`$` = keine Grenze).

Pfadkürzel:
- **S** = `kod/object/passive/spell.kod`
- **P** = `kod/object/active/holder/nomoveon/battler/player.kod`
- **U** = `kod/object/active/holder/nomoveon/battler/player/user.kod`
- **B** = `kod/object/active/holder/nomoveon/battler.kod`
- **M** = `kod/object/active/holder/nomoveon/battler/monster.kod`
- **R** = `kod/object/active/holder/room.kod`
- **SP/** = `kod/object/passive/spell/` (z. B. SP/persench.kod)
- **K** = `kod/include/blakston.khd`

Wichtige Konstanten (K, wenn nicht anders angegeben):

| Konstante | Wert | Ort |
|---|---|---|
| SPELLPOWER_MINIMUM / MAXIMUM | 1 / 99 | K:2094-2095 |
| SS_SHALILLE..SS_DM_COMMAND | 1 Shal'ille, 2 Qor, 3 Kraanan, 4 Faren, 5 Riija, 6 Jala, 7 DM | K:2035-2041 |
| SPELL_MURMUR_SQUARED | 64 (= 8 Felder Radius) | S:18 |
| SOFTCAP_PENALTY | 4 | S:21 |
| CASTS_PER_PERCENT_BONUS | 7 | S:25 |
| MAX/NO/MIN_RESISTANCE | 100 / 0 / -100 | K:1526-1528 |
| MAX_DAMAGE_PER_HIT (Spieler) | 30 | P:113 |
| MAX_HEALTH_DAMAGE_FRACTION | 3 | P:109 |
| MIN_VIGOR_CHANGE | 20000 | P:54 |
| ADVANCEMENT_LIMIT | 10 Steigerungen | P:68 |
| ADVANCE_TIMER_MIN/MAX | 900000 / 1320000 ms (15–22 min) | P:66-67 |
| POINTS_SLOPE / MIN_NEEDED_TO_ADVANCE | 7 / 75 | P:93, P:97 |
| ATTACKED_PLAYER_WAIT | 15*60 s | P:105 |
| RANGE_MOVEMENT_BONUS | 1 | K:1736 |
| SPELL_INDEFINITE_DURATION | 0 | K:2028 |
| CANNOT_IMPROVE | -100000 | K:1211 |
| FLASH_NONE/GOOD/BAD/GOOD_SELF | 0/1/2/3 | K:2178-2181 |
| EVENT_CAST/STEER/NEWOWNER/RUN/DAMAGE/ATTACK/USE/REST/DISRUPT | 1..9 | K:2218-2235 |
| ATCK_SPELL_ALL/FIRE/SHOCK/COLD/HOLY/UNHOLY/ACID/QUAKE/HUNTERSWORD | 0x1,0x2,0x4,0x8,0x10,0x20,0x40,0x80,0x100 | K:1776-1788 |
| Settings: MaxLearnPoints | 16 | util/settings.kod:71 |
| Settings: AdvancementRate | 100 (%) | util/settings.kod:64 |
| Settings: PlayerSummonedObjectLimit | 18 | util/settings.kod:122 |
| Settings: CanRecastSelfEnchantment / CanRecastJalaWithNecklace | TRUE / TRUE | util/settings.kod:80,84 |
| Level-Lernpunkte (Stufe 1..6) | [1,2,4,6,8,10] | util/system.kod:414 |

Spell-Klassenvariablen-Defaults (S:145-204): `viSpellExertion=2`, `vbCheck_Exertion=TRUE`,
`viSpell_level=1`, `viMana=1`, `viChance_To_Increase=20`, `viCast_time=0` (ms),
`viPostCast_time=1` (s), `viOutlaw=FALSE`, `viHarmful=FALSE`, `viNoNewbieOffense=FALSE`,
`viFlash=FLASH_NONE`, `vbCastable_in_HappyLand=TRUE`, `piMinHitPoints=0` (S:225).

---

### 1 Zauber-Pipeline (Ablauf mit Prüfreihenfolge)

#### 1.1 Client → Server
`BP_REQ_CAST` [spell, targets] → `UserCast(oSpell,lTargets)` (U:1247-1257, U:4792-4864):

1. Monster aufwecken, falls Spieler seit Raumeintritt noch nichts getan hat (U:4798-4801).
2. Ist Spieler in Trance (`PFLAG_TRANCE`): `BreakTrance(EVENT_CAST, castwhat=oSpell)` (U:4803-4806) – bricht z. B. laufende Trance/Jala-Lied ab.
3. `PFLAG_NO_MAGIC` → „cannot cast“ (U:4808-4813).
4. Spruch muss in `plSpells` sein, sonst stumm abbrechen (U:4815-4833).
5. `lFinalTargets = oSpell.GetTargets(who,lTargets)` – Standard: unverändert (S:981-986); Radius-/Raumzauber ersetzen hier die Zielliste (§10).
6. `Room.ReqSpellCast` (R:1341-1366): `ROOM_NO_MAGIC` → Abbruch; `ROOM_NO_MOB_COMBAT` & Monster → Abbruch; (harmful ODER outlaw-cast) & `ROOM_NO_COMBAT` → „no attack“.
7. `iSpellPower = oSpell.GetSpellPower(who)` (§2) – wird **einmal vor** der Trance berechnet.
8. `CanPayCosts(who,lTargets,iSpellPower)` (§1.2).
9. `PayCosts(...)` (§1.3) – enthält den **Erfolgswurf**; bei Misserfolg Ende.
10. `BeginCastingTrance(...)` (§1.4).

#### 1.2 Spell.CanPayCosts (S:705-939) – exakte Reihenfolge
Nicht-Spieler (Monster) → sofort TRUE (S:710-713). Unterklassen prüfen vorher ihre eigenen Dinge und `propagate`n.

1. `pbEnabled` sonst „unseen force rips the words…“ (S:715-720).
2. `IsAccessible` (HappyLand: `pbAccessible = vbCastable_in_HappyLand` bei `SERVER_NO_MURDER`, S:237-249) – nicht-DM → FALSE (S:722-728).
3. **Min-HP**: `who.GetLevel() < piMinHitPoints` (GetLevel des Spielers = Max-HP) → „not powerful enough“ (S:730-734).
4. Nur bei Spieler-Cast (nicht Item): **Rasten** → „can't cast while resting“ (S:739-744); **Forget**-Verzauberung auf diesem Spruch → eine von 4 Zufallsmeldungen (S:746-757, P:7038-7064).
5. **Post-Cast-Sperre**: `IsOkayAttackTime(seconds=viPostCast_time)` (S:760-763). P:5274-5300: Ist `ptAttackTimer` aktiv → FALSE (ohne Meldung); sonst Timer `viPostCast_time*1000` ms starten und TRUE. **Achtung:** Die Sperre wird hier schon gesetzt, auch wenn spätere Prüfungen fehlschlagen. Gilt gemeinsam für Nahkampf und Zauber.
6. Opfer = einziges Ziel, falls genau 1 Ziel (S:767-773).
7. **Token**: Spieler trägt ein `&Token` und Ziel ist nicht er selbst/keins → „hands too full“ (S:776-783).
8. Nur Nicht-Item: `piCast_attempts++` (Statistik) (S:787-796); dann **`CanPayManaVigor`** (S:597-624): Immortal → OK; `HasVigor(viSpellExertion)` (P:1346: `piVigor > amount`, strikt größer) wenn `vbCheck_Exertion` → „too tired“; `Mana < GetManaCost` → Sound `spelfail.wav` + „not enough mana“. Danach **`CanPayReagents`** (S:626-703): für jedes `[Klasse, Anzahl]` in `plReagents` aktive+passive Inventarliste durchzählen (NumberItem zählt Menge, sonst je Objekt 1); fehlt etwas: Reagenzien-Ring (`&ReagentRing`) mit `UseCharges(GetRingCharges)` (1, Bond 3; S:2443-2454) → **gesamte Reagenzprüfung sofort OK**; sonst Fehlersound + „don't have the reagents“.
9. **Karma** (`bCheckKarma`, Default TRUE): `KarmaCheck` (S:456-480) – Shal'ille braucht Karma ≥ `Level*10`, Qor Karma ≤ `-Level*10` (S:482-498); andere Schulen keine Bedingung; Immortal immer OK. Meldung Shal'ille „not worthy“, sonst „not vile enough“ (S:817-831).
10. **Reichweite** jedes Ziels: `IsTargetInRange` (S:959-979) → Spieler: `TargetWithinSightAndRange(oTarget, stroke_obj=spell)` (P:4088-4194): Ziel = self oder vom Caster getragen → OK; wird das Ziel von einem Battler getragen, zählt der Träger; anderer Raum → FALSE; **ohne iRange keine Distanzgrenze** (Basis-Spell übergibt keinen Range!); bei Einzelziel-Zaubern (`GetNumSpellTargets()=1`) und Battler-Ziel: Sichtkegel-Prüfung – Ziel darf (bei Distanz² > 1) nicht „hinter“ dem Caster liegen (Halbebene senkrecht zur Blickrichtung, 8 Sektoren, P:4140-4190) → „not in view“. Monster-Caster: nur gleicher Raum.
11. **Schutzengel/Newbie** (S:844-885): wenn Caster *nicht* `PFLAG_PKILL_ENABLE` und Raum weder `ROOM_SAFE_DEATH` noch `ROOM_KILL_ZONE`:
    - kein Einzelopfer & `viNoNewbieOffense` & ein anderer Spieler im Raum → „guardian angel: not ready“.
    - Einzelopfer = anderer Spieler: `viNoNewbieOffense` → dieselbe Meldung; `NOT oVictim.CanHelpPlayer()` → „has attacked someone too recently“. `CanHelpPlayer` (P:3923-3933): FALSE, wenn das Ziel innerhalb der letzten 15 min einen Spieler angegriffen hat (außer Chaosnacht).
12. **CanCastHere** (S:505-595): DM → OK; `ROOM_NO_COMBAT` & harmful → FALSE; nicht-Item: **Spellbane** im Raum und (`Jala-Spellpower > iSpellPower` ODER Caster ist schweigbares Monster) → Meldung „notes of X's song tangle…“, FALSE (S:523-545); **Silence** im Raum → Spieler: „words catch in your throat“, FALSE; schweigbares Monster FALSE (S:547-561); `ROOM_GUILD_PK_ONLY`: nicht-harmful auf anderen Spieler, wenn weder `AllowGuildAttack` noch `CanHelpPlayer` → FALSE (S:565-592).
13. **Safety/Outlaw**: harmful → `CheckPlayerAttack` (S:988-1041): nur wenn `viOutlaw`: ohne Ziele alle Battler im Raum (außer self/immortal DM) müssen per `ReqSomethingAttack` und `AllowPlayerAttack` angreifbar sein; mit Zielen: `GetAttackTargets` (S:1090-1135) filtert (self bei Nicht-Einzelziel, Nicht-Battler, Monster mit `NOT CanMonsterFight`, immortal DM, anderer Raum, Arena-ungültig, `NOT AllowPlayerAttack`), leer → „no proper targets“ (nur wenn nicht Einzelziel-Zauber). Nicht harmful → `CheckPlayerHelp` (S:1043-1088): Ziel ist Outlaw/Mörder (und nicht self): Caster ohne PK-Flag → „angel no unlawful“; Caster mit Safety → „safety: would brand you outlaw“.
14. **SpellResist** des ersten Ziels (Battler.SpellResist, Default FALSE B:184-187; einige Monster überschreiben): resistiert → bei Spieler-Cast `PayCosts` wird trotzdem ausgeführt (inkl. Erfolgswurf und Kosten!), Meldung „resists your spell“ (S:913-930). (Bug im Original: Meldung nutzt Schleifenvariable `i` statt Ziel.)
15. Erfolg: `FlipSpellAtrophyFlag` (Spruch gilt als benutzt, kein Verfall) (S:933-936, P:7066-7084).

`CanPayMonsterCosts` (S:941-957) = CanPayCosts + CanCastHere ohne Meldungen.

#### 1.3 PayCosts (S:1256-1338)
Nur Spieler. Erst `SuccessChance` (§3.1). Misserfolg → `SpellFailed` (S:1152-1172): Sound, **halbe Manakosten** (`GetManaCost/2`), **halbe Erschöpfung** (`10000*viSpellExertion/2`), keine Reagenzien, Meldung „You were unsuccessful in casting X.“; Rückgabe FALSE.
Erfolg (nicht Immortal): `LoseMana(GetManaCost)`, `AddExertion(10000*viSpellExertion)`, Reagenzien verbrauchen (NumberItem: `SubtractNumber(benötigt)`, sonst Objekte löschen) (S:1276-1335).

**Manakosten** `GetManaCost` (S:1544-1582): `c = viMana`; `c=0` → 0; SP > 40 → −1; SP > 80 → weiteres −1; Princess-Schild: `c -= ceil(c*Reduktion/100)`; Ergebnis `bound(c,1,$)`.

**Erschöpfung → Ausdauer** (P:1243-1290): `piExertion += amount` (minus Fraktionsrabatt %); sobald `|piExertion| > 20000`: `piVigor -= piExertion/10000`, Rest bleibt. D. h. `viSpellExertion` ≈ Vigor-Punkte pro Zauber.

**Wichtig:** Kosten und Erfolgswurf passieren **vor** der Trance. Bricht die Trance, sind Mana/Reagenzien weg.

#### 1.4 BeginCastingTrance (S:1818-1881)
1. `iCastTime = GetTranceTime(iSpellPower)` (S:1883-1902): `viCast_time=0` oder immortal DM → 0; sonst `viCast_time * (150 − SP) / 100` (SP 99 → 51 %, SP 1 → 149 %).
2. **Murmeln**: an alle Spieler im Raum mit `SquaredDistanceTo < 64` (also < 8 Felder), außer Caster: `SpellMurmurMessage` (S:1909-1948) – Mehr-/Kein-Ziel-Zauber: „X murmurs something under his breath and makes a mystical gesture.“; Einzelziel: „… and points a finger at Y/you.“ Versteckte Admins senden nichts. Multicaster unterdrücken das (SP/multicst.kod:85-88).
3. **Sofortzauber** (`iCastTime=0`): `GetGoodTargets` (S:1137-1150 = GetTargets + GetAttackTargets) → erstes Ziel Battler und `TryDeflect` (§14) → Spruch trifft **den Caster selbst mit halber Spellpower**; sonst `CastSpell(who, lFinalTargets, iSpellPower)`.
4. **Mit Castzeit**: `DoPreTranceEffects`; Meldung „You focus your whole will on casting X.“; `Trance.BeginCastingTrance(time=iCastTime)` (§1.5).

#### 1.5 Trance (kod/object/passive/trance.kod)
- `BeginCastingTrance` (trance.kod:34-49): Warp-Time im Raum → Zeit gestreckt (`GetStretchedTime`); `who.StartEnchantment(Trance, time, addicon=FALSE, state=[spell,who,lTargets,TRUE])`. `SetSpellPlayerFlag` setzt `PFLAG_TRANCE` (trance.kod:51-56; Flag wird über `ResetPlayerFlagList` aus Verzauberungen abgeleitet).
- **Ablauf** `EndEnchantment` (trance.kod:58-94): Trance-Flag löschen; Ziele neu filtern (`GetGoodTargets`, report=FALSE); **Spellpower neu berechnen**; `DoubleCheckAfterTrance` (S:1791-1816: rastet → Abbruch; harmful & Spieler & `NOT CheckPlayerAttack` oder `NOT Room.ReqSpellCast` → Abbruch; nicht harmful & `NOT CheckPlayerHelp` → Abbruch). Dann Deflect-Prüfung wie oben, sonst `CastSpell`.
- **Abbruch** `BreakTrance(state,event)` (trance.kod:96-112): Flag löschen; außer bei `EVENT_STEER`: `spell.TranceBroken` → „Your concentration is broken and the X spell fizzles.“ + spelfail.wav (S:2308-2315); Verzauberung entfernen (kein CastSpell). `RemoveEnchantmentEffects` wird verschluckt (trance.kod:114-119).
- **Auslöser** (`Player.BreakTrance`, P:10038-10082 → schickt BreakTrance an alle Spell/Trance-Verzauberungen und an den Raum (Jala, Silence)):
  - neuer Zauber/Gildenbefehl `EVENT_CAST` (U:4805, U:4878)
  - Laufen ohne Vigor/Bewegung `EVENT_RUN` (U:2972)
  - Schaden `EVENT_DAMAGE` (P:5727 in LoseHealth, außer Decay)
  - Angriff `EVENT_ATTACK` (U:4710)
  - Gegenstand benutzen `EVENT_USE` (U:4655, U:4679, U:5900)
  - Rasten `EVENT_REST` (P:9845)
  - Raumwechsel `EVENT_NEWOWNER` (P:1447, R:1955) – auch Yell-Zonen-Räume werden benachrichtigt.

#### 1.6 Spell.CastSpell (gemeinsamer Nachlauf, S:1340-1484)
Unterklassen führen zuerst ihren Effekt aus und `propagate`n hierher (Ausnahme: AttackSpell, §6).
1. Statistik `piAverage_spellpower`, `piCast_successes` (nur Spieler-Cast).
2. `DoCast` (Hand-Overlay + 3rd-Person-Animation PANM_CAST, P:8385-8395).
3. Flash (§14).
4. `ImproveAbility(who, target=erstes Ziel)` (§3.2), nur Spieler-Cast.
5. Raum: versteckter DM → nur Debug-Log, **return** (kein Sound). Sonst `PlaySpellSound` (Schul-Sound `shalille/qor/kraanan/faren/riija.wav` oder `vrSucceed_wav`, S:1486-1542; Jala: kein WAV) und `Room.SpellCast` (R:1368-1397: Shal'ille-/Qor-Bane im Raum verletzt den Caster).
6. `AffectsSpellPower` → Raumflag `ROOM_SPELL_POWER_MODIFIED` setzen (S:1430-1433).
7. Spieler: `AddSchoolCast(school, amount=viSpell_Level)` (§3.2); falls nicht schon Outlaw/Mörder: `viOutlaw AND NOT viHarmful` → sofort Outlaw („labeled an outlaw for casting an antisocial spell“); nicht-harmful (kein DMSpell) auf Outlaw/Mörder-Spieler → Outlaw („Shal'ille frowns…“) (S:1435-1479).

---

### 2 Spellpower-Berechnung (S:2066-2249)

Rückgabe 1..99. Sonderfälle: `who=$` → 1; immortal DM → 99; **Monster → 99** (S:2074-2092).

```
base     = SpellAbility(spell)/2                         (0..49)          S:2095
items    = Σ item.GetSpellModifier(spell) über GetPlayerUsing (inkl. ItemAtts)  S:2099-2104
primary, secondary je Schule (s. u.)
power    = Parlament-Fraktionsbonus (GetFactionSpellPowerBonus)          S:2200
primary  = bound(primary,0,30); secondary = bound(secondary,0,10)          S:2204-2205
sp = primary + secondary + power + base                                    S:2207
wenn Raumflag ROOM_SPELL_POWER_MODIFIED:
    sp = sp - AntimagicAura.ModifySpellPower + ManaConvergence.ModifySpellPower   S:2209-2218
sp = bound(sp,0,$)                                                         S:2221
sp = sp + ShrineBonus(school) + items                                      S:2224-2227
sp = sp + System.GetBonusSpellpower(school)  (Admin, Default 0)            S:2230
Dementia: loss = state[1]; sp -= random(loss, 2*loss)                       S:2233-2239
sp = sp + sp*PersonalModifier(flagset 3)/100                                S:2242-2243
sp = bound(sp,1,99)                                                         S:2246
```
- **ShrineBonus** (util/system.kod:2827-2840): ohne Schreinliste generisch **5**, sonst `plShrine_powers[school]`.
- Kraanan: negativer Item-Modifikator wird halbiert (S:2182-2185).

Schul-Boni (S:2110-2198):

| Schule | primary (0..30) | secondary (0..10) |
|---|---|---|
| Shal'ille | Raum `GetShalilleBonus` (R:3506-3568): Wald/Dschungel 25, Berg/Sumpf 20, Ödland/Hügel 15, Tundra/Felder 10, Wüste/Strand 5, Nekropole/sonst 0 (erste zutreffende Terrain-Flag) | `|Karma/10|`; + Forces-of-Light-Zustand/10 |
| Qor | `2 + |Stunde−12|*2` (Mitternacht 26, Mittag 2) | `|Karma/10|`; + Darkness: `(−(state+25))/10` |
| Riija | Mond-Overlayhöhe/12 (−16..30, geclamped) | `Bulk_gehalten*10/Bulk_max` |
| Faren | Raum `piFaren_bonus` (R:3577-3665): Lava 20, Strand 20+5, Wasserfall 15, Sumpf 15, See/Fluss/Kanal je 10, Brunnen 5, Ödland/Hügel 15, Tundra/Felder 10, Wüste 5, +1 je Feuerobjekt im Raum; Summe bound 0..30 | `Vigor/20` |
| Kraanan | Anzahl aktiver Objekte im Raum | `HP*10/MaxHP` |
| Jala | `GetInstrumentLevel` (höchstes benutztes Instrument, P:11502-11524) | `MaxHP/12` |

(Kommentar sagt Jala bis 40, Code clamped aber ebenfalls auf 30.)

**Erstfähigkeit beim Lernen** `InitialAbility` (S:446-454): `bound(random(R/2,R)/2, 1, 25)` mit R = Requisit-Stat.

**Requisit-Stat** `GetDivisionReq` (S:392-418): Qor/Shal'ille/Faren → **Mystik**; Kraanan → **Ausdauer (Stamina)**; Riija/Jala → **Intellekt**; sonst Mystik. (Lern-Division: Kraanan COMBAT, Jala/Riija LORE, sonst MAGIC – S:376-390.)

---

### 3 Erfolgschance & Fertigkeitssteigerung

#### 3.1 SuccessChance (S:1174-1254)
```
R   = Requisit-Stat
num = (100 − R) * SP / 100 + R                                       S:1194
Hinder-Lied (Jala) im Raum, gleiche Schule wie gehinderte:
    num -= random(JalaSP/2, JalaSP*2/3)        (SP/jala/hinder.kod:43-56)
num = bound(num, 5, 95)                                              S:1212
Einzelziel-Zauber auf Battler OHNE Sichtlinie (nicht immortal DM):    S:1214-1238
    d = SquaredDistanceTo(Ziel)/4
    d > num/2 → num = num/2 ;  d < num/2 → num = num − d   (d = num/2 → unverändert)
Erfolg wenn random(1,100) ≤ num; sonst Reagenzien-Ring „InUse“ → doch Erfolg.
```
Immortal → immer Erfolg. DMSpell/Boon → immer Erfolg (SP/dmspell.kod:67-71, SP/boon.kod:204-208). Beispiel: R=50, SP=60 → 50*60/100+50 = 80 %.

#### 3.2 ImproveAbility (S:1604-1691)
Nur Spieler. Abbruch (FALSE) wenn: Ziel `NOT CanPlayerAdvanceOnMe` (Revenants, Illusionen); Spruch nicht bekannt; `CheckAdvancementPoints` FALSE (≥ 10 Steigerungen im aktuellen Fenster, P:7522-7551); Raum `ROOM_SAFE_DEATH` oder Arena ohne echten Tod.

1. **Erster Wurf**: `random(1,100) ≤ InitialChance` (S:1648-1653) mit
   `GetInitialChance` (S:1693-1709): `bonus = vC*Int/100 + Fraktions-Lernbonus`; `fin = vC + bonus`; `fin += fin*PersonalMod/100` (vC = `viChance_To_Increase`).
2. Fähigkeit ≥ 99 oder < 1 → FALSE.
3. **Zweite Chance** `GetSecondaryChance` (S:1711-1765):
   ```
   chance = 60 + R − Spellstufe*10 − GetTotalLearnPoints(except = eigene Schule)
   difficulty = 75; Ziel ist Monster → bound(Monsterlevel,1,150)
   factor = bound(2*difficulty − Ability + 10, 50, 100)
   final  = bound(chance*factor/100, 5, $)
   final += (SchoolCast[school] / Spellstufe) / 7          (CASTS_PER_PERCENT_BONUS)
   Ability > 2*R − 1  → final = final / 4                  (SOFTCAP_PENALTY)
   final  = bound(final,1,$)
   final  = final * AdvancementRate(100) / 100
   final += final*PersonalMod/100
   ```
4. Raum `ModifyChanceToImprove` (R:1399-1410): `ROOM_HARD_LEARN` → /10; `CANNOT_IMPROVE` → FALSE.
5. `chance = bound(chance, 1 + R/10, 99)`; Erfolg wenn `random(0,100) < chance` (S:1676-1680) → `ChangeSpellAbility(+1, report)` und `ResetSchoolCast(school)`.

**Schul-Casts** (P:6159-6226): `plSchoolCasts` (11 Einträge). Jeder erfolgreiche Cast addiert `viSpell_Level` (S:1441), halbiert wenn Steigerungs-Cap erreicht (P:6177-6181); Reset bei Steigerung in der Schule.

**ChangeSpellAbility** (P:6627-6700): neue Fähigkeit `bound(alt+amount,1,99)`; Meldung „You advanced in X!“ + Sound; `AddAdvancementPoints(amount)` (Timer 15–22 min, danach Reset; P:7507-7520); `AddToSchools`.

**Kodierung** `plSpells` (P:6591-6609): `num*100 + ability`, negativ = seit letztem Check nicht benutzt (Verfall-Flag).
**Verfall** `ActivateSpellAtrophy` (P:7086-7122, beim Advancement-Timer): `highmark = AP + (50 − bound(Int,1,50))*AP/20`; für jeden unbenutzten Spruch mit `random(1,200) < highmark` und Ability > 2: −2, bzw. −1 wenn ein anderer Spruch derselben Schule benutzt wurde.

`viChance_To_Increase` Defaults: Spell 20, AttackSpell 10, BoltSpell 7, TouchAttack 10, Jala 8, Hinder 10, Multicaster 30.

---

### 4 Lernen von Zaubern

#### 4.1 AddSpell (P:6852-6949)
- Bereits bekannt: nur höhere Ability übernehmen.
- **Prerequisites** (`plPrerequisites`, Liste von Spell-IDs) müssen alle bekannt sein (P:6889-6898).
- Ability `bound(iAbility,0,99)`, 0 → `InitialAbility` (§2).
- Nicht DM/Initial: `AddToSchools`, `AddAdvancementPoints(2)`.

#### 4.2 PlayerCanLearn (P:10359-10802) – Prüfung beim Lehrer
Rückgabe `PLAYER_LEARN_*` (K:2076-2085: 0 SUCCESS, 1 ERROR, 2 ALREADY, 3 KARMA, 5 BASE, 8 IMPOSSIBLE, 9 NOBASE) bzw. `10*(fehlende Punkte)+5`.
1. Spruch existiert, accessible, enabled; bereits bekannt → ALREADY.
2. Kennt Spieler in dieser Schule einen Spruch **der nächsthöheren Stufe** → SUCCESS (P:10465-10469).
3. Kennt er auf **gleicher Stufe** schon welche: Stufe > 2 oder ≥ 2 bekannt → SUCCESS; sonst `bKnowOne` (P:10472-10487).
4. `iHave` = Summe der **3 höchsten Fähigkeiten** (Sprüche+Skills) der Schule auf Stufe−1; Stufe 1: 297 (P:10498-10635).
5. **Karma** (`KarmaCheck`) → KARMA (P:10641-10650). Das ist die einzige Schul-Exklusivität: Shal'ille (Karma ≥ +10·Stufe) und Qor (≤ −10·Stufe) schließen sich über das Karma faktisch aus; kein explizites Verbot.
6. `iHave = 0` → NOBASE.
7. **Generalisierungs-Punkte**: `iPoints = Σ LevelPoints(höchste Stufe je Schule)` über Kraanan, Shal'ille, Qor, Faren, Riija, Jala und Waffen-Skills (Blink zählt nicht, DM-Sprüche nicht, Stufe >10 ignoriert). LevelPoints[1..6] = 1,2,4,6,8,10. Wird eine neue Stufe betreten (`NOT bKnowOne`): `+LP(stufe) − LP(stufe−1)` (P:10661-10676).
8. ```
   iNeed = iPoints*7 + (297 − 16*7) − (RawInt*2*7)/5      = iPoints*7 + 185 − RawInt*14/5
   iNeed = bound(iNeed, 75, $)
   Vorstufe hat nur 1 Spruch → iNeed/3 ; nur 2 → iNeed*2/3
   iHave ≥ iNeed → SUCCESS
   iNeed > 99*bound(Vorstufenanzahl,1,3) → IMPOSSIBLE
   sonst 10*(iNeed−iHave)+5
   ```
   (P:10685-10801)
- `GetTotalLearnPoints(except)` (P:10804-10892): dieselbe Punktsumme ohne die angegebene Schule – fließt als Generalisierungs-Malus in die Steigerungschance (§3.2).
- `OfferToNewCharacters` (S:2263-2286): Stufe ≤ 2 und Schule Shal'ille/Qor/Kraanan/Faren.
- Verkaufswert `GetValue` = `250 * 2^Stufe` (S:350-364).

---

### 5 Persönliche Verzauberungen (SP/persench.kod)

Klassenvariablen: `vbCanCastOnOthers=TRUE`, `vbCanCastOnMonsters=FALSE`; `GetNumSpellTargets` = 1 bzw. 0 (persench.kod:62-74).

**CanPayCosts** (persench.kod:76-149): Ziel default = Caster; genau 1 Ziel; bei castbar-auf-andere und Nicht-Item: Ziel muss User sein (oder Monster wenn erlaubt) → „You cannot enchant X“; auf andere in permanent `ROOM_NO_COMBAT`-Räumen verboten. **Stapelung**: Ziel schon verzaubert → bei Selbstcast und `CanRecastSelfEnchantment` (TRUE) erlaubt (wird ersetzt); sonst Meldung („already enchanted“ bzw. an Caster und Ziel), FALSE. **Keine Stapelung** derselben Verzauberung.

**CastSpell** (persench.kod:151-197): Ziel ausgeloggt → Abbruch; vorhandene Verzauberung entfernen (Recast); Meldung `vrEnchantment_On` an Ziel;
`target.StartEnchantment(what=self, state=InitializeEnchantmentState(SP,who,target), time=GetDuration(SP), lastcall=GetLastCall()=TRUE, addicon=GetAddIcon()=TRUE)`; an Caster `vrSuccess` falls fremd; propagate (§1.6).
**GetDuration** hat **keinen Default** in Spell/PersonalEnchantment – jede Unterklasse definiert sie (sonst `$` → unbegrenzt). Typisches Muster: `random(d/2, d)` mit d in ms.
**EndEnchantment** (persench.kod:199-207): Meldung `vrEnchantment_Off`. Hooks für Modifikatoren: `ModifyDamage`, `ModifyHitRoll`, `DefendingHit`, `ModifyDefenseDamage`, `ModifyDefensePower` (persench.kod:229-252; Aufruf über `plAttack_modifiers`/`plDefense_modifiers` im Player).

**Buchführung beim Spieler** (P:2665-3058) – `plEnchantments` = Liste `[timer, spell, state?]`, wird mit dem Spieler gespeichert:
- `StartEnchantment(what,time,state,lastcall,addicon)` (P:2730-2769): `time=$` oder 0 → kein Timer (unbegrenzt); `lastcall` → `EnchantmentTimer`, sonst `PeriodicEnchantmentTimer`. Neuer Eintrag vorn; `ResetPlayerFlagList`; Icon an Client (`BP_ADD_ENCHANTMENT`, type=ENCHANTMENT_PLAYER=1, U:7004-7018), wenn `ShowEnchantmentIcon` (Bitmaske `viShow_enchantment_icon`: Bit 0x01 Raum, 0x02 Spieler; S:1774-1789).
- `EnchantmentTimer` (P:2665-2700): `spell.EndEnchantment(who,state)`, Icon entfernen, Eintrag löschen, Flags neu, `SomethingChanged`.
- `PeriodicEnchantmentTimer` (P:2702-2728): Eintrag löschen und `spell.StartPeriodicEnchantment(who,state)` – der Spruch muss sich selbst neu starten (z. B. Mana-Drain).
- `RemoveEnchantment(what)` (P:2988-3023): Timer löschen, `RemoveEnchantmentEffects` (Default → `EndEnchantment`, S:1957-1962), Icon weg. `RemoveEnchantmentClass` (P:3025-3058), `RemoveAllPersonalEnchantments` (ohne DM-Sprüche, P:2852-2876), `RemoveSchoolEnchantments`, `DispelIllusionEnchantments` (Riija = Illusion, S:2293-2301).
- `IsEnchanted(what | byClass)`, `GetEnchantedState(what)` (P:2793-2837).
- **Logout** `FreezeAllEnchantments` (P:2878-2904): Restzeit speichern, Timer löschen; **schädliche Verzauberungen +3000 ms**. **Login** `ReactivateAllEnchantments` (P:2906-2942): Timer neu (periodisch, falls `IsPeriodic`), `RestartEnchantmentEffect` (z. B. Blind/Hold clientseitig neu senden).

**Resistenz-Verzauberungen** (SP/persench/resist.kod): `viResistanceType = -ATCK_SPELL_ALL` (negativ = Zauberart, resist.kod:24), Flash GOOD_SELF. Stärke `SP/2 + 1` (1..50) (resist.kod:64-68) → `AddResistance(type, value)`; Dauer `d = (300 + SP*3)*1000 ms; random(d/2, d)` (resist.kod:70-78); State = Stärke, wird bei Ende wieder abgezogen (resist.kod:80-93). Unterklassen setzen den Typ (Feuer, Kälte …).

---

### 6 Angriffszauber (SP/atakspel.kod, SP/atakspel/boltspel.kod)

Defaults: `viPostCast_time=2`, `viHarmful=TRUE`, `viOutlaw=TRUE`, `viChance_To_Increase=10`, `piRange=$` (unbegrenzt), `piManaFocusBonus=5` (Bolt 10), `pbAbsolute=FALSE`, `viAttack_spell=ATCK_SPELL_ALL`, `viAttack_type=0` (atakspel.kod:27-48).

**CanPayCosts** (atakspel.kod:52-109): genau 1 Ziel; nicht auf sich selbst (außer Item); Ziel muss Battler sein; mit `piRange`: `range = piRange (+1 wenn Ziel ein Spieler, der sich in den letzten 2 s bewegt hat, U:7793-7805)`; `SquaredDistanceTo > range²` → „X is too far away to hit with Y“.

**Kein Trefferwurf**: Ein Angriffszauber trifft immer, wenn der Cast gelingt (nur `SuccessChance` §3.1, Deflect §14, SpellResist §1.2).

**CastSpell** (atakspel.kod:116-182, **ohne propagate** – Spell.CastSpell läuft nicht):
```
Sound + Room.SpellCast
dmg = random(piDamageMin, piDamageMax)
Spieler: SetKillTarget, PFLAG_DID_DAMAGE
  nicht Item: piCast_successes++, DoCast
     dmg = dmg * (50 + SP/2) / 99            (SP 99 → ~100 %, SP 1 → ~50 %)
     Mana Focus aktiv: dmg += ManaFocusSP*piManaFocusBonus/99 + 1
dmg = target.AssessDamage(what=who, damage=dmg, atype=viAttack_type,
                          aspell=GetAttackSpell(), absolute=pbAbsolute)
who.AssessHit(target, dmg, use_weapon=self)
dmg = $ → KilledSomething
Room.SomethingAttacked; DoSideEffect(who,victim,dmg); ImproveAbility (nicht Item)
```
Keine Flash/Schulcast-Buchung über Spell.CastSpell (AddSchoolCast entfällt bei Angriffszaubern!).

**Schaden beim Spieler** `AssessDamage` (P:4518-4636):
1. nicht `absolute`: alle `plDefense_modifiers.ModifyDefenseDamage` + `DefendingHit`; Resistenz (unten); `+bonus`.
2. Schmerz-Effekt 800/600/400 ms (dmg >6 / ≤6 / ≤3).
3. nicht absolute: mindestens 1; **Cap** `ceil(BaseMaxHP/3)` solange HP < 2·BaseMaxHP und Ziel kein Outlaw/Mörder (oder Setting); **max. 30 pro Treffer**.
4. Fraktionsfeinde +15 %. `LoseHealth`; Tod → `$`.

**Monster** `AssessDamage` (M:1532-1614): NPC (AI_NPC) → 0; Resistenz wie unten (nicht absolute); `+bonus`; min 1; kein 30er-Cap.

**Resistenzen** (B:189-313): `plResistances` = Liste `[wert, typ]`; typ > 0 = Waffenart-Bitmaske (ATCK_WEAP_*), typ < 0 = −Zauberart-Bitmaske.
`ResistanceCheck(atype, aspell)`: alle passenden Einträge (Bit-Schnitt ≠ 0, oder `*_ALL` passt auf jede Art ≠ 0); `maxRes` = größter (Start 0, Cap 100), `minRes` = kleinster (Start 0, Floor −100); Ergebnis `maxRes + minRes`.
`GetDamageFromResistance`: Wert > 0 → `dmg*(100−v)/100`; sonst `dmg*(−100+v)/(−100)` = `dmg*(100−v)/100` (Schwäche erhöht Schaden).
`AddResistance` addiert auf gleichen Typ, Eintrag fällt bei 0 weg.

**Projektil / BoltSpell** (boltspel.kod:47-92): vor dem Schaden `Room.SomethingShot(who, target, projectile=self, flags=PROJ_FLAG_LIGHT_SOURCE)` – rein visuell (Icon `vrProjectile_Icon`, Geschwindigkeit 5, Licht ON|DYNAMIC, Intensität 100, Farbe LIGHT_LIGHTNING). Schaden ist sofort, keine Flugzeit/Kollision. `vbIsRangedAttack=TRUE`.

---

### 7 Berührungsangriffe (SP/persench/touchatk.kod)

Persönliche Verzauberung des Casters, die den unbewaffneten Schlag ersetzt („combat stroke enchantment“).
- `vbCanCastOnOthers=FALSE`, `GetNumSpellTargets=0`, `viSpellExertion=2`, `viPostCast_time=1`, `viChance_To_Increase=10`, `viHit_Factor=80` (ungenutzt), `viMin/Max_Damage=3/6`, `viRange=2`, `viAttackType = ATCK_WEAP_UNARMED+ATCK_WEAP_PUNCH`, `viSpellType` (Zauberart, je Unterklasse) (touchatk.kod:61-90).
- **Cast** (touchatk.kod:111-118): alle anderen TouchAttack-Verzauberungen entfernen (nur eine aktiv), dann PersonalEnchantment.CastSpell. State = Spellpower (touchatk.kod:402-405).
- **Dauer** (touchatk.kod:390-400): `bound(random(SP/3, SP/2), 10, 75) * 6000 ms` → 60–450 s.
- **Als Waffe**: `Player.GetWeapon` (P:4675-4691): keine Nahkampfwaffe → erste TouchAttack-Verzauberung. Angriff läuft durch die normale Nahkampf-Pipeline (Trefferwurf B:319ff, s. combat-original.md):
  - `GetStroke` = Spruch-Fähigkeit (primär), `GetProf` = `max(Punch-Skill, Mystik*3/2)` (touchatk.kod:165-189) → Offense `stroke*3 + prof*2 + Aim*4 + BaseMaxHP*3/2` (P:4228).
  - Kann geblockt/pariert/ausgewichen werden; eigene Parade 0 (touchatk.kod:201-225).
  - Reichweite `GetRange = viRange = 2` (touchatk.kod:233-237).
  - **Schaden** `FindDamage` (touchatk.kod:246-264): `d = random(min,max); d = d/2 + (d/2)*SP/99 + 1`; `DamageFactors` (Default unverändert, keine Stärke); `bound(d,1,$)`. Danach Attack-Modifier und `AssessDamage(atype=viAttackType, aspell=viSpellType)` wie Nahkampf.
  - Steigerung über `ImproveStroke` → `ImproveAbility` (touchatk.kod:99-104), ausgelöst in `AssessHit` nur bei Swing-Bonus > 0 (P:4496-4503).
  - Eigene Treffer-/Fehl-Meldungen/Sounds (touchatk.kod:266-386), Miss-Sound zufällig aus 3.

---

### 8 Raumverzauberungen (SP/roomench.kod, R)

Basisklasse nur: `DoubleCheckAfterTrance` (roomench.kod:34-58): Raum `ROOM_NO_COMBAT`/`ROOM_NO_MAGIC` und (outlaw oder harmful) → „can't cast here“; Raum bereits mit diesem Spruch verzaubert → „already in effect here“. Pro Spruch max. **eine** Instanz pro Raum (Unterklassen prüfen das auch in CanPayCosts, z. B. SP/roomench/light.kod:68-81).

Raum-Buchführung (R:2882-3126), `plEnchantments` = `[timer, spell, state]`:
- `RoomStartEnchantment(what,time,state,addicon,lastcall)` (R:3002-3033): time > 0 → Timer (`EnchantmentTimer` bzw. `PeriodicEnchantmentTimer`), sonst unbegrenzt; Icon an alle User (`ShowUsersAddEnchantment`).
- Ablauf `EnchantmentTimer` (R:2882-2909) → `spell.EndSpell(where=room,state)`, Icon weg. Periodisch (R:2911-2936) → Eintrag weg, `spell.StartPeriodicEnchantment(where,state)`.
- `RemoveEnchantment` (R:3100-3126) → Timer löschen, `EndSpell`.
- **Betreten** (R:1799-1833, nur User): für jede Raumverzauberung `spell.StartEnchantmentNewOccupant(who,state)` (kein Default in Spell → je Spruch implementieren, z. B. Meldung).
- **Verlassen** `LeaveHold` (R:1945-1961, nur User): `BreakTrance(EVENT_NEWOWNER)`; je Verzauberung `spell.EndRoomEnchantment(who,state)` (Default leer, S:1964-1967).
- `EnchantAllOccupants(what,SP)` (R:2938-2953): `what.StartEnchantment(who,SP)` für alle **Spieler** im Raum. `UnenchantAllOccupants` (R:2955-2969).
- Muster einer Unterklasse (Light, SP/roomench/light.kod:84-128): `RoomStartEnchantment(state=SP+25, time=GetDuration)`; Dauer `d=(45+SP*3)*1000; random(d/2,d)`; EndSpell nimmt Effekt zurück.
- `EndSpell` Basis (S:2332-2358): setzt `ROOM_SPELL_POWER_MODIFIED` zurück, wenn keine andere spellpower-verändernde Verzauberung mehr da ist.

---

### 9 Wände/Felder (SP/walspell.kod, kod/object/active/wallelem.kod)

**WallSpell** (walspell.kod): `viHarmful=TRUE` (nicht outlaw – die Elemente prüfen Safety selbst), `viNumElements=9`, `GetNumSpellTargets=0`.
- `CheckForSummoningLimit` in CanPayCosts **und** DoubleCheckAfterTrance (walspell.kod:47-101): `Raum.CountHoldingSummoned + viNumElements > 18` → „too much summoning magic“.
- `CastSpell` → `PlaceWallElements(who, SP)` (je Unterklasse), dann propagate.

**Platzierung (Beispiel Feuerwand, SP/walspell/firewall.kod:72-272)**: Stufe 4 Faren, Mana 15, Exertion 10, Castzeit 1000 ms, Reagenzien 3 Rote Pilze + 2 Feuersand.
- `maxDamage = bound(SP/6, 1, 16)`, `dauer = bound(SP*2 + 30, 30, 180)` s.
- Startpunkt und Schritt je Blickrichtung (8 Sektoren, Grenzwinkel ANGLE_ENE/ESE/…): z. B. Ost: Start (Reihe−2, Spalte+1), Schritt (Zeile+1, Spalte 0); Süd: Start (Reihe+1, Spalte−2), Schritt Spalte+1; Diagonalen analog (firewall.kod:84-154). Die Wand steht also 1 Feld vor dem Caster quer zur Blickrichtung.
- 5 aktive Elemente auf `start + k*schritt` (k=0..4) mit Fein-Position des Casters, dazu 4 passive Zwischenelemente auf halber Strecke (Feinposition ± FINENESS_HALF) → 9 Objekte (firewall.kod:158-269).

**ActiveWallElement** (wallelem.kod):
- `Constructor(caster,duration)` (73-86): Ablauf-Timer `GetDuration(duration)`; Periodik-Timer `1500 * random(90,110)/100` ms (88-92); Caster ist User → `vbSummoned`.
- `vbIsHarmful=TRUE`, `vbPeriodic=TRUE`, `vbCanAffectCaster=FALSE`, `piRange=0` (= nur genau das Feld; 38-68).
- **Betreten** `SomethingMoved` (100-131, MOVEON_NOTIFY): Battler & `InRange` → `CheckForEffect`.
- **Periodisch** `PeriodicEffect` (192-216): Liste „schon getroffen“ leeren; bei `vbPeriodic` alle Battler im Bereich prüfen; Timer neu.
- `InRange` (227-270): `dist² ≤ range²` (bei Bewegung mindestens Range 1, dann aber exakt `≤ piRange²`), plus `LineOfSight(element, ziel)`; setzt Bewegungszähler des Users zurück (Speedhack-Erkennung).
- `CheckForEffect` (133-190): nur Battler; Caster ausgenommen; Raum `ROOM_NO_COMBAT` (Spieler) bzw. `ROOM_NO_MOB_COMBAT` (Monster) → nichts; pro Periode jedes Ziel nur 1×; Spieler, die sich seit Raumeintritt nicht bewegt haben, sind immun; harmful & Spieler-Caster & `NOT AllowPlayerAttack(report=FALSE)` → „unaffected“ und als getroffen markieren. Dann `DoEffect`.
- `LocationCheck` (314-330): in Gildenhallen-Foyer des Casters oder ungültig in Arena → auflösen.
- **Feuerwand-Element** (kod/object/active/wallelem/wallfire.kod): Dauer `bound(random(d−20, d+20)*1000, 30000, 200000)` ms (65-74); Schaden `AssessDamage(random(0,maxDamage), aspell=ATCK_SPELL_FIRE, report=FALSE)` (106-107); Tod → `KilledSomething` beim Caster; Meldungsstufen 0/<5/<10/<15/sonst (133-170). Illusionswand: nur ab SP ≥ 35, Illusionary-Wounds-Schaden absolute (82-103).

---

### 10 Radiuszauber

Distanzmaß überall **quadrierte Rasterdistanz** in ganzen Feldern (Row/Col), keine Wurzel:
`SquaredDistanceTo(what) = (row−row')² + (col−col')²` (kod/object/active/holder/nomoveon.kod:104-130).

- **Room.EnchantAllInRange(what, range, center, monsters, SP)** (R:2971-3000): alle Spieler (und Monster wenn `monsters`) mit `dist² < range²` (strikt kleiner) → `what.StartEnchantment(who,SP)`; gibt Liste zurück. Keine Sichtlinie.
- Muster Mana Bomb (SP/manabomb.kod:72-85): `GetTargets` → `EnchantAllInRange(range = 6 + SP/20, center=Caster, monsters=TRUE)`.
- Muster Raumweit (SP/earthqua.kod:96-115, SP/flash.kod:64-80): `GetTargets` = alle Battler im Raum; Erdbeben-Abfall (earthqua.kod:362-391): `d² ≤ 8²` → 100 %, `d² > 20²` → 0 %, dazwischen `100*(400−d²)/(400−64)` %; Schaden `random(5,9) * (1+SP/25) * % / 100`.
- Murmel-Radius beim Casten: `dist² < 64` (S:18, S:1831).
- Fernkampf-Strafe ohne Sicht in SuccessChance: `dist²/4` (§3.1).
- Multicaster-Prisma: nächstes Prisma per `SquaredDistanceTo`, keine Maximaldistanz (SP/multicst.kod:148-172).
- Bei Mehrfachzielen filtert `GetAttackTargets` (S:1090-1135) Safety/Angreifbarkeit; `CheckPlayerAttack` mit `lTargets=$` verlangt, dass **alle** Battler im Raum angreifbar sind (sonst gar kein Cast).

---

### 11 Multicaster (SP/multicst.kod, kod/object/item/passitem/prism.kod)

- Ziel ist ein **Prisma** im Raum (passives Objekt, 3/5/7 Seiten, prism.kod:115-147); `FindPrism` = nächstes Prisma ohne oder mit demselben Spruch (multicst.kod:148-172). Kein Prisma → „You can't find a prism nearby…“; Prisma voll (`Casters ≥ Seiten`) → FALSE (multicst.kod:174-192, prism.kod:149-162).
- Klassenvariablen: `viMultiCast_Spellpower=1000` (Summe, die erreicht werden muss; Faustregel Castzeit_s × 15), `viChance_To_Increase=30`, `viManaDrain=7` pro `viDrainTime=1000` ms. **Achtung**: `viMana` (10 und 1) und `viCast_time` (2000 und 0) sind doppelt deklariert (multicst.kod:37/51, 42/63) – effektiv gelten vermutlich die letzten Werte (Mana 1, keine Castzeit); bei Nachbau am konkreten Spruch prüfen.
- `CastSpell` (multicst.kod:201-218): Caster bekommt periodische Verzauberung `[prism]` (1000 ms, lastcall=FALSE), `Prism.SpellCast` (erster Caster legt Spruch/Ziele/Bedarf fest, weitere treten bei; prism.kod:179-247), Startbeitrag `SP/2`.
- Jede Sekunde `StartPeriodicEnchantment` (multicst.kod:120-146): Neustart (letzter Durchlauf, wenn `Mana < 2*Drain`), `LoseMana(7)`, Beitrag `bound((SP+1)/25, 1, 4)` (SP neu berechnet).
- `Prism.ContributeSpellPower` (prism.kod:249-291): Summe erhöhen; 1/5 Chance Lichtprojektil; Ladestufe 0..3 (Drittel); Summe ≥ Bedarf → `CastSpell`: nur wenn `Casters ≥ Seiten` → `spell.PrismCast(SpellPower=Summe, lCasters, lTargets)` und Zeremonie beenden; sonst 1/30 Chance Hinweis „lacks the right symmetry“ (prism.kod:341-358).
- Trance-Bruch/Manamangel (multicst.kod:106-118, 226-233) → `CasterBrokeTrance`: Caster entfernen; letzter Caster weg → `EndCeremony` (alles zurücksetzen, Trance aller Caster lösen, prism.kod:310-339, 368-380). Prisma ist während Zeremonie nicht bewegbar.

---

### 12 Jala-Lieder (SP/jala.kod, SP/jala/hinder.kod)

Jala-Sprüche sind **Raumverzauberungen, die solange laufen, wie der Sänger in Trance bleibt**. Pro Raum **ein** Jala-Lied (`Room.GetJalaInfo` liefert den ersten Eintrag mit Schule Jala, R:3488-3504).
- Defaults: `viMana=5` (Start), `viManaDrain=3` pro `viDrainTime=5000` ms, `viChance_To_Increase=8`, `viPostCast_time=1`, `vbCanNewbieSing=TRUE`, keine Reagenzien, `GetNumSpellTargets=0` (jala.kod:37-68).
- **CanPayCosts** (jala.kod:70-88): `NOT vbCanNewbieSing` und kein PK-Flag → „guardian angel…“; **Instrument nötig** (`GetInstrumentLevel=0` → „You need an instrument!“).
- **Lautstärke** `CalculateVolume` (jala.kod:90-101): `SP * viManaDrain * 1000 / viDrainTime` (Default `SP*3/5`), min 1.
- **CastSpell** (jala.kod:103-145): läuft schon ein Lied mit Lautstärke ≥ neuer → „…quits when they realize they can't be heard“ (kein Effekt, aber Kosten bezahlt). Sonst (lauteres Lied **überlagert**, alter Eintrag bleibt aber in der Liste vorn/hinten – GetJalaInfo nimmt den ersten = neuen): `RoomStartEnchantment(state=[volume, caster, SP], time=viDrainTime, lastcall=FALSE)`, `StartRoomEffects`, Raummeldung „A nearby bard of Jala starts to sing“, MIDI an alle User im Raum, Trance-Flag am Sänger, `EnchantAllOccupants`, dann propagate (Spell.CastSpell: Improve, SchoolCast; kein WAV).
- **Unterhalt** `StartPeriodicEnchantment` (jala.kod:236-260): alle 5 s Neustart; letzter Durchlauf wenn `Mana < 2*Drain`; `LoseMana(Drain)`. Keine feste Maximaldauer.
- **Radius** = ganzer Raum. Neue Raumbetreter: `StartEnchantmentNewOccupant` → Effekt + „A nearby bard of Jala is singing a song.“ + MIDI (jala.kod:262-270); Verlassen: `EndRoomEnchantment` → `who.RemoveEnchantment` (jala.kod:272-279).
- **Unterbrechung** `BreakTrance` (jala.kod:158-210; aufgerufen über Room.BreakTrance R:3365-3396 für den Sänger): ohne **Jala-Halskette** beendet jedes Trance-Ereignis (Zaubern, Laufen, Schaden, Angriff, Benutzen, Rasten, Raumwechsel) das Lied (`Room.RemoveEnchantment`). Mit Halskette: Schaden/Angriff/Laufen ignoriert; Zaubern ignoriert, wenn Setting erlaubt und kein anderer Jala-Spruch; Benutzen ok außer Essen/Instrument/Trank; `EVENT_DISRUPT` mit 24 % (`random(1,100)<25`) überstanden.
- **Ende** `EndSpell` (jala.kod:281-311): bei `pbUserEffects` allen Anwesenden Effekt entfernen; „The song … ends.“; Trance-Flag löschen; Hintergrundmusik neu senden.
- **Hinder-Lieder** (hinder.kod): `vbCanNewbieSing=FALSE`, harmful, nicht in HappyLand, `pbUserEffects=TRUE`, `viHinderedSchool` (Default Jala). Senken die Erfolgschance (§3.1) aller Casts der gehinderten Schule im Raum um `random(JalaSP/2, JalaSP*2/3)` (hinder.kod:43-56).
- **Spellbane** (SP/jala/spelbane.kod): Stufe 3, Mana 12, Drain 8/5 s, Exertion 6, Reagenz 1 Polished Seraphym, nicht in Städten ohne Gildenkrieg; blockiert Zauber, deren SP < Lied-SP (§1.2 Punkt 12).
- **Silence** (Raumverzauberung + Trance, SP/roomench/silence.kod:91-130): blockiert jedes Zaubern von Spielern; beim Cast beendet sie ein laufendes Jala-Lied, wenn `random(SP/2,SP) > Lied-Lautstärke`. Dauer `viDrainTime + viDrainTime*SP/99` (196-203).

---

### 13 Boons & DM-Zauber

**DMSpell** (SP/dmspell.kod): `viMana=0`, `viSpell_level=50`, `viSpellExertion=0`, `viSchool=SS_DM_COMMAND`; nur `&DM` darf casten (sonst Debug, FALSE); `PayCosts` immer TRUE, `SuccessChance` immer TRUE, nicht vergessbar; Beschreibung mit „This is a DM only command.“ Werden von `RemoveAllPersonalEnchantments` nicht entfernt (P:2863) und lösen keine Outlaw-Hilfe-Prüfung aus (S:1459).

**Boon** (SP/boon.kod) – Langzeit-Bonus/-Malus über reale Tage:
- Nur DM oder Monster dürfen casten; Ziel muss User sein (boon.kod:54-76). Mana 0, Stufe 50, Schule DM, `viPersonal_ench=FALSE`, Flash GOOD.
- Kodierung über `iSpellPower`: `Dauer = (SP mod 10) * 2` Stunden (HOURS_LOGGED_ON_PER_DAY=2), `Betrag = SP/10` (boon.kod:150-168); `AddBoon(days, amount)` baut das (170-184). DM-Cast: Default `2*1*7 = 14` Stunden, Betrag 5 (85-92).
- State = `Stunden*100 + |Betrag|` (negativ bei negativem Betrag). Verzauberung mit `BOON_HOUR = 3600000` ms; bei jedem Ablauf (`EndEnchantment`, boon.kod:215-236) Stunde −1 und neu starten, bis Stundenanteil 0 → `UndoSpellEffect`. Da Timer beim Logout eingefroren werden, zählt nur **eingeloggte** Zeit.
- `DoSpellEffect/UndoSpellEffect` je Unterklasse (z. B. Stat-Bonus).

---

### 14 Sonstiges

- **Flash** (S:1359-1384; U:3376-3390): `FLASH_GOOD_SELF` → Caster bekommt Gut-Flash; `FLASH_GOOD`/`FLASH_BAD` → jedes Ziel, das User ist. Effekt `EFFECT_FLASHXLAT`, 100 ms, Farbe GOOD_COLOR/BAD_COLOR.
- **Outlaw/Safety**: `viOutlaw` & `viHarmful` → Outlaw-Status entsteht erst durch Schaden an Spielern (Angriffspfad `AllowPlayerAttack`, P:3594); `viOutlaw` ohne harmful → Outlaw schon beim Casten (S:1451-1455); Hilfe für Outlaws/Mörder → Outlaw (S:1458-1477) bzw. vorher durch Safety/Engel blockiert (S:1043-1088). Wände: harmful aber nicht outlaw; Elemente prüfen pro Ziel `AllowPlayerAttack` (wallelem.kod:174-184). Beschreibungs-Zusatz „Casting this spell can cause you to be labeled an outlaw.“ bei `viOutlaw` (S:309-312).
- **Schutzengel** (Spieler ohne `PFLAG_PKILL_ENABLE`): keine `viNoNewbieOffense`-Sprüche gegen/neben Spieler; keine Hilfe für Spieler, die in den letzten 15 min Spieler angegriffen haben; keine Hilfe für Outlaws.
- **HappyLand** (`SERVER_NO_MURDER`): Spruch nur verfügbar, wenn `vbCastable_in_HappyLand` (S:237-249); Hinder-Lieder FALSE.
- **Spellbane**: §1.2 Punkt 12 und §12. Gilt nicht für Item-Casts.
- **Silence**: §1.2 Punkt 12 und §12. Gilt nicht für Item-Casts.
- **Deflect** (SP/persench/deflect.kod): Dauer `d=(20+SP)*1000; random(d/2,d)` (99-107); reflektiert **nur harmful Einzelziel-AttackSpells** mit `random(0,100) < State` (109-130) → Spruch trifft Caster mit **halber SP** (S:1853-1858, trance.kod:79-85).
- **Forget** (SP/forget.kod:115-203): Ziel vergisst gewürfelte Sprüche/Skills für `FORGET_BASE_TIME + FORGET_PER_SPELLPOWER*SP ± 60 s`; Casten eines vergessenen Spruchs → Zufallsmeldung (S:746-757). DM/Boon nicht vergessbar.
- **Shal'ille-/Qor-Bane** im Raum: verletzt Caster der jeweiligen Schule (R:1368-1397).
- **Monster als Caster**: Spellpower immer 99, keine Kosten/Erfolgswurf (`PayCosts`, `CanPayCosts` → TRUE für Nicht-Spieler), Reichweite = gleicher Raum, schweigbare Monster von Silence/Spellbane blockiert.
- **Titel beim Verzaubern von Gegenständen** (S:2414-2441): `bound(Ability/25+1,1,4)` → Novice/Apprentice/Journeyman/Master.
