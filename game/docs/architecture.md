# Architektur (Stand 2026-11-01)

```
game/
  Meridian.sln
  shared/Meridian.Protocol   net8.0  – Protokoll, von Server UND Client benutzt
  server/Meridian.Server     net9.0  – Server (Linux/Windows, auch Raspberry Pi)
  client/                    Godot 4.7 .NET – Client (Meridian.Client.csproj)
  tests/Meridian.Tests       net9.0  – xUnit
```

## Meridian.Protocol

| Datei | Inhalt |
|---|---|
| `Framing.cs` | 7-Byte-Header, `FrameReader` |
| `PacketWriter.cs` / `PacketReader.cs` | Little-Endian-Felder, `str` |
| `Crc32.cs` | CRC-32 (zlib) |
| `Security.cs` | Passwort-Digest, `SecurityStream` (C→S-Hash), `ServerToken` (S→C-Verschleierung) |
| `GameCodec.cs` | Server-Seite: Hash prüfen, Nachrichten verschleiern, Token-Wechsel per `BP_ECHO_PING` |
| `ClientConnection.cs` | Client-Seite auf Frame-Ebene (wie Original-Client) |
| `ClientSession.cs` | Client-Ablauf (Login → Charakter → Raum), Raum-Zustand, Ereignisse für die UI |
| `MessageTypes.cs`, `GameMessageTypes.cs` | AP_*/BP_*-Codes |
| `LoginMessages.cs`, `GameMessages.cs` | Nachrichten bauen/lesen |

## Server

- `GameServer` – TCP-Listener, eine `Session` pro Verbindung, Doppel-Login trennt die alte Session.
- `Session` – Zustandsmaschine Login-Modus ↔ Game-Modus. Ein Lese-Task; ausgehende Nachrichten
  laufen über eine Queue mit einem Schreib-Task, weil der Secure-Token pro Nachricht weiterwandert
  und die Reihenfolge deshalb exakt stimmen muss.
- `World` – vorerst ein Raum; alle Änderungen unter einem Lock, Nachrichten an andere Sessions
  werden nur eingereiht (`Session.Post`).
- `ResourceRegistry` – vergibt Ressourcen-IDs (ab `0x00F00000`) für Strings; jede Session schickt
  sie dem Client vor der ersten Benutzung per `BP_CHANGE_RESOURCE`. Damit braucht der Server keine
  kompilierten `.rsc`-Dateien des Originals.
- `AccountStore` – JSON-Datei, nur für Entwicklung (MD5 wie im Original, ungesalzen).

## Kompatibilität

- Neuer Server ↔ Original-Client: Ziel. Der Original-Client braucht die `.roo`-Datei des Raums;
  die Raum-Security muss passen (`--client-dir`). Noch nicht getestet.
- Neuer Client ↔ Original-Server: Ablauf entspricht dem Original-Client; Namen kommen dort aus
  `.rsc`-Dateien, die der neue Client nicht liest (Anzeige als `#id`). Noch nicht getestet.
