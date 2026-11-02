# Charaktererstellung (Stand 2026-11-02)

Wie im Original (`kod/util/system.kod` SendCharInfo/BP_NEW_CHARINFO, `player.kod` Konstruktion, Client
`module/char`). Code: `server/Meridian.Server/CharacterCreation.cs`, `WorldMagic.cs` (CreateCharacter).

- Ein Account ohne Charakter meldet seinen Charakter in `BP_CHARACTERS` mit Flag 1; der Original-Client öffnet
  dann den Assistenten und schickt `BP_SYSTEM/BP_SEND_CHARINFO` (49).
- `BP_CHARINFO`: 14 Haarfarben (PT_GRAY_TO_*), 4 Hautfarben (PT_BLUE_TO_SKIN1–4), je Geschlecht Haare, Kopf,
  Augen, Nasen, Münder (Listen aus system.kod), Startsprüche (`OfferToNewCharacters`: Stufe ≤ 2, Schulen
  Shal'ille/Qor/Kraanan/Faren; Kosten 10 bzw. 25), noch keine Fertigkeiten.
- `BP_SYSTEM/BP_NEW_CHARINFO` (48) wird geprüft: Name 3–30 Zeichen aus der erlaubten Menge, keine Götternamen,
  „guardian angel“, nicht als anderer Account/Charakter/NPC vergeben; Beschreibung ≤ 1000; Geschlecht 1/2.
  Werte je 1..50 und ≤ 200, sonst 3/1/4/1/5/9 (wie das Original bei manipuliertem Client); Gesichtsteile außerhalb
  der Listen werden durch den ersten erlaubten ersetzt; Sprüche über 45 Punkte → keine.
- Antwort `BP_CHARINFO_OK (id)`, danach wählt der Client den Charakter (`BP_USE_CHARACTER`).
- Der gewählte Name ist der Charaktername im Spiel (der Account-Name bleibt für den Login).
- Aussehen: Körper `bta`/`btb`, Arme/Beine je Geschlecht, Kopf, Augen, Nase, Mund mit Hautfarbe, Haare mit
  Haarfarbe (Overlays). Gespeichert in SQLite (`characters.profile`).
- Godot-Client: hat noch keinen eigenen Assistenten und steigt ohne Erstellung ein (Standard-Aussehen und -Werte).
