using Godot;
using Veld.Core;

namespace Veld.Characters;

/// <summary>
/// Third-person orbit camera. Lives at scene level and follows its target (the player's head, or the
/// saddle when riding) so mounting never changes how the mouse feels. Right mouse pulls in over the
/// shoulder for aiming.
/// </summary>
public partial class CameraRig : Node3D
{
    public Node3D? Target { get; set; }
    public Vector3 TargetOffset { get; set; } = new(0, 1.6f, 0);
    public float Yaw { get; set; }
    public float Pitch { get; set; } = -0.18f;
    public bool Aiming { get; set; }
    /// <summary>Distance behind the target when not aiming (longer for vehicles).</summary>
    public float BaseLength { get; set; } = 4.5f;
    /// <summary>While driving: ease the yaw behind the vehicle when the mouse is idle.</summary>
    public bool FollowYaw { get; set; }
    public float FollowYawValue { get; set; }
    private float _mouseIdle = 99f;

    private SpringArm3D _arm = null!;
    private Camera3D _camera = null!;
    private Vector3 _smoothed;
    private float _shoulder;

    public Camera3D Camera => _camera;

    public override void _Ready()
    {
        _arm = new SpringArm3D { SpringLength = 4.5f, CollisionMask = Layers.Solid, Margin = 0.3f };
        AddChild(_arm);
        _camera = new Camera3D { Current = true, Fov = 70f, Near = 0.1f, Far = 3000f };
        _arm.AddChild(_camera);
        TopLevel = true;
    }

    public void Exclude(Rid rid) => _arm.AddExcludedObject(rid);

    public override void _UnhandledInput(InputEvent @event)
    {
        if (Game.Instance.InputLocked || Input.MouseMode != Input.MouseModeEnum.Captured) return;
        if (@event is InputEventMouseMotion m)
        {
            _mouseIdle = 0f;
            float s = Game.Instance.MouseSensitivity * (Aiming ? 0.55f : 1f);
            Yaw -= m.Relative.X * s;
            Pitch -= m.Relative.Y * s * (Game.Instance.InvertY ? -1f : 1f);
            Pitch = Mathf.Clamp(Pitch, -1.2f, 0.9f);
        }
    }

    public override void _Process(double delta)
    {
        if (Target == null) return;
        float dt = (float)delta;
        _mouseIdle += dt;
        if (FollowYaw && _mouseIdle > 1.2f)
        {
            Yaw = Mathf.LerpAngle(Yaw, FollowYawValue, 1f - Mathf.Exp(-dt * 2.2f));
            Pitch = Mathf.Lerp(Pitch, -0.22f, 1f - Mathf.Exp(-dt * 1.5f));
        }
        var want = Target.GlobalPosition + TargetOffset;
        _smoothed = _smoothed == Vector3.Zero ? want : _smoothed.Lerp(want, 1f - Mathf.Exp(-dt * 14f));
        GlobalPosition = _smoothed;
        Rotation = new Vector3(0, Yaw, 0);
        _arm.Rotation = new Vector3(Pitch, 0, 0);
        _shoulder = Mathf.Lerp(_shoulder, Aiming ? 0.55f : 0f, 1f - Mathf.Exp(-dt * 10f));
        _arm.Position = new Vector3(_shoulder, 0, 0);
        _arm.SpringLength = Mathf.Lerp(_arm.SpringLength, Aiming ? 1.9f : BaseLength, 1f - Mathf.Exp(-dt * 10f));
        _camera.Fov = Mathf.Lerp(_camera.Fov, Aiming ? 48f : 70f, 1f - Mathf.Exp(-dt * 10f));
    }

    /// <summary>Flat forward direction of the camera (ignores pitch).</summary>
    public Vector3 FlatForward => new(-Mathf.Sin(Yaw), 0, -Mathf.Cos(Yaw));
    public Vector3 FlatRight => new(Mathf.Cos(Yaw), 0, -Mathf.Sin(Yaw));
    public Vector3 AimOrigin => _camera.GlobalPosition;
    public Vector3 AimDirection => -_camera.GlobalTransform.Basis.Z;
}
