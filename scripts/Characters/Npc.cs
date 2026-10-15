using Godot;
using Veld.Core;
using Veld.Gameplay;
using Veld.Sim;
using Veld.Sim.Characters;
using Veld.Sim.Dialogue;

namespace Veld.Characters;

/// <summary>
/// Body and view of one character who is not the player: the capsule, the model and label, gravity and
/// steering, the sight and shot raycasts with their effects, and the dialogue it opens. Every physics
/// frame it tells its NpcBrain what it senses and carries out the brain's intent; the brain decides
/// hostility, what a hit does, when to run, when to shoot and what a search of the body yields.
/// </summary>
public partial class Npc : CharacterBody3D, IInteractable, INpcView
{
    public NpcData Data { get; set; } = new();
    public NpcBrain Brain { get; private set; } = null!;
    public Faction Faction => Brain.Faction;
    public int Health => Brain.Health;
    public bool IsDead => Brain.IsDead;
    public Weapon? Weapon { get; private set; }
    public bool Provoked { get => Brain.Provoked; set => Brain.Provoked = value; }

    // the gameplay systems see this character through INpcView; explicit so Node.Name stays Godot's
    string INpcView.Id => Data.Id;
    string INpcView.Name => Data.Name;
    string INpcView.Group => Data.Group;
    Vector3 INpcView.Position => GlobalPosition;

    private HumanoidModel _model = null!;
    private Label3D _label = null!;
    private bool _pendingDead;
    private const float Gravity = 9.8f;
    private Main _main = null!;
    private GameData _data = null!;
    private DialogueSystem _dialogue = null!;

    /// <summary>Call between Instantiate and AddChild; _Ready reads the weapon from the data.</summary>
    public void Init(Main main, GameData data, NpcBrain brain, DialogueSystem dialogue)
    {
        _main = main;
        _data = data;
        Brain = brain;
        _dialogue = dialogue;
    }

    public override void _Ready()
    {
        int variant = 0;
        foreach (char c in Data.Id) variant += c;
        AddChild(new CollisionShape3D { Shape = new CapsuleShape3D { Radius = 0.32f, Height = 1.8f }, Position = new Vector3(0, 0.9f, 0) });
        FloorMaxAngle = Mathf.DegToRad(50f);
        FloorSnapLength = 0.4f;

        _model = new HumanoidModel { Name = "Model" };
        _model.Configure(Faction, variant);
        AddChild(_model);

        if (!string.IsNullOrEmpty(Data.Weapon) && _data.Weapons.TryGetValue(Data.Weapon, out var wd))
        {
            Weapon = new Weapon(wd) { Reserve = 999 };
            _model.ShowWeapon(wd.Kind);
        }

        _label = new Label3D
        {
            Text = Data.Name, Position = new Vector3(0, 2.15f, 0), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = 36, PixelSize = 0.008f, Modulate = new Color(1f, 0.95f, 0.85f), OutlineSize = 8, Visible = false,
            NoDepthTest = true,
        };
        AddChild(_label);

        Brain.Settle(GlobalPosition);
        if (_pendingDead) MarkDeadSilently();
    }

    // ------------------------------------------------------------------ interaction

    public string Prompt => IsDead ? "Search body" : "Talk to " + Data.Name;

    public bool CanInteract(Player player)
    {
        if (IsDead) return !Brain.Looted;
        return !string.IsNullOrEmpty(Data.Dialogue) && !IsHostileTo(player) && Brain.State != NpcState.Flee;
    }

    public void Interact(Player player)
    {
        if (IsDead) { Brain.Loot(); return; }
        _dialogue.Start(Data.Dialogue, this);
    }

    // ------------------------------------------------------------------ hostility

    public bool IsHostileTo(Player player) => Brain.IsHostile;

    public void TakeDamage(int amount, Node? attacker)
    {
        if (IsDead) return;
        var from = attacker is Node3D a ? a.GlobalPosition : GlobalPosition;
        if (Brain.TakeDamage(amount, attacker as Player, from)) Die();
    }

    private void Die()
    {
        _model.SetDead(true);
        _label.Visible = false;
        CollisionLayer = 0;
        CollisionMask = Layers.Terrain;
        Velocity = Vector3.Zero;
        _main.World.DeadNpcs.Add(Data.Id);
    }

    /// <summary>Used when loading a save: the NPC is already dead, no effects, no mission events.</summary>
    public void MarkDeadSilently()
    {
        if (_model == null) { _pendingDead = true; return; }
        Brain.MarkDeadSilently();
        _model.SetDead(true);
        _label.Visible = false;
        CollisionLayer = 0;
        CollisionMask = Layers.Terrain;
    }

    public void Alert(Vector3 from) => Brain.Alert(from);

    // ------------------------------------------------------------------ loop

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        if (IsDead) { Rest(dt); return; }
        Weapon?.Tick(dt);
        var player = _main.Player;
        var senses = new NpcSenses(
            GlobalPosition, GlobalTransform.Basis.X,
            player.GlobalPosition, player.IsDead, player.IsMounted, player.Speed,
            () => CanSee(player),
            new Vector2(Velocity.X, Velocity.Z).Length(),
            Weapon != null && Weapon.InMag == 0,
            Weapon != null && Weapon.CanFire);
        var intent = Brain.Think(senses, dt);
        _label.Visible = intent.ShowLabel;
        if (intent.Rest) { Rest(dt); return; }

        if (intent.Reload) Weapon?.StartReload();
        if (intent.Fire) Shoot(player, intent.SpreadDegrees);

        float wantYaw = intent.FaceTo is { } face ? YawTo(face) : Rotation.Y;
        Rotation = new Vector3(0, Mathf.LerpAngle(Rotation.Y, wantYaw, 1f - Mathf.Exp(-8f * dt)), 0);
        var forward = -GlobalTransform.Basis.Z;
        var v = Velocity;
        v.X = Mathf.Lerp(v.X, forward.X * intent.Speed, 1f - Mathf.Exp(-10f * dt));
        v.Z = Mathf.Lerp(v.Z, forward.Z * intent.Speed, 1f - Mathf.Exp(-10f * dt));
        v.Y = IsOnFloor() ? -0.5f : v.Y - Gravity * dt;
        Velocity = v;
        MoveAndSlide();

        _model.SetAiming(intent.State == NpcState.Attack);
        _model.Animate(new Vector2(Velocity.X, Velocity.Z).Length(), dt);
    }

    /// <summary>Dead or far from the player: no decisions, just the ground.</summary>
    private void Rest(float dt)
    {
        Velocity = new Vector3(0, IsOnFloor() ? 0 : Velocity.Y - Gravity * dt, 0);
        MoveAndSlide();
    }

    private bool CanSee(Player player)
    {
        var from = GlobalPosition + new Vector3(0, 1.6f, 0);
        var to = player.GlobalPosition + new Vector3(0, 1.2f, 0);
        var q = PhysicsRayQueryParameters3D.Create(from, to, Layers.Solid, new Godot.Collections.Array<Rid> { GetRid() });
        return GetWorld3D().DirectSpaceState.IntersectRay(q).Count == 0;
    }

    /// <summary>One round the brain asked for: the raycast, the effects, the sound and the damage to whatever it hit.</summary>
    private void Shoot(Player player, float spreadDegrees)
    {
        var w = Weapon!;
        var muzzle = _model.MuzzleWorld;
        var aimAt = player.GlobalPosition + new Vector3(0, player.IsMounted ? 1.5f : 1.1f, 0);
        var shot = w.Fire(this, muzzle, aimAt - muzzle, spreadDegrees, new Godot.Collections.Array<Rid> { GetRid() });
        ShotEffects.Show(GetParent(), shot, muzzle);
        _main.Sfx.Gunshot(w.Data.Kind, GlobalPosition);
        if (shot.Hit && shot.Collider is Player p) p.TakeDamage(Mathf.Max(8, w.Data.Damage / 3), this);
        else if (shot.Hit && shot.Collider is Npc other && other != this) other.TakeDamage(w.Data.Damage / 2, this);
    }

    private float YawTo(Vector3 target)
    {
        var d = target - GlobalPosition;
        d.Y = 0;
        return d.LengthSquared() < 0.001f ? Rotation.Y : Mathf.Atan2(-d.X, -d.Z);
    }
}
