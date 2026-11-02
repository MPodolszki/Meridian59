using Godot;

namespace Meridian.Client;

/// <summary>Springen mit der Leertaste (nur im Client: das Original kennt keinen Sprung, das Protokoll keine Höhe,
/// andere Spieler sehen ihn also nicht). Der Sprung ist ein Höhenversatz über dem Boden; Kollision und gemeldete
/// Position bleiben unverändert.</summary>
public partial class Main
{
    /// <summary>Absprunggeschwindigkeit und Schwerkraft: rund 0,9 m hoch, 0,85 s in der Luft.</summary>
    private const float JumpSpeed = 4.2f, Gravity = 9.8f;

    private bool _airborne;
    private float _jumpOffset, _jumpVelocity;
    private bool _autoJump;

    /// <summary>Leertaste vor der GUI abfangen, sonst „drückt“ sie den zuletzt geklickten Button erneut.
    /// In Textfeldern bleibt sie ein Leerzeichen.</summary>
    public override void _Input(InputEvent e)
    {
        if (e is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.Space } || _session is not { InWorld: true })
            return;
        if (GetViewport().GuiGetFocusOwner() is LineEdit or TextEdit)
            return;
        StartJump();
        GetViewport().SetInputAsHandled();
    }

    private void StartJump()
    {
        if (_airborne || _session is null || !_avatars.TryGetValue(_session.PlayerId, out var self))
            return;
        _airborne = true;
        _jumpVelocity = JumpSpeed;
        CharacterModel.PlayOnce(self, "Jump_Start", lockSeconds: 0.3);
    }

    /// <summary>Sprungbewegung fortschreiben; setzt <see cref="_jumpOffset"/> (Höhe über dem Boden).</summary>
    private void UpdateJump(Node3D self, float delta)
    {
        if (_autoJump && _screenshotPath is not null && _screenshotTimer is > 0 and < 0.35)
        {
            _autoJump = false;
            StartJump();
        }
        if (!_airborne)
            return;
        _jumpVelocity -= Gravity * delta;
        _jumpOffset += _jumpVelocity * delta;
        if (_jumpOffset > 0)
        {
            CharacterModel.Hold(self, "Jump_Loop");
            return;
        }
        ResetJump();
        CharacterModel.PlayOnce(self, "Jump_Land", lockSeconds: 0.25);
    }

    private void ResetJump()
    {
        _airborne = false;
        _jumpOffset = _jumpVelocity = 0;
    }
}
