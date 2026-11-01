namespace Meridian.Server;

/// <summary>Liest Kopfdaten einer .roo-Raumdatei des Original-Clients (clientd3d/bspload.c).</summary>
public static class RooFile
{
    private static readonly byte[] Magic = [0x52, 0x4F, 0x4F, 0xB1];   // "ROO" + 0xB1

    /// <summary>Security-Wert: u32 an Offset 8 (nach Magic und Version).</summary>
    public static uint ReadSecurity(string path)
    {
        using var f = File.OpenRead(path);
        Span<byte> header = stackalloc byte[12];
        f.ReadExactly(header);
        if (!header[..4].SequenceEqual(Magic))
            throw new InvalidDataException($"{path} ist keine .roo-Datei");
        return BitConverter.ToUInt32(header[8..]);
    }
}
