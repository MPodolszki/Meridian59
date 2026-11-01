namespace Meridian.Protocol;

/// <summary>
/// Sicherheitszustand einer Verbindung aus Sicht des Servers (protocol.md 2.3–2.5):
/// prüft den Security-Hash eingehender Game-Nachrichten und verschleiert das Type-Byte
/// aller ausgehenden Nachrichten. Der Token bleibt über Moduswechsel hinweg erhalten,
/// die Seeds kommen mit jedem AP_GETCHOICE neu.
/// </summary>
public sealed class GameCodec
{
    public ServerToken Token { get; } = new();

    /// <summary>Seeds aus dem letzten AP_GETCHOICE; null vor dem ersten Menü.</summary>
    public SecurityStream? Security { get; set; }

    /// <summary>Prüft eine Client-Nachricht im Game-Modus. Schaltet die Folge immer weiter, auch bei Mismatch.</summary>
    public bool Verify(Frame frame)
    {
        if (Security is null)
            throw new InvalidOperationException("Keine Seeds – AP_GETCHOICE wurde noch nicht gesendet");
        return Security.NextHash(frame.Payload) == frame.Header.Crc;
    }

    /// <summary>Rahmt eine Server-Nachricht. Wie im Original wird erst das Type-Byte verschleiert,
    /// dann die CRC über den verschleierten Payload gebildet (commcli.c SendPacket).
    /// Solange kein BP_ECHO_PING gesendet wurde, ist die Verschleierung neutral.</summary>
    public byte[] FrameServerMessage(byte[] payload, byte epoch)
    {
        byte[] secured = (byte[])payload.Clone();
        secured[0] = Token.Apply(secured[0]);
        return Framing.FrameServerMessage(secured, epoch);
    }

    /// <summary>Token-Wechsel wie GameEchoPing (game.c): neuer Token = seeds[0] &amp; 0xFF.
    /// Die gelieferte BP_ECHO_PING-Nachricht muss noch mit dem ALTEN Token gesendet werden,
    /// danach <see cref="ServerToken.Reset"/> mit dem gelieferten Token aufrufen.</summary>
    public (byte[] Payload, byte NewToken) CreateEchoPing()
    {
        if (Security is null)
            throw new InvalidOperationException("Keine Seeds – AP_GETCHOICE wurde noch nicht gesendet");
        byte newToken = (byte)(Security.Seeds[0] & 0xFF);
        return (GameReplies.EchoPing(newToken), newToken);
    }
}
