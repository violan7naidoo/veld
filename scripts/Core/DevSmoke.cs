using System.Collections.Generic;
using Godot;
using Veld.Characters;
using Veld.Gameplay;
using Veld.Sim;
using Veld.World;

namespace Veld.Core;

/// <summary>
/// Automated smoke test, map-agnostic. Start the game with `-- --smoke` (optionally `--shot=C:/some/prefix`
/// and `--quality=0|1|2`) and this node drives the player through walking, shooting, riding (if the map
/// has a horse), a dialogue, the first mission from start to finish, the wanted system, save/load and the
/// pause menu, printing PASS/FAIL lines and quitting with exit code 1 on any failure.
/// </summary>
public partial class DevSmoke : Node
{
    private int _frame;
    private int _pass, _fail;
    private string? _shot;
    private Vector3 _mark;
    private int _money;
    private int _health;
    private string? _missionId;
    private float _yawMark;
    private Vector3 _carMark;
    private Npc? _talker;
    private Main _main = null!;

    public void Init(Main main) => _main = main;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        AddChild(new DevInputShield { Name = "InputShield" });
        var args = OS.GetCmdlineUserArgs();
        foreach (var a in args)
        {
            if (a.StartsWith("--shot=")) _shot = a.Substring(7);
            if (a.StartsWith("--quality=") && int.TryParse(a.Substring(10), out var q))
            {
                _main.Settings.GraphicsQuality = (Veld.World.Quality)Mathf.Clamp(q, 0, 2);
                _main.DayNight.ApplyQuality(_main.Settings.GraphicsQuality);
            }
        }
        GD.Print($"SMOKE: started on map '{_main.Data.MapId}'" + (_shot != null ? ", screenshots to " + _shot : ""));
    }

    private void Check(bool ok, string what)
    {
        if (ok) _pass++; else _fail++;
        GD.Print((ok ? "PASS " : "FAIL ") + what);
    }

    private static void Press(string action) => Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = true, Strength = 1f });
    private static void Release(string action) => Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = false });

    private void Shot(string suffix)
    {
        if (_shot == null) return;
        var err = GetViewport().GetTexture().GetImage().SavePng(_shot + "-" + suffix + ".png");
        GD.Print($"SMOKE: screenshot {suffix} -> {err}");
    }

    public override void _PhysicsProcess(double delta)
    {
        _frame++;
        var m = _main;
        var p = m.Player;
        var w = m.World;
        if (p == null || w == null) return;
        bool horse = w.Horse != null;

        switch (_frame)
        {
            case 30:
                Check(p.GlobalPosition.Y > -400f, $"player standing at spawn {p.GlobalPosition}");
                Check(w.Npcs.Count > 0, $"npcs spawned: {w.Npcs.Count}");
                Check(p.Weapons.Count > 0 && p.CurrentWeaponId == p.Weapons[0].Data.Id, $"weapons: {p.CurrentWeaponId}, {p.Weapons.Count} carried");
                Check(horse == m.Data.World.HorseEnabled, horse ? "horse spawned" : "no horse on this map (as configured)");
                if (m.Data.OverridesApplied > 0) Check(FootprintBuilder.ModelsPlaced > 0, $"overrides: {m.Data.OverridesApplied} buildings customised, {FootprintBuilder.ModelsPlaced} real models placed, {FootprintBuilder.FacadesPlaced} facade photos");
                if (m.Data.World.Buildings.Count > 100)
                {
                    int addressed = AddressIndex.Entries.Count, total = m.Data.World.Buildings.Count;
                    Check(addressed * 10 >= total * 9, $"addresses: {addressed} of {total} buildings have a street number and street");
                    Check(w.Plates != null && w.Plates.Count * 2 >= addressed, $"number plates registered: {w.Plates?.Count ?? 0}");
                    Check(FootprintBuilder.SkeletonRoofs >= 400 && FootprintBuilder.SkeletonFallbacks == 0, $"hip roofs on {FootprintBuilder.SkeletonRoofs} non-rectangular outlines, {FootprintBuilder.SkeletonFallbacks} fell back");
                    Check(AddressIndex.SuburbAt(new Vector2(p.GlobalPosition.X, p.GlobalPosition.Z)) != "", "spawn is inside a named suburb: " + AddressIndex.SuburbAt(new Vector2(p.GlobalPosition.X, p.GlobalPosition.Z)));
                }
                _mark = p.GlobalPosition;
                Press("move_forward");
                break;
            case 100:
                Release("move_forward");
                Check(p.GlobalPosition.DistanceTo(_mark) > 2f, $"walked {p.GlobalPosition.DistanceTo(_mark):0.0} m");
                Shot("1-spawn");
                break;
            case 110:
                Press("shoot");
                break;
            case 120:
                Release("shoot");
                break;
            case 121:
                Check(p.CurrentWeapon!.InMag < p.CurrentWeapon.Data.MagSize, $"{p.CurrentWeapon.Data.Name} fired, {p.CurrentWeapon.InMag} rounds left in magazine");
                if (p.Weapons.Count > 1) Press("weapon_2");
                break;
            case 131:
                if (p.Weapons.Count > 1) Release("weapon_2");
                Press("aim");
                break;
            case 136:
                Press("deadeye");
                break;
            case 139:
                Release("deadeye");
                Check(p.DeadEyeActive && Engine.TimeScale < 0.5, $"Dead Eye slows time (scale {Engine.TimeScale:0.00})");
                Release("aim");
                break;
            case 145:
                Check(!p.DeadEyeActive && Engine.TimeScale > 0.99, "Dead Eye ends when aiming stops");
                break;
            case 152:
                if (w.PlayerCar != null) { Check(p.Focus is Vehicle, $"the car is the interaction focus ({p.GlobalPosition.DistanceTo(w.PlayerCar.GlobalPosition):0.0} m away)"); Press("interact"); }
                break;
            case 158:
                if (w.PlayerCar != null) Release("interact");
                break;
            case 134:
                if (p.Weapons.Count > 1) Check(p.CurrentWeaponId == p.Weapons[1].Data.Id, "switched to the second weapon");
                if (horse) w.Horse!.PlaceAt(p.GlobalPosition + p.Camera.FlatForward * 2.2f + Vector3.Up * 0.3f, p.Camera.Yaw + Mathf.Pi / 2);
                if (w.PlayerCar != null)
                {
                    // on the road centreline a few metres ahead of the player, along the spawn street, so walls
                    // and kerbs cannot catch it and the focus check does not depend on how far the walk got
                    float spawnYaw = Mathf.DegToRad(m.Data.World.PlayerSpawnFacing);
                    var ahead = new Vector2(-Mathf.Sin(spawnYaw), -Mathf.Cos(spawnYaw));
                    var carXZ = new Vector2(p.GlobalPosition.X, p.GlobalPosition.Z) + ahead * 3.5f;
                    w.PlayerCar.Sleeping = false;
                    w.PlayerCar.GlobalTransform = new Transform3D(Basis.Identity.Rotated(Vector3.Up, spawnYaw), w.Ground(carXZ) + Vector3.Up * 0.6f);
                    w.PlayerCar.LinearVelocity = Vector3.Zero;
                    w.PlayerCar.AngularVelocity = Vector3.Zero;
                }
                break;
            case 140:
                if (horse) { Check(p.Focus is Horse, "horse is the interaction focus"); Press("interact"); }
                break;
            case 150:
                if (horse) Release("interact");
                break;
            case 160:
                if (horse) Check(p.IsMounted, "mounted the horse");
                if (w.PlayerCar != null) Check(p.Driving == w.PlayerCar, "got into the car");
                _mark = p.GlobalPosition;
                _yawMark = p.Driving != null ? p.Driving.Rotation.Y : 0f;
                Press("move_forward");
                Press("sprint");
                break;
            case 200:
                if (p.Driving != null)
                {
                    var v = p.Driving;
                    string wheels = "";
                    foreach (var c in v.GetChildren()) if (c is VehicleWheel3D wh) wheels += (wh.IsInContact() ? "C" : "-") + $"({wh.GlobalPosition.Y - v.GlobalPosition.Y:0.00}) ";
                    GD.Print($"SMOKE: car engine {v.EngineForce:0} brake {v.Brake:0.0} steer {v.Steering:0.00} fwd {v.ForwardSpeed:0.00} vel {v.LinearVelocity} sleeping {v.Sleeping} frozen {v.Freeze} at ({v.GlobalPosition.X:0.0}, {v.GlobalPosition.Y:0.00}, {v.GlobalPosition.Z:0.0}) ground {w.Terrain.GetHeight(v.GlobalPosition.X, v.GlobalPosition.Z):0.00} wheels {wheels}");
                }
                break;
            case 230:
                // 2.5 m/s after 1.2 s of throttle: the spawn street climbs, so this is uphill
                if (p.Driving != null) { Check(p.Driving.ForwardSpeed > 2.5f, $"car drives at {p.Driving.ForwardSpeed * 3.6f:0} km/h"); Press("move_right"); }
                break;
            case 285:
                if (p.Driving != null)
                {
                    Release("move_right");
                    Check(Mathf.AngleDifference(_yawMark, p.Driving.Rotation.Y) < -0.15f, $"steering right turns the car clockwise ({Mathf.RadToDeg(Mathf.AngleDifference(_yawMark, p.Driving.Rotation.Y)):0} deg)");
                    Press("move_left");
                }
                break;
            case 330:
                Release("move_forward");
                Release("sprint");
                if (p.Driving != null) { Release("move_left"); Check(p.Driving.GlobalTransform.Basis.Y.Dot(Vector3.Up) > 0.7f, $"car still on its wheels after hard turns both ways (tilt {Mathf.RadToDeg(Mathf.Acos(Mathf.Clamp(p.Driving.GlobalTransform.Basis.Y.Dot(Vector3.Up), -1f, 1f))):0} deg, {p.Driving.ForwardSpeed * 3.6f:0} km/h)"); }
                Check(p.GlobalPosition.DistanceTo(_mark) > (p.Driving != null ? 8f : 12f), (horse ? "galloped" : p.Driving != null ? "drove" : "sprinted") + $" {p.GlobalPosition.DistanceTo(_mark):0.0} m");
                if (horse) Check(w.Horse!.Stamina < 100f, $"horse stamina drained to {w.Horse.Stamina:0}");
                GD.Print($"SMOKE: fps {Engine.GetFramesPerSecond():0} while moving, quality {m.Settings.GraphicsQuality}");
                if (w.Plates != null && w.Plates.Count > 0) Check(w.Plates.Live > 0, $"number plates streamed in around the player: {w.Plates.Live}");
                Shot(horse ? "2-riding" : p.Driving != null ? "2-driving" : "2-moving");
                if (horse || p.Driving != null) Press("interact");
                break;
            case 340:
                if (horse || p.Driving != null) Release("interact");
                break;
            case 352:
            {
                if (horse) Check(!p.IsMounted, "dismounted");
                if (w.PlayerCar != null) { Check(p.Driving == null && p.GlobalPosition.DistanceTo(w.PlayerCar.GlobalPosition) < 6f, "got out of the car beside it"); _carMark = w.PlayerCar.GlobalPosition; }
                var giver = m.Data.Missions.Count > 0 ? w.FindNpc(m.Data.Missions[0].Giver) : null;
                _talker = giver ?? w.Npcs.Find(n => !string.IsNullOrEmpty(n.Data.Dialogue) && !n.IsDead);
                Check(_talker != null, "someone to talk to exists: " + (_talker?.Data.Name ?? "nobody"));
                if (_talker != null) m.Sim.Dialogue.Start(_talker.Data.Dialogue, _talker);
                break;
            }
            case 358:
                if (_talker != null)
                {
                    Check(m.Sim.Dialogue.IsActive && m.DialogueBox.IsOpen && m.Ui.InputLocked, "dialogue opened and input locked");
                    m.Sim.Dialogue.Choose(0);
                }
                break;
            case 364:
                if (m.Sim.Dialogue.IsActive) m.Sim.Dialogue.End();
                break;
            case 370:
                Check(!m.Sim.Dialogue.IsActive && !m.Ui.InputLocked, "dialogue closed, input unlocked");
                if (m.Data.Missions.Count > 0)
                {
                    _missionId = m.Data.Missions[0].Id;
                    _money = m.Sim.Purse.Pence;
                    Check(m.Sim.Missions.Start(_missionId), $"mission '{_missionId}' started");
                }
                else Check(true, "no missions on this map");
                break;
            case 376:
                if (_missionId != null) Check(m.Sim.Missions.MarkerPosition.HasValue, "HUD has an objective marker");
                if (w.PlayerCar != null) Check(w.PlayerCar.GlobalPosition.DistanceTo(_carMark) < 1.0f, $"parked car stayed put on the hill ({w.PlayerCar.GlobalPosition.DistanceTo(_carMark):0.00} m drift from ({_carMark.X:0.00}, {_carMark.Y:0.00}, {_carMark.Z:0.00}) to ({w.PlayerCar.GlobalPosition.X:0.00}, {w.PlayerCar.GlobalPosition.Y:0.00}, {w.PlayerCar.GlobalPosition.Z:0.00}), vel {w.PlayerCar.LinearVelocity.Length():0.00}, frozen {w.PlayerCar.Freeze})");
                break;
            case 395:
                GD.Print($"SMOKE: fps {Engine.GetFramesPerSecond():0} at quality {m.Settings.GraphicsQuality}, {GetViewport().GetVisibleRect().Size}");
                break;
            case 420:
                Shot("3-mission");
                break;
            case 600:
                if (_missionId != null)
                {
                    var mission = m.Data.Mission(_missionId)!;
                    Check(m.Sim.Missions.IsDone(_missionId), $"mission '{_missionId}' completed by walking its steps");
                    Check(m.Sim.Purse.Pence == _money + mission.RewardPence, $"reward paid: {Money.Format(m.Sim.Purse.Pence - _money)}");
                }
                // the car was left far behind at the mission's first stop; H brings it round for R20
                if (w.PlayerCar != null) { _money = m.Sim.Purse.Pence; Press("whistle"); }
                break;
            case 606:
                if (w.PlayerCar != null)
                {
                    Release("whistle");
                    Check(w.PlayerCar.GlobalPosition.DistanceTo(p.GlobalPosition) < 12f && m.Sim.Purse.Pence == _money - 2000, $"H brings the car back for R20 ({w.PlayerCar.GlobalPosition.DistanceTo(p.GlobalPosition):0.0} m away, {Money.Format(_money - m.Sim.Purse.Pence)} charged)");
                }
                break;
            case 610:
            {
                var police = w.Npcs.Find(n => n.Faction == Faction.British && !n.IsDead);
                if (police != null) police.TakeDamage(10, p);
                else Check(true, "no police/soldiers on this map");
                break;
            }
            case 616:
            {
                var police = w.Npcs.Find(n => n.Faction == Faction.British && !n.IsDead);
                if (police != null)
                {
                    Check(m.Sim.Wanted.Level > 0, $"attacking the law raised wanted level to {m.Sim.Wanted.Level}");
                    Check(police.IsHostileTo(p), "they are now hostile");
                    m.Sim.Wanted.ClearWanted();
                }
                break;
            }
            case 620:
                m.SaveGame.Save();
                Check(m.SaveGame.HasSave, "saved to " + ProjectSettings.GlobalizePath(SaveGame.Path));
                _money = m.Sim.Purse.Pence;
                _health = p.Health;
                m.Sim.Purse.Add(100);
                p.Health = 40;
                break;
            case 626:
                Check(m.SaveGame.Load(), "loaded save");
                Check(m.Sim.Purse.Pence == _money && p.Health == _health, $"save restored money and health ({p.Health})");
                break;
            case 632:
                m.PauseMenu.Toggle();
                break;
            case 636:
                Check(m.PauseMenu.IsOpen && GetTree().Paused && m.Ui.InputLocked, "pause menu opens and pauses the tree");
                m.PauseMenu.Toggle();
                break;
            case 640:
            {
                Check(!GetTree().Paused && !m.Ui.InputLocked, "pause menu closes and resumes");
                m.WorldMap.Toggle();
                break;
            }
            case 646:
                Check(m.WorldMap.IsOpen && GetTree().Paused, "world map opens and pauses");
                Check(MapImage.Texture != null && MapImage.Labels.Count > 0, $"map image built with {MapImage.Labels.Count} labels");
                Shot("6-map");
                m.WorldMap.Toggle();
                break;
            case 652:
            {
                Check(!m.WorldMap.IsOpen && !GetTree().Paused, "world map closes and resumes");
                Check(MapImage.LocationName(m.Data, new Vector2(p.GlobalPosition.X, p.GlobalPosition.Z)) != "", "location name: " + MapImage.LocationName(m.Data, new Vector2(p.GlobalPosition.X, p.GlobalPosition.Z)));
                if (AddressIndex.Entries.Count > 0)
                {
                    var home = AddressIndex.Find("42 road 721");
                    Check(home != null, "address search finds 42 Road 721");
                    if (home != null) Check(MapImage.LocationName(m.Data, home.Centre).StartsWith(home.Building.Address), "HUD names the house you stand in: " + MapImage.LocationName(m.Data, home.Centre));
                    Check(AddressIndex.Find("9999999 nowhere") == null, "address search rejects an unknown address");
                }
                m.Sim.Clock.Hour = 17.55f;
                var spawn = DataLoader.XZ(m.Data.World.PlayerSpawn);
                p.Dismount();
                p.GlobalPosition = w.Ground(spawn) + Vector3.Up;
                p.Camera.Yaw = Mathf.DegToRad(m.Data.World.PlayerSpawnFacing);
                break;
            }
            case 700:
                Check(m.Sim.Clock.Hour >= 17.57f && !m.Sim.Clock.IsNight, $"clock ran to {m.Sim.Clock.TimeText} in 48 physics frames");
                Shot("4-sunset");
                m.Sim.Clock.Hour = 21.5f;
                m.Weather.Force(Veld.World.Weather.State.Storm);
                break;
            case 740:
                Check(m.Weather.Rain > 0.9f && m.Weather.Describe() == "thunderstorm", "weather forced to a thunderstorm: rain " + m.Weather.Rain.ToString("0.00"));
                Shot("5-night");
                break;
            case 746:
            {
                // the L-shaped house at 34 Road 720, seen from the nearest point of its road in daylight
                var house = new Vector2(-92f, 316f);
                var stand = house + new Vector2(0f, -14f);
                float best = float.MaxValue;
                foreach (var road in m.Data.World.Roads)
                {
                    if (road.Name != "Road 720") continue;
                    for (int i = 0; i + 1 < road.Points.Count; i++)
                    {
                        var q = Geometry2D.GetClosestPointToSegment(house, DataLoader.XZ(road.Points[i]), DataLoader.XZ(road.Points[i + 1]));
                        float d = q.DistanceTo(house);
                        if (d < best) { best = d; stand = q; }
                    }
                }
                p.GlobalPosition = w.Ground(stand) + Vector3.Up;
                var to = house - stand;
                p.Camera.Yaw = Mathf.Atan2(-to.X, -to.Y);
                p.Camera.Pitch = -0.55f;
                p.Camera.BaseLength = 12f;
                p.Camera.TargetOffset = new Vector3(0, 2.5f, 0);
                m.Sim.Clock.Hour = 12f;
                m.Weather.Force(Veld.World.Weather.State.Clear);
                break;
            }
            case 800:
                Shot("7-roof");
                break;
            case 802:
                DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(SaveGame.Path));
                GD.Print($"SMOKE RESULT: {_pass} passed, {_fail} failed");
                GetTree().Quit(_fail > 0 ? 1 : 0);
                break;
        }

        // walk the active mission's steps: teleport to goto targets, defeat kill groups, report talks
        if (_missionId != null && _frame >= 380 && _frame < 600 && _frame % 20 == 0 && m.Sim.Missions.Active != null)
        {
            var step = m.Sim.Missions.CurrentStep;
            if (step == null) return;
            switch (step.Type)
            {
                case "goto":
                {
                    var target = DataLoader.XZ(step.Position);
                    p.Dismount();
                    p.GlobalPosition = w.Ground(target) + new Vector3(0, 1f, Mathf.Min(step.Radius * 0.4f, 10f));
                    break;
                }
                case "kill_group":
                    foreach (var n in w.Group(step.Group)) if (!n.IsDead) n.TakeDamage(1000, p);
                    break;
                case "talk":
                case "return":
                    m.Sim.Missions.OnTalked(string.IsNullOrEmpty(step.Target) ? m.Sim.Missions.Active.Giver : step.Target);
                    break;
            }
        }
    }
}
