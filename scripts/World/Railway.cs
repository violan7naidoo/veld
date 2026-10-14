using System.Collections.Generic;
using Godot;
using Veld.Core;

namespace Veld.World;

/// <summary>
/// Cape Government Railways line: a Path3D through the data points, sleepers and rails as MultiMeshes,
/// blockhouses at intervals, and the Train node that runs along it.
/// </summary>
public partial class Railway : Node3D
{
    public Path3D Path { get; private set; } = null!;
    public List<Vector3> BlockhousePositions { get; } = new();
    public Train Train { get; private set; } = null!;

    public void Build(RailwayData data, Terrain terrain)
    {
        var curve = new Curve3D { BakeInterval = 2f };
        var pts = new List<Vector3>();
        foreach (var p in data.Points)
        {
            var xz = DataLoader.XZ(p);
            pts.Add(new Vector3(xz.X, terrain.GetHeight(xz.X, xz.Y) + 0.35f, xz.Y));
        }
        if (data.Loop) pts.Add(pts[0]);
        // Catmull-Rom style in/out handles for gentle bends
        for (int i = 0; i < pts.Count; i++)
        {
            var prev = pts[(i - 1 + pts.Count) % pts.Count];
            var next = pts[(i + 1) % pts.Count];
            var tangent = (next - prev) * 0.2f;
            curve.AddPoint(pts[i], -tangent, tangent);
        }
        Path = new Path3D { Name = "Track", Curve = curve };
        AddChild(Path);

        float length = curve.GetBakedLength();
        int sleeperCount = (int)(length / 0.9f);
        var sleeperMesh = new BoxMesh { Size = new Vector3(2.4f, 0.16f, 0.26f) };
        sleeperMesh.Material = Props.Material(new Color(0.28f, 0.2f, 0.12f));
        var sleepers = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = sleeperMesh, InstanceCount = sleeperCount };

        float railStep = 2f;
        int railCount = (int)(length / railStep) * 2;
        var railMesh = new BoxMesh { Size = new Vector3(0.08f, 0.14f, railStep + 0.05f) };
        railMesh.Material = Props.Material(new Color(0.45f, 0.42f, 0.4f), 0.5f, 0.6f);
        var rails = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = railMesh, InstanceCount = railCount };

        for (int i = 0; i < sleeperCount; i++)
        {
            float d = i * 0.9f;
            var t = curve.SampleBakedWithRotation(d, true, true);
            sleepers.SetInstanceTransform(i, new Transform3D(t.Basis, t.Origin + new Vector3(0, 0.08f, 0)));
        }
        int r = 0;
        for (int i = 0; i < railCount / 2 && r + 1 < railCount; i++)
        {
            float d = i * railStep + railStep / 2;
            var t = curve.SampleBakedWithRotation(d, true, true);
            var right = t.Basis.X.Normalized();
            rails.SetInstanceTransform(r++, new Transform3D(t.Basis, t.Origin + right * 0.72f + new Vector3(0, 0.23f, 0)));
            rails.SetInstanceTransform(r++, new Transform3D(t.Basis, t.Origin - right * 0.72f + new Vector3(0, 0.23f, 0)));
        }
        AddChild(new MultiMeshInstance3D { Name = "Sleepers", Multimesh = sleepers });
        AddChild(new MultiMeshInstance3D { Name = "Rails", Multimesh = rails });

        // blockhouses every spacing metres, set back from the line
        for (float d = data.BlockhouseSpacing * 0.5f; d < length - 50f; d += data.BlockhouseSpacing)
        {
            var t = curve.SampleBakedWithRotation(d, true, true);
            var side = t.Basis.X.Normalized() * 9f;
            var pos = t.Origin + side;
            pos.Y = terrain.GetHeight(pos.X, pos.Z);
            var bh = new Node3D { Name = "Blockhouse", Position = pos };
            TownBuilder.Blockhouse(bh, new Vector3(4.6f, 6f, 4.6f));
            AddChild(bh);
            BlockhousePositions.Add(pos);
        }

        Train = new Train { Name = "Train" };
        Train.Configure(Path, data.TrainSpeed, data.Wagons);
        AddChild(Train);
    }
}

/// <summary>A locomotive and wagons following the track. Bodies are animatable so they push the player.</summary>
public partial class Train : Node3D
{
    private readonly List<PathFollow3D> _cars = new();
    private Path3D _path = null!;
    private float _speed;
    private float _progress;
    private const float CarLength = 9.5f;

    public void Configure(Path3D path, float speed, int wagons)
    {
        _path = path;
        _speed = speed;
        for (int i = 0; i <= wagons; i++)
        {
            var follow = new PathFollow3D { Loop = true, RotationMode = PathFollow3D.RotationModeEnum.Oriented, CubicInterp = true };
            path.AddChild(follow);
            var body = new AnimatableBody3D { SyncToPhysics = true, CollisionLayer = Layers.Train, CollisionMask = 0 };
            var size = new Vector3(2.6f, 3.2f, CarLength - 1f);
            body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size }, Position = new Vector3(0, 1.8f, 0) });
            if (i == 0) Locomotive(body); else Wagon(body, i);
            follow.AddChild(body);
            _cars.Add(follow);
        }
    }

    private static void Locomotive(Node3D n)
    {
        n.AddChild(Props.Box(new Vector3(2.6f, 0.6f, 8.5f), Props.Iron, new Vector3(0, 0.75f, 0)));
        n.AddChild(Props.Cylinder(1.0f, 5.5f, new Color(0.12f, 0.14f, 0.16f), new Vector3(0, 2.0f, 1.0f)));
        n.GetChild<MeshInstance3D>(n.GetChildCount() - 1).Rotation = new Vector3(Mathf.Pi / 2, 0, 0);
        n.AddChild(Props.Box(new Vector3(2.4f, 2.4f, 2.4f), new Color(0.35f, 0.12f, 0.1f), new Vector3(0, 2.4f, -2.8f)));
        n.AddChild(Props.Cylinder(0.35f, 1.8f, Props.Iron, new Vector3(0, 3.6f, 3.2f)));
        n.AddChild(Props.Cylinder(0.5f, 0.8f, new Color(0.7f, 0.6f, 0.2f), new Vector3(0, 3.3f, 0.6f)));
        for (int i = -1; i <= 1; i++)
            for (int s = -1; s <= 1; s += 2)
            {
                var wheel = Props.Cylinder(0.75f, 0.3f, new Color(0.5f, 0.1f, 0.1f), new Vector3(s * 1.2f, 0.75f, i * 2.2f));
                wheel.Rotation = new Vector3(0, 0, Mathf.Pi / 2);
                n.AddChild(wheel);
            }
        // smoke
        n.AddChild(new CpuParticles3D
        {
            Position = new Vector3(0, 4.6f, 3.2f), Amount = 40, Lifetime = 3f, Emitting = true,
            Mesh = new SphereMesh { Radius = 0.5f, Height = 1f, RadialSegments = 6, Rings = 3, Material = Props.ParticleMaterial() },
            Direction = new Vector3(0, 1, -1), Spread = 25f, InitialVelocityMin = 2f, InitialVelocityMax = 4f,
            Gravity = new Vector3(0, 0.6f, 0), ScaleAmountMin = 0.6f, ScaleAmountMax = 2.5f, Color = new Color(0.3f, 0.3f, 0.3f, 0.6f),
        });
    }

    private static void Wagon(Node3D n, int index)
    {
        var color = index % 2 == 0 ? new Color(0.45f, 0.3f, 0.18f) : new Color(0.35f, 0.35f, 0.3f);
        n.AddChild(Props.Box(new Vector3(2.6f, 0.5f, 8.5f), Props.Iron, new Vector3(0, 0.7f, 0)));
        n.AddChild(Props.Box(new Vector3(2.5f, 2.4f, 8f), color, new Vector3(0, 2.2f, 0)));
        n.AddChild(Props.Box(new Vector3(2.7f, 0.15f, 8.2f), Props.CorrugatedGrey, new Vector3(0, 3.5f, 0)));
        for (int i = -1; i <= 1; i += 2)
            for (int s = -1; s <= 1; s += 2)
            {
                var wheel = Props.Cylinder(0.55f, 0.3f, Props.Iron, new Vector3(s * 1.2f, 0.55f, i * 2.8f));
                wheel.Rotation = new Vector3(0, 0, Mathf.Pi / 2);
                n.AddChild(wheel);
            }
    }

    public Vector3 LocomotivePosition => _cars.Count > 0 ? _cars[0].GlobalPosition : GlobalPosition;

    public override void _PhysicsProcess(double delta)
    {
        if (_path == null) return;
        float length = _path.Curve.GetBakedLength();
        _progress = (_progress + _speed * (float)delta) % length;
        for (int i = 0; i < _cars.Count; i++)
            _cars[i].Progress = (_progress - i * CarLength + length) % length;
    }
}
