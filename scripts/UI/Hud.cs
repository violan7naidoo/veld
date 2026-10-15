using System.Collections.Generic;
using Godot;
using Veld.Core;
using Veld.Sim;
using Veld.Sim.Missions;
using Veld.World;

namespace Veld.UI;

/// <summary>
/// In-game overlay: health and stamina bars, ammunition, money, clock, wanted status, the current
/// objective, a compass strip with the objective and horse markers, the interaction prompt, a
/// crosshair while aiming, and notification toasts. Everything is built in code; restyle here.
/// </summary>
public partial class Hud : Control
{
    private ProgressBar _health = null!, _stamina = null!, _deadEye = null!;
    private Minimap _minimap = null!;
    private Label _location = null!;
    private Label _markWaypoint = null!;
    private float _locTimer;
    private Label _ammo = null!, _money = null!, _clock = null!, _wanted = null!, _objective = null!, _prompt = null!, _weapon = null!;
    private Control _compass = null!;
    private Label _compassN = null!, _compassE = null!, _compassS = null!, _compassW = null!, _markObjective = null!, _markHorse = null!;
    private ColorRect _crosshair = null!, _damage = null!;
    private VBoxContainer _toasts = null!;
    private readonly List<(Label label, float life)> _live = new();
    private float _damageFlash;
    private Main _main = null!;
    private GameData _data = null!;
    private Purse _purse = null!;
    private Notifier _notifier = null!;
    private UiState _ui = null!;
    private MissionManager _missions = null!;
    private WantedSystem _wantedSystem = null!;

    private static readonly Color Parchment = new(0.95f, 0.9f, 0.78f);
    private static readonly Color Ink = new(0.08f, 0.06f, 0.04f, 0.65f);

    /// <summary>Call before adding to the tree.</summary>
    public void Init(Main main, GameData data, Purse purse, Notifier notifier, UiState ui, MissionManager missions, WantedSystem wanted)
    {
        _main = main;
        _data = data;
        _purse = purse;
        _notifier = notifier;
        _ui = ui;
        _missions = missions;
        _wantedSystem = wanted;
    }

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;

        _damage = new ColorRect { Color = new Color(0.6f, 0f, 0f, 0f), MouseFilter = MouseFilterEnum.Ignore };
        _damage.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_damage);

        // bottom-left: bars, weapon, ammo
        var bl = new VBoxContainer { Position = new Vector2(24, -140), MouseFilter = MouseFilterEnum.Ignore };
        bl.SetAnchorsPreset(LayoutPreset.BottomLeft);
        bl.AnchorTop = 1; bl.AnchorBottom = 1; bl.OffsetTop = -170; bl.OffsetBottom = -24; bl.OffsetLeft = 276; bl.OffsetRight = 592;
        AddChild(bl);
        _weapon = MakeLabel(20, Parchment);
        bl.AddChild(_weapon);
        _ammo = MakeLabel(26, Parchment);
        bl.AddChild(_ammo);
        _health = Bar(new Color(0.75f, 0.15f, 0.12f));
        bl.AddChild(_health);
        _stamina = Bar(new Color(0.8f, 0.65f, 0.25f));
        bl.AddChild(_stamina);
        _deadEye = Bar(new Color(0.9f, 0.9f, 0.75f));
        bl.AddChild(_deadEye);

        // bottom-left: minimap with the location name above it
        _minimap = new Minimap { Name = "Minimap" };
        _minimap.Init(_main, _data, _ui, _missions, _wantedSystem);
        _minimap.SetAnchorsPreset(LayoutPreset.BottomLeft);
        _minimap.AnchorTop = 1; _minimap.AnchorBottom = 1;
        _minimap.OffsetLeft = 24; _minimap.OffsetRight = 254; _minimap.OffsetTop = -254; _minimap.OffsetBottom = -24;
        AddChild(_minimap);
        _location = MakeLabel(17, Parchment);
        _location.SetAnchorsPreset(LayoutPreset.BottomLeft);
        _location.AnchorTop = 1; _location.AnchorBottom = 1;
        _location.OffsetLeft = 24; _location.OffsetRight = 420; _location.OffsetTop = -284; _location.OffsetBottom = -256;
        AddChild(_location);

        // top-left: money, clock, wanted
        var tl = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        tl.SetAnchorsPreset(LayoutPreset.TopLeft);
        tl.OffsetLeft = 24; tl.OffsetTop = 20; tl.OffsetRight = 420; tl.OffsetBottom = 140;
        AddChild(tl);
        _money = MakeLabel(24, Parchment);
        tl.AddChild(_money);
        _clock = MakeLabel(18, Parchment);
        tl.AddChild(_clock);
        _wanted = MakeLabel(22, new Color(0.95f, 0.3f, 0.25f));
        tl.AddChild(_wanted);

        // top-right: objective
        _objective = MakeLabel(20, Parchment);
        _objective.HorizontalAlignment = HorizontalAlignment.Right;
        _objective.AutowrapMode = TextServer.AutowrapMode.Word;
        _objective.SetAnchorsPreset(LayoutPreset.TopRight);
        _objective.AnchorLeft = 1; _objective.AnchorRight = 1;
        _objective.OffsetLeft = -460; _objective.OffsetRight = -24; _objective.OffsetTop = 20; _objective.OffsetBottom = 120;
        AddChild(_objective);

        // top-centre: compass
        _compass = new Control { MouseFilter = MouseFilterEnum.Ignore, ClipContents = true };
        _compass.SetAnchorsPreset(LayoutPreset.CenterTop);
        _compass.AnchorLeft = 0.5f; _compass.AnchorRight = 0.5f;
        _compass.OffsetLeft = -220; _compass.OffsetRight = 220; _compass.OffsetTop = 14; _compass.OffsetBottom = 44;
        AddChild(_compass);
        var compassBg = new ColorRect { Color = Ink, MouseFilter = MouseFilterEnum.Ignore };
        compassBg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _compass.AddChild(compassBg);
        _compassN = CompassLabel("N"); _compassE = CompassLabel("E"); _compassS = CompassLabel("S"); _compassW = CompassLabel("W");
        _markObjective = CompassLabel("◆"); _markObjective.Modulate = new Color(1f, 0.85f, 0.3f);
        _markHorse = CompassLabel("♞"); _markHorse.Modulate = new Color(0.8f, 0.9f, 1f);
        _markWaypoint = CompassLabel("◆"); _markWaypoint.Modulate = new Color(0.4f, 0.85f, 1f);
        var tick = new ColorRect { Color = Parchment, Size = new Vector2(2, 30), Position = new Vector2(219, 0), MouseFilter = MouseFilterEnum.Ignore };
        _compass.AddChild(tick);

        // centre: crosshair and prompt
        _crosshair = new ColorRect { Color = new Color(1, 1, 1, 0.9f), Size = new Vector2(4, 4), MouseFilter = MouseFilterEnum.Ignore, Visible = false };
        _crosshair.SetAnchorsPreset(LayoutPreset.Center);
        _crosshair.OffsetLeft = -2; _crosshair.OffsetTop = -2; _crosshair.OffsetRight = 2; _crosshair.OffsetBottom = 2;
        AddChild(_crosshair);
        _prompt = MakeLabel(22, Parchment);
        _prompt.HorizontalAlignment = HorizontalAlignment.Center;
        _prompt.SetAnchorsPreset(LayoutPreset.CenterBottom);
        _prompt.AnchorLeft = 0.5f; _prompt.AnchorRight = 0.5f; _prompt.AnchorTop = 1; _prompt.AnchorBottom = 1;
        _prompt.OffsetLeft = -300; _prompt.OffsetRight = 300; _prompt.OffsetTop = -150; _prompt.OffsetBottom = -110;
        AddChild(_prompt);

        // bottom-right: toasts
        _toasts = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.End };
        _toasts.SetAnchorsPreset(LayoutPreset.BottomRight);
        _toasts.AnchorLeft = 1; _toasts.AnchorRight = 1; _toasts.AnchorTop = 1; _toasts.AnchorBottom = 1;
        _toasts.OffsetLeft = -560; _toasts.OffsetRight = -24; _toasts.OffsetTop = -300; _toasts.OffsetBottom = -24;
        AddChild(_toasts);

        _notifier.Notified += Toast;
    }

    public override void _ExitTree()
    {
        _notifier.Notified -= Toast;
    }

    private static Label MakeLabel(int size, Color color)
    {
        var l = new Label { MouseFilter = MouseFilterEnum.Ignore };
        l.AddThemeFontSizeOverride("font_size", size);
        l.AddThemeColorOverride("font_color", color);
        l.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.8f));
        l.AddThemeConstantOverride("outline_size", 4);
        return l;
    }

    private Label CompassLabel(string text)
    {
        var l = MakeLabel(18, Parchment);
        l.Text = text;
        l.Position = new Vector2(0, 2);
        _compass.AddChild(l);
        return l;
    }

    private static ProgressBar Bar(Color fill)
    {
        var bar = new ProgressBar { MinValue = 0, MaxValue = 100, Value = 100, ShowPercentage = false, CustomMinimumSize = new Vector2(300, 14), MouseFilter = MouseFilterEnum.Ignore };
        var bg = new StyleBoxFlat { BgColor = Ink };
        var fg = new StyleBoxFlat { BgColor = fill };
        bar.AddThemeStyleboxOverride("background", bg);
        bar.AddThemeStyleboxOverride("fill", fg);
        return bar;
    }

    public void SetPrompt(string text) => _prompt.Text = text;
    public void FlashDamage() => _damageFlash = 1f;

    private void Toast(string message)
    {
        var l = MakeLabel(18, Parchment);
        l.Text = message;
        l.AutowrapMode = TextServer.AutowrapMode.Word;
        l.HorizontalAlignment = HorizontalAlignment.Right;
        _toasts.AddChild(l);
        _live.Add((l, 7f));
        while (_live.Count > 5) { _live[0].label.QueueFree(); _live.RemoveAt(0); }
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        var player = _main.Player;

        _health.Value = player.Health;
        _stamina.Value = player.IsMounted ? player.Mounted!.Stamina : player.Stamina;
        _deadEye.Value = player.DeadEye;
        _locTimer += dt;
        if (_locTimer > 0.5f)
        {
            _locTimer = 0f;
            _location.Text = MapImage.LocationName(_data, new Vector2(player.GlobalPosition.X, player.GlobalPosition.Z));
        }
        var w = player.CurrentWeapon;
        if (player.Driving != null)
        {
            _weapon.Text = player.Driving.Kind == "taxi" ? "Minibus taxi" : "Car";
            _ammo.Text = $"{Mathf.Abs(player.Driving.ForwardSpeed) * 3.6f:0} km/h";
        }
        else
        {
            _weapon.Text = w == null ? "Unarmed  (1/2 to draw)" : w.Data.Name;
            _ammo.Text = w == null ? "" : w.Reloading ? "reloading…" : $"{w.InMag} / {w.Reserve}";
        }
        _money.Text = Money.Format(_purse.Pence);
        var clock = _main.Sim.Clock;
        _clock.Text = $"Day {clock.Day}, {clock.TimeText}  —  " + (_data.World.IsModern ? "today" : "April 1901") + "  ·  " + _main.Weather.Describe();
        _wanted.Text = _wantedSystem.Label;
        _objective.Text = _missions.ObjectiveText;
        _crosshair.Visible = player.Aiming;

        // compass: 360 degrees across 2 widths of the strip, centred on the camera yaw
        float yaw = player.Camera.Yaw;
        float width = _compass.Size.X;
        void Place(Label l, float worldAngle, bool visible = true)
        {
            float rel = Mathf.Wrap(worldAngle - yaw, -Mathf.Pi, Mathf.Pi);
            float x = width / 2f + rel / Mathf.Pi * width * 0.9f;
            l.Position = new Vector2(x - 8, 2);
            l.Visible = visible && Mathf.Abs(rel) < Mathf.Pi * 0.55f;
        }
        // yaw 0 looks down -Z which we call north
        Place(_compassN, 0f); Place(_compassE, -Mathf.Pi / 2); Place(_compassS, Mathf.Pi); Place(_compassW, Mathf.Pi / 2);
        var marker = _missions.MarkerPosition;
        if (marker.HasValue)
        {
            var d = marker.Value - player.GlobalPosition;
            Place(_markObjective, Mathf.Atan2(-d.X, -d.Z));
            _markObjective.Text = $"◆ {d.Length():0}m";
        }
        else _markObjective.Visible = false;
        if (_ui.Waypoint.HasValue)
        {
            var d = _ui.Waypoint.Value - player.GlobalPosition;
            Place(_markWaypoint, Mathf.Atan2(-d.X, -d.Z));
            _markWaypoint.Text = $"◆ {d.Length():0}m";
            if (d.Length() < 12f) { _ui.Waypoint = null; _notifier.Notify("You have reached your waypoint."); }
        }
        else _markWaypoint.Visible = false;
        var horse = _main.World.Horse;
        if (horse != null && !player.IsMounted)
        {
            var d = horse.GlobalPosition - player.GlobalPosition;
            Place(_markHorse, Mathf.Atan2(-d.X, -d.Z), d.Length() > 6f);
        }
        else _markHorse.Visible = false;

        _damageFlash = Mathf.Max(0f, _damageFlash - dt * 2.5f);
        _damage.Color = new Color(0.6f, 0f, 0f, _damageFlash * 0.35f + (player.Health < 30 ? 0.12f : 0f));

        for (int i = _live.Count - 1; i >= 0; i--)
        {
            var (label, life) = _live[i];
            life -= dt;
            if (life <= 0f) { label.QueueFree(); _live.RemoveAt(i); continue; }
            label.Modulate = new Color(1, 1, 1, Mathf.Clamp(life, 0f, 1f));
            _live[i] = (label, life);
        }
    }
}
