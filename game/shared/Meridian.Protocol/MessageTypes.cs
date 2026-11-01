namespace Meridian.Protocol;

/// <summary>Nachrichtencodes im Login-/Synched-Modus (include/proto.h, AP_*).</summary>
public enum LoginMessage : byte
{
    // Client → Server
    Ping = 1,
    Login = 2,
    Register = 3,
    ReqGame = 4,
    ReqAdmin = 5,
    Resync = 6,           // beide Richtungen
    GetClient = 7,        // beide Richtungen
    GetResource = 8,
    GetAll = 9,
    ReqMenu = 10,
    AdminNote = 11,

    // Server → Client
    GetLogin = 21,
    GetChoice = 22,
    LoginOk = 23,
    LoginFailed = 24,
    Game = 25,
    Admin = 26,
    AccountUsed = 27,
    TooManyLogins = 28,
    Timeout = 29,
    Credits = 30,
    Download = 31,
    Upload = 32,
    NoCredits = 33,
    Message = 34,
    DeleteRsc = 35,
    DeleteAllRsc = 36,
    NoCharacters = 37,
    Guest = 38,
    ServiceReport = 39,
}

/// <summary>Aktion nach AP_MESSAGE (clientd3d/login.c).</summary>
public enum LoginAction : byte
{
    Nothing = 0,
    Logoff = 1,
}

/// <summary>Account-Typ in AP_LOGINOK, wie ihn der Client interpretiert (clientd3d/login.h).</summary>
public enum ClientAccountType : byte
{
    Normal = 0,
    Admin = 1,
    Guest = 2,
}
