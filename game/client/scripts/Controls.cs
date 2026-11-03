using Godot;

namespace Meridian.Client;

/// <summary>
/// Steuerung und Kamera nach WoW-/EverQuest-Vorbild (Third Person):
/// W/S vor/zurück, A/D drehen (mit rechter Maustaste seitlich), Q/E seitlich, Shift gehen, NumLock Dauerlauf,
/// Leertaste springen. Rechte Maustaste: Figur dreht sich sofort in Blickrichtung der Kamera und folgt der Maus;
/// linke Maustaste: Kamera kreist frei um die Figur und schwenkt beim Vorwärtslaufen zurück; beide Tasten laufen
/// vorwärts. Mausrad zoomt weich, ganz eingezoomt Ego-Ansicht. Die Kamera hängt fest am Kopf; Hindernisse holen
/// sie schnell heran, danach fährt sie langsam wieder hinaus. Ein Linksklick ohne Ziehen benutzt Türen/Hebel bzw.
/// schaut eine Figur an.
/// Tempo wie WoW: Rennen 7 yd/s, rückwärts 4,5 yd/s, Gehen 2,5 yd/s, Drehen mit Tasten 180°/s.
/// </summary>
public partial class Main
{
    private const float TurnSpeed = Mathf.Pi;           // A/D: 180°/s
    private const float MouseSensitivity = 0.005f;
    private const float BackpedalFactor = 4.5f / 7f;
    private const float MinZoom = 0.6f, MaxZoom = 18f, FirstPersonZoom = 1.0f;
    private const float CameraReturnSpeed = 4f;         // m/s, wenn ein Hindernis den Weg wieder freigibt

    private float _camYawOffset;                         // nur Kamera (linke Maustaste)
    private float _camPitch = 0.3f;                      // Blick leicht von oben
    private float _camZoom = 5f;                         // gewünschter Abstand (Mausrad)
    private float _camDistance = 5f;                     // weich nachgeführter Abstand
    private float _camCollision = 5f;                    // durch Hindernisse verkürzter Abstand
    private bool _autoRun;
    private Vector2 _pressPosition;
    private bool _dragged;

    /// <summary>Maus und Tasten der Steuerung; true, wenn das Ereignis verbraucht wurde.</summary>
    private bool HandleControlsInput(InputEvent e)
    {
        switch (e)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.WheelUp, Pressed: true }:
                _camZoom = Mathf.Max(MinZoom, _camZoom * 0.85f);
                return true;
            case InputEventMouseButton { ButtonIndex: MouseButton.WheelDown, Pressed: true }:
                _camZoom = Mathf.Min(MaxZoom, Mathf.Max(_camZoom, 1.2f) * 1.15f);
                return true;
            case InputEventMouseButton { ButtonIndex: MouseButton.Left or MouseButton.Right } button:
                if (button.Pressed)
                {
                    if (button.ButtonIndex == MouseButton.Right)
                    {
                        // Rechte Maustaste: Figur dreht sich sofort in die Blickrichtung der Kamera.
                        _yaw += _camYawOffset;
                        _camYawOffset = 0;
                    }
                    _pressPosition = button.Position;
                    _dragged = false;
                    return true;
                }
                if (!Input.IsMouseButtonPressed(MouseButton.Left) && !Input.IsMouseButtonPressed(MouseButton.Right))
                {
                    if (Input.MouseMode == Input.MouseModeEnum.Captured)
                    {
                        Input.MouseMode = Input.MouseModeEnum.Visible;
                        GetViewport().WarpMouse(_pressPosition);
                    }
                    // Klick ohne Ziehen: Hebel/Tür benutzen oder Figur anschauen.
                    if (!_dragged && button.ButtonIndex == MouseButton.Left && !TryActivateAt(_pressPosition) && !TryLookAt(_pressPosition))
                        TryOpenDoorAt(_pressPosition);
                }
                return true;
            case InputEventMouseMotion motion
                when Input.IsMouseButtonPressed(MouseButton.Left) || Input.IsMouseButtonPressed(MouseButton.Right):
                if (!_dragged && motion.Position.DistanceTo(_pressPosition) < 4f && Input.MouseMode != Input.MouseModeEnum.Captured)
                    return true;
                _dragged = true;
                Input.MouseMode = Input.MouseModeEnum.Captured;
                if (Input.IsMouseButtonPressed(MouseButton.Right))
                    _yaw -= motion.Relative.X * MouseSensitivity;            // Figur folgt der Maus
                else
                    _camYawOffset -= motion.Relative.X * MouseSensitivity;   // nur die Kamera kreist
                _camPitch = Mathf.Clamp(_camPitch + motion.Relative.Y * MouseSensitivity, -1.2f, 1.4f);
                return true;
            case InputEventKey { Pressed: true, Echo: false, Keycode: Key.Numlock }:
                _autoRun = !_autoRun;
                return true;
        }
        return false;
    }

    /// <summary>Bewegungswunsch aus Tasten und Maus: (seitlich, vor/zurück) und Tempo; dreht dabei die Figur.</summary>
    private (Vector2 Input, float Speed) ReadMovement(float delta)
    {
        var input = Vector2.Zero;
        if (_paralyzed)   // EFFECT_PARALYZE (Festhalten, Kara'hols Fluch): keine Bewegung
            return (input, 0f);
        bool rmb = Input.IsMouseButtonPressed(MouseButton.Right);
        bool both = rmb && Input.IsMouseButtonPressed(MouseButton.Left);
        bool forward = Input.IsPhysicalKeyPressed(Key.W) || both;
        bool back = Input.IsPhysicalKeyPressed(Key.S);
        if (back || Input.IsPhysicalKeyPressed(Key.W))
            _autoRun = false;
        if (forward || _autoRun) input.Y -= 1;
        if (back) input.Y += 1;
        // A/D: ohne rechte Maustaste drehen, mit ihr seitlich gehen (wie WoW); Q/E immer seitlich.
        if (Input.IsPhysicalKeyPressed(Key.A)) { if (rmb) input.X -= 1; else _yaw += TurnSpeed * delta; }
        if (Input.IsPhysicalKeyPressed(Key.D)) { if (rmb) input.X += 1; else _yaw -= TurnSpeed * delta; }
        if (Input.IsPhysicalKeyPressed(Key.Q)) input.X -= 1;
        if (Input.IsPhysicalKeyPressed(Key.E)) input.X += 1;
        input += _routeInput;   // Test-Automatik (AutoRoute.cs)
        // Rennen ist Standard, Shift geht; rückwärts (auch schräg rückwärts) mit Rückwärtstempo.
        float speed = Input.IsPhysicalKeyPressed(Key.Shift) ? WalkSpeed : RunSpeed;
        if (input.Y > 0)
            speed *= BackpedalFactor;
        // „Smart“-Kamera: Läuft man vorwärts, schwenkt eine frei gedrehte Kamera wieder hinter die Figur.
        if (input.Y < 0 && !Input.IsMouseButtonPressed(MouseButton.Left))
            _camYawOffset = Mathf.LerpAngle(_camYawOffset, 0, 1f - Mathf.Exp(-2.5f * delta));
        return (input, speed);
    }

    /// <summary>Körper in Laufrichtung drehen: schräg vorwärts 45°, seitlich 90°, schräg rückwärts gespiegelt
    /// (die Figur schaut nach vorn und läuft rückwärts), wie die Strafe-Darstellung in WoW.</summary>
    private void UpdateBodyFacing(Node3D self, Vector2 input, float delta)
    {
        if (self.GetNodeOrNull<Node3D>("Character") is not { } body)
            return;
        float target = input == Vector2.Zero ? 0
            : input.Y <= 0 ? Mathf.Atan2(-input.X, -input.Y)
            : Mathf.Atan2(input.X, input.Y);
        body.Rotation = new Vector3(0, Mathf.LerpAngle(body.Rotation.Y, target, 1f - Mathf.Exp(-12f * delta)), 0);
    }

    private void UpdateCamera(Node3D self)
    {
        float dt = (float)GetProcessDeltaTime();
        // Beim Springen auf Fußhöhe bleiben, damit der Sprung sichtbar ist und die Kamera nicht hüpft.
        var feet = self.Position with { Y = self.Position.Y - _jumpOffset };
        var head = feet + new Vector3(0, 1.15f, 0);
        if (_cameraMode == "front")
        {
            _camera.Position = head + new Vector3(0, 0.5f, -3.2f).Rotated(Vector3.Up, _yaw);
            _camera.LookAt(head);
            return;
        }
        // Zoom weich; ganz eingezoomt Ego-Ansicht (eigene Figur ausgeblendet).
        _camDistance = Mathf.Lerp(_camDistance, _camZoom, 1f - Mathf.Exp(-10f * dt));
        bool firstPerson = _camZoom <= FirstPersonZoom;
        if (self.GetNodeOrNull<Node3D>("Character") is { } body)
            body.Visible = !firstPerson;
        var direction = new Vector3(0, Mathf.Sin(_camPitch), Mathf.Cos(_camPitch)).Rotated(Vector3.Up, _yaw + _camYawOffset);
        if (firstPerson)
        {
            _camera.Position = head + new Vector3(0, 0.1f, 0);
            _camera.LookAt(_camera.Position - direction);
            return;
        }
        // Kollision: freier Abstand entlang des Strahls vom Kopf; heran sofort, hinaus langsam.
        float free = _camDistance;
        for (float d = 0.3f; d <= _camDistance; d += 0.15f)
        {
            var probe = head + direction * d;
            if (_layout is not null && (probe.Y < BlockTopAt(probe) + 0.15f
                || !_outdoors && probe.Y > _layout.CeilingMeters(CellSize) - 0.25f))
            {
                free = Mathf.Max(0.4f, d - 0.25f);
                break;
            }
        }
        _camCollision = free < _camCollision ? free : Mathf.Min(free, _camCollision + CameraReturnSpeed * dt);
        // Fest am Kopf (kein Nachschwimmen).
        _camera.Position = head + direction * _camCollision;
        _camera.LookAt(head);
    }

    /// <summary>Aktuelles Ziel für Zauber (wie WoW: angeklickte Figur bzw. mit F angegriffenes Monster).</summary>
    private uint _currentTarget;

    private void SetTarget(uint id)
    {
        _currentTarget = id;
        if (_session is not null && _avatars.TryGetValue(id, out var node) && node.GetNodeOrNull<Label3D>("Name") is { } label)
            _status.Text = $"Ziel: {label.Text}";
    }

    /// <summary>Linksklick auf eine Figur (NPC, Monster, Spieler): anschauen und als Ziel wählen.</summary>
    private bool TryLookAt(Vector2 screen)
    {
        if (_session is not { InWorld: true })
            return false;
        var origin = _camera.ProjectRayOrigin(screen);
        var dir = _camera.ProjectRayNormal(screen);
        uint best = 0;
        float bestDistance = 0.7f;
        foreach (var (id, node) in _avatars)
        {
            if (id == _session.PlayerId)
                continue;
            var center = node.Position + new Vector3(0, 0.8f, 0);
            float along = (center - origin).Dot(dir);
            float d = along <= 0 ? float.MaxValue : (origin + dir * along).DistanceTo(center);
            if (d < bestDistance)
                (best, bestDistance) = (id, d);
        }
        if (best == 0)
            return false;
        SetTarget(best);
        _ = _session.LookAsync(best);
        return true;
    }
}
