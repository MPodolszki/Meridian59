using System.Collections.Generic;
using System.Linq;
using Godot;
using Meridian.Protocol;

namespace Meridian.Client;

/// <summary>Charaktererstellung wie char.dll des Originals: Name, Beschreibung, Geschlecht, Gesicht und Farben,
/// sechs Werte (je 1..50, zusammen höchstens 200) und Startsprüche/-fertigkeiten für höchstens 45 Punkte.
/// Erscheint, wenn der Server den Charakter als neu meldet; die Prüfung wiederholt der Server.</summary>
public partial class Main
{
    private const int MaxStatTotal = 200;
    private const int AbilityBudget = 45;
    private static readonly string[] StatNames = ["Stärke", "Intellekt", "Ausdauer", "Geschick", "Mystik", "Zielen"];
    /// <summary>Standardwerte des Servers (Attributes.Default), zusammen 200.</summary>
    private static readonly int[] DefaultStats = [35, 30, 35, 30, 40, 30];

    private PanelContainer _creationPanel = null!;
    private CharacterChoices? _choices;
    private LineEdit _newName = null!;
    private TextEdit _newDescription = null!;
    private OptionButton _gender = null!, _hair = null!, _eyes = null!, _nose = null!, _mouth = null!, _hairColor = null!, _skinColor = null!;
    private readonly List<SpinBox> _stats = new();
    private Label _statTotal = null!, _abilityTotal = null!;
    private VBoxContainer _abilityBox = null!;
    private readonly List<(CheckBox Box, CreationAbility Ability, bool Spell)> _abilities = new();
    private Button _create = null!;

    private void BuildCreationUi(CanvasLayer ui)
    {
        _creationPanel = new PanelContainer { Position = new Vector2(320, 20), Visible = false };
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(620, 610), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _creationPanel.AddChild(scroll);
        var box = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        scroll.AddChild(box);
        box.AddChild(new Label { Text = "Neuer Charakter" });

        _newName = AddField(box, "Name (3–30 Zeichen)", "");
        _newName.MaxLength = 30;
        box.AddChild(new Label { Text = "Beschreibung" });
        _newDescription = new TextEdit { CustomMinimumSize = new Vector2(0, 60), WrapMode = TextEdit.LineWrappingMode.Boundary };
        box.AddChild(_newDescription);

        var look = new GridContainer { Columns = 4 };
        box.AddChild(look);
        _gender = AddChoice(look, "Geschlecht");
        _gender.AddItem("Mann");
        _gender.AddItem("Frau");
        _gender.ItemSelected += _ => FillFaceChoices();
        _hair = AddChoice(look, "Frisur");
        _eyes = AddChoice(look, "Augen");
        _nose = AddChoice(look, "Nase");
        _mouth = AddChoice(look, "Mund");
        _hairColor = AddChoice(look, "Haarfarbe");
        _skinColor = AddChoice(look, "Hautfarbe");

        box.AddChild(new HSeparator());
        var stats = new GridContainer { Columns = 4 };
        box.AddChild(stats);
        for (int i = 0; i < StatNames.Length; i++)
        {
            stats.AddChild(new Label { Text = StatNames[i] });
            var spin = new SpinBox { MinValue = 1, MaxValue = 50, Value = DefaultStats[i] };
            spin.ValueChanged += _ => UpdateCreationTotals();
            stats.AddChild(spin);
            _stats.Add(spin);
        }
        _statTotal = new Label();
        box.AddChild(_statTotal);

        box.AddChild(new HSeparator());
        _abilityTotal = new Label();
        box.AddChild(_abilityTotal);
        _abilityBox = new VBoxContainer();
        box.AddChild(_abilityBox);

        _create = new Button { Text = "Erstellen" };
        _create.Pressed += OnCreatePressed;
        box.AddChild(_create);
        ui.AddChild(_creationPanel);
    }

    private static OptionButton AddChoice(GridContainer grid, string label)
    {
        grid.AddChild(new Label { Text = label });
        var option = new OptionButton { CustomMinimumSize = new Vector2(150, 0) };
        grid.AddChild(option);
        return option;
    }

    private bool HandleCreationEvent(ClientEvent e)
    {
        switch (e)
        {
            case ClientEvent.CharacterCreation c:
                ShowCreation(c.Choices);
                return true;
            case ClientEvent.CharacterRejected:
                _create.Disabled = false;
                _status.Text = "Name schon vergeben oder ungültig – bitte einen anderen wählen.";
                return true;
            case ClientEvent.EnteredRoom:
                _creationPanel.Visible = false;
                return false;   // Raumwechsel wird weiter normal behandelt
        }
        return false;
    }

    private void ShowCreation(CharacterChoices choices)
    {
        _choices = choices;
        _loginPanel.Visible = false;
        _newName.Text = choices.Name;
        _gender.Selected = 0;
        FillFaceChoices();
        Fill(_hairColor, choices.HairColors.Length);
        Fill(_skinColor, choices.SkinColors.Length);

        foreach (var child in _abilityBox.GetChildren())
            child.QueueFree();
        _abilities.Clear();
        _abilityBox.AddChild(new Label { Text = "Zaubersprüche" });
        foreach (var s in choices.Spells)
            AddAbility(s, true, $"{s.Name} ({SchoolNames[Mathf.Clamp(s.School, 0, 6)]}, {s.Cost} Punkte)");
        _abilityBox.AddChild(new Label { Text = "Fertigkeiten" });
        foreach (var s in choices.Skills)
            AddAbility(s, false, $"{s.Name} ({s.Cost} Punkte)");

        _create.Disabled = false;
        UpdateCreationTotals();
        _creationPanel.Visible = true;
        _newName.GrabFocus();
        if (_autoCreate == "show" && _screenshotPath is not null)
            _screenshotTimer = _screenshotDelay;
        else if (_autoCreate == "auto")
        {
            _gender.Selected = 1;
            FillFaceChoices();
            _abilities.FirstOrDefault(a => a.Spell).Box?.SetPressedNoSignal(true);
            UpdateCreationTotals();
            CallDeferred(MethodName.OnCreatePressed);
        }
    }

    private void AddAbility(CreationAbility ability, bool spell, string text)
    {
        var check = new CheckBox { Text = text, TooltipText = ability.Description };
        check.Toggled += _ => UpdateCreationTotals();
        _abilityBox.AddChild(check);
        _abilities.Add((check, ability, spell));
    }

    /// <summary>Gesichtslisten des gewählten Geschlechts (die Grafiken zeigt erst das Original; hier nur Varianten).</summary>
    private void FillFaceChoices()
    {
        if (_choices is null)
            return;
        var face = Face();
        Fill(_hair, face.Hair.Length);
        Fill(_eyes, face.Eyes.Length);
        Fill(_nose, face.Noses.Length);
        Fill(_mouth, face.Mouths.Length);
    }

    private FaceOptions Face() => _gender.Selected == 1 ? _choices!.Female : _choices!.Male;

    private static void Fill(OptionButton option, int count)
    {
        option.Clear();
        for (int i = 0; i < count; i++)
            option.AddItem($"Variante {i + 1}");
        option.Selected = count > 0 ? 0 : -1;
    }

    private int StatSum() => _stats.Sum(s => (int)s.Value);
    private int AbilityCost() => _abilities.Where(a => a.Box.ButtonPressed).Sum(a => a.Ability.Cost);

    private void UpdateCreationTotals()
    {
        int sum = StatSum(), cost = AbilityCost();
        _statTotal.Text = $"Summe der Werte: {sum} / {MaxStatTotal}";
        _statTotal.Modulate = sum > MaxStatTotal ? Colors.Red : Colors.White;
        _abilityTotal.Text = $"Punkte für Sprüche und Fertigkeiten: {cost} / {AbilityBudget}";
        _abilityTotal.Modulate = cost > AbilityBudget ? Colors.Red : Colors.White;
    }

    private async void OnCreatePressed()
    {
        if (_session is null || _choices is null)
            return;
        string name = _newName.Text.Trim();
        if (name.Length is < 3 or > 30)
        {
            _status.Text = "Der Name muss 3 bis 30 Zeichen lang sein.";
            return;
        }
        if (StatSum() > MaxStatTotal || AbilityCost() > AbilityBudget)
        {
            _status.Text = "Zu viele Punkte verteilt.";
            return;
        }
        var face = Face();
        _create.Disabled = true;
        _status.Text = "Charakter wird erstellt …";
        await _session.CreateCharacterAsync(_choices.CharacterId, name, _newDescription.Text.Trim(), _gender.Selected == 1 ? 2 : 1,
            [face.Head, face.Hair[_hair.Selected], face.Eyes[_eyes.Selected], face.Noses[_nose.Selected], face.Mouths[_mouth.Selected]],
            _choices.HairColors[_hairColor.Selected], _choices.SkinColors[_skinColor.Selected],
            _stats.Select(s => (int)s.Value).ToList(),
            _abilities.Where(a => a.Spell && a.Box.ButtonPressed).Select(a => a.Ability.Id).ToList(),
            _abilities.Where(a => !a.Spell && a.Box.ButtonPressed).Select(a => a.Ability.Id).ToList());
    }
}
