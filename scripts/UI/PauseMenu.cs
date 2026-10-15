using Godot;
using Veld.Audio;
using Veld.Core;
using Veld.Sim;
using Veld.Sim.Dialogue;
using Veld.World;

namespace Veld.UI;

/// <summary>Escape menu: resume, save, load, a few settings, quit. Pauses the scene tree while open.</summary>
public partial class PauseMenu : Control
{
    public bool IsOpen => Visible;
    private Button _load = null!;
    private Label _status = null!;
    private Main _main = null!;
    private GameData _data = null!;
    private Settings _settings = null!;
    private SaveGame _save = null!;
    private UiState _ui = null!;
    private DialogueSystem _dialogue = null!;

    /// <summary>Call before adding to the tree.</summary>
    public void Init(Main main, GameData data, Settings settings, SaveGame save, UiState ui, DialogueSystem dialogue)
    {
        _main = main;
        _data = data;
        _settings = settings;
        _save = save;
        _ui = ui;
        _dialogue = dialogue;
    }

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        ProcessMode = ProcessModeEnum.Always;
        Visible = false;

        var dim = new ColorRect { Color = new Color(0, 0, 0, 0.55f) };
        dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(dim);

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);
        var panel = new PanelContainer();
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.12f, 0.09f, 0.06f, 0.96f), BorderColor = new Color(0.8f, 0.7f, 0.5f),
            BorderWidthLeft = 2, BorderWidthRight = 2, BorderWidthTop = 2, BorderWidthBottom = 2,
            ContentMarginLeft = 32, ContentMarginRight = 32, ContentMarginTop = 24, ContentMarginBottom = 24,
        };
        panel.AddThemeStyleboxOverride("panel", style);
        center.AddChild(panel);
        var v = new VBoxContainer { CustomMinimumSize = new Vector2(360, 0) };
        v.AddThemeConstantOverride("separation", 8);
        panel.AddChild(v);

        var title = new Label { Text = "VELD", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 34);
        title.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.5f));
        v.AddChild(title);
        var sub = new Label { Text = string.IsNullOrEmpty(_data.World.Name) ? "Cape Colony, April 1901" : _data.World.Name, HorizontalAlignment = HorizontalAlignment.Center };
        sub.AddThemeColorOverride("font_color", new Color(0.8f, 0.75f, 0.65f));
        v.AddChild(sub);
        v.AddChild(new HSeparator());

        v.AddChild(Btn("Resume", Toggle));
        v.AddChild(Btn("Save game", () => { _save.Save(); _status.Text = "Saved to user://save.json"; }));
        _load = Btn("Load game", () => { if (_save.Load()) { _status.Text = "Loaded."; Toggle(); } else _status.Text = "No save found."; });
        v.AddChild(_load);
        v.AddChild(new HSeparator());

        v.AddChild(Slider("Mouse sensitivity", 0.0008f, 0.008f, _settings.MouseSensitivity, x => { _settings.MouseSensitivity = x; _settings.Save(); }));
        v.AddChild(Slider("Volume", 0f, 1f, _settings.Volume, x => { _settings.Volume = x; Sfx.ApplyVolume(x); _settings.Save(); }));
        var invert = new CheckBox { Text = "Invert mouse Y", ButtonPressed = _settings.InvertY };
        invert.Toggled += on => { _settings.InvertY = on; _settings.Save(); };
        v.AddChild(invert);
        var qRow = new HBoxContainer();
        qRow.AddChild(new Label { Text = "Graphics quality", CustomMinimumSize = new Vector2(150, 0) });
        var quality = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        quality.AddItem("Low (no GI, short shadows)"); quality.AddItem("Medium (GI, AO)"); quality.AddItem("High (GI, AO, volumetric light, TAA)");
        quality.Selected = (int)_settings.GraphicsQuality;
        quality.ItemSelected += i => { _settings.GraphicsQuality = (Quality)(int)i; _main.DayNight.ApplyQuality((Quality)(int)i); _settings.Save(); };
        qRow.AddChild(quality);
        v.AddChild(qRow);
        var timeRow = new HBoxContainer();
        timeRow.AddChild(new Label { Text = "Time of day" });
        var time = new HSlider { MinValue = 0, MaxValue = 24, Step = 0.25, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        time.ValueChanged += x => _main.Sim.Clock.Hour = (float)x;
        timeRow.AddChild(time);
        v.AddChild(timeRow);
        v.AddChild(new HSeparator());

        var help = new Label
        {
            Text = "WASD move · Shift sprint · Space jump · Mouse look\nRight mouse aim · Left mouse shoot · R reload · 1/2 weapons · X holster\nE talk / mount / search · H whistle horse · M map (click to set a waypoint)\nQ Dead Eye while aiming · T skip 2 hours · Esc menu\nIn a car: W/S drive, A/D steer, Space handbrake, H horn, E get out",
            AutowrapMode = TextServer.AutowrapMode.Word,
        };
        help.AddThemeFontSizeOverride("font_size", 13);
        help.AddThemeColorOverride("font_color", new Color(0.75f, 0.7f, 0.6f));
        v.AddChild(help);
        if (!string.IsNullOrEmpty(_data.World.Attribution))
        {
            var attr = new Label { Text = _data.World.Attribution, AutowrapMode = TextServer.AutowrapMode.Word, HorizontalAlignment = HorizontalAlignment.Center };
            attr.AddThemeFontSizeOverride("font_size", 11);
            attr.AddThemeColorOverride("font_color", new Color(0.6f, 0.58f, 0.5f));
            v.AddChild(attr);
        }
        v.AddChild(Btn("Quit to desktop", () => GetTree().Quit()));
        _status = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _status.AddThemeColorOverride("font_color", new Color(0.8f, 0.75f, 0.65f));
        v.AddChild(_status);

        VisibilityChanged += () => { if (Visible) time.SetValueNoSignal(_main.Sim.Clock.Hour); };
    }

    private static Button Btn(string text, System.Action onPressed)
    {
        var b = new Button { Text = text };
        b.AddThemeFontSizeOverride("font_size", 18);
        b.Pressed += onPressed;
        return b;
    }

    private static HBoxContainer Slider(string text, float min, float max, float value, System.Action<float> onChange)
    {
        var row = new HBoxContainer();
        row.AddChild(new Label { Text = text, CustomMinimumSize = new Vector2(150, 0) });
        var s = new HSlider { MinValue = min, MaxValue = max, Step = (max - min) / 100f, Value = value, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        s.ValueChanged += x => onChange((float)x);
        row.AddChild(s);
        return row;
    }

    public void Toggle()
    {
        Visible = !Visible;
        _status.Text = "";
        _load.Disabled = !_save.HasSave;
        GetTree().Paused = Visible;
        _ui.InputLocked = Visible || _dialogue.IsActive;
        Input.MouseMode = Visible || _dialogue.IsActive ? Input.MouseModeEnum.Visible : Input.MouseModeEnum.Captured;
    }
}
