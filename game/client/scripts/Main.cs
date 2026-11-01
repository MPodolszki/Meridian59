using System;
using System.Collections.Generic;
using Godot;
using Meridian.Protocol;

namespace Meridian.Client;

/// <summary>
/// Einstiegspunkt des Clients. Baut Oberfläche und 3D-Szene im Code auf (Platzhalter-Grafik),
/// verbindet sich über <see cref="ClientSession"/> und stellt Spieler als Kapseln dar.
/// Steuerung: WASD laufen, Shift rennen, rechte Maustaste gedrückt halten zum Drehen,
/// Leertaste Tür/Ausgang benutzen, Enter für Chat.
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
    private readonly HashSet<uint> _npcs = new();

    private Camera3D _camera = null!;
    private MeshInstance3D _grass = null!;
    private Node3D? _roomGeometry;
    private RoomLayout? _layout;
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
    /// [--screenshot DATEI] [--walk SEKUNDEN]. Mit --screenshot wird nach dem Betreten des Raums
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
    private string _cameraMode = "behind";   // "front" für Screenshots von vorne
    private string? _screenshotPath;
    private float _autoWalkSeconds;
    private double _screenshotTimer = -1;

    private void UpdateAutomation(double delta)
    {
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
        AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-50, 30, 0), ShadowEnabled = true });

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
        _session = new ClientSession();
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
        switch (e)
        {
            case ClientEvent.Status s:
                _status.Text = s.Text;
                break;
            case ClientEvent.LoginFailed f:
                _status.Text = f.Reason;
                _loginPanel.Visible = true;
                break;
            case ClientEvent.EnteredRoom room:
                _loginPanel.Visible = false;
                _status.Text = $"Im Raum: {room.RoomName}";
                LoadRoomGeometry(room.RoomFile);
                if (_screenshotPath is not null && _screenshotTimer < 0)
                    _screenshotTimer = 1.5 + _autoWalkSeconds;
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
                    _targets[moved.Object.Id] = target;
                    if (_avatars.TryGetValue(moved.Object.Id, out var node))
                        node.Rotation = new Vector3(0, ToYaw(moved.Object.Angle), 0);
                }
                break;
            case ClientEvent.ObjectRemoved removed:
                _npcs.Remove(removed.Id);
                if (_avatars.Remove(removed.Id, out var gone))
                    gone.QueueFree();
                _targets.Remove(removed.Id);
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
                break;
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
        bool outdoors = _layout.Rows * _layout.Cols > 420;
        _roomGeometry = _layout.Build(CellSize, outdoors);
        AddChild(_roomGeometry);
    }

    /// <summary>Feld (0-basiert) unter einer Weltposition.</summary>
    private static (int Row, int Col) CellAt(Vector3 p) =>
        ((int)Mathf.Floor(p.Z / CellSize) - 1, (int)Mathf.Floor(p.X / CellSize) - 1);

    private bool CanStandAt(Vector3 p)
    {
        if (_layout is null)
            return true;
        var (row, col) = CellAt(p);
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
        if (text.Length > 0 && _session is { InWorld: true })
            await _session.SayAsync(text);
    }

    // ---------------------------------------------------------------- Darstellung

    private void AddAvatar(ClientObject o)
    {
        if (_avatars.TryGetValue(o.Id, out var old))
            old.QueueFree();

        bool self = o.Id == _session!.PlayerId;
        var spawn = ToWorld(o.Row, o.Col);
        if (self)
            BuildRoomGeometry(spawn);
        spawn.Y = GroundHeightAt(spawn);
        var root = new Node3D { Position = spawn, Rotation = new Vector3(0, ToYaw(o.Angle), 0) };
        // Bisher gibt es nur eine Rasse: alle Spieler sind Zwerge. DwarfModel muss direktes Kind sein (Animate).
        root.AddChild(DwarfModel.Create());
        if (o.IsNpc)
            _npcs.Add(o.Id);
        root.AddChild(new Label3D
        {
            Text = _session.Resolve(o.NameRsc),
            Modulate = o.IsNpc ? new Color(1f, 0.85f, 0.3f) : Colors.White,   // NPCs gelb wie in vielen MMOs
            Position = new Vector3(0, 1.7f, 0),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
        });
        AddChild(root);
        _avatars[o.Id] = root;
        _targets[o.Id] = root.Position;
        if (self)
        {
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
        if (e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Space } && !_chatInput.HasFocus())
            GoThroughDoor();
        if (e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.E } && !_chatInput.HasFocus())
            LookAtNearestNpc();
    }

    public override void _Process(double delta)
    {
        _session?.ProcessEvents();
        UpdateAutomation(delta);

        // Andere Spieler weich zur gemeldeten Position bewegen und passend animieren.
        foreach (var (id, node) in _avatars)
        {
            if (id == _session?.PlayerId || !_targets.TryGetValue(id, out var target))
                continue;
            var before = node.Position;
            node.Position = node.Position.MoveToward(target, RunSpeed * (float)delta);
            DwarfModel.Animate(node, before.DistanceTo(node.Position) / Mathf.Max((float)delta, 1e-4f));
        }

        if (_session is not { InWorld: true } || !_avatars.TryGetValue(_session.PlayerId, out var self))
            return;

        UpdateOwnAvatar(self, (float)delta);
        UpdateCamera(self);
        SendPositionIfNeeded(self);
    }

    private void UpdateOwnAvatar(Node3D self, float delta)
    {
        if (_chatInput.HasFocus())
            return;

        var input = Vector2.Zero;
        if (_autoWalkSeconds > 0 && _screenshotTimer > 1.5)
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
        if (input == Vector2.Zero)
        {
            DwarfModel.Animate(self, 0);
            return;
        }

        float speed = Input.IsPhysicalKeyPressed(Key.Shift) ? RunSpeed : WalkSpeed;
        var dir = new Vector3(input.X, 0, input.Y).Normalized().Rotated(Vector3.Up, _yaw);
        var step = dir * speed * delta;
        var pos = self.Position;
        // Kollision mit Wänden/Gebäuden: Achsen getrennt prüfen, damit man an Wänden entlanggleitet.
        if (CanStandAt(pos + new Vector3(step.X, 0, 0))) pos.X += step.X;
        if (CanStandAt(pos + new Vector3(0, 0, step.Z))) pos.Z += step.Z;
        // Nicht unter die Ursprungs-Koordinaten (Feld 1) laufen.
        pos = new Vector3(Mathf.Max(pos.X, CellSize), 0, Mathf.Max(pos.Z, CellSize));
        pos.Y = GroundHeightAt(pos);
        var moved = new Vector2(pos.X - self.Position.X, pos.Z - self.Position.Z).Length();
        self.Position = pos;
        DwarfModel.Animate(self, moved / Mathf.Max(delta, 1e-4f));
    }

    /// <summary>Leertaste wie im Original: Tür/Ausgang auf dem aktuellen Feld benutzen.
    /// Vorher die aktuelle Position melden, da der Server sonst die bis zu 1 s alte kennt.</summary>
    private async void GoThroughDoor()
    {
        if (_session is not { InWorld: true } || !_avatars.TryGetValue(_session.PlayerId, out var self))
            return;
        _lastMoveSent = DateTime.UtcNow;
        _lastSentPos = self.Position;
        var (row, col) = ToKod(self.Position);
        await _session.MoveAsync(row, col, running: false);
        await _session.GoAsync();
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
        var head = self.Position + new Vector3(0, 1.1f, 0);
        // Einfache Kamera-Kollision: entlang des Kamerastrahls bis zum ersten Gebäude, das höher ist.
        float allowed = 1f;
        for (float t = 0.05f; t <= 1f; t += 0.05f)
        {
            var probe = self.Position + offset * t;
            if (_layout is not null && probe.Y < BlockTopAt(probe))
            {
                allowed = Mathf.Max(0.15f, t - 0.1f);
                break;
            }
        }
        // Bei Hindernissen nur den Abstand verkürzen, nicht die Höhe – sonst schaut die Kamera von unten.
        var shortened = new Vector3(offset.X * allowed, Mathf.Max(offset.Y * allowed, Mathf.Min(offset.Y, 2.2f)), offset.Z * allowed);
        _camera.Position = self.Position + shortened;
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
        ((ushort)Mathf.Clamp(p.Z / CellSize * 64f, 64, ushort.MaxValue),
         (ushort)Mathf.Clamp(p.X / CellSize * 64f, 64, ushort.MaxValue));

    // KOD-Winkel 0..4095 = 360°, 0 = Osten (+Spalte = +X), 1024 = Süden (+Zeile = +Z).
    // Figuren schauen bei yaw 0 nach -Z, also gilt yaw = -θ - π/2.
    private static float ToYaw(ushort angle) => -(angle / 4096f * Mathf.Tau) - Mathf.Pi / 2;

    private static ushort ToAngle(float yaw) =>
        (ushort)((int)Mathf.PosMod((-yaw - Mathf.Pi / 2) / Mathf.Tau * 4096f, 4096f) & 0x0FFF);
}
