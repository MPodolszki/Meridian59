# Wildnis und Tempel (Stand 2026-11-03)

Quelle: `kod/object/active/holder/room/monsroom/` (Kartenquadrate `a5 … j3`, `forest2–4`, `badland1`),
`monsroom/objroom/` (`b6 c5 c7 e6 h4 h9 k5`, `forest1/5`, `canyon1/2`, `icecave1`, `cave2`), `field1.kod`,
`badland2.kod`, `cave3.kod`; Tempel `temple.kod`, `tempkra.kod`, `tempqor.kod`, Priesterinnen
`kod/.../monster/temples/`.

Erzeugt mit:

```bash
cd game
python3 tools/make_wilderness.py && python -I tools/roo2layout.py server/data/world/wilderness.json ../resource/rooms client/rooms
python3 tools/make_wilderness.py      # Größen aus den Layouts
python3 tools/make_temples.py && python -I tools/roo2layout.py server/data/world/temples.json ../resource/rooms client/rooms
python3 tools/make_temples.py && python3 tools/add_region_exits.py && python3 tools/export_exits.py
```

`tools/kodroom.py` liest die Raumklassen automatisch: Name, `.roo`, Musik, Licht, Außenanteil, Gelände,
Ankunftspunkt, `plExits` (auch verschlossene Türen), `plEdge_Exits`, Monster (`plMonsters`, `plGenerators`,
`piGen_time`, `piGen_percent`, `piMonster_count_max`, `piInit_count_min/max`) und Bäume (`Create(&Tree)`).

## Neu im Server
- **Zonen-Übergänge** (`regionExits`): `SomethingMoved` mit `UtilGoNearSquare` – wer ein Feld der Zone betritt,
  wechselt den Raum (z. B. Marion Ostrand → Wald C4, C4 Nordwestecke → Tempel der Shal'ille). Die Bedingungen
  (`new_row > 35 and new_col < 9` …) werden in Rechtecke umgerechnet. `add_region_exits.py` ergänzt sie auch in
  den Städten.
- **Spawn wie monsroom.kod**: beim Start `piInit_count` Monster, danach alle `piGen_time` ms mit `piGen_percent` %
  Chance eines, höchstens `piMonster_count_max`.

## Räume
53 Wildnis-Räume, u. a. Deep Woods of Ileria (C4, Spinnenjunge, lebende Bäume), West/East Merchant Way (C5–F4),
Main gate to Cor Noth (G4), Main gate to the city of Tos (H6), Main gate of Barloque (I3), The Badlands (badland1,
K5), The Cragged Mountains (G8, I8), Ukgoth (I9, Trolle), Dreaded Caves of Ice (icecave1). Alle Städte sind zu Fuß
verbunden (Test `WildernessTests.Towns_AreConnectedThroughTheWilderness`).

## Tempel
| RID | Tempel | Zugang | Priesterin | lehrbar (schon im Spiel) |
|---|---|---|---|---|
| 48 | The Temple of Shal'ille | C4 Nordwestecke (Zone) | Xiana | Kleine/Große Heilung, Heilige Berührung, Gift heilen, Heiliges Symbol, Letzte Riten, Lebensatem, Säure widerstehen, Gleißen |
| 801 | The Temple of Kraanan | G9 Westrand | Qerti'nya | Segen, Superstärke, Waffe erschaffen, Nahrung erschaffen, Kraftübertragung |
| 802 | The Temple of Qor | I8 (38/26) oder H9 (26/12) | Zuxana | Säureberührung, Schwächen, Festhalten |
| – | (offener Himmel) Badlands 44/59 | badland1 | Tenuv'vyal (Faren) | Zap, Feuer/Kälte/Blitzen widerstehen, Flammenberührung, Eisige Finger, Feuerball, Erdbeben, Mystische Berührung |

`make_temples.py` meldet die übrigen Sprüche der Listen, die es noch nicht gibt; sie werden lehrbar, sobald sie in
`spells.json` stehen.

## Abweichungen und offen
- Qor: Im Original öffnet sich abwechselnd der Eingang in I8 oder H9 (`ExitsTimer`), der Südausgang führt dorthin.
  Hier sind beide Eingänge offen, der Ausgang führt immer nach I8. Tür im Tempel (Deckenhub) ohne Animation.
- Tempel-Sonderlogik fehlt: Szepter auf dem Altar (Shal'ille), Opferwaffen als Zierobjekte (Kraanan), Quests der
  Priesterinnen, Vergessenstrank, Bonus auf das Üben der Tempelschule (`ModifyChanceToImprove`).
- Riija-Tempel liegt auf Ko'catan und kommt mit Ko'catan.
- Sonderlogik der Wildnis fehlt: Hebel- und Lichterrätsel (e5, forest-Ringe), Steinkreis und Portal nach Canyon2
  (i9), Manaknoten, Kräuter-/Pilzwachstum (`CreateObjectGeneration`), Yeti-Höhle, Brombeeren, Seancen.
- `StandardLeaveDir`-Sonderfälle: nur K5 → Badlands übernommen; die übrigen sperren Ränder oder hängen an Ko'catan.
- Forest1–5, Canyon1 und Field1 sind im Original nur über Sonderwege erreichbar (Portal, Rätsel) und hier noch
  nicht angebunden.
- Monster nur tagsüber/nachts wie im Original gibt es nur für Friedhof und Krypta; die Wildnis spawnt immer.
- Zonen-Übergänge prüfen das Zielfeld nicht auf Belegung (`UtilGoNearSquare` sucht ein freies Nachbarfeld).

## Manaknoten (mananode.kod, 2026-11-03)
Sechs Knoten des Festlands: A5 (20/17), G9 (52/30), H9 (45/32), Eishöhle (25/23), Höhle Cave2 (23/53), Castle
Victoria Obergeschoss. Benutzen (Linksklick, BP_REQ_ACTIVATE) auf weniger als 3 Felder: einmal je Knoten
(Bitmaske NODE_*, gespeichert) maximales Mana + ((5 + Mystik)/10) + 3. Im Godot-Client eine schwebende, leuchtende
Kugel. Abweichungen: die Knoten in I9 (Steinkreis), Canyon2 und den Badlands entstehen im Original durch Rätsel bzw.
Zeitgeber und fehlen; verfluchte/tote Knoten und das Lösen bei Tod gibt es nicht.
