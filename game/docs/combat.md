# Kampf (Stand 2026-11-01, vereinfacht)

Grob am Original orientiert (`kod/.../monster.kod`, `player.kod`, `monster/zombie.kod`, `monster/skel.kod`),
aber stark vereinfacht. Werte in `server/Meridian.Server/Combat.cs`.

| | Spieler | Zombie | Skelett |
|---|---|---|---|
| Lebenspunkte | 20 (wie Original) | 24 | 36 |
| Schaden | Faust 1–4 (unarmed.kod), sonst Waffe | 1–4 | 2–6 |
| Trefferchance | 75 % | 60 % | 65 % |
| Angriffstakt | 1,2 s | 2,4 s | 1,8 s |
| Tempo | – | sehr langsam (Original SPEED_VERY_SLOW) | normal (SPEED_AVERAGE) |
| Aggro-Reichweite | – | 5 Felder | 7 Felder |

- Nahkampfreichweite 1,5 Felder. Angriff per `BP_REQ_ATTACK` (attack_info, Ziel-ID).
- Waffenschaden (weapon.kod `GetBaseDamage`): Wucht 4–8, Stich 3–8, Hieb 5–11; Qualität niedrig −1, hoch +1.
  Streitkolben (Wucht, niedrig) 3–7, Hammer 4–8, Langschwert 3–8, Axt 5–11. Trefferchance noch fest 75 %.
- Rüstung/Schild mindern jeden Treffer um Random(r/3, r) (defmod.kod, r = `viDamage_base`: Kettenrüstung 2,
  Goldschild 1), höchstens bis 1 Schaden. `viDefense_base` (Ausweichen) ist noch nicht umgesetzt.
- Ausdauer (Vigor, player.kod): Start 100, max. 200. Bewegung kostet 2 × (Tempo·5/6)² Anstrengung, ein Schlag 2000;
  je 10000 Anstrengung = 1 Ausdauer (abgezogen ab 20000 angesammelt). Unter 2 Ausdauer kein Angriff.
  Erholung bis 80 alle 1000 + 30·(51 − Stamina) ms. Essen gibt Ausdauer (Nährwert).
- Lebenspunkte-Regeneration wie `CalculateHealthTime`: alle ((200 − Ausdauer)²/6 + 1000) · (125 − Stamina)/100 ·
  100/max(40, min(100, MaxLP)) ms ein Punkt, auch im Kampf. Stamina gibt es noch nicht, angenommen 25.
- Tod: Wiederbelebung am Ankunftspunkt von Tos mit vollen Lebenspunkten (kein Verlust).
- Monster: Friedhof (70) und Krypta (71) mit den Spawn-Punkten und Anteilen aus KOD
  (85/15 bzw. 80/20 Zombie/Skelett), max. 6 bzw. 5; bis zur Hälfte alle 2 s, danach alle 20 s ein neues. **Abweichung:** im Original nur nachts
  (Stunde < 5 oder > 21); ohne Tag-Nacht-Wechsel vorerst immer.
- Monster laufen geradlinig (noch keine Wegfindung/Wände auf dem Server), verfolgen Ziele bis zur doppelten
  Aggro-Reichweite und kehren sonst zum Spawn-Punkt zurück.

Protokoll:
- Lebenspunkte: `BP_STAT` Gruppe 1, Nr. 1 (wie `user_stat_health`).
- Kampftexte: `BP_MESSAGE`.
- Animationen: `BP_CHANGE` mit `ANIMATE_ONCE`; Konvention des neuen Clients: Gruppe 2–4 = Angriff, 9 = Sterben.
- Monster haben `OF_ATTACKABLE`, Spieler `OF_PLAYER`, NPCs keins von beiden.
- Ausgänge am Raumrand (`plEdge_Exits`): ausgelöst, sobald eine gemeldete Position außerhalb des Rasters liegt
  (Raumgröße `rows`/`cols` in `tos.json`, aus den Layouts).
