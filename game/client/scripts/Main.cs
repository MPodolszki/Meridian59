using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Meridian.Protocol;

namespace Meridian.Client;

/// <summary>
/// Einstiegspunkt des Clients. Baut Oberfläche und 3D-Szene im Code auf (Platzhalter-Grafik),
/// verbindet sich über <see cref="ClientSession"/> und stellt Spieler als Kapseln dar.
/// Steuerung: WASD laufen, Shift rennen, rechte Maustaste gedrückt halten zum Drehen,
/// Linksklick auf eine Tür öffnet sie (Leertaste bleibt fürs Springen frei), Enter für Chat, I Inventar, B Kaufen, G Aufheben (TradeUi.cs).
/// </summary>
public partial class Main : Node3D
{
    /// <summary>Meter pro Grid-Feld (64 KOD-Feineinheiten).</summary>
    private const float CellSize = 2.5f;
    private const float WalkSpeed = 4f;     // m/s
    private const float RunSpeed = 8f;
    private static readonly TimeSpan SendInterval = TimeSpan.FromSeconds(1);   // wie Original-Client

    private ClientSession? _session;
    private readonly Dictionary<uint, Node3D> _avatars = new();
    private readonly Dictionary<uint, Vector3> _targets = new();

    /// <summary>Bewegung anderer Objekte zwischen zwei Servermeldungen: geschätztes Tempo aus Abstand und Zeit,
    /// geglättete Ist-Geschwindigkeit (für die Animation) und die vom Server gemeldete Blickrichtung.</summary>
    private sealed class RemoteMotion
    {
        public double LastUpdate;
        public float Speed;
        public float VisualSpeed;
        public float ServerYaw;
    }
    private readonly Dictionary<uint, RemoteMotion> _motion = new();

    /// <summary>Eigene Geschwindigkeit (beschleunigt/bremst statt sofort zu starten/stoppen).</summary>
    private Vector3 _velocity;
    private const float Acceleration = 28f;   // m/s²
    private const float Deceleration = 40f;
    private readonly HashSet<uint> _npcs = new();
    private readonly HashSet<uint> _monsters = new();
    private ProgressBar _health = null!;
    private int _lastHp = int.MaxValue;
    private Label _healthText = null!;
    private ProgressBar _vigor = null!;
    private Label _vigorText = null!;

    private Camera3D _camera = null!;
    private DirectionalLight3D _sun = null!;
    private Godot.Environment _environment = null!;
    private MeshInstance3D _grass = null!;
    private Node3D? _roomGeometry;
    private RoomLayout? _layout;
    private bool _outdoors;
    private Control _loginPanel = null!;
    private LineEdit _host = null!, _port = null!, _user = null!, _password = null!;
    private Label _status = null!;
    private RichTextLabel _chatLog = null!;
    private LineEdit _chatInput = null!;

    private float _yaw;
    private Vector3 _lastSentPos;
    private float _lastSentYaw;
    private DateTime _lastMoveSent, _lastTurnSent;

    public override void _Ready()
    {
        BuildWorld();
        BuildUi();
        ApplyCommandLine();
    }

    /// <summary>
    /// Für Tests/Entwicklung: godot --path game/client -- --login NAME PASSWORT [--host H] [--port P]
    /// [--screenshot DATEI] [--walk SEKUNDEN] [--create show|auto] [--jump 1] [--cast SPRUCH]. Mit --screenshot wird nach dem Betreten des Raums
    /// (und optionalem Laufen) ein Bild gespeichert und der Client beendet.
    /// </summary>
    private void ApplyCommandLine()
    {
        string[] args = OS.GetCmdlineUserArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            switch (args[i])
            {
                case "--host": _host.Text = args[++i]; break;
                case "--port": _port.Text = args[++i]; break;
                case "--screenshot": _screenshotPath = args[++i]; break;
                case "--camera": _cameraMode = args[++i]; break;
                case "--delay": _screenshotDelay = float.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
                case "--fight": _autoFight = args[++i] == "1"; break;
                case "--inventory": _autoInventory = args[++i] == "1"; break;
                case "--shop": _autoShop = args[++i] == "1"; break;
                case "--create": _autoCreate = args[++i]; break;
                case "--jump": _autoJump = args[++i] == "1"; break;
                case "--cast": _autoCast = args[++i]; break;
                case "--walk": _autoWalkSeconds = float.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
                case "--login" when i + 2 < args.Length:
                    _user.Text = args[++i];
                    _password.Text = args[++i];
                    _autoLogin = true;
                    break;
            }
        }
        if (_autoLogin)
            CallDeferred(MethodName.OnConnectPressed);
    }

    private bool _autoLogin;
    /// <summary>Neuer Charakter: "show" fotografiert den Dialog, "auto" erstellt mit Vorgaben und betritt die Welt.</summary>
    private string? _autoCreate;
    /// <summary>Spruch (Name), den „--create auto“ wählt und der Client nach dem Betreten auf sich wirkt.</summary>
    private string? _autoCast;
    private float _screenshotDelay = 1.5f;
    private bool _autoFight;
    private bool _autoInventory;
    private bool _autoShop;
    private double _autoFightTimer;
    private string _cameraMode = "behind";   // "front" für Screenshots von vorne
    private string? _screenshotPath;
    private float _autoWalkSeconds;
    private double _screenshotTimer = -1;

    private void UpdateAutomation(double delta)
    {
        UpdateAutoCast(delta);
        if (_autoFight && _session is { InWorld: true } && (_autoFightTimer -= delta) <= 0)
        {
            _autoFightTimer = 1.3;
            AttackNearestMonster();
        }
        if (_autoInventory && _session is { InWorld: true })
            _inventoryPanel.Visible = true;
        if (_autoShop && _session is { InWorld: true } && NearestNpc(o => o.IsSeller) != 0)
        {
            _autoShop = false;
            OpenShop();
        }
        if (_screenshotPath is null || _screenshotTimer < 0)
            return;
        _screenshotTimer -= delta;
        if (_screenshotTimer > 0)
            return;
        var image = GetViewport().GetTexture().GetImage();
        image.SavePng(_screenshotPath);
        GD.Print($"Screenshot gespeichert: {_screenshotPath}");
        _screenshotPath = null;
        GetTree().Quit();
    }

    public override void _ExitTree() => _session?.Dispose();

    // ---------------------------------------------------------------- Aufbau

    private void BuildWorld()
    {
        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Sky,
            Sky = new Sky { SkyMaterial = new ProceduralSkyMaterial() },
            AmbientLightSource = Godot.Environment.AmbientSource.Sky,
        };
        AddChild(new WorldEnvironment { Environment = env });
        _environment = env;
        _sun = new DirectionalLight3D { RotationDegrees = new Vector3(-50, 30, 0), ShadowEnabled = true };
        AddChild(_sun);

        // Boden über die ganze Raumfläche (Räume sind bis ca. 100 Felder = 250 m groß), Gitter alle 1 Feld.
        var ground = new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = new Vector2(600, 600) },
            Position = new Vector3(250, 0, 250),
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoTexture = CreateGridTexture(),
                Uv1Scale = new Vector3(600 / CellSize, 600 / CellSize, 1),
                TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic,
            },
        };
        AddChild(ground);
        _grass = ground;

        _camera = new Camera3D { Position = new Vector3(0, 6, 10) };
        AddChild(_camera);
        _camera.LookAt(Vector3.Zero);
    }

    /// <summary>Platzhalter-Bodentextur: Gras mit dunklerer Kante, eine Kachel = ein Grid-Feld.</summary>
    private static ImageTexture CreateGridTexture()
    {
        const int size = 64;
        var image = Image.CreateEmpty(size, size, true, Image.Format.Rgb8);
        var grass = new Color(0.36f, 0.46f, 0.28f);
        var edge = new Color(0.30f, 0.39f, 0.23f);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                image.SetPixel(x, y, x < 2 || y < 2 ? edge : grass);
        image.GenerateMipmaps();
        return ImageTexture.CreateFromImage(image);
    }

    private void BuildUi()
    {
        var ui = new CanvasLayer();
        AddChild(ui);

        var panel = new PanelContainer { Position = new Vector2(20, 20) };
        var box = new VBoxContainer { CustomMinimumSize = new Vector2(280, 0) };
        panel.AddChild(box);
        box.AddChild(new Label { Text = "Meridian – Anmeldung" });
        _host = AddField(box, "Server", "127.0.0.1");
        _port = AddField(box, "Port", "5959");
        _user = AddField(box, "Name", "");
        _password = AddField(box, "Passwort", "");
        _password.Secret = true;
        var connect = new Button { Text = "Verbinden" };
        connect.Pressed += OnConnectPressed;
        _password.TextSubmitted += _ => OnConnectPressed();
        box.AddChild(connect);
        _loginPanel = panel;
        ui.AddChild(panel);

        _status = new Label { Position = new Vector2(20, 690) };
        ui.AddChild(_status);

        // Lebensanzeige oben links (sichtbar, sobald man im Spiel ist)
        _health = new ProgressBar { Position = new Vector2(20, 20), Size = new Vector2(260, 22), ShowPercentage = false, Visible = false };
        _health.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = new Color(0.7f, 0.1f, 0.1f) });
        ui.AddChild(_health);
        _healthText = new Label { Position = new Vector2(28, 20), Visible = false };
        ui.AddChild(_healthText);
        _vigor = new ProgressBar { Position = new Vector2(20, 46), Size = new Vector2(260, 22), ShowPercentage = false, Visible = false };
        _vigor.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = new Color(0.75f, 0.6f, 0.1f) });
        ui.AddChild(_vigor);
        _vigorText = new Label { Position = new Vector2(28, 46), Visible = false };
        ui.AddChild(_vigorText);

        _chatLog = new RichTextLabel
        {
            Position = new Vector2(20, 450),
            Size = new Vector2(500, 190),
            ScrollFollowing = true,
        };
        ui.AddChild(_chatLog);
        _chatInput = new LineEdit { Position = new Vector2(20, 645), Size = new Vector2(500, 32), PlaceholderText = "Enter zum Chatten" };
        _chatInput.TextSubmitted += OnChatSubmitted;
        ui.AddChild(_chatInput);

        BuildTradeUi(ui);
        BuildSpellUi(ui);
        BuildCreationUi(ui);
    }

    private static LineEdit AddField(VBoxContainer box, string label, string value)
    {
        box.AddChild(new Label { Text = label });
        var edit = new LineEdit { Text = value };
        box.AddChild(edit);
        return edit;
    }

    // ---------------------------------------------------------------- Netzwerk

    private async void OnConnectPressed()
    {
        _session?.Dispose();
        _session = new ClientSession { CreateNewCharacters = true };
        _session.Event += OnSessionEvent;
        try
        {
            await _session.ConnectAsync(_host.Text, int.Parse(_port.Text), _user.Text, _password.Text);
        }
        catch (Exception ex)
        {
            _status.Text = $"Verbindung fehlgeschlagen: {ex.Message}";
        }
    }

    private void OnSessionEvent(ClientEvent e)
    {
        if (HandleTradeEvent(e) || HandleSpellEvent(e) || HandleCreationEvent(e))
            return;
        switch (e)
        {
            case ClientEvent.Status s:
                _status.Text = s.Text;
                break;
            case ClientEvent.LoginFailed f:
                _status.Text = f.Reason;
                _loginPanel.Visible = true;
                break;
            case ClientEvent.HealthChanged hp:
                // Trefferreaktion, wenn die eigenen Lebenspunkte sinken (nicht beim ersten Wert und nicht beim Tod).
                if (_lastHp != int.MaxValue && hp.Hp < _lastHp && hp.Hp > 0
                    && _session is { InWorld: true } && _avatars.TryGetValue(_session.PlayerId, out var hurt))
                    CharacterModel.PlayOnce(hurt, "Hit");
                _lastHp = hp.Hp;
                _health.Visible = _healthText.Visible = true;
                _health.MaxValue = hp.MaxHp;
                _health.Value = hp.Hp;
                _healthText.Text = $"Lebenspunkte {hp.Hp} / {hp.MaxHp}";
                break;
            case ClientEvent.AmbientLight light:
                ApplyAmbientLight(light.Light);
                break;
            case ClientEvent.VigorChanged v:
                _vigor.Visible = _vigorText.Visible = true;
                _vigor.MaxValue = v.MaxVigor;
                _vigor.Value = v.Vigor;
                _vigorText.Text = $"Ausdauer {v.Vigor} / {v.MaxVigor}";
                break;
            case ClientEvent.ObjectAnimated anim:
                if (_avatars.TryGetValue(anim.Id, out var animated))
                {
                    // Eigene Figur ohne Waffe: Faustschlag statt Schwerthieb.
                    bool unarmed = anim.Id == _session!.PlayerId && !_inventory.Any(o => o.Used && o.Icon is "mace.bgf" or "axe.bgf" or "hammer.bgf" or "sword.bgf" or "shswd.bgf" or "scimitar.bgf" or "mystswd.bgf");
                    CharacterModel.PlayOnce(animated, anim.Death ? "Die" : unarmed ? "Punch" : "Attack");
                }
                if (anim.Death)
                    _monsters.Remove(anim.Id);   // tote Monster nicht mehr als Ziel wählen
                break;
            case ClientEvent.EnteredRoom room:
                ResetJump();
                _loginPanel.Visible = false;
                _status.Text = $"Im Raum: {room.RoomName}";
                LoadRoomGeometry(room.RoomFile);
                if (_screenshotPath is not null && _screenshotTimer < 0)
                    _screenshotTimer = _screenshotDelay + _autoWalkSeconds;
                break;
            case ClientEvent.ObjectAdded added:
                AddAvatar(added.Object);
                break;
            case ClientEvent.ObjectMoved moved:
                if (moved.Object.Id == _session!.PlayerId)
                {
                    // Server korrigiert die eigene Position (z. B. verschlossene Tür).
                    if (_avatars.TryGetValue(moved.Object.Id, out var selfNode))
                        selfNode.Position = _lastSentPos = ToWorld(moved.Object.Row, moved.Object.Col);
                }
                else
                {
                    var target = ToWorld(moved.Object.Row, moved.Object.Col);
                    target.Y = GroundHeightAt(target);
                    // Tempo aus Abstand zur vorigen Meldung schätzen, damit Figuren gleichmäßig laufen statt zu sprinten.
                    double now = Time.GetTicksMsec() / 1000.0;
                    var m = _motion.TryGetValue(moved.Object.Id, out var mm) ? mm : _motion[moved.Object.Id] = new RemoteMotion { LastUpdate = now };
                    if (_targets.TryGetValue(moved.Object.Id, out var previous))
                    {
                        float dt = (float)Mathf.Clamp(now - m.LastUpdate, 0.15, 1.5);
                        float estimate = Mathf.Min(previous.DistanceTo(target) / dt, RunSpeed * 1.25f);
                        m.Speed = m.Speed <= 0 ? estimate : Mathf.Lerp(m.Speed, estimate, 0.5f);
                    }
                    m.LastUpdate = now;
                    m.ServerYaw = ToYaw(moved.Object.Angle);
                    _targets[moved.Object.Id] = target;
                }
                break;
            case ClientEvent.ObjectRemoved removed:
                _npcs.Remove(removed.Id);
                _npcObjects.Remove(removed.Id);
                _floorItems.Remove(removed.Id);
                _monsters.Remove(removed.Id);
                if (_avatars.Remove(removed.Id, out var gone))
                    gone.QueueFree();
                _targets.Remove(removed.Id);
                _motion.Remove(removed.Id);
                break;
            case ClientEvent.Chat chat:
                _chatLog.AppendText(chat.Text + "\n");
                break;
            case ClientEvent.Disconnected d:
                _status.Text = $"Getrennt: {d.Reason}";
                _loginPanel.Visible = true;
                foreach (var node in _avatars.Values)
                    node.QueueFree();
                _avatars.Clear();
                _targets.Clear();
                _floorItems.Clear();
                _spellsRequested = false;
                _inventoryPanel.Visible = _shopPanel.Visible = _offerPanel.Visible = _incomingPanel.Visible = false;
                break;
        }
    }

    /// <summary>Umgebungslicht des Servers (0..255, abhängig von Tageszeit und Raum) auf Sonne und Himmel anwenden.
    /// Unter ca. 100 wird es Nacht: Sonne flach und schwach, Himmel dunkelblau.</summary>
    private byte _lastLight = 200;

    private void ApplyAmbientLight(byte light)
    {
        _lastLight = light;
        // Straßen von Tos: nachts ca. 120, mittags 255 → Bereich 0,45..0,95 auf Nacht..Tag abbilden.
        float t = Mathf.SmoothStep(0.45f, 0.95f, Mathf.Clamp(light / 255f, 0f, 1f));
        _sun.LightEnergy = Mathf.Lerp(0.04f, 1.2f, t);
        _sun.RotationDegrees = new Vector3(Mathf.Lerp(-8f, -55f, t), 30, 0);
        // Innenräume liegen unter einer Decke; dort tragen Lampen und mehr Umgebungslicht.
        _environment.AmbientLightEnergy = _outdoors ? Mathf.Lerp(0.3f, 1f, t) : Mathf.Lerp(0.7f, 1.1f, t);
        RoomDecor.SetDaylight(t);
        if (_environment.Sky?.SkyMaterial is ProceduralSkyMaterial sky)
        {
            sky.SkyTopColor = new Color(0.02f, 0.03f, 0.08f).Lerp(new Color(0.38f, 0.45f, 0.55f), t);
            sky.SkyHorizonColor = new Color(0.05f, 0.06f, 0.12f).Lerp(new Color(0.65f, 0.67f, 0.7f), t);
            sky.GroundHorizonColor = sky.SkyHorizonColor;
        }
    }

    /// <summary>Ersetzt die Raumgeometrie (Platzhalter aus rooms/*.json). Ohne Layout bleibt nur die Grasfläche.</summary>
    private void LoadRoomGeometry(string roomFile)
    {
        _roomGeometry?.QueueFree();
        _roomGeometry = null;
        _layout = RoomLayout.Load(roomFile);
        // Die Grasfläche ist nur Rückfallebene für Räume ohne Layout.
        _grass.Visible = _layout is null;
        // Die Geometrie wird erst gebaut, wenn die eigene Position bekannt ist (BuildRoomGeometry).
    }

    /// <summary>Baut die Raumgeometrie mit dem Boden am Standort des Spielers als Bezugshöhe.</summary>
    private void BuildRoomGeometry(Vector3 playerPosition)
    {
        if (_layout is null)
            return;
        _roomGeometry?.QueueFree();
        var (row, col) = CellAt(playerPosition);
        _layout.SetGroundAt(row, col);
        // Außenbereiche haben Fels/Pflaster, Innenräume Putz/Holz; Außen erkennbar an der Größe.
        bool outdoors = _outdoors = _layout.Rows * _layout.Cols > 420;
        _roomGeometry = _layout.Build(CellSize, outdoors);
        AddChild(_roomGeometry);
        ApplyAmbientLight(_lastLight);   // Innen/außen ist erst jetzt bekannt
    }

    /// <summary>Feld (0-basiert) unter einer Weltposition.</summary>
    private static (int Row, int Col) CellAt(Vector3 p) =>
        ((int)Mathf.Floor(p.Z / CellSize) - 1, (int)Mathf.Floor(p.X / CellSize) - 1);

    private bool CanStandAt(Vector3 p, Vector3 from)
    {
        if (_layout is null)
            return true;
        var (row, col) = CellAt(p);
        // Über den Raumrand hinaus darf man von einem begehbaren Randfeld aus (Rand-Ausgänge, KOD plEdge_Exits).
        bool outside = row < 0 || col < 0 || row >= _layout.Rows || col >= _layout.Cols;
        if (outside)
        {
            var (fr, fc) = CellAt(from);
            return _layout.IsWalkable(fr, fc);
        }
        return _layout.IsWalkable(row, col);
    }

    /// <summary>Oberkante der Geometrie an einer Position (Dach, Wand oder Boden).</summary>
    private float BlockTopAt(Vector3 p)
    {
        var (row, col) = CellAt(p);
        int f = _layout!.FloorAt(row, col);
        if (f < 0)
            return 8f;   // massive Wand
        return _layout.HeightMeters(f, CellSize) + (_layout.IsWalkable(row, col) ? 0 : 0.25f);
    }

    private float GroundHeightAt(Vector3 p)
    {
        if (_layout is null)
            return 0;
        var (row, col) = CellAt(p);
        int f = _layout.FloorAt(row, col);
        return f < 0 ? 0 : _layout.HeightMeters(f, CellSize);
    }

    private async void OnChatSubmitted(string text)
    {
        _chatInput.Clear();
        _chatInput.ReleaseFocus();
        if (text.Length > 0 && _session is { InWorld: true } && !await TryBankCommand(text))
            await _session.SayAsync(text);
    }

    // ---------------------------------------------------------------- Darstellung

    private void AddAvatar(ClientObject o)
    {
        if (_avatars.TryGetValue(o.Id, out var old))
            old.QueueFree();

        bool self = o.Id == _session!.PlayerId;
        var spawn = ToWorld(o.Row, o.Col);
        if (o.IsGettable)
        {
            var item = CreateFloorItem(o);
            item.Position = spawn with { Y = GroundHeightAt(spawn) };
            AddChild(item);
            _avatars[o.Id] = item;
            _targets[o.Id] = item.Position;
            _floorItems.Add(o.Id);
            return;
        }
        if (self)
            BuildRoomGeometry(spawn);
        spawn.Y = GroundHeightAt(spawn);
        var root = new Node3D { Position = spawn, Rotation = new Vector3(0, ToYaw(o.Angle), 0) };
        // Modell nach Icon (zombie.bgf → models/zombie.glb); Spieler und NPCs sind vorerst Zwerge.
        root.AddChild(CharacterModel.Create(o.Icon));
        if (o.IsAttackable)
            _monsters.Add(o.Id);
        if (o.IsNpc)
        {
            _npcs.Add(o.Id);
            _npcObjects[o.Id] = o;
        }
        root.AddChild(new Label3D
        {
            Text = _session.Resolve(o.NameRsc),
            // NPCs gelb, Monster rot wie in vielen MMOs
            Modulate = o.IsAttackable ? new Color(1f, 0.35f, 0.3f) : o.IsNpc ? new Color(1f, 0.85f, 0.3f) : Colors.White,
            Position = new Vector3(0, o.IsAttackable ? 2.0f : 1.7f, 0),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
        });
        AddChild(root);
        _avatars[o.Id] = root;
        _targets[o.Id] = root.Position;
        if (self)
        {
            _torchLight = null;
            CallDeferred(MethodName.UpdateTorchLight);
            _yaw = root.Rotation.Y;
            _lastSentPos = root.Position;
            _lastSentYaw = _yaw;
        }
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventMouseMotion motion && Input.IsMouseButtonPressed(MouseButton.Right))
            _yaw -= motion.Relative.X * 0.005f;
        if (e is InputEventKey { Pressed: true, Keycode: Key.Enter } && !_chatInput.HasFocus())
            _chatInput.GrabFocus();
        if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click)
            TryOpenDoorAt(click.Position);
        if (e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.E } && !_chatInput.HasFocus())
            LookAtNearestNpc();
        if (e is InputEventKey { Pressed: true, Keycode: Key.F } && !_chatInput.HasFocus())
            AttackNearestMonster();
        if (e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.I } && !_chatInput.HasFocus())
            _inventoryPanel.Visible = !_inventoryPanel.Visible;
        if (e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.B } && !_chatInput.HasFocus())
            OpenShop();
        if (e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.G } && !_chatInput.HasFocus())
            PickUpNearest();
        if (e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Z } && !_chatInput.HasFocus())
            _spellPanel.Visible = !_spellPanel.Visible;
    }

    public override void _Process(double delta)
    {
        _session?.ProcessEvents();
        UpdateAutomation(delta);

        // Andere Figuren gleichmäßig zur gemeldeten Position bewegen, weich drehen und passend animieren.
        float dtf = (float)delta;
        foreach (var (id, node) in _avatars)
        {
            if (id == _session?.PlayerId || !_targets.TryGetValue(id, out var target) || _floorItems.Contains(id))
                continue;
            var m = _motion.TryGetValue(id, out var mm) ? mm : _motion[id] = new RemoteMotion { ServerYaw = node.Rotation.Y };
            var before = node.Position;
            float distance = before.DistanceTo(target);
            // Mit dem geschätzten Tempo laufen; liegt die Figur weit zurück, etwas schneller aufholen.
            float speed = Mathf.Clamp(Mathf.Max(m.Speed, distance * 1.5f), 0.5f, RunSpeed * 1.5f);
            node.Position = before.MoveToward(target, speed * dtf);
            var step = node.Position - before;
            float actual = new Vector2(step.X, step.Z).Length() / Mathf.Max(dtf, 1e-4f);
            m.VisualSpeed = Mathf.Lerp(m.VisualSpeed, actual, 1f - Mathf.Exp(-8f * dtf));
            // In Laufrichtung drehen; im Stand zur gemeldeten Blickrichtung.
            float wantedYaw = m.VisualSpeed > 0.4f && step.LengthSquared() > 1e-6f
                ? Mathf.Atan2(-step.X, -step.Z)
                : m.ServerYaw;
            node.Rotation = new Vector3(0, Mathf.LerpAngle(node.Rotation.Y, wantedYaw, 1f - Mathf.Exp(-10f * dtf)), 0);
            CharacterModel.Animate(node, m.VisualSpeed);
        }

        if (_session is not { InWorld: true } || !_avatars.TryGetValue(_session.PlayerId, out var self))
            return;

        UpdateOwnAvatar(self, (float)delta);
        UpdateExitHint(self);
        UpdateCamera(self);
        SendPositionIfNeeded(self);
    }

    private void UpdateOwnAvatar(Node3D self, float delta)
    {
        // Fußhöhe ohne Sprung; der Sprung kommt als Versatz obendrauf (JumpControl.cs).
        float foot = self.Position.Y - _jumpOffset;
        UpdateJump(self, delta);
        if (_chatInput.HasFocus())
        {
            self.Position = self.Position with { Y = foot + _jumpOffset };
            return;
        }

        var input = Vector2.Zero;
        if (_autoWalkSeconds > 0 && _screenshotTimer > _screenshotDelay)
        {
            input.Y -= 1;          // automatisch geradeaus laufen
            _yaw += 0.6f * delta;  // und dabei leicht drehen
        }
        if (Input.IsPhysicalKeyPressed(Key.W)) input.Y -= 1;
        if (Input.IsPhysicalKeyPressed(Key.S)) input.Y += 1;
        if (Input.IsPhysicalKeyPressed(Key.A)) input.X -= 1;
        if (Input.IsPhysicalKeyPressed(Key.D)) input.X += 1;
        if (Input.IsPhysicalKeyPressed(Key.Q)) _yaw += 2f * delta;
        if (Input.IsPhysicalKeyPressed(Key.E)) _yaw -= 2f * delta;

        self.Rotation = new Vector3(0, _yaw, 0);

        // Weich beschleunigen und abbremsen statt sofort mit voller Geschwindigkeit zu starten/stoppen.
        float speed = Input.IsPhysicalKeyPressed(Key.Shift) ? RunSpeed : WalkSpeed;
        var desired = input == Vector2.Zero ? Vector3.Zero : new Vector3(input.X, 0, input.Y).Normalized().Rotated(Vector3.Up, _yaw) * speed;
        _velocity = _velocity.MoveToward(desired, (desired == Vector3.Zero ? Deceleration : Acceleration) * delta);
        if (_velocity.LengthSquared() < 1e-4f)
        {
            _velocity = Vector3.Zero;
            self.Position = self.Position with { Y = Mathf.Lerp(foot, GroundHeightAt(self.Position), 1f - Mathf.Exp(-15f * delta)) + _jumpOffset };
            if (!_airborne)
                CharacterModel.Animate(self, 0);
            return;
        }

        var step = _velocity * delta;
        var pos = self.Position;
        // Kollision mit Wänden/Gebäuden: Achsen getrennt prüfen, damit man an Wänden entlanggleitet.
        if (CanStandAt(pos + new Vector3(step.X, 0, 0), pos)) pos.X += step.X; else _velocity.X = 0;
        if (CanStandAt(pos + new Vector3(0, 0, step.Z), pos)) pos.Z += step.Z; else _velocity.Z = 0;
        // Ohne Layout: nicht unter die Ursprungs-Koordinaten (Feld 1) laufen. Mit Layout regelt das CanStandAt.
        if (_layout is null)
            pos = new Vector3(Mathf.Max(pos.X, CellSize), 0, Mathf.Max(pos.Z, CellSize));
        // Außerhalb des Rasters sofort melden, damit der Server den Rand-Ausgang auslöst.
        var (cellRow, cellCol) = CellAt(pos);
        if (_layout is not null && (cellRow < 0 || cellCol < 0 || cellRow >= _layout.Rows || cellCol >= _layout.Cols))
            _lastMoveSent = DateTime.MinValue;
        // Höhe (Stufen, Bordsteine) weich nachführen statt zu springen.
        pos.Y = Mathf.Lerp(foot, GroundHeightAt(pos), 1f - Mathf.Exp(-15f * delta)) + _jumpOffset;
        var moved = new Vector2(pos.X - self.Position.X, pos.Z - self.Position.Z).Length();
        self.Position = pos;
        if (!_airborne)
            CharacterModel.Animate(self, moved / Mathf.Max(delta, 1e-4f));
    }

    private string? _exitHint;

    /// <summary>Hinweis, wenn man auf einem Ausgangsfeld steht.</summary>
    private void UpdateExitHint(Node3D self)
    {
        if (_layout is null)
            return;
        var (row, col) = CellAt(self.Position);
        string? target = null;
        foreach (var e in _layout.Exits)
            if (e.Row == row && e.Col == col)
                target = e.Target;
        string? hint = target is null ? null : $"Linksklick auf die Tür: hinausgehen{(target.Length > 0 ? " → " + target : "")}";
        if (hint == _exitHint)
            return;
        _exitHint = hint;
        if (hint is not null)
            _status.Text = hint;
    }

    /// <summary>Reichweite zum Öffnen einer Tür per Klick (Meter vom Spieler zum Ausgangsfeld).</summary>
    private const float DoorReach = 4f;

    /// <summary>Linksklick: liegt eine Tür bzw. ein Ausgang nahe am Klickstrahl und in Reichweite, dorthin treten
    /// und „Gehen“ schicken (BP_REQ_GO verlangt, dass man auf dem Ausgangsfeld steht).</summary>
    private async void TryOpenDoorAt(Vector2 screen)
    {
        if (_session is not { InWorld: true } || _layout is null || !_avatars.TryGetValue(_session.PlayerId, out var self))
            return;
        var origin = _camera.ProjectRayOrigin(screen);
        var dir = _camera.ProjectRayNormal(screen);
        (int Row, int Col, float Distance)? best = null;
        foreach (var (row, col, _) in _layout.Exits)
        {
            var center = new Vector3((col + 1.5f) * CellSize, GroundHeightAt(new Vector3((col + 1.5f) * CellSize, 0, (row + 1.5f) * CellSize)) + 1.2f, (row + 1.5f) * CellSize);
            // Abstand des Ausgangs (Türhöhe) zum Klickstrahl; Tür steht am Feldrand, daher großzügig.
            float along = (center - origin).Dot(dir);
            if (along <= 0)
                continue;
            float rayDistance = (origin + dir * along).DistanceTo(center);
            if (rayDistance < CellSize && (best is null || rayDistance < best.Value.Distance))
                best = (row, col, rayDistance);
        }
        if (best is not { } door)
            return;
        var exitPos = new Vector3((door.Col + 1.5f) * CellSize, 0, (door.Row + 1.5f) * CellSize);
        if (new Vector2(exitPos.X - self.Position.X, exitPos.Z - self.Position.Z).Length() > DoorReach)
        {
            _status.Text = "Die Tür ist zu weit weg.";
            return;
        }
        exitPos.Y = GroundHeightAt(exitPos);
        self.Position = exitPos;
        await GoThroughDoor();
    }

    /// <summary>Tür/Ausgang auf dem aktuellen Feld benutzen.
    /// Vorher die aktuelle Position melden, da der Server sonst die bis zu 1 s alte kennt.</summary>
    private async System.Threading.Tasks.Task GoThroughDoor()
    {
        if (_session is not { InWorld: true } || !_avatars.TryGetValue(_session.PlayerId, out var self))
            return;
        _lastMoveSent = DateTime.UtcNow;
        _lastSentPos = self.Position;
        var (row, col) = ToKod(self.Position);
        await _session.MoveAsync(row, col, running: false);
        await _session.GoAsync();
    }

    /// <summary>F: nächstes Monster in Reichweite angreifen. Vorher Position melden (Server prüft die Reichweite).</summary>
    private async void AttackNearestMonster()
    {
        if (_session is not { InWorld: true } || !_avatars.TryGetValue(_session.PlayerId, out var self))
            return;
        uint best = 0;
        float bestDistance = 6f;
        foreach (uint id in _monsters)
        {
            if (_avatars.TryGetValue(id, out var m) && m.Position.DistanceTo(self.Position) < bestDistance)
            {
                best = id;
                bestDistance = m.Position.DistanceTo(self.Position);
            }
        }
        if (best == 0)
        {
            _status.Text = "Kein Gegner in der Nähe.";
            return;
        }
        // Zum Gegner drehen
        var dir = _avatars[best].Position - self.Position;
        _yaw = Mathf.Atan2(-dir.X, -dir.Z);
        _lastMoveSent = DateTime.UtcNow;
        _lastSentPos = self.Position;
        var (row, col) = ToKod(self.Position);
        await _session.MoveAsync(row, col, running: false);
        await _session.AttackAsync(best);
    }

    /// <summary>E: nächsten NPC in Reichweite anschauen (BP_REQ_LOOK, Beschreibung erscheint im Chat).</summary>
    private async void LookAtNearestNpc()
    {
        if (_session is not { InWorld: true } || !_avatars.TryGetValue(_session.PlayerId, out var self))
            return;
        uint best = 0;
        float bestDistance = 4f;
        foreach (uint id in _npcs)
        {
            if (_avatars.TryGetValue(id, out var npc) && npc.Position.DistanceTo(self.Position) < bestDistance)
            {
                best = id;
                bestDistance = npc.Position.DistanceTo(self.Position);
            }
        }
        if (best == 0)
            _status.Text = "Niemand in der Nähe.";
        else
            await _session.LookAsync(best);
    }

    private void UpdateCamera(Node3D self)
    {
        // Kamera auf Zwergenhöhe abgestimmt (Kopf bei ca. 1,2 m).
        var offset = _cameraMode == "front"
            ? new Vector3(0, 1.6f, -3.2f).Rotated(Vector3.Up, _yaw)
            : new Vector3(0, 3, 5.5f).Rotated(Vector3.Up, _yaw);
        // Beim Springen auf Fußhöhe bleiben, damit der Sprung sichtbar ist und die Kamera nicht hüpft.
        var feet = self.Position with { Y = self.Position.Y - _jumpOffset };
        var head = feet + new Vector3(0, 1.1f, 0);
        // Einfache Kamera-Kollision: entlang des Kamerastrahls bis zum ersten Gebäude, das höher ist.
        float allowed = 1f;
        for (float t = 0.05f; t <= 1f; t += 0.05f)
        {
            var probe = feet + offset * t;
            if (_layout is not null && probe.Y < BlockTopAt(probe))
            {
                allowed = Mathf.Max(0.15f, t - 0.1f);
                break;
            }
        }
        // Bei Hindernissen nur den Abstand verkürzen, nicht die Höhe – sonst schaut die Kamera von unten.
        var shortened = new Vector3(offset.X * allowed, Mathf.Max(offset.Y * allowed, Mathf.Min(offset.Y, 2.2f)), offset.Z * allowed);
        // Kamera weich nachführen; nur bei großen Sprüngen (Raumwechsel, Teleport) sofort setzen.
        var wantedCamera = feet + shortened;
        _camera.Position = _camera.Position.DistanceTo(wantedCamera) > 8f
            ? wantedCamera
            : _camera.Position.Lerp(wantedCamera, 1f - Mathf.Exp(-12f * (float)GetProcessDeltaTime()));
        // Innenräume haben eine Decke: Kamera darunter halten.
        if (_layout is not null && !_outdoors)
            _camera.Position = _camera.Position with { Y = Mathf.Min(_camera.Position.Y, _layout.CeilingMeters(CellSize) - 0.3f) };
        _camera.LookAt(head);
    }

    private async void SendPositionIfNeeded(Node3D self)
    {
        var now = DateTime.UtcNow;
        if (now - _lastMoveSent >= SendInterval && self.Position.DistanceTo(_lastSentPos) > 0.05f)
        {
            _lastMoveSent = now;
            _lastSentPos = self.Position;
            var (row, col) = ToKod(self.Position);
            await _session!.MoveAsync(row, col, Input.IsPhysicalKeyPressed(Key.Shift));
        }
        if (now - _lastTurnSent >= SendInterval && Mathf.Abs(_yaw - _lastSentYaw) > 0.05f)
        {
            _lastTurnSent = now;
            _lastSentYaw = _yaw;
            await _session!.TurnAsync(ToAngle(_yaw));
        }
    }

    // ---------------------------------------------------------------- Koordinaten

    // KOD: Zeile/Spalte * 64 + fein, 1-basiert (protocol.md 4.1). Spalte → X, Zeile → Z.
    private static Vector3 ToWorld(ushort row, ushort col) =>
        new(col / 64f * CellSize, 0, row / 64f * CellSize);

    private static (ushort Row, ushort Col) ToKod(Vector3 p) =>
        ((ushort)Mathf.Clamp(p.Z / CellSize * 64f, 0, ushort.MaxValue),
         (ushort)Mathf.Clamp(p.X / CellSize * 64f, 0, ushort.MaxValue));

    // KOD-Winkel 0..4095 = 360°, 0 = Osten (+Spalte = +X), 1024 = Süden (+Zeile = +Z).
    // Figuren schauen bei yaw 0 nach -Z, also gilt yaw = -θ - π/2.
    private static float ToYaw(ushort angle) => -(angle / 4096f * Mathf.Tau) - Mathf.Pi / 2;

    private static ushort ToAngle(float yaw) =>
        (ushort)((int)Mathf.PosMod((-yaw - Mathf.Pi / 2) / Mathf.Tau * 4096f, 4096f) & 0x0FFF);
}
