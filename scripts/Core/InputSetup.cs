using Godot;

namespace Veld.Core;

/// <summary>
/// Defines the input actions in code so they are version-proof and easy to read. Change a key here,
/// or override any action in Project Settings > Input Map (editor settings win over these defaults).
/// </summary>
public static class InputSetup
{
    public static void Configure()
    {
        Key("move_forward", Godot.Key.W);
        Key("move_back", Godot.Key.S);
        Key("move_left", Godot.Key.A);
        Key("move_right", Godot.Key.D);
        Key("jump", Godot.Key.Space);
        Key("sprint", Godot.Key.Shift);
        Key("interact", Godot.Key.E);
        Key("whistle", Godot.Key.H);
        Key("reload", Godot.Key.R);
        Key("weapon_1", Godot.Key.Key1);
        Key("weapon_2", Godot.Key.Key2);
        Key("holster", Godot.Key.X);
        Key("pause", Godot.Key.Escape);
        Key("map_time_skip", Godot.Key.T);
        Key("screenshot", Godot.Key.F12);
        Key("map", Godot.Key.M);
        Key("deadeye", Godot.Key.Q);
        Mouse("shoot", MouseButton.Left);
        Mouse("aim", MouseButton.Right);
    }

    private static void Key(string action, Key key)
    {
        if (InputMap.HasAction(action)) return; // editor-defined mapping takes precedence
        InputMap.AddAction(action);
        InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = key });
    }

    private static void Mouse(string action, MouseButton button)
    {
        if (InputMap.HasAction(action)) return;
        InputMap.AddAction(action);
        InputMap.ActionAddEvent(action, new InputEventMouseButton { ButtonIndex = button });
    }
}
