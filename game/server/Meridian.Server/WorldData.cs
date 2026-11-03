using System.Text.Json;

namespace Meridian.Server;

/// <summary>Weltbeschreibung aus JSON (z. B. data/world/tos.json, aus den KOD-Quellen abgeleitet).</summary>
public sealed record WorldDefinition(int StartRoom, IReadOnlyList<RoomDefinition> Rooms)
{
    /// <summary>Diese Gebietsdatei bestimmt den Startraum neuer Charaktere, wenn mehrere zusammengeführt werden.</summary>
    public bool Start { get; init; }
    /// <summary>Hier erwacht man nach dem Tod, sofern der Raum nichts anderes sagt (Standard: Startraum).</summary>
    public int? RespawnRoom { get; init; }

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    public static WorldDefinition Load(string path) =>
        JsonSerializer.Deserialize<WorldDefinition>(File.ReadAllText(path), Json)
        ?? throw new InvalidDataException($"{path}: leere Weltbeschreibung");

    /// <summary>Gebiete laden: eine Datei oder alle *.json eines Ordners (tos.json, raza.json, …), Räume
    /// zusammengeführt. Startraum aus der Datei mit "start": true (sonst der ersten).</summary>
    public static WorldDefinition LoadAll(string fileOrDirectory)
    {
        if (!Directory.Exists(fileOrDirectory))
            return Load(fileOrDirectory);
        var parts = AreaFiles(fileOrDirectory).Select(Load).ToList();
        if (parts.Count == 0)
            throw new InvalidDataException($"{fileOrDirectory}: keine Gebietsdateien");
        var rooms = parts.SelectMany(p => p.Rooms).ToList();
        if (rooms.GroupBy(r => r.Id).FirstOrDefault(g => g.Count() > 1) is { } dup)
            throw new InvalidDataException($"Raum {dup.Key} ist in mehreren Gebietsdateien definiert");
        return new WorldDefinition((parts.FirstOrDefault(p => p.Start) ?? parts[0]).StartRoom, rooms)
        {
            RespawnRoom = parts.Select(p => p.RespawnRoom).FirstOrDefault(r => r is not null),
        };
    }

    /// <summary>Gebietsdateien eines Ordners, sortiert (Dialoge/Läden heißen gleich, siehe Program.cs).</summary>
    public static IReadOnlyList<string> AreaFiles(string fileOrDirectory) => Directory.Exists(fileOrDirectory)
        ? Directory.GetFiles(fileOrDirectory, "*.json").Order(StringComparer.Ordinal).ToList()
        : [fileOrDirectory];

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
    /// <summary>Außenanteil 0..10 (KOD piOutside_factor, Standard OUTDOORS_COMPLETE): wie stark das Tageslicht wirkt.</summary>
    public int OutsideFactor { get; init; } = 10;
    public bool Outdoors { get; init; }
    /// <summary>Ankunftspunkt (KOD viTeleport_Row/Col).</summary>
    public GridPos? Teleport { get; init; }
    /// <summary>Security der .roo-Datei; wird beim Start aus der Client-Datei gelesen, falls verfügbar.</summary>
    public uint Security { get; init; }
    public IReadOnlyList<ExitDefinition> Exits { get; init; } = [];
    public IReadOnlyList<LockedDoorDefinition> LockedDoors { get; init; } = [];
    public IReadOnlyList<EdgeExitDefinition> EdgeExits { get; init; } = [];
    /// <summary>Zonen-Übergänge (KOD SomethingMoved mit UtilGoNearSquare): Betreten eines der Felder wechselt den Raum.</summary>
    public IReadOnlyList<RegionExitDefinition> RegionExits { get; init; } = [];
    public IReadOnlyList<PlacedObjectDefinition> Objects { get; init; } = [];
    public IReadOnlyList<NpcDefinition> Npcs { get; init; } = [];
    /// <summary>ROOM_NO_COMBAT: hier kann niemand angreifen (room.kod ReqSomethingAttack).</summary>
    public bool NoCombat { get; init; }
    /// <summary>Wer hier stirbt, erwacht in diesem Raum (Raza: Gasthaus, user.kod).</summary>
    public int? RespawnRoom { get; init; }
    /// <summary>ModifyChanceToImprove der Neulingsräume: Fähigkeiten nur bis zu diesem Wert, Chance × 3/2.</summary>
    public int? ImproveCap { get; init; }
    /// <summary>Feste Bewohner, die nicht nachwachsen (Kühe in Raza, CreateStandardObjects).</summary>
    public IReadOnlyList<FixedMonsterDefinition> FixedMonsters { get; init; } = [];
    /// <summary>Monsterräume (plGenerators/plMonsters): Spawnpunkte, Arten mit Gewicht, Höchstzahl.</summary>
    public IReadOnlyList<GridPos> Generators { get; init; } = [];
    public IReadOnlyList<MonsterTableEntry> Monsters { get; init; } = [];
    public int MaxMonsters { get; init; }
    /// <summary>monsroom.kod piGen_time (ms) und piGen_percent: alle GenTime ms mit GenPercent % Chance ein neues
    /// Monster; 0 = alte Regel (Raum füllt sich schnell zur Hälfte, dann alle 20 s).</summary>
    public int GenTime { get; init; }
    public int GenPercent { get; init; } = 100;
    /// <summary>piInit_count_min/max: so viele Monster beim Start.</summary>
    public int InitCountMin { get; init; } = 1;
    public int InitCountMax { get; init; } = 5;
    /// <summary>Raumrätsel: "mausoleum" (guest6.kod Hebel, Tür, Endkampf).</summary>
    public string? Puzzle { get; init; }
    /// <summary>Portal (newbport.kod): beim ersten Betreten Warnung, beim zweiten hinüber.</summary>
    public PortalDefinition? Portal { get; init; }
}

public sealed record PortalDestination(int Room, int Weight);

public sealed record FixedMonsterDefinition(string Kind, int Row, int Col);

public sealed record MonsterTableEntry(string Kind, int Weight);

public sealed record PortalDefinition(int Row, int Col, int ToRoom)
{
    /// <summary>SetRandomHomeroom: Ziele mit Gewicht (Marion 3, Cor 4, Jasper 3 von 10); nur vorhandene Räume zählen,
    /// sonst <see cref="ToRoom"/>.</summary>
    public IReadOnlyList<PortalDestination> Destinations { get; init; } = [];
    public string? Warning { get; init; }
    public string? Text { get; init; }
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
    /// <summary>Bedingter Rand (ROW/COL_IS_GREATER/LESS_THAN): gilt nur, wenn Zeile/Spalte darüber bzw. darunter liegt.</summary>
    public int? RowAbove { get; init; }
    public int? RowBelow { get; init; }
    public int? ColAbove { get; init; }
    public int? ColBelow { get; init; }

    public bool Applies(int row, int col) => (RowAbove is not { } ra || row > ra) && (RowBelow is not { } rb || row < rb)
        && (ColAbove is not { } ca || col > ca) && (ColBelow is not { } cb || col < cb);
}

/// <summary>Zone als Rechtecke [zeile1, spalte1, zeile2, spalte2]; Angle: Blickrichtung nach dem Wechsel
/// (EAST, SOUTH_WEST …), sonst unverändert.</summary>
public sealed record RegionExitDefinition(IReadOnlyList<int[]> Rects, int To, int ToRow, int ToCol)
{
    public string? Angle { get; init; }

    public bool Contains(int row, int col) => Rects.Any(q => row >= q[0] && row <= q[2] && col >= q[1] && col <= q[3]);
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
    /// <summary>Grafik aus KOD (vrIcon), z. B. "tsinnk.bgf".</summary>
    public string? Icon { get; init; }
    public int FineRow { get; init; } = 32;
    public int FineCol { get; init; } = 32;
    public int Angle { get; init; }
    /// <summary>Eigener Text beim Anschauen (Schilder); sonst aus dem Dialog der Klasse.</summary>
    public string? Description { get; init; }
}
