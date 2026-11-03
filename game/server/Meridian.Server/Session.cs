using System.Net.Sockets;
using System.Threading.Channels;
using Meridian.Protocol;

namespace Meridian.Server;

/// <summary>
/// Eine Client-Verbindung (protocol.md 1.2/3.1).
/// Login-Modus: AP_GETLOGIN → AP_LOGIN → AP_LOGINOK, AP_GETCHOICE, AP_CREDITS → AP_REQ_GAME → AP_GAME.
/// Game-Modus: Charakterwahl, Raum betreten, Ping, Bewegung, Chat, BP_REQ_QUIT zurück ins Menü.
/// Alle ausgehenden Nachrichten laufen in Reihenfolge durch eine Queue, weil der Secure-Token
/// pro gesendeter Nachricht weiterwandert.
/// </summary>
public sealed class Session(int id, TcpClient client, GameServer server)
{
    public const string CharModule = "char.dll";
    public const string InterfaceModule = "merintr.dll";
    public const string Motd = "Willkommen auf dem neuen Meridian-Server!";

    /// <summary>Epoche im Game-Modus. Der Server hat noch keine Garbage Collection, sie bleibt konstant.</summary>
    public const byte Epoch = 1;

    private enum Mode { Synched, Game }

    private readonly NetworkStream _stream = client.GetStream();
    private readonly Channel<Outgoing> _outbox = Channel.CreateUnbounded<Outgoing>(new() { SingleReader = true });
    private readonly HashSet<string> _sentResources = new(StringComparer.Ordinal);
    private readonly GameCodec _codec = new();
    private volatile Mode _mode = Mode.Synched;
    private Account? _account;
    private int _failedLogins;
    private Avatar? _avatar;

    /// <summary>Ausgehende Nachricht. Epoch: 0 im Login-Modus, <see cref="Epoch"/> im Game-Modus.</summary>
    private sealed record Outgoing(byte[] Payload, byte Epoch, byte? NewToken = null);

    /// <summary>Jede ankommende Game-Nachricht protokollieren (Umgebungsvariable MERIDIAN_TRACE=1).</summary>
    private static readonly bool Trace = Environment.GetEnvironmentVariable("MERIDIAN_TRACE") == "1";

    public int Id => id;
    public string? AccountName => _account?.Name;

    public async Task RunAsync(CancellationToken serverCt)
    {
        Log.Info($"Session {id}: Verbindung von {client.Client.RemoteEndPoint}");
        using var idle = CancellationTokenSource.CreateLinkedTokenSource(serverCt);
        idle.CancelAfter(server.Options.MenuTimeout);
        var writer = Task.Run(() => WriteLoopAsync(serverCt), CancellationToken.None);
        var reader = new FrameReader(_stream, Framing.MaxClientPayload);

        try
        {
            PostLogin(LoginReplies.GetLogin());
            while (await reader.ReadAsync(idle.Token) is { } frame)
            {
                idle.CancelAfter(_mode == Mode.Game ? server.Options.GameTimeout : server.Options.MenuTimeout);
                if (frame.Header.Length == 0)
                    continue;   // Original ignoriert leere Nachrichten
                bool keepGoing = _mode == Mode.Game ? HandleGameFrame(frame) : HandleSynchedFrame(frame);
                if (!keepGoing)
                    break;
            }
        }
        catch (OperationCanceledException) when (!serverCt.IsCancellationRequested)
        {
            Log.Info($"Session {id}: Timeout");
            if (_mode == Mode.Synched)
                PostLogin(LoginReplies.Timeout());
        }
        catch (OperationCanceledException) { }
        catch (ProtocolException ex)
        {
            // Resync (Beacon-Handshake) ist noch nicht implementiert → Verbindung trennen.
            Log.Warn($"Session {id}: Protokollfehler: {ex.Message}");
        }
        catch (IOException) { }
        catch (ObjectDisposedException) { }
        finally
        {
            LeaveWorld();
            _outbox.Writer.TryComplete();
            await writer;
            Close();
            Log.Info($"Session {id}: getrennt");
        }
    }

    // ---------------------------------------------------------------- Login-Modus

    private bool HandleSynchedFrame(Frame frame)
    {
        if (frame.Header.Epoch != 0)
            throw new ProtocolException($"Epoch {frame.Header.Epoch} im Login-Modus");

        var type = (LoginMessage)frame.Type;

        // Vor erfolgreichem Login wird alles außer AP_LOGIN ignoriert (synched.c).
        if (_account is null && type != LoginMessage.Login)
            return true;

        switch (type)
        {
            case LoginMessage.Login:
                return HandleLogin(LoginRequest.Parse(frame.Payload));

            case LoginMessage.ReqMenu:
                SendMenu();
                return true;

            case LoginMessage.ReqGame:
                EnterGameMode();
                return true;

            case LoginMessage.Ping:
                return true;

            default:
                Log.Info($"Session {id}: Login-Nachricht {type} ({frame.Type}) wird nicht unterstützt");
                return true;
        }
    }

    private bool HandleLogin(LoginRequest login)
    {
        if (_account is not null)
            return true;   // schon angemeldet

        var account = server.Accounts.Verify(login.Username, login.PasswordDigest);
        if (account is null)
        {
            _failedLogins++;
            Log.Info($"Session {id}: Login fehlgeschlagen für '{login.Username}' ({_failedLogins}. Versuch)");
            if (_failedLogins >= server.Options.MaxLoginAttempts)
            {
                PostLogin(LoginReplies.TooManyLogins());
                return false;
            }
            PostLogin(LoginReplies.LoginFailed());
            return true;
        }

        _account = account;
        server.ClaimAccount(account.Name, this);
        Log.Info($"Session {id}: {account.Name} angemeldet (Client {login.VersionMajor}.{login.VersionMinor})");
        PostLogin(LoginReplies.LoginOk(account.Type));
        SendMenu();
        return true;
    }

    private void SendMenu()
    {
        // Neue Seeds für den Security-Hash im Game-Modus (protocol.md 2.4).
        _codec.Security = new SecurityStream(SecurityStream.CreateSeeds());
        PostLogin(LoginReplies.GetChoice(_codec.Security.Seeds));
        PostLogin(LoginReplies.Credits());
    }

    private void EnterGameMode()
    {
        Log.Info($"Session {id}: {_account!.Name} betritt den Game-Modus");
        PostLogin(new PacketWriter(LoginMessage.Game).ToArray());
        _mode = Mode.Game;
        // Der Client lädt beim Eintritt in STATE_GAME seine Ressourcen neu → dynamische neu senden.
        lock (_sentResources)
            _sentResources.Clear();
        EnsureResource(CharModule);
        PostGame(GameReplies.LoadModule(server.Resources[CharModule]));
    }

    // ---------------------------------------------------------------- Game-Modus

    private bool HandleGameFrame(Frame frame)
    {
        if (!_codec.Verify(frame))
        {
            // Original: LogSpoofs/HangupSpoofs sind standardmäßig an.
            Log.Warn($"Session {id}: Security-Hash falsch (Nachricht {frame.Type}) – trenne");
            return false;
        }
        if (Trace && frame.Type != (byte)GameMessage.Ping)
            Log.Info($"Session {id}: ← {(GameMessage)frame.Type} ({frame.Type}), Epoche {frame.Header.Epoch}, {frame.Payload.Length} Byte");
        if (frame.Header.Epoch != Epoch)
            return true;   // veraltete Epoche: still verwerfen (game.c)

        var type = (GameMessage)frame.Type;
        var r = new PacketReader(frame.Payload.AsSpan(1));
        switch (type)
        {
            case GameMessage.Ping:
                SendEchoPing();
                break;

            case GameMessage.SendCharacters:
            {
                uint cid = _account!.CharacterId;
                PostGame(GameReplies.Characters(
                    [new CharacterEntry(cid, server.World.CharacterName(cid) ?? _account.Name, server.World.IsFirstTime(cid))], Motd));
                break;
            }

            // Charaktererstellung des Original-Clients (char.dll) über BP_SYSTEM.
            case GameMessage.System:
                HandleSystem(ref r);
                break;

            case GameMessage.UseCharacter:
                UseCharacter(r.U32() & 0x0FFFFFFF);
                break;

            case GameMessage.SendPlayer:
                if (_avatar is not null)
                    SendPlayer();
                break;

            case GameMessage.SendRoomContents:
                if (_avatar is not null)
                    SendRoomContents();
                break;

            case GameMessage.ReqInventory:
                if (_avatar is not null)
                    SendInventory();
                break;

            // Abfragen des Original-Clients nach dem Betreten (user.kod).
            case GameMessage.SendStatGroups when _avatar is not null:
                foreach (string g in StatGroupNames)
                    EnsureResource(g);
                PostGame(GameReplies.StatGroups(StatGroupNames.Select(g => server.Resources[g]).ToList()));
                break;

            case GameMessage.SendStats when _avatar is not null:
                SendStatGroup(r.U8());
                break;

            case GameMessage.SendSpells when _avatar is not null:
                SendSpells();
                break;

            case GameMessage.ReqCast when _avatar is not null:
            {
                uint spell = r.U32() & ObjectId.Mask;
                server.World.Cast(_avatar, spell, ObjectRef.ReadList(ref r));
                break;
            }

            case GameMessage.SendSkills when _avatar is not null:
                PostGame(GameReplies.Skills(server.World.KnownSkills(_avatar).Select(k => server.World.ToSkillObject(this, k.Skill)).ToList()));
                SendSkillStats();
                break;

            case GameMessage.SendEnchantments when _avatar is not null:
                // user.kod SendEnchantmentIcons: nur Typ 1 (eigene Verzauberungen); Raumverzauberungen gibt es nicht.
                if (r.Remaining == 0 || r.U8() == GameReplies.EnchantmentPlayer)
                    foreach (var o in server.World.EnchantmentObjects(_avatar))
                        PostGame(GameReplies.AddEnchantment(o));
                break;

            case GameMessage.ReqUse when _avatar is not null:
                server.World.Use(_avatar, r.U32() & ObjectId.Mask);
                break;

            case GameMessage.ReqUnuse when _avatar is not null:
                server.World.Unuse(_avatar, r.U32() & ObjectId.Mask);
                break;

            case GameMessage.ReqGet when _avatar is not null:
                server.World.Get(_avatar, r.U32() & ObjectId.Mask);
                break;

            case GameMessage.ReqDrop when _avatar is not null:
                server.World.Drop(_avatar, ObjectRef.ReadList(ref r));
                break;

            case GameMessage.ReqBuy when _avatar is not null:
                server.World.RequestBuyList(_avatar, r.U32() & ObjectId.Mask);
                break;

            case GameMessage.ReqBuyItems when _avatar is not null:
            {
                uint seller = r.U32() & ObjectId.Mask;
                server.World.BuyItems(_avatar, seller, ObjectRef.ReadList(ref r));
                break;
            }

            case GameMessage.ReqOffer when _avatar is not null:
            {
                uint taker = r.U32() & ObjectId.Mask;
                server.World.Offer(_avatar, taker, ObjectRef.ReadList(ref r));
                break;
            }

            case GameMessage.ReqCounterOffer when _avatar is not null:
                server.World.CounterOffer(_avatar, ObjectRef.ReadList(ref r));
                break;

            case GameMessage.AcceptOffer when _avatar is not null:
                server.World.AcceptOffer(_avatar);
                break;

            case GameMessage.CancelOffer when _avatar is not null:
                server.World.CancelOffer(_avatar);
                break;

            case GameMessage.UserCommand when _avatar is not null:
            {
                var command = (UserCommandType)r.U8();
                switch (command)
                {
                    case UserCommandType.Deposit:
                    case UserCommandType.Withdraw:
                        server.World.BankCommand(_avatar, command, (int)Math.Min(r.U32(), int.MaxValue));
                        break;
                    case UserCommandType.Balance:
                        server.World.BankCommand(_avatar, command);
                        break;
                    case UserCommandType.Stand:
                    case UserCommandType.Safety:
                        break;   // Ausruhen und PvP-Sicherheit gibt es noch nicht
                    default:
                        Log.Info($"Session {id}: Benutzerbefehl {command} wird noch nicht unterstützt");
                        break;
                }
                break;
            }

            case GameMessage.ReqMove when _avatar is not null:
            {
                ushort row = r.U16(), col = r.U16();
                byte speed = r.U8();
                switch (server.World.Move(_avatar, row, col, speed))
                {
                    case MoveResult.ChangedRoom edge:
                        Log.Info($"Session {id}: {_avatar.Name} → {edge.Room.Definition.Name} (Rand)");
                        NotifyRoomChanged();
                        break;
                    case MoveResult.Blocked edgeBlocked:
                        SendText(edgeBlocked.Message ?? "Hier geht es nicht weiter.");
                        break;
                }
                break;
            }

            case GameMessage.ReqGo when _avatar is not null:
                switch (server.World.TryGo(_avatar))
                {
                    case MoveResult.ChangedRoom changed:
                        Log.Info($"Session {id}: {_avatar.Name} → {changed.Room.Definition.Name}");
                        SendPlayer();
                        SendRoomContents();
                        break;
                    case MoveResult.Blocked blocked:
                        SendText(blocked.Message ?? "Hier geht es nicht weiter.");
                        break;
                }
                break;

            case GameMessage.ReqAttack when _avatar is not null:
                r.U8();   // attack_info
                server.World.Attack(_avatar, r.U32() & 0x0FFFFFFF, DateTime.UtcNow);
                break;

            case GameMessage.ReqActivate when _avatar is not null:
                server.World.Activate(_avatar, r.U32() & ObjectId.Mask);
                break;

            case GameMessage.ReqLook when _avatar is not null:
                if (server.World.LookAt(_avatar, r.U32() & 0x0FFFFFFF) is { } look)
                {
                    EnsureResource("%q");
                    PostGame(GameReplies.Look(look.Object, server.Resources["%q"], look.Text));
                }
                break;

            case GameMessage.ReqTurn when _avatar is not null:
                r.U32();   // eigene ID
                server.World.Turn(_avatar, r.U16());
                break;

            case GameMessage.SayTo when _avatar is not null:
            {
                var sayType = (SayType)r.U8();
                string text = r.Str();
                if (text.Length > 0)
                {
                    Log.Info($"Session {id}: {_avatar.Name} sagt: {text}");
                    server.World.Say(_avatar, sayType, text);
                }
                break;
            }

            case GameMessage.ReqQuit:
                LeaveWorld();
                PostGame(GameReplies.Quit());
                _mode = Mode.Synched;
                SendMenu();
                break;

            default:
                Log.Info($"Session {id}: Game-Nachricht {type} ({frame.Type}) wird noch nicht unterstützt");
                break;
        }
        return true;
    }

    /// <summary>BP_SYSTEM: 49 = BP_SEND_CHARINFO (Auswahllisten), 48 = BP_NEW_CHARINFO (neuer Charakter).</summary>
    private void HandleSystem(ref PacketReader r)
    {
        byte sub = r.U8();
        if (_avatar is not null || _account is null)
            return;
        if (sub == CharacterCreation.SendCharInfo)
        {
            uint Rsc(string s)
            {
                EnsureResource(s);
                return server.Resources[s];
            }
            PostGame(CharacterCreation.CharInfo(Rsc, server.World.Spells.All.OrderBy(s => s.School).ThenBy(s => s.Level).ToList(),
                server.World.Skills.All.Where(s => s.Level <= 2).OrderBy(s => s.Skid).ToList()));
            return;
        }
        if (sub != CharacterCreation.NewCharInfo)
            return;
        var req = CharacterCreation.Parse(ref r);
        bool ok = req.CharacterId == _account.CharacterId
            && server.World.CreateCharacter(req, n => server.Accounts.IsNameTaken(n, _account.Name));
        Log.Info($"Session {id}: Charaktererstellung {(ok ? "angenommen" : "abgelehnt")} für '{req.Name}'");
        PostGame(ok ? CharacterCreation.CharInfoOk(req.CharacterId) : CharacterCreation.CharInfoNotOk());
        if (ok)
            server.SaveGame("neuer Charakter");
    }

    private void UseCharacter(uint characterId)
    {
        if (characterId != _account!.CharacterId || _avatar is not null)
            return;

        string name = server.World.CharacterName(characterId) ?? _account.Name;
        Log.Info($"Session {id}: {name} betritt die Welt");
        _avatar = new Avatar(characterId, name, this);
        EnsureResource(InterfaceModule);
        PostGame(GameReplies.LoadModule(server.Resources[InterfaceModule]));
        // Grafiken der Spielerfiguren (Körperteile, Waffen in der Hand) für alle Spieler im Raum.
        foreach (string icon in CharacterCreation.AllIcons
                     .Concat(server.World.Items.Kinds.SelectMany(k => new[] { k.Overlay, k.LegsMale, k.LegsFemale }).OfType<string>()))
            EnsureResource(icon);

        server.World.Enter(_avatar);
        SendPlayer();
        SendRoomContents();
        SendHealth();
        SendVigor();
        SendMana();
        SendInventory();
        SendEchoPing();
    }

    private void SendPlayer()
    {
        var room = _avatar!.Room.Definition;
        string icon = server.World.PlayerIcon;
        EnsureResource(room.Roo);
        EnsureResource(room.Name);
        EnsureResource(icon);
        EnsureResource(_avatar.Name);
        var res = server.Resources;
        byte light = server.World.RoomLight(_avatar.Room);
        PostGame(GameReplies.Player(new PlayerInfo(
            _avatar.Id, res[icon], res[_avatar.Name],
            _avatar.Room.ObjectId, res[room.Roo], res[room.Name], room.Security,
            light, PlayerLight: server.World.PlayerLight(_avatar))));
        if (room.Music is { } music)
        {
            EnsureResource(music);
            PostGame(GameReplies.PlayMusic(res[music]));
        }
    }

    private void SendRoomContents()
    {
        var room = _avatar!.Room;
        var present = server.World.Snapshot(room);
        foreach (var a in present)
            EnsureResource(a.Name);
        foreach (var n in room.Npcs)
        {
            EnsureResource(n.DisplayName);
            EnsureResource(server.World.NpcIcon(n));
        }
        var monsters = server.World.MonstersIn(room);
        foreach (var m in monsters)
        {
            EnsureResource(m.Kind.Name);
            EnsureResource(m.Kind.Icon);
        }
        var objects = present.Select(server.World.ToRoomObject)
            .Concat(room.Npcs.Select(server.World.ToRoomObject))
            .Concat(monsters.Select(server.World.ToRoomObject))
            .Concat(server.World.FloorItemsIn(room, this))
            .ToList();
        PostGame(GameReplies.RoomContents(room.ObjectId, objects));
        foreach (var sector in server.World.SectorPackets(room))   // bewegte Sektoren (Mausoleum-Tür)
            PostGame(sector);
    }

    /// <summary>Nach Raumwechsel durch die Welt (Rand-Ausgang, Tod): neuen Raum melden.</summary>
    public void NotifyRoomChanged()
    {
        SendPlayer();
        SendRoomContents();
    }

    /// <summary>Lebenspunkte als BP_STAT (Gruppe 1, Nr. 1 – wie user_stat_health im Original).</summary>
    public void SendHealth()
    {
        if (_avatar is null)
            return;
        EnsureResource(HealthName);
        PostGame(GameReplies.Stat(1, 1, server.Resources[HealthName], _avatar.Hp, _avatar.MaxHp));
    }

    public const string HealthName = "Lebenspunkte";
    public const string VigorName = "Ausdauer";
    public const string ManaName = "Mana";

    /// <summary>Stat-Gruppen wie user.kod ToCliStatGroups (Condition, Stats, Spells, Skills).</summary>
    private static readonly string[] StatGroupNames = ["Zustand", "Werte", "Sprüche", "Fertigkeiten"];

    /// <summary>BP_STAT_GROUP auf BP_SEND_STATS (user.kod ToCliStats): 1 = Lebenspunkte, Mana, Ausdauer;
    /// 2 = Charakterwerte; 3 = Sprüche mit Fähigkeit; 4 = Fertigkeiten (noch leer).</summary>
    private void SendStatGroup(byte group)
    {
        if (_avatar is null)
            return;
        var a = _avatar.State.Attributes;
        if (group == 2)
        {
            string[] names = ["Stärke", "Intellekt", "Ausdauer", "Geschick", "Mystik", "Zielen", "Karma"];
            int[] values = [a.Might, a.Intellect, a.Stamina, a.Agility, a.Mysticism, a.Aim, _avatar.State.Karma];
            foreach (string n in names)
                EnsureResource(n);
            PostGame(GameReplies.StatGroup(2, names.Select((n, i) => ((byte)(i + 1), server.Resources[n], values[i], i == 6 ? 100 : 50)).ToList()));
            return;
        }
        if (group == 3)
        {
            SendSpellStats();
            return;
        }
        if (group == 4)
        {
            SendSkillStats();
            return;
        }
        if (group != 1)
        {
            PostGame(GameReplies.StatGroup(group, []));
            return;
        }
        EnsureResource(HealthName);
        EnsureResource(ManaName);
        EnsureResource(VigorName);
        var res = server.Resources;
        PostGame(GameReplies.StatGroup(1,
        [
            (1, res[HealthName], _avatar.Hp, _avatar.MaxHp),
            (2, res[ManaName], _avatar.State.Mana, _avatar.State.MaxMana),
            (3, res[VigorName], _avatar.State.Vigor, CombatRules.MaxVigor),
        ]));
    }

    /// <summary>Ausdauer als BP_STAT (Gruppe 1, Nr. 3 – wie user_stat_vigor im Original).</summary>
    public void SendVigor()
    {
        if (_avatar is null)
            return;
        EnsureResource(VigorName);
        PostGame(GameReplies.Stat(1, 3, server.Resources[VigorName], _avatar.State.Vigor, CombatRules.MaxVigor));
    }

    /// <summary>Mana als BP_STAT (Gruppe 1, Nr. 2 – user_stat_mana).</summary>
    public void SendMana()
    {
        if (_avatar is null)
            return;
        EnsureResource(ManaName);
        PostGame(GameReplies.Stat(1, 2, server.Resources[ManaName], _avatar.State.Mana, _avatar.State.MaxMana));
    }

    /// <summary>BP_SPELLS (user.kod ToCliSpells) und danach die Spruchwerte (Gruppe 3).</summary>
    private void SendSpells()
    {
        var known = server.World.KnownSpells(_avatar!);
        PostGame(GameReplies.Spells(known.Select(k => (server.World.ToSpellObject(this, k.Spell), (byte)k.Spell.Targets, (byte)k.Spell.School)).ToList()));
        SendSpellStats();
    }

    /// <summary>Gruppe 3: je Spruch Name, Objekt, Fähigkeit in %, Icon (SendStatSpell).</summary>
    public void SendSpellStats()
    {
        if (_avatar is null)
            return;
        var known = server.World.KnownSpells(_avatar);
        PostGame(GameReplies.StatGroupList(3, known.Select((k, i) =>
        {
            var o = server.World.ToSpellObject(this, k.Spell);
            return ((byte)(i + 1), o.NameRsc, o.Id, k.Ability, o.Appearance.IconRsc);
        }).ToList()));
    }

    /// <summary>Gruppe 4: je Fertigkeit Name, Objekt, Fähigkeit in %, Icon (SendStatSkill).</summary>
    public void SendSkillStats()
    {
        if (_avatar is null)
            return;
        var known = server.World.KnownSkills(_avatar);
        PostGame(GameReplies.StatGroupList(4, known.Select((k, i) =>
        {
            var o = server.World.ToSkillObject(this, k.Skill);
            return ((byte)(i + 1), o.NameRsc, o.Id, k.Ability, o.Appearance.IconRsc);
        }).ToList()));
    }

    public void SendSpellAdded(SpellDefinition spell)
    {
        PostGame(GameReplies.SpellAdd(server.World.ToSpellObject(this, spell), (byte)spell.Targets, (byte)spell.School));
        SendSpellStats();
    }

    /// <summary>BP_INVENTORY und danach BP_USE_LIST wie user.kod.</summary>
    private void SendInventory()
    {
        PostGame(GameReplies.Inventory(server.World.InventoryOf(_avatar!)));
        PostGame(GameReplies.UseList(server.World.UsedItems(_avatar!)));
        server.World.SendWindowOverlays(_avatar!);
    }

    /// <summary>Systemtext im Chatfenster (BP_MESSAGE mit dynamischer Format-Ressource).</summary>
    public void SendText(string text)
    {
        EnsureResource("%q");
        PostGame(GameReplies.Text(server.Resources["%q"], text));
    }

    private void SendEchoPing()
    {
        var (payload, newToken) = _codec.CreateEchoPing();
        _outbox.Writer.TryWrite(new Outgoing(payload, Epoch, newToken));
    }

    private void LeaveWorld()
    {
        if (_avatar is null)
            return;
        server.World.Leave(_avatar);
        _avatar = null;
        // Abweichung vom Original (nur periodisch/Admin): beim Ausloggen sichern, damit nichts verloren geht.
        server.SaveGame($"{_account?.Name} loggt aus");
    }

    // ---------------------------------------------------------------- Senden

    /// <summary>Stellt sicher, dass der Client die String-Ressource kennt (BP_CHANGE_RESOURCE).
    /// Wird auch von anderen Sessions (über World) aufgerufen.</summary>
    public void EnsureResource(string value)
    {
        lock (_sentResources)
        {
            if (_mode != Mode.Game || !_sentResources.Add(value))
                return;
            PostGame(GameReplies.ChangeResource(server.Resources[value], value));
        }
    }

    /// <summary>Reiht eine Game-Nachricht ein. Threadsicher, wird auch von <see cref="World"/> benutzt.</summary>
    public void Post(byte[] payload) => _outbox.Writer.TryWrite(new Outgoing(payload, Epoch));

    private void PostGame(byte[] payload) => Post(payload);

    private void PostLogin(byte[] payload) => _outbox.Writer.TryWrite(new Outgoing(payload, 0));

    private async Task WriteLoopAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var msg in _outbox.Reader.ReadAllAsync(ct))
            {
                // Auch AP_*-Nachrichten werden verschleiert, sobald ein Token aktiv ist (protocol.md 2.5).
                byte[] frame = _codec.FrameServerMessage(msg.Payload, msg.Epoch);
                if (msg.NewToken is { } token)
                    _codec.Token.Reset(token);   // erst NACH der Echo-Ping-Nachricht (game.c)
                await _stream.WriteAsync(frame, ct);
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or OperationCanceledException)
        {
            Close();
        }
    }

    public void Close() => client.Close();
}
