using System.Text.Json;

namespace Meridian.Server;

/// <summary>Weltbeschreibung aus JSON (z. B. data/world/tos.json, aus den KOD-Quellen abgeleitet).</summary>
public sealed record WorldDefinition(int StartRoom, IReadOnlyList<RoomDefinition> Rooms)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    public static WorldDefinition Load(string path) =>
        JsonSerializer.Deserialize<WorldDefinition>(File.ReadAllText(path), Json)
        ?? throw new InvalidDataException($"{path}: leere Weltbeschreibung");

    /// <summary>Ein einzelner Raum ohne Ausgänge (Standard, wenn keine Weltdatei angegeben ist).</summary>
    public static WorldDefinition SingleRoom(string roo = "tosinn.roo", string name = "Gasthaus") =>
        new(1, [new RoomDefinition(1, name, roo) { Teleport = new GridPos(5, 5) }]);
}

public sealed record GridPos(int Row, int Col);

/// <summary>Raum. Koordinaten sind Grid-Zeile/-Spalte wie in KOD (1-basiert).</summary>
public sealed record RoomDefinition(int Id, string Name, string Roo)
{
    public string? Constant { get; init; }
    public string? KodClass { get; init; }
    public string? Music { get; init; }
    /// <summary>Größe in Feldern (aus der .roo); nötig für Ausgänge am Raumrand.</summary>
    public int? Rows { get; init; }
    public int? Cols { get; init; }
    public int? BaseLight { get; init; }
    public bool Outdoors { get; init; }
    /// <summary>Ankunftspunkt (KOD viTeleport_Row/Col).</summary>
    public GridPos? Teleport { get; init; }
    /// <summary>Security der .roo-Datei; wird beim Start aus der Client-Datei gelesen, falls verfügbar.</summary>
    public uint Security { get; init; }
    public IReadOnlyList<ExitDefinition> Exits { get; init; } = [];
    public IReadOnlyList<LockedDoorDefinition> LockedDoors { get; init; } = [];
    public IReadOnlyList<EdgeExitDefinition> EdgeExits { get; init; } = [];
    public IReadOnlyList<PlacedObjectDefinition> Objects { get; init; } = [];
    public IReadOnlyList<NpcDefinition> Npcs { get; init; } = [];
}

public sealed record ExitDefinition(int Row, int Col, int To, int ToRow, int ToCol)
{
    public string? ToConstant { get; init; }
    public string? Rotate { get; init; }
}

public sealed record LockedDoorDefinition(int Row, int Col)
{
    public string? Message { get; init; }
}

public sealed record EdgeExitDefinition(string Edge, int To, int ToRow, int ToCol)
{
    public string? ToConstant { get; init; }
    public string? Rotate { get; init; }
}

public sealed record PlacedObjectDefinition(string Type, int Row, int Col)
{
    public int FineRow { get; init; } = 32;
    public int FineCol { get; init; } = 32;
    public int Angle { get; init; }
}

public sealed record NpcDefinition(string KodClass, int Row, int Col)
{
    public string? Name { get; init; }
    public string? Role { get; init; }
    public int FineRow { get; init; } = 32;
    public int FineCol { get; init; } = 32;
    public int Angle { get; init; }
}
