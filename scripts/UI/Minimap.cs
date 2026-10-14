using Godot;
using Veld.Characters;
using Veld.Core;
using Veld.World;

namespace Veld.UI;

/// <summary>
/// The small map in the corner of the HUD. Heading-up: the world rotates so that the way you are
/// looking is up, with a north marker on the rim. Shows the objective, waypoint, horse, mission
/// givers and the police when you are wanted.
/// </summary>
public partial class Minimap : Control
{
    public float ViewRadius { get; set; } = 110f; // metres from centre to edge

    private static readonly Color Frame = new(0.95f, 0.9f, 0.78f);

    public override void _Ready()
    {
        ClipContents = true;
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Process(double delta) => QueueRedraw();

    public override void _Draw()
    {
        var game = Game.Instance;
        var player = game.Player;
        var tex = MapImage.Texture;
        DrawRect(new Rect2(Vector2.Zero, Size), new Color(0.08f, 0.07f, 0.06f, 0.85f));
        if (player == null || tex == null) return;

        var centre = Size / 2f;
        float screenPxPerM = (Size.X / 2f) / ViewRadius;
        float texPxPerM = 1f / MapImage.MetresPerPixel;
        float yaw = player.Camera.Yaw;
        var pxz = new Vector2(player.GlobalPosition.X, player.GlobalPosition.Z);
        var ppx = MapImage.ToPx(pxz);

        // map: a square of the texture around the player, rotated so that the camera heading points up
        float r = ViewRadius * texPxPerM * 1.5f;
        var src = new Rect2(ppx.X - r, ppx.Y - r, 2f * r, 2f * r).Intersection(new Rect2(0, 0, MapImage.Size, MapImage.Size));
        if (src.Size.X > 0 && src.Size.Y > 0)
        {
            DrawSetTransform(centre, yaw, Vector2.One * (screenPxPerM / texPxPerM));
            DrawTextureRectRegion(tex, new Rect2(src.Position - ppx, src.Size), src);
        }

        // markers, in metres relative to the player, same rotation
        DrawSetTransform(centre, yaw, Vector2.One * screenPxPerM);
        var marker = game.Missions.MarkerPosition;
        if (marker.HasValue) Diamond(Rel(marker.Value, pxz), 5f / screenPxPerM, new Color(1f, 0.85f, 0.3f));
        if (game.Waypoint.HasValue) Diamond(Rel(game.Waypoint.Value, pxz), 5f / screenPxPerM, new Color(0.4f, 0.85f, 1f));
        var horse = game.World?.Horse;
        if (horse != null && !player.IsMounted) DrawCircle(Rel(horse.GlobalPosition, pxz), 3.5f / screenPxPerM, new Color(0.95f, 0.95f, 1f));
        var car = game.World?.PlayerCar;
        if (car != null && player.Driving != car) DrawRect(new Rect2(Rel(car.GlobalPosition, pxz) - Vector2.One * 3f / screenPxPerM, Vector2.One * 6f / screenPxPerM), new Color(0.95f, 0.95f, 1f));
        if (game.World != null)
            foreach (var n in game.World.Npcs)
            {
                if (n.IsDead) continue;
                var rel = Rel(n.GlobalPosition, pxz);
                if (rel.Length() > ViewRadius * 1.4f) continue;
                if (n.Faction == Faction.British && game.Wanted.Level > 0) DrawCircle(rel, 3.5f / screenPxPerM, new Color(0.3f, 0.5f, 1f));
                else if (GivesMission(game, n)) DrawCircle(rel, 4f / screenPxPerM, new Color(1f, 0.85f, 0.3f));
                else if (n.IsHostileTo(player)) DrawCircle(rel, 3f / screenPxPerM, new Color(1f, 0.25f, 0.2f));
            }

        // player arrow and north marker (screen space)
        DrawSetTransform(Vector2.Zero);
        Vector2[] arrow = { centre + new Vector2(0, -9), centre + new Vector2(6, 7), centre + new Vector2(0, 4), centre + new Vector2(-6, 7) };
        DrawColoredPolygon(arrow, new Color(1f, 1f, 1f));
        var north = new Vector2(0, -1).Rotated(yaw) * (Size.X / 2f - 12f);
        DrawString(ThemeDB.FallbackFont, centre + north + new Vector2(-5, 6), "N", HorizontalAlignment.Left, -1, 16, Frame);
        DrawRect(new Rect2(Vector2.Zero, Size), Frame, false, 2f);
    }

    private static Vector2 Rel(Vector3 world, Vector2 playerXZ) => new(world.X - playerXZ.X, world.Z - playerXZ.Y);

    private void Diamond(Vector2 at, float r, Color c)
    {
        Vector2[] pts = { at + new Vector2(0, -r), at + new Vector2(r, 0), at + new Vector2(0, r), at + new Vector2(-r, 0) };
        DrawColoredPolygon(pts, c);
    }

    public static bool GivesMission(Game game, Npc n)
    {
        foreach (var m in game.Data.Missions)
            if (m.Giver == n.Data.Id && game.Missions.IsAvailable(m.Id)) return true;
        return false;
    }
}
