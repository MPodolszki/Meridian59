using System.Buffers.Binary;
using System.Text;

namespace Meridian.Protocol;

/// <summary>Baut einen Nachrichten-Payload (Type-Byte + Felder, Little Endian).</summary>
public sealed class PacketWriter
{
    // Original-Client und -Server arbeiten mit Windows-ANSI; Latin-1 deckt die üblichen Zeichen ab.
    public static readonly Encoding TextEncoding = Encoding.Latin1;

    private readonly List<byte> _buffer = new();

    public PacketWriter(byte type) => _buffer.Add(type);

    public PacketWriter(LoginMessage type) : this((byte)type) { }

    public PacketWriter U8(byte value)
    {
        _buffer.Add(value);
        return this;
    }

    public PacketWriter U16(ushort value)
    {
        Span<byte> tmp = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(tmp, value);
        _buffer.AddRange(tmp.ToArray());
        return this;
    }

    public PacketWriter U32(uint value)
    {
        Span<byte> tmp = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(tmp, value);
        _buffer.AddRange(tmp.ToArray());
        return this;
    }

    /// <summary>str: u16 Länge + Bytes, ohne Nullterminator.</summary>
    public PacketWriter Str(string value) => Bytes(TextEncoding.GetBytes(value));

    /// <summary>str aus Rohbytes (z. B. Passwort-Digest).</summary>
    public PacketWriter Bytes(ReadOnlySpan<byte> value)
    {
        U16(checked((ushort)value.Length));
        _buffer.AddRange(value.ToArray());
        return this;
    }

    public byte[] ToArray() => _buffer.ToArray();
}
