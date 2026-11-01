using System.Buffers.Binary;

namespace Meridian.Protocol;

/// <summary>Liest Felder aus einem Payload. Wirft <see cref="ProtocolException"/> bei zu kurzen Daten.</summary>
public ref struct PacketReader
{
    private readonly ReadOnlySpan<byte> _data;
    private int _pos;

    public PacketReader(ReadOnlySpan<byte> payload)
    {
        _data = payload;
        _pos = 0;
    }

    public readonly int Remaining => _data.Length - _pos;

    public byte U8() => Take(1)[0];

    public ushort U16() => BinaryPrimitives.ReadUInt16LittleEndian(Take(2));

    public uint U32() => BinaryPrimitives.ReadUInt32LittleEndian(Take(4));

    public ReadOnlySpan<byte> Bytes() => Take(U16());

    public string Str() => PacketWriter.TextEncoding.GetString(Bytes());

    private ReadOnlySpan<byte> Take(int count)
    {
        if (count > Remaining)
            throw new ProtocolException($"Payload zu kurz: brauche {count} Byte, {Remaining} übrig");
        var slice = _data.Slice(_pos, count);
        _pos += count;
        return slice;
    }
}

public sealed class ProtocolException(string message) : Exception(message);
