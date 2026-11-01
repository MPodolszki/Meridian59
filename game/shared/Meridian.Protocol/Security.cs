using System.Security.Cryptography;

namespace Meridian.Protocol;

/// <summary>Passwort-Digest wie im Original: MD5, jedes 0x00-Byte wird 0x01 (protocol.md 3.2).</summary>
public static class PasswordDigest
{
    public static byte[] Compute(string password)
    {
        byte[] digest = MD5.HashData(PacketWriter.TextEncoding.GetBytes(password));
        for (int i = 0; i < digest.Length; i++)
            if (digest[i] == 0)
                digest[i] = 1;
        return digest;
    }
}

/// <summary>
/// Zufallsfolge für den Client→Server-Security-Hash im Game-Modus (protocol.md 2.4).
/// Server und Client schalten bei jeder gerahmten Game-Nachricht genau einmal weiter.
/// </summary>
public sealed class SecurityStream
{
    public const int StreamCount = 5;

    private readonly uint[] _seeds;

    public SecurityStream(ReadOnlySpan<uint> seeds)
    {
        if (seeds.Length != StreamCount)
            throw new ArgumentException($"Genau {StreamCount} Seeds erwartet", nameof(seeds));
        _seeds = seeds.ToArray();
    }

    public ReadOnlySpan<uint> Seeds => _seeds;

    public static uint[] CreateSeeds()
    {
        var seeds = new uint[StreamCount];
        RandomNumberGenerator.Fill(System.Runtime.InteropServices.MemoryMarshal.AsBytes(seeds.AsSpan()));
        return seeds;
    }

    public uint Step()
    {
        for (int i = 0; i < StreamCount; i++)
            _seeds[i] = unchecked(_seeds[i] * 9301u + 49297u) % 233280u;
        return _seeds[_seeds[4] % 4];
    }

    /// <summary>Erwarteter Wert im crc-Feld einer Client-Nachricht. Schaltet die Folge weiter.</summary>
    public ushort NextHash(ReadOnlySpan<byte> payload)
    {
        ushort h = (ushort)Step();
        h ^= (ushort)payload.Length;
        // Type-Byte wird im Original vorzeichenbehaftet erweitert (char → int).
        h ^= (ushort)((short)(sbyte)payload[0] << 4);
        h ^= (ushort)(Crc32.Compute(payload) & 0xFFFF);
        return h;
    }
}

/// <summary>
/// XOR-Obfuskation des Type-Bytes Server→Client ("Secure Token"/"Redbook", protocol.md 2.5).
/// Gleicher Algorithmus zum Ver- und Entschleiern.
/// </summary>
public sealed class ServerToken
{
    public const string DefaultRedbook = "BLAKSTON: Greenwich Q Zjiria";

    private uint _token;
    private byte[]? _redbook;
    private int _slide;

    /// <summary>Neuer Token nach BP_ECHO_PING; erst NACH dem Senden dieser Nachricht aufrufen.</summary>
    public void Reset(byte token, string redbook = DefaultRedbook)
    {
        _token = token;
        _redbook = PacketWriter.TextEncoding.GetBytes(redbook);
        _slide = 0;
    }

    public byte Apply(byte type)
    {
        byte result = (byte)(type ^ (byte)(_token & 0xFF));
        if (_redbook is { Length: > 0 })
        {
            _token += (uint)(_redbook[_slide] & 0x7F);
            _slide = (_slide + 1) % _redbook.Length;
        }
        return result;
    }
}
