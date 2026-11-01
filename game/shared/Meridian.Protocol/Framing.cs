using System.Buffers.Binary;

namespace Meridian.Protocol;

/// <summary>
/// 7-Byte-Header: u16 len, u16 crc, u16 len (Kopie), u8 epoch (protocol.md 2.1).
/// len zählt den Payload inkl. Type-Byte.
/// </summary>
public static class Framing
{
    public const int HeaderSize = 7;

    /// <summary>Server-Grenze für Client-Nachrichten (blakserv.h LEN_MAX_CLIENT_MSG).</summary>
    public const int MaxClientPayload = 6000;

    /// <summary>Grenze, ab der der Original-Client resynct (clientd3d COMBUFSIZE).</summary>
    public const int MaxServerPayload = 40000;

    /// <summary>Rahmt einen Server→Client-Payload; crc = untere 16 Bit von CRC32.</summary>
    public static byte[] FrameServerMessage(ReadOnlySpan<byte> payload, byte epoch = 0)
    {
        ushort crc = (ushort)(Crc32.Compute(payload) & 0xFFFF);
        return Frame(payload, crc, epoch);
    }

    public static byte[] Frame(ReadOnlySpan<byte> payload, ushort crc, byte epoch)
    {
        if (payload.Length > ushort.MaxValue)
            throw new ArgumentException("Payload zu groß", nameof(payload));

        var frame = new byte[HeaderSize + payload.Length];
        var span = frame.AsSpan();
        BinaryPrimitives.WriteUInt16LittleEndian(span, (ushort)payload.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(span[2..], crc);
        BinaryPrimitives.WriteUInt16LittleEndian(span[4..], (ushort)payload.Length);
        span[6] = epoch;
        payload.CopyTo(span[HeaderSize..]);
        return frame;
    }
}

public readonly record struct FrameHeader(ushort Length, ushort Crc, ushort LengthVerify, byte Epoch)
{
    public bool IsConsistent => Length == LengthVerify;

    public static FrameHeader Parse(ReadOnlySpan<byte> header) => new(
        BinaryPrimitives.ReadUInt16LittleEndian(header),
        BinaryPrimitives.ReadUInt16LittleEndian(header[2..]),
        BinaryPrimitives.ReadUInt16LittleEndian(header[4..]),
        header[6]);
}

public sealed record Frame(FrameHeader Header, byte[] Payload)
{
    public byte Type => Payload[0];
}

/// <summary>Liest gerahmte Nachrichten aus einem Stream.</summary>
public sealed class FrameReader(Stream stream, int maxPayload)
{
    private readonly byte[] _header = new byte[Framing.HeaderSize];

    /// <summary>Nächster Frame, oder null wenn die Gegenseite die Verbindung sauber geschlossen hat.</summary>
    public async Task<Frame?> ReadAsync(CancellationToken ct)
    {
        if (!await ReadExactAsync(_header, ct))
            return null;

        var header = FrameHeader.Parse(_header);
        if (!header.IsConsistent)
            throw new ProtocolException($"Header inkonsistent: len={header.Length}, verify={header.LengthVerify}");
        if (header.Length > maxPayload)
            throw new ProtocolException($"Payload zu groß: {header.Length}");

        var payload = new byte[header.Length];
        if (!await ReadExactAsync(payload, ct))
            throw new ProtocolException("Verbindung mitten in Nachricht geschlossen");
        return new Frame(header, payload);
    }

    private async Task<bool> ReadExactAsync(byte[] buffer, CancellationToken ct)
    {
        int read = 0;
        while (read < buffer.Length)
        {
            int n = await stream.ReadAsync(buffer.AsMemory(read), ct);
            if (n == 0)
            {
                if (read == 0)
                    return false;
                throw new ProtocolException("Verbindung mitten in Nachricht geschlossen");
            }
            read += n;
        }
        return true;
    }
}
