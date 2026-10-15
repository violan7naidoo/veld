namespace Veld.Sim;

/// <summary>Dice as a port: the game rolls the engine's generator, tests choose the outcome.</summary>
public interface IRandom
{
    int Range(int minInclusive, int maxInclusive);
    float Range(float min, float max);
}
