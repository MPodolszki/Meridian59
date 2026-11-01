using System.Collections.Concurrent;

namespace Meridian.Server;

/// <summary>
/// Vergibt Ressourcen-IDs für Strings (Namen, Texte, Dateinamen von Modulen/Räumen/Grafiken).
/// Der Server schickt sie dem Client per BP_CHANGE_RESOURCE, bevor er sie benutzt. So hängt der
/// Server nicht von den kompilierten .rsc-Dateien des Originals ab.
/// </summary>
public sealed class ResourceRegistry
{
    // Weit oberhalb der IDs, die der Original-Compiler vergibt, um Kollisionen zu vermeiden.
    public const uint FirstId = 0x00F00000;

    private readonly ConcurrentDictionary<string, uint> _ids = new(StringComparer.Ordinal);
    private int _next = (int)FirstId;

    public uint this[string value] => _ids.GetOrAdd(value, _ => (uint)Interlocked.Increment(ref _next));
}
