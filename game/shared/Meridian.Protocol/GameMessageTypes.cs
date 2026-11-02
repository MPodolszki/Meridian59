namespace Meridian.Protocol;

/// <summary>Nachrichtencodes im Game-Modus (include/proto.h, BP_*). Manche Codes gelten in beide Richtungen.</summary>
public enum GameMessage : byte
{
    EchoPing = 1,               // S→C
    Resync = 2,                 // beide
    Ping = 3,                   // C→S
    System = 6,                 // C→S, Unterbefehl folgt
    Logoff = 20,                // C→S (Original-Server ignoriert)
    Wait = 21,                  // S→C
    Unwait = 22,                // S→C
    ChangePassword = 23,        // C→S
    ChangeResource = 30,        // S→C
    SysMessage = 31,            // S→C
    Message = 32,               // S→C
    SendPlayer = 40,            // C→S
    SendStats = 41,
    SendRoomContents = 42,
    SendObjectContents = 43,
    SendPlayers = 44,
    SendCharacters = 45,
    UseCharacter = 46,
    SendSpells = 50,
    SendSkills = 51,
    SendStatGroups = 52,
    SendEnchantments = 53,
    ReqQuit = 54,
    CharInfoOk = 56,            // S→C
    CharInfoNotOk = 57,         // S→C
    LoadModule = 58,            // S→C
    UnloadModule = 59,          // S→C
    Action = 90,                // C→S
    ReqMove = 100,
    ReqTurn = 101,
    ReqGo = 102,
    ReqAttack = 103,
    ReqUse = 106,
    ReqUnuse = 107,
    SayTo = 110,
    SayGroup = 111,
    ReqPut = 112,
    ReqGet = 113,
    ReqCast = 105,
    ReqLook = 116,
    ReqActivate = 109,
    ReqInventory = 117,
    ReqDrop = 118,
    ReqOffer = 120,
    AcceptOffer = 121,
    CancelOffer = 122,
    ReqCounterOffer = 123,
    ReqBuy = 124,
    ReqBuyItems = 125,
    Player = 130,               // S→C
    Stat = 131,
    StatGroup = 132,
    StatGroups = 133,
    RoomContents = 134,
    ObjectContents = 135,
    Players = 136,
    PlayerAdd = 137,
    PlayerRemove = 138,
    Characters = 139,
    CharInfo = 140,
    Spells = 141,
    SpellAdd = 142,
    SpellRemove = 143,
    Skills = 144,
    AddEnchantment = 147,
    RemoveEnchantment = 148,
    Quit = 149,
    Background = 150,
    PlayerOverlay = 151,
    UserCommand = 155,          // beide
    Admin = 162,
    PlayWave = 170,
    SectorMove = 223,
    PlayMusic = 171,
    Move = 200,
    Turn = 201,
    Use = 203,
    Unuse = 204,
    UseList = 205,
    Said = 206,
    Look = 207,
    Inventory = 208,
    InventoryAdd = 209,
    InventoryRemove = 210,
    Offer = 211,
    OfferCanceled = 212,
    Offered = 213,
    CounterOffer = 214,
    CounterOffered = 215,
    BuyList = 216,
    Create = 217,
    Remove = 218,
    Change = 219,
    LightAmbient = 220,
    LightPlayer = 221,
    LightShading = 222,
    InvalidateData = 228,
}

/// <summary>Unterbefehle von BP_USERCOMMAND (UC_* aus include/proto.h), soweit benutzt.</summary>
public enum UserCommandType : byte
{
    Stand = 6,
    Safety = 7,                 // u8
    Deposit = 35,               // u32 Betrag
    Withdraw = 36,              // u32 Betrag
    Balance = 37,
}

/// <summary>SAY_* aus include/proto.h.</summary>
public enum SayType : byte
{
    Normal = 1,
    Yell = 2,
    Everyone = 3,
    Group = 4,
    Resource = 5,
    Emote = 6,
    Message = 7,
    DM = 9,
    Guild = 10,
}

/// <summary>ANIMATE_* aus include/proto.h.</summary>
public enum AnimationType : byte
{
    None = 1,
    Cycle = 2,
    Once = 3,
}
