namespace Veld.Core;

/// <summary>The wanted level as other systems read and clear it.</summary>
public interface IWantedState
{
    int Level { get; }
    void ClearWanted();
}
