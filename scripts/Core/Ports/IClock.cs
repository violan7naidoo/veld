namespace Veld.Core;

/// <summary>The game clock as the gameplay systems see it. Advance wraps past midnight into the next day.</summary>
public interface IClock
{
    float Hour { get; }
    int Day { get; }
    void Advance(float hours);
}
