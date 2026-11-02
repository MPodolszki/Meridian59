using System.Text.Json;
using Meridian.Protocol;

namespace Meridian.Server;

/// <summary>Account mit vorerst genau einem Charakter, der wie der Account heißt.</summary>
public sealed record Account(string Name, string PasswordDigestHex, ClientAccountType Type = ClientAccountType.Normal, uint CharacterId = 0);

/// <summary>
/// Accounts in der SQLite-Datenbank (Tabelle accounts, siehe <see cref="GameStore"/>), im Speicher gecacht.
/// Gespeichert wird der Passwort-Digest des Original-Protokolls (MD5, ungesalzen), weil der Original-Client
/// nur diesen schickt. Eine alte accounts.json wird beim ersten Öffnen übernommen.
/// </summary>
public sealed class AccountStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>Objekt-IDs der Charaktere beginnen hier (unter 2^28, Tag-Bits frei).</summary>
    public const uint FirstCharacterId = 1000;

    private readonly GameStore _store;
    private readonly Dictionary<string, Account> _accounts;
    private readonly object _gate = new();

    private AccountStore(GameStore store)
    {
        _store = store;
        _accounts = store.LoadAccounts().ToDictionary(a => a.Name, StringComparer.OrdinalIgnoreCase);
    }

    public int Count
    {
        get { lock (_gate) return _accounts.Count; }
    }

    /// <summary>Öffnet die Accounts in der Datenbank. Ist sie leer und gibt es <paramref name="importJson"/>,
    /// werden die Accounts daraus übernommen und die Datei in *.imported umbenannt.</summary>
    public static AccountStore Open(string databasePath, string? importJson = null) => Open(new GameStore(databasePath), importJson);

    public static AccountStore Open(GameStore store, string? importJson = null)
    {
        var accounts = new AccountStore(store);
        if (accounts.Count == 0 && importJson is not null && File.Exists(importJson))
        {
            var old = JsonSerializer.Deserialize<List<Account>>(File.ReadAllText(importJson), JsonOptions) ?? [];
            store.SaveAccounts(old);
            foreach (var a in old)
                accounts._accounts[a.Name] = a;
            File.Move(importJson, importJson + ".imported", overwrite: true);
            Log.Info($"{old.Count} Accounts aus {importJson} in die Datenbank übernommen");
        }
        return accounts;
    }

    public void AddOrUpdate(string name, string password, ClientAccountType type = ClientAccountType.Normal)
    {
        if (name.Length is 0 or > LoginRequest.MaxUsernameLength)
            throw new ArgumentException($"Name muss 1..{LoginRequest.MaxUsernameLength} Zeichen haben", nameof(name));
        lock (_gate)
        {
            uint characterId = _accounts.TryGetValue(name, out var existing)
                ? existing.CharacterId
                : Math.Max(FirstCharacterId, _accounts.Values.Select(a => a.CharacterId).DefaultIfEmpty(0u).Max() + 1);
            var account = new Account(existing?.Name ?? name, Convert.ToHexString(PasswordDigest.Compute(password)), type, characterId);
            _store.SaveAccounts([account]);
            _accounts[account.Name] = account;
        }
    }

    /// <summary>Ist der Name als Account eines anderen vergeben (Charakternamen müssen eindeutig sein)?</summary>
    public bool IsNameTaken(string name, string ownAccount)
    {
        lock (_gate)
            return _accounts.ContainsKey(name) && !string.Equals(name, ownAccount, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Account bei passendem Namen (ohne Groß/Klein) und Digest, sonst null.</summary>
    public Account? Verify(string name, ReadOnlySpan<byte> passwordDigest)
    {
        Account? account;
        lock (_gate)
            _accounts.TryGetValue(name, out account);
        if (account is null)
            return null;
        byte[] expected = Convert.FromHexString(account.PasswordDigestHex);
        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(expected, passwordDigest)
            ? account
            : null;
    }
}
