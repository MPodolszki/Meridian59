# Entscheidungen

Format: Datum – Entscheidung – Begründung.

## 2026-11-01 – Arbeit im Fork, Branch `game`
Der Fork https://github.com/MPodolszki/Meridian59 enthält den Original-Code.
Das neue Spiel entsteht im Branch `game` unter `game/` (server, client, docs).
`master` bleibt das unveränderte Original.

**Begründung:** Original-Code direkt als Referenz griffbereit, ein einziges
Repo für die Synchronisation über mehrere Rechner, saubere Trennung durch
eigenen Branch und eigenes Verzeichnis.

## 2026-11-01 – Client-Engine: Godot
Der neue Client wird mit Godot gebaut.

**Begründung:** Läuft auf allen gängigen Plattformen (Windows, Linux, macOS,
später ggf. mobil/Web), Open Source, gute 3D-Unterstützung für das Ziel
1st/3rd-Person-MMO.

Offen: Godot-Version (4.x) und Sprache im Client (GDScript vs. C#).

## 2026-11-01 – Protokoll zunächst kompatibel zum Original
Der neue Server und Client sprechen anfangs das Original-Protokoll
(siehe protocol.md).

**Begründung:** Gegentests möglich: neuer Server ↔ alter Client,
neuer Client ↔ alter Server.
