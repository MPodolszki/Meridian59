# Nahkampf im Original (Referenz aus KOD, 2026-11-02)

Kurzfassung der Formeln aus `kod/` (battler.kod = B, player.kod = P, user.kod = U, monster.kod = M,
skill.kod = SK, stroke.kod = ST, weapon.kod = W, defmod.kod = DM, shield.kod = SH). Ganzzahlarithmetik.
Umsetzung siehe combat.md.

## Treffer (B:319 TryAttack, für Spieler und Monster)
chance = bound(offense(Angreifer) × 55 / defense(Verteidiger), 10, 95); Treffer, wenn chance ≥ random(1,100).

## Spieler
- Schlagtechnik: unbewaffnet Faustschlag (punch, 430), mit Nahkampfwaffe immer Hieb (slash, 421).
- Offensive (P:4197): Technik×3 + Waffenfertigkeit×2 + Zielen×4 + Grund-MaxLP×3/2;
  Waffe: Wucht +75, Stich +125, Hieb +0; Qualität hoch +50; begrenzt 1..1000. Unbewaffnet: Fertigkeit = Raufen.
- Defensive (P:4264): Parieren×2 (nur mit Waffe) + Blocken (nur mit Schild, + Schildbonus, max. 120)
  + Ausweichen×3 + Geschick×4 + Grund-MaxLP×3/2 + viDefense_base der Rüstung (Leder +50, Kette −50 …); 1..1000.
- Schaden (ST:201, ST:332): Waffe Grundschaden × viDamage_factor/100 (Hieb 80); unbewaffnet random(1,4).
  Danach Fertigkeitsbonus (Fertigkeit+1)×5/100 (unbewaffnet Raufen×5/100) + (100 + bound(Stärke−25, 0, 40)) × d / 100.
- Angriffstakt 1000 ms (IsOkayAttackTime), keine Waffenabhängigkeit.
- viHit_Factor wird nirgends gelesen.

## Monster
- Offensive = Defensive = 3×Stufe + 60×Schwierigkeit (Zombie 405, Skelett 525), wenn nicht gesetzt.
- Max. LP: Stufe < 40 → Stufe, sonst 120×Stufe/100 (Zombie 66, Skelett 90), beim Erschaffen Fuzzy (±25 %).
- Schaden: Fuzzy(Stufe / random(10,15)) (Zombie ~3–6, Skelett ~4–9). Fuzzy(n) = n − n/4 + random(0, n/2).
- Angriffstakt: Fuzzy(1000) + 3500 − 70×(3×Schwierigkeit + Tempo) (Zombie ~3,1–3,6 s, Skelett ~2,4–2,9 s).
- Reichweite bound(2 + Schwierigkeit/6, 2, 3).
- Resistenzen: Zombie unheilig +50, heilig −25; Skelett Stich/Schock/Kälte/unheilig +70, Feuer −10, heilig −20,
  Wucht −20. Schaden × (100 − Resistenz) / 100.

## Spieler wird getroffen (P:4518)
Rüstung/Schild mindern um random(r/3, r) (max. Schaden − 1; Schild nur, wenn Blocken gelingt); mind. 1;
höchstens (Grund-MaxLP+2)/3 pro Treffer, solange LP < 2×Grund-MaxLP; höchstens 30.

## Fertigkeiten
| SKID | Fertigkeit | Stufe | Verbesserung | Bezugswert |
|---|---|---|---|---|
| 401 | Ausweichen | 2 | 20 | Geschick |
| 402 | Parieren | 4 | – | Geschick |
| 404 | Blocken | 1 | 60 | Geschick |
| 421 | Hieb | 1 | 20 | Geschick |
| 430 | Faustschlag | 1 | 60 | Geschick |
| 450 | Raufen | 2 | 25 | Stärke |
| 451 | Schwertkampf | 3 | – | Zielen |
| 452 | Streitkolbenkampf | 1 | 40 | Stärke |
| 453 | Krummsäbelkampf | 4 | – | Geschick |
| 454 | Hammerkampf | 3 | – | Stärke |
| 455 | Axtkampf | 3 | – | Stärke |
| 457 | Kurzschwertkampf | 2 | 60 | Zielen |

- Neue Charaktere bekommen keine Fertigkeiten automatisch; bei der Erstellung wählbar sind die mit Stufe ≤ 2
  (Kosten 10/25, gemeinsames Budget 45 mit den Sprüchen).
- Verbessern (SK:294): Anfangschance wie bei Sprüchen; Zweitchance 60 + Bezugswert − Stufe×10 − Lernpunkte
  der Sprüche, × Faktor (Monsterstufe), begrenzt 1 + Bezugswert/10 .. 99. Höchstens 10 Verbesserungen je
  15–22 Minuten (Advancement Points, Raumwechsel −2).
- Technik/Waffenfertigkeit üben: alle 75 Schläge mit derselben Waffenart ein Bonus; bei einem Treffer wird
  dann zu 40 % die Waffenfertigkeit, sonst die Technik verbessert, wenn sie unter der Monsterstufe liegt.
- Ausweichen/Parieren/Blocken üben: beim Sieg über das Ziel, wenn man dabei mindestens einmal verfehlt wurde
  (30 % Parieren mit Waffe, sonst 30 % Blocken mit Schild, sonst Ausweichen).

## Protokoll
`BP_SKILLS` (u16 n, object_desc[n]) und Stat-Gruppe 4 (Listeneinträge mit Fähigkeit %), wie bei Sprüchen.
