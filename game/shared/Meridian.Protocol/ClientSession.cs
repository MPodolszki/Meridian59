using System.Collections.Concurrent;
using System.Net.Sockets;

namespace Meridian.Protocol;

/// <summary>Ein Objekt im aktuellen Raum aus Client-Sicht. Koordinaten in KOD-Feinkoordinaten.</summary>
public sealed class ClientObject(uint id)
{
    public uint Id => id;
    public uint NameRsc { get; set; }
    public uint IconRsc { get; set; }
    public ushort Row { get; set; }
    public ushort Col { get; set; }
    public ushort Angle { get; set; }
    public uint Flags { get; set; }
    /// <summary>Menge bei Mengen-Gegenständen (Tag 1 in der ID), sonst null.</summary>
    public uint? Amount { get; set; }
    /// <summary>Im Inventar: benutzt/ausgerüstet (BP_USE, BP_USE_LIST).</summary>
    public bool Used { get; set; }

    /// <summary>OF_PLAYER (include/proto.h) nicht gesetzt → Nicht-Spieler-Figur (NPC oder Monster).</summary>
    public bool IsNpc => (Flags & ObjectFlags.Player) == 0 && !IsAttackable && !IsGettable;

    /// <summary>OF_GETTABLE: Gegenstand auf dem Boden.</summary>
    public bool IsGettable => (Flags & ObjectFlags.Gettable) != 0;

    /// <summary>OF_BUYABLE: Händler, bei dem man kaufen kann.</summary>
    public bool IsSeller => (Flags & ObjectFlags.Buyable) != 0;

    /// <summary>OF_OFFERABLE: NPC, dem man etwas anbieten kann.</summary>
    public bool IsOfferable => (Flags & ObjectFlags.Offerable) != 0;

    /// <summary>OF_ATTACKABLE: Monster, die man angreifen kann.</summary>
    public bool IsAttackable => (Flags & 0x00000008) != 0;

    /// <summary>Icon-Ressource als Text (z. B. "zombie.bgf"); der Client wählt daran das Modell.</summary>
    public string Icon { get; set; } = "";
}

/// <summary>Ereignisse für die Oberfläche. Werden im Thread von <see cref="ClientSession.ProcessEvents"/> ausgelöst.</summary>
public abstract record ClientEvent
{
    public sealed record Status(string Text) : ClientEvent;
    public sealed record LoginFailed(string Reason) : ClientEvent;
    /// <summary>Raum betreten; RoomFile ist der Dateiname aus BP_PLAYER (z. B. "tos.roo").</summary>
    public sealed record EnteredRoom(uint PlayerId, string RoomName, string RoomFile = "") : ClientEvent;
    public sealed record ObjectAdded(ClientObject Object) : ClientEvent;
    public sealed record ObjectMoved(ClientObject Object) : ClientEvent;
    public sealed record ObjectRemoved(uint Id) : ClientEvent;
    public sealed record Chat(string Text) : ClientEvent;
    public sealed record HealthChanged(int Hp, int MaxHp) : ClientEvent;
    /// <summary>Umgebungslicht des Raums 0..255 (BP_PLAYER, BP_LIGHT_AMBIENT; Tageszeit).</summary>
    public sealed record AmbientLight(byte Light) : ClientEvent;
    /// <summary>Mana (BP_STAT Gruppe 1, Nr. 2).</summary>
    public sealed record ManaChanged(int Mana, int MaxMana) : ClientEvent;
    /// <summary>Bekannte Sprüche (BP_SPELLS, BP_SPELL_ADD, Fähigkeit aus Stat-Gruppe 3).</summary>
    public sealed record SpellsChanged(IReadOnlyList<ClientSpell> Spells) : ClientEvent;
    /// <summary>Ausdauer (BP_STAT Gruppe 1, Nr. 3).</summary>
    public sealed record VigorChanged(int Vigor, int MaxVigor) : ClientEvent;
    /// <summary>Einmalige Animation eines Objekts (BP_CHANGE): Angriff oder Sterben.</summary>
    public sealed record ObjectAnimated(uint Id, bool Death) : ClientEvent;
    public sealed record Disconnected(string Reason) : ClientEvent;
    /// <summary>Inventar hat sich geändert (BP_INVENTORY/_ADD/_REMOVE oder Mengenänderung).</summary>
    public sealed record InventoryChanged(IReadOnlyList<ClientObject> Items) : ClientEvent;
    /// <summary>Ladenliste eines Händlers (BP_BUY_LIST).</summary>
    public sealed record BuyList(ClientObject Seller, IReadOnlyList<BuyOption> Items) : ClientEvent;
    /// <summary>Eigene angebotene Gegenstände bestätigt (BP_OFFERED).</summary>
    public sealed record Offered(IReadOnlyList<ClientObject> Items) : ClientEvent;
    /// <summary>Gegenangebot (BP_COUNTEROFFER), bei Händlern Geld.</summary>
    public sealed record CounterOffer(IReadOnlyList<ClientObject> Items) : ClientEvent;
    public sealed record OfferCanceled : ClientEvent;
    /// <summary>Ein anderer Spieler bietet dir etwas an (BP_OFFER); antworten mit Gegenangebot oder Abbruch.</summary>
    public sealed record OfferReceived(ClientObject Offerer, IReadOnlyList<ClientObject> Items) : ClientEvent;
    /// <summary>Eigenes Gegenangebot bestätigt (BP_COUNTEROFFERED).</summary>
    public sealed record CounterOffered(IReadOnlyList<ClientObject> Items) : ClientEvent;
}

/// <summary>Spruch aus Sicht des Clients: Objekt (Name/Icon), Anzahl Ziele, Schule, Fähigkeit in %.</summary>
public sealed record ClientSpell(ClientObject Spell, int Targets, int School, int Ability);

/// <summary>Ware einer Ladenliste mit Preis pro Stück.</summary>
public sealed record BuyOption(ClientObject Item, uint Price);

/// <summary>
/// Hochsprachlicher Client für das Meridian-Protokoll ohne Abhängigkeit von Godot:
/// führt den Login-Ablauf wie der Original-Client durch, wählt den ersten Charakter,
/// hält den Raum-Zustand und pingt alle 5 s. Netzwerk läuft im Hintergrund; Ereignisse
/// werden gesammelt und erst in <see cref="ProcessEvents"/> (z. B. aus Godots _Process) ausgeliefert.
/// </summary>
public sealed class ClientSession : IDisposable
{
    public static readonly TimeSpan PingInterval = TimeSpan.FromSeconds(5);

    private readonly ConcurrentQueue<ClientEvent> _events = new();
    private readonly Dictionary<uint, string> _resources = new();
    private readonly Dictionary<uint, ClientObject> _objects = new();
    private readonly List<ClientObject> _inventory = new();
    private readonly List<ClientSpell> _spells = new();

    /// <summary>Bekannte Sprüche.</summary>
    public IReadOnlyList<ClientSpell> KnownSpells
    {
        get { lock (_spells) return _spells.ToList(); }
    }
    private readonly CancellationTokenSource _cts = new();
    private TcpClient? _tcp;
    private ClientConnection? _conn;
    private string _username = "";
    private string _password = "";
    private bool _charactersRequested;

    public uint PlayerId { get; private set; }
    public uint RoomId { get; private set; }
    public bool InWorld => PlayerId != 0;

    public event Action<ClientEvent>? Event;

    /// <summary>Aktuelles Inventar (nur im Thread von <see cref="ProcessEvents"/> bzw. über Ereignisse lesen).</summary>
    public IReadOnlyList<ClientObject> Inventory
    {
        get { lock (_inventory) return _inventory.ToList(); }
    }

    /// <summary>Anzeigename mit Menge, z. B. „3500 Schillinge“.</summary>
    public string Describe(ClientObject o) => o.Amount is { } n ? $"{n} {Resolve(o.NameRsc)}" : Resolve(o.NameRsc);

    /// <summary>Text einer Ressource (per BP_CHANGE_RESOURCE gelernt), sonst "#id".</summary>
    public string Resolve(uint rsc)
    {
        lock (_resources)
            return _resources.TryGetValue(rsc, out var s) ? s : $"#{rsc}";
    }

    public async Task ConnectAsync(string host, int port, string username, string password)
    {
        _username = username;
        _password = password;
        _tcp = new TcpClient { NoDelay = true };
        Enqueue(new ClientEvent.Status($"Verbinde mit {host}:{port} …"));
        await _tcp.ConnectAsync(host, port, _cts.Token);
        _conn = new ClientConnection(_tcp.GetStream());
        _ = Task.Run(ReceiveLoopAsync);
        _ = Task.Run(PingLoopAsync);
    }

    /// <summary>Liefert gesammelte Ereignisse im aufrufenden Thread aus.</summary>
    public void ProcessEvents()
    {
        while (_events.TryDequeue(out var e))
            Event?.Invoke(e);
    }

    public Task SayAsync(string text) =>
        SendGameAsync(new PacketWriter((byte)GameMessage.SayTo).U8((byte)SayType.Normal).Str(text).ToArray());

    public Task MoveAsync(ushort row, ushort col, bool running) =>
        SendGameAsync(new PacketWriter((byte)GameMessage.ReqMove)
            .U16(row).U16(col).U8(running ? (byte)36 : (byte)18).U32(RoomId).ToArray());

    public Task TurnAsync(ushort angle) =>
        SendGameAsync(new PacketWriter((byte)GameMessage.ReqTurn).U32(PlayerId).U16(angle).ToArray());

    /// <summary>„Gehen/Öffnen“ (Leertaste im Original): Ausgang oder Tür auf dem aktuellen Feld.</summary>
    public Task GoAsync() => SendGameAsync([(byte)GameMessage.ReqGo]);

    /// <summary>Objekt anschauen (BP_REQ_LOOK); die Antwort kommt als Chat-Nachricht.</summary>
    public Task LookAsync(uint id) => SendGameAsync(new PacketWriter((byte)GameMessage.ReqLook).U32(id).ToArray());

    /// <summary>Angriff (BP_REQ_ATTACK, attack_info 1 = Nahkampf).</summary>
    public Task AttackAsync(uint id) =>
        SendGameAsync(new PacketWriter((byte)GameMessage.ReqAttack).U8(1).U32(id).ToArray());

    public Task QuitAsync() => SendGameAsync([(byte)GameMessage.ReqQuit]);

    /// <summary>Spruchliste anfordern (BP_SEND_SPELLS); Antwort BP_SPELLS und Stat-Gruppe 3.</summary>
    public Task RequestSpellsAsync() => SendGameAsync([(byte)GameMessage.SendSpells]);

    /// <summary>Zaubern (BP_REQ_CAST): Spruch und Ziele (Spruch mit einem Ziel braucht genau eins).</summary>
    public Task CastAsync(uint spellId, IReadOnlyCollection<uint> targets) =>
        SendGameAsync(ObjectRef.WriteList(new PacketWriter((byte)GameMessage.ReqCast).U32(spellId),
            targets.Select(t => new ObjectRef(t, null)).ToList()).ToArray());

    /// <summary>Gegenstand benutzen (BP_REQ_USE): Essen essen, Waffe/Schild/Rüstung/Fackel anlegen.</summary>
    public Task UseAsync(uint id) => SendGameAsync(new PacketWriter((byte)GameMessage.ReqUse).U32(id).ToArray());

    /// <summary>Benutzten Gegenstand ablegen (BP_REQ_UNUSE).</summary>
    public Task UnuseAsync(uint id) => SendGameAsync(new PacketWriter((byte)GameMessage.ReqUnuse).U32(id).ToArray());

    /// <summary>Gegenstand vom Boden aufheben (BP_REQ_GET).</summary>
    public Task GetAsync(uint id) => SendGameAsync(new PacketWriter((byte)GameMessage.ReqGet).U32(id).ToArray());

    /// <summary>Gegenstände ablegen (BP_REQ_DROP).</summary>
    public Task DropAsync(IReadOnlyCollection<ObjectRef> items) =>
        SendGameAsync(ObjectRef.WriteList(new PacketWriter((byte)GameMessage.ReqDrop), items).ToArray());

    /// <summary>Ladenliste eines Händlers anfordern (BP_REQ_BUY).</summary>
    public Task RequestBuyAsync(uint seller) => SendGameAsync(new PacketWriter((byte)GameMessage.ReqBuy).U32(seller).ToArray());

    /// <summary>Waren kaufen (BP_REQ_BUY_ITEMS); Mengen-Gegenstände mit Anzahl.</summary>
    public Task BuyItemsAsync(uint seller, IReadOnlyCollection<ObjectRef> items) =>
        SendGameAsync(ObjectRef.WriteList(new PacketWriter((byte)GameMessage.ReqBuyItems).U32(seller), items).ToArray());

    /// <summary>Gegenstände anbieten (BP_REQ_OFFER), z. B. einem Händler zum Verkauf.</summary>
    public Task OfferAsync(uint taker, IReadOnlyCollection<ObjectRef> items) =>
        SendGameAsync(ObjectRef.WriteList(new PacketWriter((byte)GameMessage.ReqOffer).U32(taker), items).ToArray());

    /// <summary>Gegenangebot auf ein Angebot eines Spielers (BP_REQ_COUNTEROFFER); leer = Geschenk annehmen.</summary>
    public Task CounterOfferAsync(IReadOnlyCollection<ObjectRef> items) =>
        SendGameAsync(ObjectRef.WriteList(new PacketWriter((byte)GameMessage.ReqCounterOffer), items).ToArray());

    public Task AcceptOfferAsync() => SendGameAsync([(byte)GameMessage.AcceptOffer]);

    public Task CancelOfferAsync() => SendGameAsync([(byte)GameMessage.CancelOffer]);

    /// <summary>Bankbefehle (BP_USERCOMMAND UC_DEPOSIT/UC_WITHDRAW/UC_BALANCE) an einen Bankier im Raum.</summary>
    public Task DepositAsync(uint amount) =>
        SendGameAsync(new PacketWriter((byte)GameMessage.UserCommand).U8((byte)UserCommandType.Deposit).U32(amount).ToArray());

    public Task WithdrawAsync(uint amount) =>
        SendGameAsync(new PacketWriter((byte)GameMessage.UserCommand).U8((byte)UserCommandType.Withdraw).U32(amount).ToArray());

    public Task BalanceAsync() =>
        SendGameAsync(new PacketWriter((byte)GameMessage.UserCommand).U8((byte)UserCommandType.Balance).ToArray());

    private async Task SendGameAsync(byte[] payload)
    {
        if (_conn is { GameMode: true } && InWorld)
            await _conn.SendAsync(payload, _cts.Token);
    }

    private async Task ReceiveLoopAsync()
    {
        string reason = "Verbindung geschlossen";
        try
        {
            while (await _conn!.ReceiveAsync(_cts.Token) is { } frame)
            {
                if (_conn.GameMode || frame.Type == (byte)GameMessage.Quit)
                    await HandleGameAsync(frame);
                else
                    await HandleLoginAsync(frame);
            }
        }
        catch (OperationCanceledException) { reason = "Getrennt"; }
        catch (Exception ex) when (ex is IOException or ProtocolException or SocketException or ObjectDisposedException)
        {
            reason = ex.Message;
        }
        PlayerId = 0;
        Enqueue(new ClientEvent.Disconnected(reason));
    }

    private async Task PingLoopAsync()
    {
        using var timer = new PeriodicTimer(PingInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(_cts.Token))
                if (_conn is { GameMode: true })
                    await _conn.SendAsync([(byte)GameMessage.Ping], _cts.Token);
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException or ObjectDisposedException) { }
    }

    private async Task HandleLoginAsync(Frame frame)
    {
        var r = new PacketReader(frame.Payload.AsSpan(1));
        switch ((LoginMessage)frame.Type)
        {
            case LoginMessage.GetLogin:
                await _conn!.SendAsync(new LoginRequest(7, 37, 0, _username, PasswordDigest.Compute(_password)).ToPayload());
                break;
            case LoginMessage.LoginOk:
                Enqueue(new ClientEvent.Status("Angemeldet"));
                break;
            case LoginMessage.LoginFailed:
                Enqueue(new ClientEvent.LoginFailed("Name oder Passwort falsch"));
                break;
            case LoginMessage.TooManyLogins:
                Enqueue(new ClientEvent.LoginFailed("Zu viele Fehlversuche"));
                break;
            case LoginMessage.AccountUsed:
                Enqueue(new ClientEvent.LoginFailed("Account wird bereits benutzt"));
                break;
            case LoginMessage.Message:
                string text = r.Str();
                var action = (LoginAction)r.U8();
                if (action == LoginAction.Logoff)
                    Enqueue(new ClientEvent.LoginFailed(text));
                else
                    Enqueue(new ClientEvent.Status(text));
                break;
            case LoginMessage.GetChoice:
                // Der Original-Client geht nach dem Menü sofort ins Spiel (login.c EnterGame).
                _charactersRequested = false;
                await _conn!.SendAsync(new PacketWriter(LoginMessage.ReqGame).U32(0).U32(737 * 3 + 3).Str(Environment.MachineName).ToArray());
                break;
            case LoginMessage.Game:
                Enqueue(new ClientEvent.Status("Betrete das Spiel …"));
                break;
            case LoginMessage.NoCharacters:
                Enqueue(new ClientEvent.LoginFailed("Dieser Account hat keine Charaktere"));
                break;
        }
    }

    private async Task HandleGameAsync(Frame frame)
    {
        var r = new PacketReader(frame.Payload.AsSpan(1));
        switch ((GameMessage)frame.Type)
        {
            case GameMessage.ChangeResource:
            {
                uint id = r.U32();
                string value = r.Str();
                lock (_resources)
                    _resources[id] = value;
                break;
            }
            case GameMessage.LoadModule:
                // Erstes Modul nach AP_GAME ist das Charakter-Modul; es fordert die Liste an (char.c).
                if (!InWorld && !_charactersRequested)
                {
                    _charactersRequested = true;
                    await _conn!.SendAsync([(byte)GameMessage.SendCharacters]);
                }
                break;
            case GameMessage.Characters:
            {
                if (r.U16() == 0)
                {
                    Enqueue(new ClientEvent.LoginFailed("Keine Charaktere vorhanden"));
                    break;
                }
                uint id = r.U32();
                Enqueue(new ClientEvent.Status($"Wähle Charakter {r.Str()}"));
                await _conn!.SendAsync(new PacketWriter((byte)GameMessage.UseCharacter).U32(id).ToArray());
                break;
            }
            case GameMessage.Player:
            {
                PlayerId = r.U32();
                r.U32(); r.U32();               // Icon, Name
                RoomId = r.U32();
                uint roomFile = r.U32();
                uint roomName = r.U32();
                r.U32();                        // Security
                byte ambient = r.U8();
                ClearRoom();
                Enqueue(new ClientEvent.EnteredRoom(PlayerId, Resolve(roomName), Resolve(roomFile)));
                Enqueue(new ClientEvent.AmbientLight(ambient));
                break;
            }
            case GameMessage.RoomContents:
            {
                r.U32();
                int n = r.U16();
                for (int i = 0; i < n; i++)
                    AddObject(ReadRoomObject(ref r));
                break;
            }
            case GameMessage.Create:
                AddObject(ReadRoomObject(ref r));
                break;
            case GameMessage.Remove:
            {
                uint id = r.U32() & 0x0FFFFFFF;
                if (_objects.Remove(id))
                    Enqueue(new ClientEvent.ObjectRemoved(id));
                break;
            }
            case GameMessage.Move:
            {
                uint id = r.U32() & 0x0FFFFFFF;
                if (_objects.TryGetValue(id, out var o))
                {
                    o.Row = r.U16();
                    o.Col = r.U16();
                    Enqueue(new ClientEvent.ObjectMoved(o));
                }
                break;
            }
            case GameMessage.Turn:
            {
                uint id = r.U32() & 0x0FFFFFFF;
                if (_objects.TryGetValue(id, out var o))
                {
                    o.Angle = r.U16();
                    Enqueue(new ClientEvent.ObjectMoved(o));
                }
                break;
            }
            case GameMessage.Stat:
            {
                byte group = r.U8(), num = r.U8();
                r.U32();                                // Name
                if (r.U8() == 1 && r.U8() == 1 && group == 1 && num is 1 or 2 or 3)
                {
                    int value = (int)r.U32();
                    r.U32(); r.U32();                   // min, max (Balken)
                    int max = (int)r.U32();
                    Enqueue(num switch
                    {
                        1 => new ClientEvent.HealthChanged(value, max),
                        2 => new ClientEvent.ManaChanged(value, max),
                        _ => new ClientEvent.VigorChanged(value, max),
                    });
                }
                break;
            }
            case GameMessage.Change:
            {
                var o = ReadObjectDesc(ref r);
                if (PeekOnceAnimation(ref r) is { } low)
                    Enqueue(new ClientEvent.ObjectAnimated(o.Id, low >= 9));
                else
                    UpdateInventory(list => { int i = list.FindIndex(x => x.Id == o.Id); if (i >= 0) { o.Used = list[i].Used; list[i] = o; } });
                break;
            }
            case GameMessage.Inventory:
            {
                var items = ReadObjectList(ref r);
                UpdateInventory(list => { list.Clear(); list.AddRange(items); });
                break;
            }
            case GameMessage.InventoryAdd:
            {
                var o = ReadObjectDesc(ref r);
                UpdateInventory(list => { list.RemoveAll(x => x.Id == o.Id); list.Add(o); });
                break;
            }
            case GameMessage.Spells:
            {
                int n = r.U16();
                var list = new List<ClientSpell>(n);
                for (int i = 0; i < n; i++)
                {
                    var o = ReadObjectDesc(ref r);
                    list.Add(new ClientSpell(o, r.U8(), r.U8(), 0));
                }
                UpdateSpells(s => { s.Clear(); s.AddRange(list); });
                break;
            }
            case GameMessage.SpellAdd:
            {
                var o = ReadObjectDesc(ref r);
                var spell = new ClientSpell(o, r.U8(), r.U8(), 0);
                UpdateSpells(s => { s.RemoveAll(x => x.Spell.Id == o.Id); s.Add(spell); });
                break;
            }
            case GameMessage.StatGroup:
            {
                byte group = r.U8();
                int n = r.U8();
                if (group != 3)
                    break;
                var abilities = new Dictionary<uint, int>();
                for (int i = 0; i < n; i++)
                {
                    r.U8(); r.U32();                    // Nr., Name
                    if (r.U8() != 2)
                        break;                          // nur STATS_LIST
                    uint id = r.U32() & ObjectId.Mask;
                    abilities[id] = (int)r.U32();
                    r.U32();                            // Icon
                }
                UpdateSpells(s =>
                {
                    for (int i = 0; i < s.Count; i++)
                        if (abilities.TryGetValue(s[i].Spell.Id, out int ability))
                            s[i] = s[i] with { Ability = ability };
                });
                break;
            }
            case GameMessage.LightAmbient:
                Enqueue(new ClientEvent.AmbientLight(r.U8()));
                break;
            case GameMessage.Use:
            case GameMessage.Unuse:
            {
                uint id = r.U32() & ObjectId.Mask;
                bool used = (GameMessage)frame.Type == GameMessage.Use;
                UpdateInventory(list => { foreach (var o in list) if (o.Id == id) o.Used = used; });
                break;
            }
            case GameMessage.UseList:
            {
                int n = r.U16();
                var ids = new HashSet<uint>();
                for (int i = 0; i < n; i++)
                    ids.Add(r.U32() & ObjectId.Mask);
                UpdateInventory(list => { foreach (var o in list) o.Used = ids.Contains(o.Id); });
                break;
            }
            case GameMessage.InventoryRemove:
            {
                uint id = r.U32() & ObjectId.Mask;
                UpdateInventory(list => list.RemoveAll(x => x.Id == id));
                break;
            }
            case GameMessage.BuyList:
            {
                var seller = ReadObjectDesc(ref r);
                int n = r.U16();
                var options = new List<BuyOption>(n);
                for (int i = 0; i < n; i++)
                {
                    var item = ReadObjectDesc(ref r);
                    options.Add(new BuyOption(item, r.U32()));
                }
                Enqueue(new ClientEvent.BuyList(seller, options));
                break;
            }
            case GameMessage.Offered:
                Enqueue(new ClientEvent.Offered(ReadObjectList(ref r)));
                break;
            case GameMessage.CounterOffer:
                Enqueue(new ClientEvent.CounterOffer(ReadObjectList(ref r)));
                break;
            case GameMessage.Offer:
            {
                var offerer = ReadObjectDesc(ref r);
                Enqueue(new ClientEvent.OfferReceived(offerer, ReadObjectList(ref r)));
                break;
            }
            case GameMessage.CounterOffered:
                Enqueue(new ClientEvent.CounterOffered(ReadObjectList(ref r)));
                break;
            case GameMessage.OfferCanceled:
                Enqueue(new ClientEvent.OfferCanceled());
                break;
            case GameMessage.Said:
            {
                r.U32(); r.U32(); r.U8();
                Enqueue(new ClientEvent.Chat(FormatMessage(ref r)));
                break;
            }
            case GameMessage.Look:
            {
                ReadObjectDesc(ref r);
                byte flags = r.U8();
                Enqueue(new ClientEvent.Chat(FormatMessage(ref r)));
                _ = flags;   // Inschriften (DF_INSCRIBED) schickt der Server nicht
                break;
            }
            case GameMessage.Message:
            case GameMessage.SysMessage:
                Enqueue(new ClientEvent.Chat(FormatMessage(ref r)));
                break;
            case GameMessage.Quit:
                ClearRoom();
                PlayerId = 0;
                Enqueue(new ClientEvent.Status("Zurück im Menü"));
                break;
        }
    }

    /// <summary>Format-Ressource mit Parametern (protocol.md 4.8); unterstützt %q, %d/%i, %s (ohne Rekursion).</summary>
    private string FormatMessage(ref PacketReader r)
    {
        string fmt = Resolve(r.U32());
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < fmt.Length; i++)
        {
            if (fmt[i] != '%' || i + 1 >= fmt.Length || r.Remaining == 0 && fmt[i + 1] != '%')
            {
                sb.Append(fmt[i]);
                continue;
            }
            char spec = fmt[++i];
            switch (spec)
            {
                case 'q': sb.Append(r.Str()); break;
                case 'd': case 'i': sb.Append((int)r.U32()); break;
                case 's': sb.Append(Resolve(r.U32())); break;
                case '%': sb.Append('%'); break;
                default: sb.Append('%').Append(spec); break;
            }
        }
        return sb.ToString();
    }

    private List<ClientObject> ReadObjectList(ref PacketReader r)
    {
        int n = r.U16();
        var list = new List<ClientObject>(n);
        for (int i = 0; i < n; i++)
            list.Add(ReadObjectDesc(ref r));
        return list;
    }

    private void UpdateSpells(Action<List<ClientSpell>> change)
    {
        List<ClientSpell> snapshot;
        lock (_spells)
        {
            change(_spells);
            snapshot = _spells.ToList();
        }
        Enqueue(new ClientEvent.SpellsChanged(snapshot));
    }

    private void UpdateInventory(Action<List<ClientObject>> change)
    {
        List<ClientObject> snapshot;
        lock (_inventory)
        {
            change(_inventory);
            snapshot = _inventory.ToList();
        }
        Enqueue(new ClientEvent.InventoryChanged(snapshot));
    }

    private ClientObject ReadRoomObject(ref PacketReader r)
    {
        var o = ReadObjectDesc(ref r);
        o.Row = r.U16();
        o.Col = r.U16();
        o.Angle = r.U16();
        SkipAnimation(ref r);
        SkipOverlays(ref r);
        return o;
    }

    /// <summary>object_desc (protocol.md 4.5) mit Licht, Animation und Overlays.</summary>
    private ClientObject ReadObjectDesc(ref PacketReader r)
    {
        uint rawId = r.U32();
        var o = new ClientObject(rawId & ObjectId.Mask);
        if (rawId >> 28 == 1)
            o.Amount = r.U32();
        o.IconRsc = r.U32();
        o.Icon = Resolve(o.IconRsc);
        o.NameRsc = r.U32();
        o.Flags = r.U32();
        r.U32();                                // rarity
        if (r.U16() != 0) { r.U8(); r.U16(); }  // dlight
        SkipAnimation(ref r);
        SkipOverlays(ref r);
        return o;
    }

    /// <summary>Liest eine Animation; bei ANIMATE_ONCE die untere Gruppe, sonst null.</summary>
    private static ushort? PeekOnceAnimation(ref PacketReader r)
    {
        byte type = r.U8();
        if (type is 9 or 10)
        {
            r.U8();
            type = r.U8();
        }
        if ((AnimationType)type != AnimationType.Once)
            return null;
        r.U32();
        return r.U16();
    }

    private static void SkipOverlays(ref PacketReader r)
    {
        int n = r.U8();
        for (int i = 0; i < n; i++)
        {
            r.U32(); r.U8();                    // Icon, Hotspot
            SkipAnimation(ref r);
        }
    }

    /// <summary>Optionales Präfix (9 = Palette, 10 = Effekt) plus Animation (protocol.md 4.2).</summary>
    private static void SkipAnimation(ref PacketReader r)
    {
        byte type = r.U8();
        if (type is 9 or 10)
        {
            r.U8();
            type = r.U8();
        }
        switch ((AnimationType)type)
        {
            case AnimationType.None: r.U16(); break;
            case AnimationType.Cycle: r.U32(); r.U16(); r.U16(); break;
            case AnimationType.Once: r.U32(); r.U16(); r.U16(); r.U16(); break;
            default: throw new ProtocolException($"Unbekannter Animationstyp {type}");
        }
    }

    private void AddObject(ClientObject o)
    {
        _objects[o.Id] = o;
        Enqueue(new ClientEvent.ObjectAdded(o));
    }

    private void ClearRoom()
    {
        foreach (uint id in _objects.Keys)
            Enqueue(new ClientEvent.ObjectRemoved(id));
        _objects.Clear();
    }

    private void Enqueue(ClientEvent e) => _events.Enqueue(e);

    public void Dispose()
    {
        _cts.Cancel();
        _tcp?.Dispose();
    }
}
