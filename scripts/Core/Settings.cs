using Godot;
using Veld.World;

namespace Veld.Core;

/// <summary>The player's settings and their file. Load keeps the defaults when the file is missing.</summary>
public sealed class Settings
{
    public const string Path = "user://settings.cfg";

    public float MouseSensitivity { get; set; } = 0.0025f;
    public bool InvertY { get; set; }
    public float Volume { get; set; } = 0.8f;
    public Quality GraphicsQuality { get; set; } = Quality.High;

    public void Load()
    {
        var cf = new ConfigFile();
        if (cf.Load(Path) != Error.Ok) return;
        MouseSensitivity = cf.GetValue("input", "sensitivity", MouseSensitivity).AsSingle();
        InvertY = cf.GetValue("input", "invert_y", InvertY).AsBool();
        Volume = cf.GetValue("audio", "volume", Volume).AsSingle();
        GraphicsQuality = (Quality)Mathf.Clamp(cf.GetValue("graphics", "quality", (int)GraphicsQuality).AsInt32(), 0, 2);
    }

    public void Save()
    {
        var cf = new ConfigFile();
        cf.SetValue("input", "sensitivity", MouseSensitivity);
        cf.SetValue("input", "invert_y", InvertY);
        cf.SetValue("audio", "volume", Volume);
        cf.SetValue("graphics", "quality", (int)GraphicsQuality);
        cf.Save(Path);
    }
}
