using System.Text.Json;
using Meridian.Protocol;

namespace Meridian.Server;

public sealed record Account(string Name, string PasswordDigestHex, ClientAccountType Type = ClientAccountType.Normal);

/// <summary>
/// Vorläufiger Account-Speicher als JSON-Datei. Gespeichert wird der Passwort-Digest des
/// Original-Protokolls (MD5, ungesalzen) – nur für Entwicklung, wird durch eine echte
/// Persistenz ersetzt (offene Entscheidung).
/// </summary>
public sealed class AccountStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _path;
    private readonly Dictionary<string, Account> _accounts;

    private AccountStore(string path, IEnumerable<Account> accounts)
    {
        _path = path;
        _accounts = accounts.ToDictionary(a => a.Name, StringComparer.OrdinalIgnoreCase);
    }

    public int Count => _accounts.Count;

    public static AccountStore Load(string path)
    {
        if (!File.Exists(path))
            return new AccountStore(path, []);
        var accounts = JsonSerializer.Deserialize<List<Account>>(File.ReadAllText(path), JsonOptions) ?? [];
        return new AccountStore(path, accounts);
    }

    public void AddOrUpdate(string name, string password, ClientAccountType type = ClientAccountType.Normal)
    {
        if (name.Length is 0 or > LoginRequest.MaxUsernameLength)
            throw new ArgumentException($"Name muss 1..{LoginRequest.MaxUsernameLength} Zeichen haben", nameof(name));
        _accounts[name] = new Account(name, Convert.ToHexString(PasswordDigest.Compute(password)), type);
        File.WriteAllText(_path, JsonSerializer.Serialize(_accounts.Values.ToList(), JsonOptions));
    }

    /// <summary>Account bei passendem Namen (ohne Groß/Klein) und Digest, sonst null.</summary>
    public Account? Verify(string name, ReadOnlySpan<byte> passwordDigest)
    {
        if (!_accounts.TryGetValue(name, out var account))
            return null;
        byte[] expected = Convert.FromHexString(account.PasswordDigestHex);
        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(expected, passwordDigest)
            ? account
            : null;
    }
}
