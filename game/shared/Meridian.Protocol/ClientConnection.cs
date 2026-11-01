namespace Meridian.Protocol;

/// <summary>
/// Client-Seite des Protokolls, verhält sich wie der Original-Client (clientd3d/com.c, server.c):
/// entschleiert das Type-Byte jeder empfangenen Nachricht, übernimmt Seeds und Token,
/// spiegelt das Epoch-Byte und berechnet im Game-Modus den Security-Hash.
/// Gedacht für Tests und den neuen Godot-Client.
/// </summary>
public sealed class ClientConnection(Stream stream)
{
    private readonly FrameReader _reader = new(stream, Framing.MaxServerPayload);
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private SecurityStream? _security;

    public ServerToken Token { get; } = new();
    public bool GameMode { get; private set; }
    public byte LastEpoch { get; private set; }

    /// <summary>Nächste Nachricht mit entschleiertem Type-Byte, oder null bei Verbindungsende.</summary>
    public async Task<Frame?> ReceiveAsync(CancellationToken ct = default)
    {
        var frame = await _reader.ReadAsync(ct);
        if (frame is null)
            return null;

        frame.Payload[0] = Token.Apply(frame.Payload[0]);
        LastEpoch = frame.Header.Epoch;
        Track(frame);
        return frame;
    }

    private void Track(Frame frame)
    {
        if (!GameMode)
        {
            switch ((LoginMessage)frame.Type)
            {
                case LoginMessage.GetChoice:
                    var r = new PacketReader(frame.Payload.AsSpan(1));
                    var seeds = new uint[SecurityStream.StreamCount];
                    for (int i = 0; i < seeds.Length; i++)
                        seeds[i] = r.U32();
                    _security = new SecurityStream(seeds);
                    break;
                case LoginMessage.Game:
                    GameMode = true;
                    break;
            }
            return;
        }

        switch ((GameMessage)frame.Type)
        {
            case GameMessage.EchoPing:
                // Redbook-ID wird hier nicht aufgelöst → Default-String (wie bei unbekannter ID im Original).
                Token.Reset((byte)(frame.Payload[1] ^ 0xED));
                break;
            case GameMessage.Quit:
                GameMode = false;
                break;
        }
    }

    public async Task SendAsync(byte[] payload, CancellationToken ct = default)
    {
        await _sendLock.WaitAsync(ct);
        try
        {
            byte[] frame = GameMode
                ? Framing.Frame(payload, _security!.NextHash(payload), LastEpoch)
                : Framing.Frame(payload, crc: 0, epoch: 0);
            await stream.WriteAsync(frame, ct);
        }
        finally
        {
            _sendLock.Release();
        }
    }
}
