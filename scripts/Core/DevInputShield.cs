using Godot;

namespace Veld.Core;

/// <summary>
/// Added by the dev harnesses so a person at the keyboard cannot disturb a run: keeps the mouse free,
/// and releases any action a real key or mouse button pressed. The harnesses' own InputEventAction
/// events pass through untouched. Never added in normal play.
/// </summary>
public partial class DevInputShield : Node
{
    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    /// <summary>Menus and dialogue recapture the mouse when they close; undo that every frame.</summary>
    public override void _Process(double delta)
    {
        if (Input.MouseMode == Input.MouseModeEnum.Captured) Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is not (InputEventKey or InputEventMouseButton) || !@event.IsPressed()) return;
        foreach (var action in InputMap.GetActions())
            if (InputMap.EventIsAction(@event, action)) Input.ActionRelease(action);
    }
}
