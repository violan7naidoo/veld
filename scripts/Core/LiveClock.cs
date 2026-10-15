using Veld.Sim;

namespace Veld.Core;

/// <summary>The scene's DayNight behind IClock, read through Main on every call.</summary>
public sealed class LiveClock : IClock
{
    private readonly Main _main;

    public LiveClock(Main main) => _main = main;

    public float Hour => _main.DayNight.Hour;
    public int Day => _main.DayNight.Day;
    public void Advance(float hours) => _main.DayNight.Advance(hours);
}
