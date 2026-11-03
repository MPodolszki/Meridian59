# Welt: was noch fehlt (Stand 2026-11-03)

Vergleich aller Raumklassen unter `kod/object/active/holder/room/` (269 mit Raumnummer) mit `server/data/world/*.json`:
78 umgesetzt (Raza, Tos, Marion, Cor Noth, Jasper, Barloque mit Grüften/Friedhof), 191 offen. Ko'catan (Stadt,
Wald, Kanalisation, Mietzimmer: 39 Räume) und die Orkhöhlen (orccave1–6, Erweiterungen, Orkgruben: 11) kommen später.

| Gebiet | Räume | KOD | Inhalt im Original |
|---|---|---|---|
| Wildnis Festland | 41 Karten + 12 | `monsroom/outdoors*`, `objroom/outdoors*`, forest1–5, canyon1–2, field1, badland1–2, icecave1, cave2–3 | Verbindet alle Städte; Monster je Gebiet, Kräuter, Tempel- und Hüttenzugänge |
| Feenwald | 8 | `monsroom/feyforst` | Feen, Elfen-Dialoge, Feenstaub-Aufgaben |
| Tempel | 4 + Faren | temple (Shal'ille, C4), templekraanan (G9), templeqor (I8), templeriija, badland1 (Faren) | Priester lehren die Schulsprüche, Opfer/Segen |
| Hütten, Posten | 5 | hermithut, bowmakerhut, tradingpost (+Keller), university | Einsiedler, Bogenbauer, Handelsposten, Universität |
| Kanalisationen | 7 | barlsewer1–3 + sewerkinglair, jassewer1–3 | Ratten, Lupoggs, Rattenkönig |
| Burgen | 13 | castle1/1b/1c, castle2a–e, duke1–5 | Herzog Akardius, Prinzessin, Wachen, Thronsaal |
| Nekropolis und Unterwelt | 11 | necropolis1–5/3a/3b, necarena, lichmaze, underworld, throne1 | Untote, Lich-Labyrinth, Totenreich nach dem Tod |
| Spinnennest, Kerker | 2 | spidernest1, dungeonvictoria | Spinnenkönigin; Kerker |
| Wohnen | 15 + 2 + 8 | guildhall1–15, Mietzimmer Barloque/Marion, guest1–8 | Gildenhallen kaufen, Zimmer mieten, Gästehäuser |
| Sonstiges | 2 | assassinheadquarters (Barloque), forgottentoo (Cor Noth) | Assassinen-Versteck |
| Verwaltung/Test | 7 | godroom, profileroom, outofgrace, mockersroom, gallery, bazmansroom, oldbarloque*/oldjasper/oldmarion | Admin- und Altlasten, werden nicht gebaut |

## Reihenfolge
1. ~~Wildnis und Tempel~~ erledigt (2026-11-03, `docs/wilderness.md`): 53 Wildnis-Räume, Tempel Shal'ille, Kraanan, Qor, Faren-Priesterin.
2. ~~Kanalisationen Barloque und Jasper~~ erledigt (`docs/areas.md`), mit Lupogg-König und Assassinen-Versteck.
3. ~~Burgen und Herzog~~ erledigt (`docs/areas.md`).
4. ~~Nekropolis, Lich-Labyrinth, Unterwelt, Spinnennest~~ erledigt (`docs/areas.md`).
5. Wohnen: ~~Gildenhallen~~ als Räume erledigt (ohne Gilden); Mietzimmer brauchen das Mietsystem. Gästehäuser guest1–8 sind Raza (schon da).
6. ~~Feenwald, Hütten, Universität~~ erledigt (`docs/areas.md`); Bogenbauer und Handelsposten liegen auf Ko'catan.
