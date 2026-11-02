using Godot;

namespace Meridian.Client;

/// <summary>
/// Steuerung und Kamera nach WoW-Vorbild:
/// W/S vor/zurück (rückwärts langsamer), A/D drehen (mit rechter Maustaste seitlich), Q/E seitlich,
/// Shift gehen statt rennen, NumLock Dauerlauf, Leertaste springen.
/// Rechte Maustaste ziehen: Figur und Kamera drehen; linke Maustaste ziehen: nur die Kamera kreist;
/// beide Tasten: vorwärts laufen; Mausrad: Zoom. Ein Linksklick ohne Ziehen benutzt Türen/Hebel bzw. schaut
/// eine Figur an.
/// </summary>
public partial class Main
{
    private const float TurnSpeed = Mathf.Pi;           // A/D: 180°/s
    private const float MouseSensitivity = 0.005f;
    private const float BackpedalFactor = 0.6f;
    private const float MinZoom = 1.5f, MaxZoom = 18f;

    private float _camYawOffset;                         // nur Kamera (linke Maustaste)
    private float _camPitch = 0.35f;                     // Blick leicht von oben
    private float _camDistance = 5f;
    private bool _autoRun;
    private Vector2 _pressPosition;
    private bool _dragged;

    /// <summary>Maus und Tasten der Steuerung; true, wenn das Ereignis verbraucht wurde.</summary>
    private bool HandleControlsInput(InputEvent e)
    {
        switch (e)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.WheelUp, Pressed: true }:
                _camDistance = Mathf.Max(MinZoom, _camDistance * 0.9f);
                return true;
            case InputEventMouseButton { ButtonIndex: MouseButton.WheelDown, Pressed: true }:
                _camDistance = Mathf.Min(MaxZoom, _camDistance * 1.1f);
                return true;
            case InputEventMouseButton { ButtonIndex: MouseButton.Left or MouseButton.Right } button:
                if (button.Pressed)
                {
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
                {
                    // Figur dreht sich mit, die Kamera steht wieder hinter ihr.
                    _yaw -= motion.Relative.X * MouseSensitivity;
                    _camYawOffset = 0;
                }
                else
                    _camYawOffset -= motion.Relative.X * MouseSensitivity;
                _camPitch = Mathf.Clamp(_camPitch + motion.Relative.Y * MouseSensitivity, -0.4f, 1.3f);
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
        bool rmb = Input.IsMouseButtonPressed(MouseButton.Right);
        bool both = rmb && Input.IsMouseButtonPressed(MouseButton.Left);
        bool forward = Input.IsPhysicalKeyPressed(Key.W) || both;
        bool back = Input.IsPhysicalKeyPressed(Key.S);
        if (forward || back)
            _autoRun = _autoRun && !back && !Input.IsPhysicalKeyPressed(Key.W);
        if (forward || _autoRun) input.Y -= 1;
        if (back) input.Y += 1;
        // A/D: ohne rechte Maustaste drehen, mit ihr seitlich gehen (wie WoW); Q/E immer seitlich.
        if (Input.IsPhysicalKeyPressed(Key.A)) { if (rmb) input.X -= 1; else _yaw += TurnSpeed * delta; }
        if (Input.IsPhysicalKeyPressed(Key.D)) { if (rmb) input.X += 1; else _yaw -= TurnSpeed * delta; }
        if (Input.IsPhysicalKeyPressed(Key.Q)) input.X -= 1;
        if (Input.IsPhysicalKeyPressed(Key.E)) input.X += 1;
        input += _routeInput;   // Test-Automatik (AutoRoute.cs)
        // Rennen ist Standard, Shift geht; rückwärts langsamer.
        float speed = Input.IsPhysicalKeyPressed(Key.Shift) ? WalkSpeed : RunSpeed;
        if (input.Y > 0)
            speed *= BackpedalFactor;
        // Läuft man los, schwenkt eine mit der linken Maustaste gedrehte Kamera wieder hinter die Figur.
        if (input != Vector2.Zero && !Input.IsMouseButtonPressed(MouseButton.Left))
            _camYawOffset = Mathf.LerpAngle(_camYawOffset, 0, 1f - Mathf.Exp(-3f * delta));
        return (input, speed);
    }

    private void UpdateCamera(Node3D self)
    {
        // Beim Springen auf Fußhöhe bleiben, damit der Sprung sichtbar ist und die Kamera nicht hüpft.
        var feet = self.Position with { Y = self.Position.Y - _jumpOffset };
        var head = feet + new Vector3(0, 1.1f, 0);
        var offset = _cameraMode == "front"
            ? new Vector3(0, 0.5f, -3.2f).Rotated(Vector3.Up, _yaw)
            : new Vector3(0, Mathf.Sin(_camPitch), Mathf.Cos(_camPitch)).Rotated(Vector3.Up, _yaw + _camYawOffset) * _camDistance;
        // Kamera-Kollision: entlang des Strahls vom Kopf bis zum ersten höheren Block oder zur Wand.
        float allowed = 1f;
        for (float t = 0.05f; t <= 1f; t += 0.05f)
        {
            var probe = head + offset * t;
            if (_layout is not null && probe.Y < BlockTopAt(probe))
            {
                allowed = Mathf.Max(0.12f, t - 0.08f);
                break;
            }
        }
        var wantedCamera = head + offset * allowed;
        // Innenräume haben eine Decke: Kamera darunter halten.
        if (_layout is not null && !_outdoors)
            wantedCamera = wantedCamera with { Y = Mathf.Min(wantedCamera.Y, _layout.CeilingMeters(CellSize) - 0.3f) };
        // Weich nachführen; nur bei großen Sprüngen (Raumwechsel, Teleport) sofort setzen.
        _camera.Position = _camera.Position.DistanceTo(wantedCamera) > 8f
            ? wantedCamera
            : _camera.Position.Lerp(wantedCamera, 1f - Mathf.Exp(-18f * (float)GetProcessDeltaTime()));
        _camera.LookAt(head);
    }

    /// <summary>Linksklick auf eine Figur (NPC, Monster, Spieler): anschauen.</summary>
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
        _ = _session.LookAsync(best);
        return true;
    }
}
