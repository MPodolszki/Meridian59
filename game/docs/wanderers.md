# Wandernde NPCs (towns/wanderer)

`tools/make_wanderers.py` liest die Wanderer aus `kod/object/active/holder/nomoveon/battler/monster/towns/wanderer`
nach `server/data/world/wanderers.json` (nur `wanderers`, keine Räume), Läden und Dialoge daneben. Server:
`WorldWanderers.cs` wie wanderer.kod – nach 20 s und dann alle Random(30, 90) Minuten zu einem anderen Ziel aus
plDestinations; sind Spieler im Raum, bleibt er bis zum nächsten Mal (nach fünf Versuchen springt er trotzdem).

| NPC | Ziele | lehrt (im Spiel) |
|---|---|---|
| Maleval, Hexenmeister (DarkWizard) | Qor-Tempel, Gemächer des Herzogs, Apotheke Tos, Marion, Hof der Burg | Schockende Wut, Feuerstoß, Explosiver Frost, Säurespritzer, Vampirischer Entzug |
| Jonas D'Accor, General (JealousGeneral) | Hütte des Ältesten Jasper, Kraanan-Tempel, Schenken Jasper/Barloque, Gasthaus Cor Noth | Eile, Blocken |
| Parrin Aragone, Spielmann (Minstrel) | Herzog, Cor Noth, Schenken Jasper/Barloque, Ratskammer | (Jala-Lieder, noch nicht im Spiel) |
| Miriana, Ketzerin (Heretic) | A1, F7, A6, I7 | (Wände, Bindung, Stille, Flammenring – noch nicht im Spiel) |
| Tendrath, Geist eines Druiden (HunterGhost) | G8, I9, Castle Victoria, K5 | (Spinnennetz, Blitzwand, Ablenken, Jagd – noch nicht) |
| Izzio, Kräutersammler | G5, C5, I3, Shal'ille-Tempel, F8 | – (sein Kräuterhandel fehlt noch) |

Abweichungen: Izzios Laden und die Quest-/Sonderdialoge der Wanderer fehlen; die Ketzerin wird nicht besessen.
