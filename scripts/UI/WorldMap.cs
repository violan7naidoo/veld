using Godot;
using Veld.Characters;
using Veld.Core;
using Veld.World;

namespace Veld.UI;

/// <summary>
/// The full-screen map (M). Pauses the game. Scroll to zoom, drag to pan, click to set a waypoint,
/// right-click to clear it. Road names and places appear as you zoom in, house numbers when close;
/// the search box finds an address ("42 Road 721") and sets the waypoint on it.
/// </summary>
public partial class WorldMap : Control
{
    public bool IsOpen => Visible;

    private float _zoom = 1f;      // screen px per texture px
    private Vector2 _centre;        // texture px at the screen centre
    private bool _dragging;
    private Vector2 _dragStart, _dragCentre;
    private bool _moved;
    private Label _title = null!;
    private LineEdit _search = null!;

    private static readonly Color Parchment = new(0.95f, 0.9f, 0.78f);

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        ProcessMode = ProcessModeEnum.Always;
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;
        _title = new Label { MouseFilter = MouseFilterEnum.Ignore };
        _title.AddThemeFontSizeOverride("font_size", 18);
        _title.AddThemeColorOverride("font_color", Parchment);
        _title.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.9f));
        _title.AddThemeConstantOverride("outline_size", 5);
        _title.Position = new Vector2(24, 18);
        AddChild(_title);
        _search = new LineEdit { PlaceholderText = "Find an address, e.g. 42 Road 721", ClearButtonEnabled = true };
        _search.SetAnchorsPreset(LayoutPreset.TopRight);
        _search.AnchorLeft = 1; _search.AnchorRight = 1;
        _search.OffsetLeft = -360; _search.OffsetRight = -24; _search.OffsetTop = 18; _search.OffsetBottom = 52;
        _search.TextSubmitted += Search;
        AddChild(_search);
    }

    private void Search(string text)
    {
        var game = Game.Instance;
        var entry = AddressIndex.Find(text);
        if (entry == null || game.World == null)
        {
            game.Notify(text.Trim() == "" ? "Type a house number and street, e.g. 42 Road 721." : $"No address matches \"{text.Trim()}\".");
            return;
        }
        game.Waypoint = game.World.Ground(entry.Centre);
        _centre = MapImage.ToPx(entry.Centre);
        _zoom = Mathf.Max(_zoom, 4f);
        game.Notify($"Waypoint set: {entry.Building.Address}, {entry.Building.Suburb}.");
        _search.ReleaseFocus();
        QueueRedraw();
    }

    public void Toggle()
    {
        if (!Visible && Game.Instance.Player != null)
        {
            var p = Game.Instance.Player.GlobalPosition;
            _centre = MapImage.ToPx(new Vector2(p.X, p.Z));
            float fit = Mathf.Min(Size.X, Size.Y) / MapImage.Size;
            _zoom = Mathf.Max(fit, Mathf.Clamp(_zoom, fit, 8f));
        }
        Visible = !Visible;
        if (!Visible) _search.ReleaseFocus();
        GetTree().Paused = Visible || Game.Instance.Main!.PauseMenu.IsOpen;
        Game.Instance.InputLocked = Visible || Game.Instance.Main!.PauseMenu.IsOpen || Game.Instance.Dialogue.IsActive;
        Input.MouseMode = Game.Instance.InputLocked ? Input.MouseModeEnum.Visible : Input.MouseModeEnum.Captured;
        QueueRedraw();
    }

    public override void _GuiInput(InputEvent e)
    {
        if (!Visible) return;
        if (e is InputEventMouseButton mb)
        {
            if (mb.ButtonIndex == MouseButton.WheelUp && mb.Pressed) ZoomAt(mb.Position, 1.25f);
            else if (mb.ButtonIndex == MouseButton.WheelDown && mb.Pressed) ZoomAt(mb.Position, 1f / 1.25f);
            else if (mb.ButtonIndex == MouseButton.Left)
            {
                if (mb.Pressed) { _dragging = true; _moved = false; _dragStart = mb.Position; _dragCentre = _centre; }
                else
                {
                    _dragging = false;
                    if (!_moved)
                    {
                        var world = MapImage.ToWorld(ScreenToTex(mb.Position));
                        if (Game.Instance.World != null && Game.Instance.World.Terrain.InBounds(world.X, world.Y, 0f))
                        {
                            Game.Instance.Waypoint = Game.Instance.World.Ground(world);
                            Game.Instance.Notify("Waypoint set.");
                        }
                    }
                }
            }
            else if (mb.ButtonIndex == MouseButton.Right && mb.Pressed)
            {
                Game.Instance.Waypoint = null;
            }
            AcceptEvent();
            QueueRedraw();
        }
        else if (e is InputEventMouseMotion mm && _dragging)
        {
            var d = mm.Position - _dragStart;
            if (d.Length() > 4f) _moved = true;
            _centre = _dragCentre - d / _zoom;
            AcceptEvent();
            QueueRedraw();
        }
    }

    private void ZoomAt(Vector2 screen, float factor)
    {
        var before = ScreenToTex(screen);
        _zoom = Mathf.Clamp(_zoom * factor, 0.2f, 12f);
        var after = ScreenToTex(screen);
        _centre += before - after;
    }

    private Vector2 ScreenToTex(Vector2 screen) => _centre + (screen - Size / 2f) / _zoom;
    private Vector2 TexToScreen(Vector2 tex) => Size / 2f + (tex - _centre) * _zoom;
    private Vector2 WorldToScreen(Vector2 world) => TexToScreen(MapImage.ToPx(world));

    public override void _Draw()
    {
        var game = Game.Instance;
        var tex = MapImage.Texture;
        DrawRect(new Rect2(Vector2.Zero, Size), new Color(0.07f, 0.06f, 0.05f, 0.97f));
        if (tex == null) return;
        var origin = TexToScreen(Vector2.Zero);
        DrawTextureRect(tex, new Rect2(origin, Vector2.One * MapImage.Size * _zoom), false);
        DrawRect(new Rect2(origin, Vector2.One * MapImage.Size * _zoom), Parchment, false, 2f);

        var font = ThemeDB.FallbackFont;
        float mPerScreenPx = MapImage.MetresPerPixel / _zoom;
        // labels, by importance as you zoom
        foreach (var l in MapImage.Labels)
        {
            if (l.Kind == 0 && mPerScreenPx > 2.2f) continue;
            if (l.Kind == 1 && mPerScreenPx > 6f) continue;
            var s = WorldToScreen(l.World);
            if (s.X < -200 || s.Y < -50 || s.X > Size.X + 200 || s.Y > Size.Y + 50) continue;
            int size = l.Kind == 2 ? 22 : l.Kind == 1 ? 15 : 12;
            var col = l.Kind == 2 ? new Color(1f, 0.95f, 0.8f) : l.Kind == 1 ? new Color(0.95f, 0.9f, 0.6f) : new Color(0.92f, 0.92f, 0.95f);
            float w = font.GetStringSize(l.Text, HorizontalAlignment.Left, -1, size).X;
            DrawStringOutline(font, s + new Vector2(-w / 2f, size / 2f), l.Text, HorizontalAlignment.Left, -1, size, 4, new Color(0, 0, 0, 0.85f));
            DrawString(font, s + new Vector2(-w / 2f, size / 2f), l.Text, HorizontalAlignment.Left, -1, size, col);
        }

        // house numbers, only when zoomed right in
        if (mPerScreenPx < 0.45f)
        {
            var numberColour = new Color(0.1f, 0.08f, 0.06f);
            foreach (var e in AddressIndex.Entries)
            {
                var s = WorldToScreen(e.Centre);
                if (s.X < -20 || s.Y < -20 || s.X > Size.X + 20 || s.Y > Size.Y + 20) continue;
                float w = font.GetStringSize(e.Building.Number, HorizontalAlignment.Left, -1, 11).X;
                DrawString(font, s + new Vector2(-w / 2f, 4), e.Building.Number, HorizontalAlignment.Left, -1, 11, numberColour);
            }
        }

        // markers
        var player = game.Player;
        if (game.World != null && player != null)
        {
            foreach (var n in game.World.Npcs)
            {
                if (n.IsDead) continue;
                var s = WorldToScreen(new Vector2(n.GlobalPosition.X, n.GlobalPosition.Z));
                if (n.Faction == Faction.British && game.Wanted.Level > 0) DrawCircle(s, 5f, new Color(0.3f, 0.5f, 1f));
                else if (Minimap.GivesMission(game, n)) { DrawCircle(s, 7f, new Color(1f, 0.85f, 0.3f)); DrawString(font, s + new Vector2(-3, 5), "!", HorizontalAlignment.Left, -1, 13, Colors.Black); }
            }
            var horse = game.World.Horse;
            if (horse != null && !player.IsMounted) { var s = WorldToScreen(new Vector2(horse.GlobalPosition.X, horse.GlobalPosition.Z)); DrawCircle(s, 5f, Colors.White); DrawString(font, s + new Vector2(8, 5), game.Data.World.HorseName, HorizontalAlignment.Left, -1, 13, Colors.White); }
            var car = game.World.PlayerCar;
            if (car != null && player.Driving != car) { var s = WorldToScreen(new Vector2(car.GlobalPosition.X, car.GlobalPosition.Z)); DrawRect(new Rect2(s - Vector2.One * 5f, Vector2.One * 10f), Colors.White); DrawString(font, s + new Vector2(8, 5), "Your car", HorizontalAlignment.Left, -1, 13, Colors.White); }
            var marker = game.Missions.MarkerPosition;
            if (marker.HasValue) Diamond(WorldToScreen(new Vector2(marker.Value.X, marker.Value.Z)), 9f, new Color(1f, 0.85f, 0.3f));
            if (game.Waypoint.HasValue) Diamond(WorldToScreen(new Vector2(game.Waypoint.Value.X, game.Waypoint.Value.Z)), 9f, new Color(0.4f, 0.85f, 1f));
            var ps = WorldToScreen(new Vector2(player.GlobalPosition.X, player.GlobalPosition.Z));
            float yaw = player.Camera.Yaw;
            Vector2[] arrow = { ps + new Vector2(0, -11).Rotated(yaw), ps + new Vector2(7, 8).Rotated(yaw), ps + new Vector2(0, 4).Rotated(yaw), ps + new Vector2(-7, 8).Rotated(yaw) };
            DrawColoredPolygon(arrow, Colors.White);
            DrawPolyline(new[] { arrow[0], arrow[1], arrow[2], arrow[3], arrow[0] }, Colors.Black, 1.5f);
        }

        // scale bar
        float barMetres = mPerScreenPx > 4f ? 500f : mPerScreenPx > 1.5f ? 200f : mPerScreenPx > 0.6f ? 100f : 50f;
        float barPx = barMetres / mPerScreenPx;
        var b0 = new Vector2(24, Size.Y - 30);
        DrawLine(b0, b0 + new Vector2(barPx, 0), Parchment, 3f);
        DrawString(font, b0 + new Vector2(0, -6), $"{barMetres:0} m", HorizontalAlignment.Left, -1, 13, Parchment);

        _title.Text = $"{game.Data.World.Name}\nM or Esc to close  ·  scroll to zoom  ·  drag to pan  ·  click to set a waypoint, right-click to clear"
                      + (player != null ? $"\nYou are at: {MapImage.LocationName(game.Data, new Vector2(player.GlobalPosition.X, player.GlobalPosition.Z))}" : "");
    }

    private void Diamond(Vector2 at, float r, Color c)
    {
        Vector2[] pts = { at + new Vector2(0, -r), at + new Vector2(r, 0), at + new Vector2(0, r), at + new Vector2(-r, 0) };
        DrawColoredPolygon(pts, c);
        DrawPolyline(new[] { pts[0], pts[1], pts[2], pts[3], pts[0] }, Colors.Black, 1.5f);
    }
}
