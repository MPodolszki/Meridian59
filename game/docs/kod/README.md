# Kataloge des Original-Codes (KOD)

- `monsters_catalog.json` – alle 172 Klassen unter `kod/object/active/holder/nomoveon/battler/monster/**`
  (Werte, Resistenzen, Spezialangriffe, Spawn-Räume, abgeleitete Werte nach monster.kod). Erklärung: `../monsters-original.md`.
- `spells_catalog.json` – alle 174 Zauberklassen unter `kod/object/passive/spell/**` (SID, Schule, Stufe, Mana,
  Reagenzien, Lehrer, Wirkung mit Formeln und Zeilenangaben, Abhängigkeiten). Erklärung: `../spells-original.md`.

Kopfdaten sind maschinell aus dem KOD gelesen (Vererbung und Konstanten aufgelöst); Wirkung, Kategorie, Körperform
(`bodyShape`, `visualDe`) und Hinweise sind von Hand bzw. per Analyse ergänzt (Stand 2026-11-02) und bei Zweifel am
KOD zu prüfen. Grundlage für `server/data/monsters/monsters.json` (tools/make_monsters.py) und die Sprüche.
