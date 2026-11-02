using System.Collections.Generic;
using System.Linq;
using Godot;
using Meridian.Protocol;

namespace Meridian.Client;

/// <summary>Magie im Client: Mana-Balken, Spruchfenster (Z) mit Fähigkeit in %, Zaubern auf sich selbst
/// bzw. ohne Ziel. Die Sprüche fordert der Client nach dem Betreten an (BP_SEND_SPELLS wie das Original).</summary>
public partial class Main
{
    private ProgressBar _manaBar = null!;
    private Label _manaText = null!;
    private PanelContainer _spellPanel = null!;
    private ItemList _spellList = null!;
    private List<ClientSpell> _knownSpells = new();
    private bool _spellsRequested;

    private void BuildSpellUi(CanvasLayer ui)
    {
        _manaBar = new ProgressBar { Position = new Vector2(20, 72), Size = new Vector2(260, 22), ShowPercentage = false, Visible = false };
        _manaBar.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = new Color(0.2f, 0.35f, 0.8f) });
        ui.AddChild(_manaBar);
        _manaText = new Label { Position = new Vector2(28, 72), Visible = false };
        ui.AddChild(_manaText);

        _spellPanel = new PanelContainer { Position = new Vector2(940, 400), Visible = false };
        var box = new VBoxContainer { CustomMinimumSize = new Vector2(320, 220) };
        _spellPanel.AddChild(box);
        box.AddChild(new Label { Text = "Zaubersprüche (Z)" });
        _spellList = new ItemList { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _spellList.ItemActivated += _ => CastSelected();
        box.AddChild(_spellList);
        var cast = new Button { Text = "Zaubern" };
        cast.Pressed += CastSelected;
        box.AddChild(cast);
        ui.AddChild(_spellPanel);
    }

    private static readonly string[] SchoolNames = ["", "Shal'ille", "Qor", "Kraanan", "Faren", "Riija", "Jala"];

    private bool HandleSpellEvent(ClientEvent e)
    {
        switch (e)
        {
            case ClientEvent.ManaChanged m:
                _manaBar.Visible = _manaText.Visible = true;
                _manaBar.MaxValue = Mathf.Max(m.MaxMana, 1);
                _manaBar.Value = m.Mana;
                _manaText.Text = $"Mana {m.Mana} / {m.MaxMana}";
                return true;
            case ClientEvent.SpellsChanged s:
                _knownSpells = s.Spells.ToList();
                _spellList.Clear();
                foreach (var sp in _knownSpells)
                    _spellList.AddItem($"{_session!.Resolve(sp.Spell.NameRsc)}  ({SchoolNames[Mathf.Clamp(sp.School, 0, 6)]}, {sp.Ability} %)");
                return true;
            case ClientEvent.EnteredRoom when !_spellsRequested && _session is not null:
                _spellsRequested = true;
                _ = _session.RequestSpellsAsync();
                return false;   // Raumwechsel wird weiter normal behandelt
        }
        return false;
    }

    /// <summary>Ausgewählten Spruch wirken: mit einem Ziel auf sich selbst, sonst ohne Ziel.</summary>
    private async void CastSelected()
    {
        if (_session is not { InWorld: true } || _spellList.GetSelectedItems() is not { Length: > 0 } sel || sel[0] >= _knownSpells.Count)
            return;
        var spell = _knownSpells[sel[0]];
        await SendOwnPosition();
        if (_avatars.TryGetValue(_session.PlayerId, out var self))
            CharacterModel.PlayOnce(self, "Cast");
        await _session.CastAsync(spell.Spell.Id, spell.Targets == 1 ? [_session.PlayerId] : []);
    }
}
