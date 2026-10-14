using System.Collections.Generic;
using Godot;

namespace Veld.World;

/// <summary>
/// House number plates beside front doors. FootprintBuilder registers one per addressed house; this
/// node only keeps plates alive for the map chunks around the node it follows, so 14,000 plates cost
/// a few hundred labels at a time.
/// </summary>
public partial class AddressPlates : Node3D
{
    private const float ChunkSize = 150f;
    private const int Reach = 1;

    private readonly record struct Plate(Vector3 Position, float Yaw, string Text);

    private readonly Dictionary<(int, int), List<Plate>> _specs = new();
    private readonly Dictionary<(int, int), Node3D> _live = new();
    private Node3D? _follow;
    private (int, int)? _centre;
    private float _timer;
    private StandardMaterial3D? _plateMaterial;
    private QuadMesh? _plateMesh;

    /// <summary>Plates registered by the builders.</summary>
    public int Count { get; private set; }
    /// <summary>Labels currently in the scene.</summary>
    public int Live { get; private set; }

    public void Follow(Node3D node) => _follow = node;

    /// <summary>A plate on a wall at <paramref name="position"/> whose face points along <paramref name="outward"/>.</summary>
    public void Add(Vector3 position, Vector3 outward, string text)
    {
        var key = Key(position.X, position.Z);
        if (!_specs.TryGetValue(key, out var list)) { list = new List<Plate>(); _specs[key] = list; }
        list.Add(new Plate(position, Mathf.Atan2(outward.X, outward.Z), text));
        Count++;
    }

    private static (int, int) Key(float x, float z) => (Mathf.FloorToInt(x / ChunkSize), Mathf.FloorToInt(z / ChunkSize));

    public override void _Process(double delta)
    {
        if (_follow == null) return;
        _timer += (float)delta;
        if (_timer < 0.5f) return;
        _timer = 0f;
        var p = _follow.GlobalPosition;
        var centre = Key(p.X, p.Z);
        if (_centre == centre) return;
        _centre = centre;

        var wanted = new HashSet<(int, int)>();
        for (int dx = -Reach; dx <= Reach; dx++)
            for (int dz = -Reach; dz <= Reach; dz++)
                wanted.Add((centre.Item1 + dx, centre.Item2 + dz));
        var drop = new List<(int, int)>();
        foreach (var key in _live.Keys) if (!wanted.Contains(key)) drop.Add(key);
        foreach (var key in drop) { _live[key].QueueFree(); _live.Remove(key); }
        foreach (var key in wanted)
            if (!_live.ContainsKey(key) && _specs.TryGetValue(key, out var list)) _live[key] = Spawn(key, list);
        Live = 0;
        foreach (var key in _live.Keys) Live += _specs[key].Count;
    }

    private Node3D Spawn((int, int) key, List<Plate> plates)
    {
        _plateMaterial ??= new StandardMaterial3D { AlbedoColor = new Color(0.96f, 0.95f, 0.9f), Roughness = 0.6f };
        _plateMesh ??= new QuadMesh { Size = new Vector2(0.42f, 0.24f), Material = _plateMaterial };
        var holder = new Node3D { Name = $"Plates_{key.Item1}_{key.Item2}" };
        foreach (var plate in plates)
        {
            var rotation = new Vector3(0, plate.Yaw, 0);
            var board = new MeshInstance3D
            {
                Mesh = _plateMesh, Position = plate.Position, Rotation = rotation,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                VisibilityRangeEnd = 60f, VisibilityRangeEndMargin = 8f, VisibilityRangeFadeMode = GeometryInstance3D.VisibilityRangeFadeModeEnum.Self,
            };
            holder.AddChild(board);
            var forward = new Vector3(Mathf.Sin(plate.Yaw), 0, Mathf.Cos(plate.Yaw));
            var label = new Label3D
            {
                Text = plate.Text, FontSize = 72, PixelSize = 0.0027f, OutlineSize = 0,
                Modulate = new Color(0.12f, 0.1f, 0.1f), Shaded = false,
                Position = plate.Position + forward * 0.006f, Rotation = rotation,
                VisibilityRangeEnd = 45f, VisibilityRangeEndMargin = 6f, VisibilityRangeFadeMode = GeometryInstance3D.VisibilityRangeFadeModeEnum.Self,
            };
            holder.AddChild(label);
        }
        AddChild(holder);
        return holder;
    }
}
