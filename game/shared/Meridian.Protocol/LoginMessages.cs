namespace Meridian.Protocol;

/// <summary>AP_LOGIN (protocol.md 5.1). Die Systeminfo-Felder dienen im Original nur dem Logging.</summary>
public sealed record LoginRequest(
    byte VersionMajor,
    byte VersionMinor,
    uint OsType,
    string Username,
    byte[] PasswordDigest)
{
    public const int MaxUsernameLength = 50;
    public const int MaxPasswordLength = 32;

    /// <summary>Liest den Payload hinter dem Type-Byte.</summary>
    public static LoginRequest Parse(ReadOnlySpan<byte> payload)
    {
        var r = new PacketReader(payload[1..]);
        byte major = r.U8();
        byte minor = r.U8();
        uint osType = r.U32();
        r.U32(); r.U32(); r.U32(); r.U32();   // OS major/minor, RAM, CPU
        r.U16(); r.U16();                     // Bildschirmbreite/-höhe
        r.U32(); r.U32(); r.U32();            // Farbtiefe, Bandbreite, reserved
        string username = r.Str();
        byte[] password = r.Bytes().ToArray();

        if (username.Length > MaxUsernameLength || password.Length > MaxPasswordLength)
            throw new ProtocolException("Benutzername oder Passwort zu lang");
        return new LoginRequest(major, minor, osType, username, password);
    }

    /// <summary>Baut AP_LOGIN wie der Original-Client 7.37 (für Tests und den neuen Client).</summary>
    public byte[] ToPayload() => new PacketWriter(LoginMessage.Login)
        .U8(VersionMajor).U8(VersionMinor)
        .U32(OsType).U32(0).U32(0).U32(0).U32(0)
        .U16(0).U16(0)
        .U32(0).U32(0).U32(0)
        .Str(Username)
        .Bytes(PasswordDigest)
        .ToArray();
}

/// <summary>Fertige Server→Client-Payloads für den Login-Modus.</summary>
public static class LoginReplies
{
    public static byte[] GetLogin() => new PacketWriter(LoginMessage.GetLogin).ToArray();

    public static byte[] LoginOk(ClientAccountType type) =>
        new PacketWriter(LoginMessage.LoginOk).U8((byte)type).ToArray();

    public static byte[] LoginFailed() => new PacketWriter(LoginMessage.LoginFailed).ToArray();

    public static byte[] TooManyLogins() => new PacketWriter(LoginMessage.TooManyLogins).ToArray();

    public static byte[] Timeout() => new PacketWriter(LoginMessage.Timeout).ToArray();

    public static byte[] GetChoice(ReadOnlySpan<uint> seeds)
    {
        var w = new PacketWriter(LoginMessage.GetChoice);
        foreach (uint seed in seeds)
            w.U32(seed);
        return w.ToArray();
    }

    /// <summary>Der Original-Server sendet immer 20; der Client ignoriert den Wert.</summary>
    public static byte[] Credits(uint credits = 20) =>
        new PacketWriter(LoginMessage.Credits).U32(credits).ToArray();

    public static byte[] Message(string text, LoginAction action) =>
        new PacketWriter(LoginMessage.Message).Str(text).U8((byte)action).ToArray();
}
