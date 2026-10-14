using System.Collections.Generic;
using Veld.Core;

namespace Veld.Tests.Fakes;

/// <summary>Records what the systems ask of the clock; the midnight wrap itself lives in DayNight.</summary>
public sealed class FakeClock : IClock
{
    public float Hour { get; set; } = 7.5f;
    public int Day { get; set; } = 1;
    public List<float> Advanced { get; } = new();

    public void Advance(float hours) => Advanced.Add(hours);
}
