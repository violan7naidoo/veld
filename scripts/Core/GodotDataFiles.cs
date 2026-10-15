using Godot;
using Veld.Sim;

namespace Veld.Core;

/// <summary>The game's data files: the res:// tree, which is the pack in an exported build.</summary>
public sealed class GodotDataFiles : IDataFiles
{
    public bool Exists(string path) => FileAccess.FileExists(path);

    public string? ReadText(string path)
    {
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        return file?.GetAsText();
    }
}
