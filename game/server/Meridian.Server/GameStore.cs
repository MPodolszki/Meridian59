using Microsoft.Data.Sqlite;

namespace Meridian.Server;

/// <summary>Gespeicherter Spielstand eines Charakters (Position, Lebenspunkte, Inventar, Konten).</summary>
public sealed record SavedCharacter
{
    public int? Room { get; init; }
    public ushort Row { get; init; }
    public ushort Col { get; init; }
    public ushort Angle { get; init; }
    public int? Hp { get; init; }
    public int? Vigor { get; init; }
    public int Stomach { get; init; }
    /// <summary>Charakterwerte, Mana und Sprüche (null = noch nie vergeben).</summary>
    public SavedMagic? Magic { get; init; }
    /// <summary>Name, Beschreibung, Aussehen aus der Charaktererstellung (null = Standard).</summary>
    public SavedProfile? Profile { get; init; }
    public List<SavedItem> Inventory { get; init; } = [];
    public Dictionary<int, int> Bank { get; init; } = [];
}

/// <summary>Magie eines Charakters (als JSON in characters.magic).</summary>
public sealed record SavedMagic(Attributes Attributes, int Karma, int Mana, int MaxMana,
    Dictionary<int, int> Spells, Dictionary<int, int> SchoolCasts)
{
    /// <summary>Fertigkeiten (SKID → Fähigkeit %); fehlt in älteren Ständen.</summary>
    public Dictionary<int, int> Skills { get; init; } = new();
    /// <summary>Persönliche Verzauberungen (SID → Zustand, Restdauer in ms); die Zeit läuft offline nicht weiter.</summary>
    public Dictionary<int, SavedEnchantment> Enchantments { get; init; } = new();
    /// <summary>Heimatstadt (Raum, negativ = fest gewählt); fehlt in älteren Ständen.</summary>
    public int? Hometown { get; init; }
}

public sealed record SavedEnchantment(int State, long RemainingMs);

/// <summary>Charakterprofil (als JSON in characters.profile).</summary>
public sealed record SavedProfile(string? Name, string Description, Appearance Appearance, bool Created);

/// <summary>Gegenstand per Schlüssel aus dem Katalog; Objekt-IDs werden beim Laden neu vergeben.</summary>
public sealed record SavedItem(string Item, int Number = 1, bool Used = false);

/// <summary>
/// Accounts und Spielstand in einer SQLite-Datenbank (eine Datei, kein Datenbankserver, läuft auch auf dem Pi).
/// Ein Charakter wird immer komplett in einer Transaktion geschrieben, ein Absturz hinterlässt also nie einen
/// halben Stand. Schema-Version in PRAGMA user_version. Siehe docs/decisions.md.
/// </summary>
public sealed class GameStore
{
    public const int SchemaVersion = 5;

    private readonly string _connectionString;
    private readonly object _write = new();

    public GameStore(string path)
    {
        Path = System.IO.Path.GetFullPath(path);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        _connectionString = new SqliteConnectionStringBuilder { DataSource = Path, Pooling = false }.ToString();
        Migrate();
    }

    public string Path { get; }

    private SqliteConnection Open()
    {
        var c = new SqliteConnection(_connectionString);
        c.Open();
        Execute(c, "PRAGMA foreign_keys = ON; PRAGMA journal_mode = WAL;");
        return c;
    }

    private void Migrate()
    {
        using var c = Open();
        long version = (long)Scalar(c, "PRAGMA user_version")!;
        if (version > SchemaVersion)
            throw new InvalidDataException($"{Path}: Schema-Version {version} ist neuer als dieser Server ({SchemaVersion})");
        if (version < 1)
        {
            using var tx = c.BeginTransaction();
            Execute(c, """
                CREATE TABLE characters (
                    id       INTEGER PRIMARY KEY,
                    room     INTEGER,
                    row      INTEGER NOT NULL,
                    col      INTEGER NOT NULL,
                    angle    INTEGER NOT NULL,
                    hp       INTEGER,
                    saved_at TEXT NOT NULL
                );
                CREATE TABLE inventory (
                    character_id INTEGER NOT NULL REFERENCES characters(id) ON DELETE CASCADE,
                    slot         INTEGER NOT NULL,
                    item         TEXT NOT NULL,
                    number       INTEGER NOT NULL,
                    PRIMARY KEY (character_id, slot)
                );
                CREATE TABLE bank_accounts (
                    character_id INTEGER NOT NULL REFERENCES characters(id) ON DELETE CASCADE,
                    bank         INTEGER NOT NULL,
                    amount       INTEGER NOT NULL,
                    PRIMARY KEY (character_id, bank)
                );
                PRAGMA user_version = 1;
                """, tx);
            tx.Commit();
        }
        if (version < 2)
        {
            using var tx = c.BeginTransaction();
            Execute(c, """
                CREATE TABLE accounts (
                    name          TEXT PRIMARY KEY COLLATE NOCASE,
                    password_md5  TEXT NOT NULL,
                    type          INTEGER NOT NULL,
                    character_id  INTEGER NOT NULL UNIQUE
                );
                PRAGMA user_version = 2;
                """, tx);
            tx.Commit();
        }
        if (version < 3)
        {
            using var tx = c.BeginTransaction();
            Execute(c, """
                ALTER TABLE characters ADD COLUMN vigor INTEGER;
                ALTER TABLE characters ADD COLUMN stomach INTEGER NOT NULL DEFAULT 0;
                ALTER TABLE inventory ADD COLUMN used INTEGER NOT NULL DEFAULT 0;
                PRAGMA user_version = 3;
                """, tx);
            tx.Commit();
        }
        if (version < 4)
        {
            using var tx = c.BeginTransaction();
            Execute(c, "ALTER TABLE characters ADD COLUMN magic TEXT; PRAGMA user_version = 4;", tx);
            tx.Commit();
        }
        if (version < 5)
        {
            using var tx = c.BeginTransaction();
            Execute(c, "ALTER TABLE characters ADD COLUMN profile TEXT; PRAGMA user_version = 5;", tx);
            tx.Commit();
        }
    }

    // ---------------------------------------------------------------- Accounts

    public List<Account> LoadAccounts()
    {
        using var c = Open();
        using var cmd = Command(c, "SELECT name, password_md5, type, character_id FROM accounts");
        using var r = cmd.ExecuteReader();
        var list = new List<Account>();
        while (r.Read())
            list.Add(new Account(r.GetString(0), r.GetString(1), (Meridian.Protocol.ClientAccountType)r.GetInt32(2), (uint)r.GetInt64(3)));
        return list;
    }

    public void SaveAccounts(IEnumerable<Account> accounts)
    {
        lock (_write)
        {
            using var c = Open();
            using var tx = c.BeginTransaction();
            foreach (var a in accounts)
                Execute(c, """
                    INSERT INTO accounts (name, password_md5, type, character_id) VALUES ($name, $pw, $type, $id)
                    ON CONFLICT(name) DO UPDATE SET password_md5 = $pw, type = $type;
                    """, tx, ("$name", a.Name), ("$pw", a.PasswordDigestHex), ("$type", (int)a.Type), ("$id", a.CharacterId));
            tx.Commit();
        }
    }

    /// <summary>Schreibt die Charaktere, jeden vollständig, alle in einer Transaktion.</summary>
    public void Save(IReadOnlyDictionary<uint, SavedCharacter> characters, DateTime now)
    {
        if (characters.Count == 0)
            return;
        lock (_write)
        {
            using var c = Open();
            using var tx = c.BeginTransaction();
            foreach (var (id, ch) in characters)
            {
                Execute(c, """
                    INSERT INTO characters (id, room, row, col, angle, hp, vigor, stomach, magic, profile, saved_at)
                    VALUES ($id, $room, $row, $col, $angle, $hp, $vigor, $stomach, $magic, $profile, $at)
                    ON CONFLICT(id) DO UPDATE SET room = $room, row = $row, col = $col, angle = $angle, hp = $hp,
                        vigor = $vigor, stomach = $stomach, magic = $magic, profile = $profile, saved_at = $at;
                    DELETE FROM inventory WHERE character_id = $id;
                    DELETE FROM bank_accounts WHERE character_id = $id;
                    """, tx,
                    ("$id", id), ("$room", ch.Room), ("$row", ch.Row), ("$col", ch.Col), ("$angle", ch.Angle),
                    ("$hp", ch.Hp), ("$vigor", ch.Vigor), ("$stomach", ch.Stomach),
                    ("$magic", ch.Magic is null ? null : System.Text.Json.JsonSerializer.Serialize(ch.Magic)),
                    ("$profile", ch.Profile is null ? null : System.Text.Json.JsonSerializer.Serialize(ch.Profile)), ("$at", now.ToString("O")));
                for (int slot = 0; slot < ch.Inventory.Count; slot++)
                    Execute(c, "INSERT INTO inventory (character_id, slot, item, number, used) VALUES ($id, $slot, $item, $number, $used)", tx,
                        ("$id", id), ("$slot", slot), ("$item", ch.Inventory[slot].Item), ("$number", ch.Inventory[slot].Number),
                        ("$used", ch.Inventory[slot].Used ? 1 : 0));
                foreach (var (bank, amount) in ch.Bank)
                    Execute(c, "INSERT INTO bank_accounts (character_id, bank, amount) VALUES ($id, $bank, $amount)", tx,
                        ("$id", id), ("$bank", bank), ("$amount", amount));
            }
            tx.Commit();
        }
    }

    public Dictionary<uint, SavedCharacter> LoadAll()
    {
        using var c = Open();
        var result = new Dictionary<uint, SavedCharacter>();
        using (var cmd = Command(c, "SELECT id, room, row, col, angle, hp, vigor, stomach, magic, profile FROM characters"))
        using (var r = cmd.ExecuteReader())
        {
            while (r.Read())
                result[(uint)r.GetInt64(0)] = new SavedCharacter
                {
                    Room = r.IsDBNull(1) ? null : r.GetInt32(1),
                    Row = (ushort)r.GetInt32(2),
                    Col = (ushort)r.GetInt32(3),
                    Angle = (ushort)r.GetInt32(4),
                    Hp = r.IsDBNull(5) ? null : r.GetInt32(5),
                    Vigor = r.IsDBNull(6) ? null : r.GetInt32(6),
                    Stomach = r.GetInt32(7),
                    Magic = r.IsDBNull(8) ? null : System.Text.Json.JsonSerializer.Deserialize<SavedMagic>(r.GetString(8)),
                    Profile = r.IsDBNull(9) ? null : System.Text.Json.JsonSerializer.Deserialize<SavedProfile>(r.GetString(9)),
                };
        }
        using (var cmd = Command(c, "SELECT character_id, item, number, used FROM inventory ORDER BY character_id, slot"))
        using (var r = cmd.ExecuteReader())
        {
            while (r.Read())
                if (result.TryGetValue((uint)r.GetInt64(0), out var ch))
                    ch.Inventory.Add(new SavedItem(r.GetString(1), r.GetInt32(2), r.GetInt32(3) != 0));
        }
        using (var cmd = Command(c, "SELECT character_id, bank, amount FROM bank_accounts"))
        using (var r = cmd.ExecuteReader())
        {
            while (r.Read())
                if (result.TryGetValue((uint)r.GetInt64(0), out var ch))
                    ch.Bank[r.GetInt32(1)] = r.GetInt32(2);
        }
        return result;
    }

    private static SqliteCommand Command(SqliteConnection c, string sql, SqliteTransaction? tx = null, params (string Name, object? Value)[] args)
    {
        var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        cmd.Transaction = tx;
        foreach (var (name, value) in args)
            cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        return cmd;
    }

    private static void Execute(SqliteConnection c, string sql, SqliteTransaction? tx = null, params (string, object?)[] args)
    {
        using var cmd = Command(c, sql, tx, args);
        cmd.ExecuteNonQuery();
    }

    private static object? Scalar(SqliteConnection c, string sql)
    {
        using var cmd = Command(c, sql);
        return cmd.ExecuteScalar();
    }
}
