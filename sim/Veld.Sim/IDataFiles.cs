namespace Veld.Sim;

/// <summary>Where the data files come from: the Godot pack in the game, a folder or a fake elsewhere.</summary>
public interface IDataFiles
{
    bool Exists(string path);
    /// <summary>The file's text, or null when it cannot be opened.</summary>
    string? ReadText(string path);
}
