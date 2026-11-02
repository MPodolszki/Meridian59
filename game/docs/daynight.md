# Tag und Nacht (Stand 2026-11-02)

Wie im Original (`kod/util/system.kod` RecalcLightAndWeather/NewGameHour, `room.kod` GetRoomLight).
Code: `server/Meridian.Server/GameClock.cs`.

- **Uhr:** Ein Spieltag dauert 2 echte Stunden, eine Spielstunde 5 Minuten. Stunde = ((UTC − 5 h) mod 2 h) / 5 min.
  Zum Testen: Server mit `--hour 23` (feste Stunde).
- **Helligkeit:** Lichtstunde = abs((Stunde − 2) mod 24) (Blakod-`mod` ist abs(a % b)); bis 12 linear steigend,
  danach fallend, in Prozent, begrenzt auf 15..75. Hell um 14 Uhr, am dunkelsten zwischen 1 und 3 Uhr.
- **Raumlicht:** Grundlicht + Außenanteil × (Helligkeit − 50) / 4, begrenzt auf 0..255. Außenanteil
  (`piOutside_factor`, Standard 10) je Raum in `data/world/tos.json` (`outsideFactor`): Straßen 10, Friedhof 8,
  Gasthaus/Arena 5, Bank/Apotheke/Halle 3, Taverne/Schmiede/Gerber 0.
- **Protokoll:** Raumlicht steht in `BP_PLAYER`; ändert es sich mit der neuen Stunde, bekommen alle im Raum
  `BP_LIGHT_AMBIENT`.
- **Monster:** Friedhof und Krypta erzeugen nur nachts neue Monster (Stunde < 5 oder > 21).
- **Client:** setzt Sonne, Himmel und Umgebungslicht aus dem Raumlicht (Straße nachts ≈ 120, mittags 255).

Noch nicht umgesetzt: Wetter/Sturm, Sonnenstand per `BP_LIGHT_SHADING`, Himmelsgrafik je Wetter,
Launen der NPCs bei Dämmerung (MOODMOD_DAWN/DUSK).
