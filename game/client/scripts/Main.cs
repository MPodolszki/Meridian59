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

    private Camera3D _camera = null!;
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

        var ground = new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = new Vector2(200, 200) },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.35f, 0.45f, 0.3f) },
        };
        AddChild(ground);

        _camera = new Camera3D { Position = new Vector3(0, 6, 10) };
        AddChild(_camera);
        _camera.LookAt(Vector3.Zero);
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
                    _targets[moved.Object.Id] = ToWorld(moved.Object.Row, moved.Object.Col);
                    if (_avatars.TryGetValue(moved.Object.Id, out var node))
                        node.Rotation = new Vector3(0, ToYaw(moved.Object.Angle), 0);
                }
                break;
            case ClientEvent.ObjectRemoved removed:
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
        var root = new Node3D { Position = ToWorld(o.Row, o.Col), Rotation = new Vector3(0, ToYaw(o.Angle), 0) };
        // Eigener Charakter als Zwerg; andere Spieler vorerst als Kapsel, bis Rassen übertragen werden.
        if (self)
            root.AddChild(DwarfModel.Create());
        else
            root.AddChild(new MeshInstance3D
            {
                Mesh = new CapsuleMesh { Radius = 0.35f, Height = 1.8f },
                Position = new Vector3(0, 0.9f, 0),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.85f, 0.5f, 0.2f) },
            });
        root.AddChild(new Label3D
        {
            Text = _session.Resolve(o.NameRsc),
            Position = new Vector3(0, self ? 1.7f : 2.2f, 0),
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
    }

    public override void _Process(double delta)
    {
        _session?.ProcessEvents();

        // Andere Spieler weich zur gemeldeten Position bewegen.
        foreach (var (id, node) in _avatars)
            if (id != _session?.PlayerId && _targets.TryGetValue(id, out var target))
                node.Position = node.Position.MoveToward(target, RunSpeed * (float)delta);

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
        if (Input.IsPhysicalKeyPressed(Key.W)) input.Y -= 1;
        if (Input.IsPhysicalKeyPressed(Key.S)) input.Y += 1;
        if (Input.IsPhysicalKeyPressed(Key.A)) input.X -= 1;
        if (Input.IsPhysicalKeyPressed(Key.D)) input.X += 1;
        if (Input.IsPhysicalKeyPressed(Key.Q)) _yaw += 2f * delta;
        if (Input.IsPhysicalKeyPressed(Key.E)) _yaw -= 2f * delta;

        self.Rotation = new Vector3(0, _yaw, 0);
        if (input == Vector2.Zero)
            return;

        float speed = Input.IsPhysicalKeyPressed(Key.Shift) ? RunSpeed : WalkSpeed;
        var dir = new Vector3(input.X, 0, input.Y).Normalized().Rotated(Vector3.Up, _yaw);
        var pos = self.Position + dir * speed * delta;
        // Nicht unter die Ursprungs-Koordinaten (Feld 1) laufen.
        self.Position = new Vector3(Mathf.Max(pos.X, CellSize), 0, Mathf.Max(pos.Z, CellSize));
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

    private void UpdateCamera(Node3D self)
    {
        // Kamera auf Zwergenhöhe abgestimmt (Kopf bei ca. 1,2 m).
        var offset = new Vector3(0, 3, 5.5f).Rotated(Vector3.Up, _yaw);
        _camera.Position = self.Position + offset;
        _camera.LookAt(self.Position + new Vector3(0, 1.1f, 0));
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

    // Winkel 0..4095 = 360°.
    private static float ToYaw(ushort angle) => angle / 4096f * Mathf.Tau;

    private static ushort ToAngle(float yaw) => (ushort)((int)Mathf.PosMod(yaw / Mathf.Tau * 4096f, 4096f) & 0x0FFF);
}
