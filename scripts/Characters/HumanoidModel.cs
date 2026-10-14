using Godot;
using Veld.World;

namespace Veld.Characters;

/// <summary>
/// Placeholder person built from capsules and spheres: head with a hat or helmet, coat, belt,
/// jointed arms and legs (elbows and knees bend), boots, and a rifle or revolver in the right hand.
/// Replace this class with a rigged GLB + AnimationPlayer when you have real art; Player and Npc only
/// call Configure, Animate, SetAiming, SetDead, ShowWeapon and MuzzleWorld.
/// </summary>
public partial class HumanoidModel : Node3D
{
    private Node3D _torso = null!;
    private Node3D _lArm = null!, _rArm = null!, _lFore = null!, _rFore = null!, _hand = null!;
    private Node3D _lLeg = null!, _rLeg = null!, _lShin = null!, _rShin = null!;
    private Node3D _head = null!;
    private Node3D _rifle = null!, _revolver = null!;
    private float _phase;
    private float _idle;
    private bool _aiming;
    private bool _dead;

    public float Height => 1.8f;

    private const float Hip = 0.86f, Knee = 0.44f, Shoulder = 1.42f, Elbow = 1.14f;

    public void Configure(Faction faction, int variant, Color? skin = null)
    {
        var (coat, trousers, hat, helmet) = Factions.Outfit(faction, variant);
        var skinColor = skin ?? (variant % 4 == 0 ? new Color(0.42f, 0.28f, 0.18f) : variant % 5 == 0 ? new Color(0.62f, 0.44f, 0.3f) : new Color(0.84f, 0.66f, 0.52f));
        var skinMat = Props.Material(skinColor, 0.75f);
        var coatMat = Props.Cloth(coat);
        var trouserMat = Props.Cloth(trousers);
        var hatMat = Props.Material(hat, 0.85f);
        var bootMat = Props.Material(new Color(0.16f, 0.11f, 0.08f), 0.5f);
        var hairMat = Props.Material(variant % 3 == 0 ? new Color(0.1f, 0.08f, 0.06f) : new Color(0.3f, 0.2f, 0.1f), 0.9f);

        // torso: capsule chest, coat skirt, belt, neck
        _torso = new Node3D { Name = "Torso" };
        AddChild(_torso);
        _torso.AddChild(Props.Capsule(0.22f, 0.7f, coatMat, new Vector3(0, 1.15f, 0)));
        _torso.AddChild(Props.Cylinder(0.24f, 0.26f, coatMat, new Vector3(0, 0.9f, 0), 0.22f, 12));
        _torso.AddChild(Props.Cylinder(0.235f, 0.06f, Props.Material(new Color(0.25f, 0.16f, 0.1f), 0.5f), new Vector3(0, 0.98f, 0), -1, 12));
        _torso.AddChild(Props.Cylinder(0.065f, 0.12f, skinMat, new Vector3(0, 1.49f, 0), -1, 8));
        // collar
        _torso.AddChild(Props.Cylinder(0.11f, 0.05f, Props.Material(faction == Faction.British ? coat.Darkened(0.1f) : new Color(0.9f, 0.88f, 0.8f)), new Vector3(0, 1.5f, 0), -1, 10));
        if (faction == Faction.Boer || faction == Faction.Outlaw)
        {
            var band = Props.Box(new Vector3(0.09f, 0.7f, 0.46f), Props.Planks(new Color(0.4f, 0.28f, 0.16f)), new Vector3(0, 1.15f, 0));
            band.Rotation = new Vector3(0, 0, Mathf.DegToRad(38f));
            _torso.AddChild(band);
        }

        // head, face, hair, hat
        _head = new Node3D { Name = "Head", Position = new Vector3(0, 1.62f, 0) };
        _torso.AddChild(_head);
        _head.AddChild(Props.Sphere(0.125f, skinMat, Vector3.Zero, 1.15f));
        _head.AddChild(Props.Sphere(0.018f, Props.Material(new Color(0.05f, 0.04f, 0.03f)), new Vector3(-0.045f, 0.02f, -0.11f)));
        _head.AddChild(Props.Sphere(0.018f, Props.Material(new Color(0.05f, 0.04f, 0.03f)), new Vector3(0.045f, 0.02f, -0.11f)));
        _head.AddChild(Props.Box(new Vector3(0.03f, 0.04f, 0.03f), skinMat, new Vector3(0, -0.015f, -0.125f)));
        if (variant % 3 == 0) _head.AddChild(Props.Box(new Vector3(0.11f, 0.025f, 0.04f), hairMat, new Vector3(0, -0.045f, -0.11f)));
        if (variant % 7 == 0) _head.AddChild(Props.Box(new Vector3(0.14f, 0.07f, 0.1f), hairMat, new Vector3(0, -0.1f, -0.06f)));
        if (Factions.Modern)
        {
            bool cap = faction == Faction.British || variant % 2 == 0;
            if (cap)
            {
                _head.AddChild(Props.Sphere(0.132f, hatMat, new Vector3(0, 0.035f, 0), 0.75f));
                _head.AddChild(Props.Box(new Vector3(0.16f, 0.015f, 0.1f), hatMat, new Vector3(0, 0.03f, -0.16f)));
            }
            else _head.AddChild(Props.Sphere(0.13f, hairMat, new Vector3(0, 0.03f, 0.01f), 0.8f));
        }
        else if (helmet)
        {
            _head.AddChild(Props.Sphere(0.16f, hatMat, new Vector3(0, 0.06f, 0), 0.75f));
            _head.AddChild(Props.Cylinder(0.25f, 0.025f, hatMat, new Vector3(0, 0.0f, 0), -1, 16));
            _head.AddChild(Props.Box(new Vector3(0.3f, 0.02f, 0.01f), Props.Material(new Color(0.3f, 0.25f, 0.15f)), new Vector3(0, 0.02f, -0.16f)));
        }
        else
        {
            _head.AddChild(Props.Cylinder(0.13f, 0.15f, hatMat, new Vector3(0, 0.17f, 0), 0.12f, 14));
            _head.AddChild(Props.Cylinder(0.3f, 0.025f, hatMat, new Vector3(0, 0.1f, 0), -1, 18));
            _head.AddChild(Props.Cylinder(0.135f, 0.03f, Props.Material(new Color(0.15f, 0.1f, 0.06f)), new Vector3(0, 0.12f, 0), -1, 14));
        }

        // arms: shoulder pivot -> upper arm; elbow pivot -> forearm + hand
        _lArm = ArmPivot(new Vector3(-0.27f, Shoulder, 0), coatMat, skinMat, out _lFore, out _);
        _rArm = ArmPivot(new Vector3(0.27f, Shoulder, 0), coatMat, skinMat, out _rFore, out _hand);
        // legs: hip pivot -> thigh; knee pivot -> shin + boot
        _lLeg = LegPivot(new Vector3(-0.12f, Hip, 0), trouserMat, bootMat, helmet, out _lShin);
        _rLeg = LegPivot(new Vector3(0.12f, Hip, 0), trouserMat, bootMat, helmet, out _rShin);

        // weapons in the right hand, barrel running down the forearm (-y) so a raised arm points them forward
        _rifle = new Node3D { Name = "Rifle", Visible = false };
        _rifle.AddChild(Props.Box(new Vector3(0.05f, 0.34f, 0.09f), Props.Planks(Props.DarkWood), new Vector3(0, 0.12f, 0.07f)));
        _rifle.AddChild(Props.Box(new Vector3(0.04f, 0.5f, 0.05f), Props.Planks(Props.DarkWood), new Vector3(0, -0.3f, 0.04f)));
        _rifle.AddChild(Props.Cylinder(0.014f, 0.82f, Props.Material(Props.Iron, 0.35f, 0.8f), new Vector3(0, -0.38f, 0.0f), -1, 8));
        _rifle.AddChild(Props.Cylinder(0.02f, 0.1f, Props.Material(Props.Iron, 0.35f, 0.8f), new Vector3(0, 0.02f, 0.0f), -1, 8));
        _rifle.AddChild(Props.Box(new Vector3(0.08f, 0.03f, 0.03f), Props.Material(Props.Iron, 0.35f, 0.8f), new Vector3(0.05f, 0.0f, 0.0f)));
        _hand.AddChild(_rifle);
        _revolver = new Node3D { Name = "Revolver", Visible = false };
        _revolver.AddChild(Props.Box(new Vector3(0.03f, 0.2f, 0.05f), Props.Material(Props.Iron, 0.35f, 0.8f), new Vector3(0, -0.12f, 0.0f)));
        _revolver.AddChild(Props.Cylinder(0.026f, 0.06f, Props.Material(Props.Iron, 0.35f, 0.8f), new Vector3(0, -0.06f, 0.0f), -1, 8).WithRotation(new Vector3(Mathf.Pi / 2, 0, 0)));
        _revolver.AddChild(Props.Box(new Vector3(0.035f, 0.08f, 0.04f), Props.Planks(Props.DarkWood), new Vector3(0, 0.02f, 0.03f)));
        _hand.AddChild(_revolver);
    }

    private Node3D ArmPivot(Vector3 shoulder, Godot.Material coat, Godot.Material skin, out Node3D fore, out Node3D hand)
    {
        var p = new Node3D { Position = shoulder };
        p.AddChild(Props.Sphere(0.072f, coat, Vector3.Zero));
        p.AddChild(Props.Capsule(0.07f, 0.34f, coat, new Vector3(0, -(Shoulder - Elbow) / 2, 0)));
        fore = new Node3D { Position = new Vector3(0, -(Shoulder - Elbow), 0) };
        fore.AddChild(Props.Capsule(0.06f, 0.3f, coat, new Vector3(0, -0.13f, 0)));
        hand = new Node3D { Position = new Vector3(0, -0.28f, 0) };
        hand.AddChild(Props.Sphere(0.05f, skin, Vector3.Zero, 1.2f));
        fore.AddChild(hand);
        p.AddChild(fore);
        AddChild(p);
        return p;
    }

    private Node3D LegPivot(Vector3 hip, Godot.Material trousers, Godot.Material boot, bool puttees, out Node3D shin)
    {
        var p = new Node3D { Position = hip };
        p.AddChild(Props.Capsule(0.085f, 0.46f, trousers, new Vector3(0, -(Hip - Knee) / 2, 0)));
        shin = new Node3D { Position = new Vector3(0, -(Hip - Knee), 0) };
        shin.AddChild(Props.Capsule(0.07f, 0.4f, puttees ? Props.Cloth(new Color(0.5f, 0.45f, 0.3f)) : trousers, new Vector3(0, -0.18f, 0)));
        shin.AddChild(Props.Box(new Vector3(0.13f, 0.1f, 0.26f), boot, new Vector3(0, -0.39f, -0.04f)));
        p.AddChild(shin);
        AddChild(p);
        return p;
    }

    public void ShowWeapon(string? kind)
    {
        _rifle.Visible = kind == "rifle";
        _revolver.Visible = kind == "revolver";
    }

    public void SetAiming(bool aiming) => _aiming = aiming;

    public void SetDead(bool dead)
    {
        _dead = dead;
        Rotation = dead ? new Vector3(0, Rotation.Y, Mathf.Pi / 2) : new Vector3(0, Rotation.Y, 0);
        Position = dead ? new Vector3(0, 0.3f, 0) : Vector3.Zero;
        if (dead)
        {
            _lArm.Rotation = new Vector3(0.3f, 0, 0.6f); _rArm.Rotation = new Vector3(-0.4f, 0, -0.5f);
            _lLeg.Rotation = new Vector3(0.2f, 0, 0.1f); _rLeg.Rotation = new Vector3(-0.3f, 0, -0.1f);
        }
    }

    /// <summary>speed in m/s; drives the walk cycle.</summary>
    public void Animate(float speed, float delta, bool riding = false)
    {
        if (_dead) return;
        _idle += delta;
        float breathe = Mathf.Sin(_idle * 1.6f) * 0.012f;
        bool rifle = _rifle.Visible;

        if (riding)
        {
            _lLeg.Rotation = new Vector3(1.05f, 0, 0.42f); _rLeg.Rotation = new Vector3(1.05f, 0, -0.42f);
            _lShin.Rotation = new Vector3(-1.1f, 0, 0); _rShin.Rotation = new Vector3(-1.1f, 0, 0);
            if (_aiming)
            {
                _rArm.Rotation = new Vector3(1.45f, 0, -0.05f); _rFore.Rotation = new Vector3(0.05f, 0, 0);
                _lArm.Rotation = rifle ? new Vector3(1.2f, -0.5f, 0.3f) : new Vector3(0.5f, 0, 0.2f);
                _lFore.Rotation = rifle ? new Vector3(0.5f, 0, 0) : new Vector3(0.4f, 0, 0);
            }
            else
            {
                _lArm.Rotation = new Vector3(0.55f, 0, 0.12f); _rArm.Rotation = new Vector3(0.55f, 0, -0.12f);
                _lFore.Rotation = new Vector3(0.6f, 0, 0); _rFore.Rotation = new Vector3(0.6f, 0, 0);
            }
            _torso.Position = new Vector3(0, breathe, 0);
            _head.Rotation = new Vector3(0.05f, 0, 0);
            return;
        }

        float cadence = Mathf.Clamp(speed, 0f, 8f);
        float gait = Mathf.Clamp(speed / 3f, 0f, 1f);
        _phase += delta * cadence * (speed > 4.5f ? 2.0f : 2.4f);
        float swing = Mathf.Sin(_phase) * gait * (speed > 4.5f ? 0.95f : 0.7f);
        float lift = Mathf.Max(0f, -Mathf.Sin(_phase)) * gait * 0.9f;
        float liftR = Mathf.Max(0f, Mathf.Sin(_phase)) * gait * 0.9f;

        _lLeg.Rotation = new Vector3(swing, 0, 0.02f);
        _rLeg.Rotation = new Vector3(-swing, 0, -0.02f);
        _lShin.Rotation = new Vector3(-lift, 0, 0);
        _rShin.Rotation = new Vector3(-liftR, 0, 0);

        if (_aiming)
        {
            // right arm straight forward along the sight line, left hand supporting the rifle
            _rArm.Rotation = new Vector3(1.5f, 0, -0.06f); _rFore.Rotation = new Vector3(0.02f, 0, 0);
            _lArm.Rotation = rifle ? new Vector3(1.25f, -0.55f, 0.35f) : new Vector3(0.25f, 0, 0.15f);
            _lFore.Rotation = rifle ? new Vector3(0.55f, 0, 0) : new Vector3(0.3f, 0, 0);
            _torso.Rotation = new Vector3(0, 0, 0);
        }
        else
        {
            _lArm.Rotation = new Vector3(-swing * 0.75f, 0, 0.12f);
            _rArm.Rotation = new Vector3(swing * 0.75f + (rifle ? 0.15f : 0f), 0, -0.12f);
            _lFore.Rotation = new Vector3(0.35f + Mathf.Max(0f, -swing) * 0.4f, 0, 0);
            _rFore.Rotation = new Vector3(0.35f + Mathf.Max(0f, swing) * 0.4f, 0, 0);
        }
        _torso.Position = new Vector3(0, Mathf.Abs(Mathf.Sin(_phase)) * 0.035f * gait + breathe * (1f - gait), 0);
        _torso.Rotation = new Vector3(gait * 0.06f, Mathf.Sin(_phase) * 0.05f * gait, 0);
        _head.Rotation = new Vector3(-gait * 0.06f, -Mathf.Sin(_phase) * 0.04f * gait, 0);
    }

    public Vector3 MuzzleWorld => _hand.ToGlobal(new Vector3(0, _rifle.Visible ? -0.8f : -0.24f, 0));
}
