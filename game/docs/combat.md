# Kampf (Stand 2026-11-01, vereinfacht)

Grob am Original orientiert (`kod/.../monster.kod`, `player.kod`, `monster/zombie.kod`, `monster/skel.kod`),
aber stark vereinfacht. Werte in `server/Meridian.Server/Combat.cs`.

| | Spieler | Zombie | Skelett |
|---|---|---|---|
| Lebenspunkte | 20 (wie Original) | 24 | 36 |
| Schaden | 2–6 (Fäuste) | 1–4 | 2–6 |
| Trefferchance | 75 % | 60 % | 65 % |
| Angriffstakt | 1,2 s | 2,4 s | 1,8 s |
| Tempo | – | sehr langsam (Original SPEED_VERY_SLOW) | normal (SPEED_AVERAGE) |
| Aggro-Reichweite | – | 5 Felder | 7 Felder |

- Nahkampfreichweite 1,5 Felder. Angriff per `BP_REQ_ATTACK` (attack_info, Ziel-ID).
- Regeneration außerhalb des Kampfes (8 s ohne Kampf): +1 LP alle 4 s.
- Tod: Wiederbelebung am Ankunftspunkt von Tos mit vollen Lebenspunkten (kein Verlust).
- Monster: Friedhof (70) und Krypta (71) mit den Spawn-Punkten und Anteilen aus KOD
  (85/15 bzw. 80/20 Zombie/Skelett), max. 6 bzw. 5, neue alle 20 s. **Abweichung:** im Original nur nachts
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
