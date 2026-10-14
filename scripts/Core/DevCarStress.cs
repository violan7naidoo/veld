using System.Collections.Generic;
using Godot;
using Veld.Gameplay;

namespace Veld.Core;

/// <summary>
/// Repro harness for the "car flips or falls through the map when I get in" bug. Start the game
/// with `-- --carstress[=N]`: the player is teleported beside each of the first N drivable cars in
/// turn, gets in, and for a second the car's roll, lift above the ground, sinking below it and speed
/// are sampled with no driver input; then the player gets out and in again to exercise the
/// freeze-on-exit path. Every car that flips, lifts, sinks or bolts is a FAIL line. Exits non-zero
/// on any failure. Deterministic: same cars, same frames, no input.
/// </summary>
public partial class DevCarStress : Node
{
    private const int FramesPerPhase = 60;
    private int _frame;
    private int _index;
    private int _count = 40;
    private int _failures;
    private float _worstSpeed, _worstLift;
    private readonly List<Vehicle> _cars = new();
    private Sample _first = new(), _second = new();
    private string _preEnter = "";

    private sealed class Sample
    {
        public float MaxRoll, MaxLift = float.MinValue, MinLift = float.MaxValue, MaxSpeed;
        public bool Failed => MaxRoll > 45f || MaxLift > 1.5f || MinLift < -0.4f || MaxSpeed > 8f;
        public override string ToString() => $"roll {MaxRoll:0}deg lift {MaxLift:0.00}..{MinLift:0.00} m speed {MaxSpeed:0.0} m/s";
    }

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        foreach (var a in OS.GetCmdlineUserArgs())
            if (a.StartsWith("--carstress=") && int.TryParse(a.Substring(12), out var n)) _count = n;
        var w = Game.Instance.World!;
        if (w.PlayerCar != null) _cars.Add(w.PlayerCar);
        foreach (var v in w.Vehicles) if (v != w.PlayerCar && _cars.Count < _count) _cars.Add(v);
        GD.Print($"CARSTRESS: {_cars.Count} cars, {FramesPerPhase} frames per phase");
    }

    public override void _PhysicsProcess(double delta)
    {
        var g = Game.Instance;
        var p = g.Player;
        var w = g.World;
        if (p == null || w == null) return;
        if (_frame < 120) { _frame++; return; } // let the world settle and every parked car freeze
        if (_index >= _cars.Count) { Finish(); return; }

        var car = _cars[_index];
        int t = (_frame - 120) % (FramesPerPhase * 3 + 10);
        switch (t)
        {
            case 0:
                _first = new Sample(); _second = new Sample();
                if (p.Driving != null) p.ExitVehicle();
                p.GlobalPosition = car.ExitPosition();
                p.Velocity = Vector3.Zero;
                break;
            case 5:
                _preEnter = Describe(car, w);
                p.EnterVehicle(car);
                break;
            case FramesPerPhase + 5:
                p.ExitVehicle();
                break;
            case FramesPerPhase + 15:
                p.EnterVehicle(car);
                break;
            case FramesPerPhase * 2 + 15:
                p.ExitVehicle();
                Report(car, w);
                _index++;
                break;
        }
        if (t > 5 && t <= FramesPerPhase + 5) Measure(car, w, _first);
        else if (t > FramesPerPhase + 15 && t <= FramesPerPhase * 2 + 15) Measure(car, w, _second);
        _frame++;
    }

    private static void Measure(Vehicle car, World.WorldBuilder w, Sample s)
    {
        float lift = car.GlobalPosition.Y - w.Terrain.GetHeight(car.GlobalPosition.X, car.GlobalPosition.Z);
        float roll = Mathf.RadToDeg(Mathf.Acos(Mathf.Clamp(car.GlobalTransform.Basis.Y.Dot(Vector3.Up), -1f, 1f)));
        s.MaxRoll = Mathf.Max(s.MaxRoll, roll);
        s.MaxLift = Mathf.Max(s.MaxLift, lift);
        s.MinLift = Mathf.Min(s.MinLift, lift);
        s.MaxSpeed = Mathf.Max(s.MaxSpeed, car.LinearVelocity.Length());
    }

    /// <summary>The car's state the frame before the player gets in: what the physics will have to resolve.</summary>
    private static string Describe(Vehicle car, World.WorldBuilder w)
    {
        float ground = w.Terrain.GetHeight(car.GlobalPosition.X, car.GlobalPosition.Z);
        var n = w.Terrain.GetNormal(car.GlobalPosition.X, car.GlobalPosition.Z);
        float slope = Mathf.RadToDeg(Mathf.Acos(Mathf.Clamp(n.Dot(Vector3.Up), -1f, 1f)));
        string wheels = "";
        foreach (var c in car.GetChildren())
            if (c is VehicleWheel3D wh) wheels += (wh.IsInContact() ? "C" : "-");
        float roll = Mathf.RadToDeg(Mathf.Acos(Mathf.Clamp(car.GlobalTransform.Basis.Y.Dot(Vector3.Up), -1f, 1f)));
        return $"slope {slope:0}deg lift {car.GlobalPosition.Y - ground:0.00} m roll {roll:0}deg wheels {wheels} frozen {car.Freeze} sleeping {car.Sleeping}";
    }

    private void Report(Vehicle car, World.WorldBuilder w)
    {
        bool failed = _first.Failed || _second.Failed;
        if (failed) _failures++;
        _worstSpeed = Mathf.Max(_worstSpeed, Mathf.Max(_first.MaxSpeed, _second.MaxSpeed));
        _worstLift = Mathf.Max(_worstLift, Mathf.Max(_first.MaxLift, _second.MaxLift));
        var pos = car.GlobalPosition;
        GD.Print($"{(failed ? "FAIL" : "PASS")} car {_index} {car.Kind} at ({pos.X:0},{pos.Z:0}) | before: {_preEnter} | enter 1: {_first} | enter 2: {_second}");
    }

    private void Finish()
    {
        GD.Print($"CARSTRESS RESULT: {_failures} of {_cars.Count} cars flipped, lifted, sank or bolted; worst entry {_worstSpeed:0.0} m/s, {_worstLift:0.00} m lift");
        GetTree().Quit(_failures > 0 ? 1 : 0);
    }
}
