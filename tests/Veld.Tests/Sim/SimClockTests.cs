using Veld.Sim;
using Xunit;

namespace Veld.Tests.Sim;

/// <summary>Day length 720 throughout, so one game hour is thirty real seconds.</summary>
public class SimClockTests
{
    [Fact]
    public void Tick_advances_one_hour_per_thirty_seconds()
    {
        var clock = new SimClock(7.5f, 720f);

        clock.Tick(30f);

        Assert.Equal(8.5f, clock.Hour, 3);
        Assert.Equal(1, clock.Day);
    }

    [Fact]
    public void Tick_wraps_past_midnight_into_the_next_day()
    {
        var clock = new SimClock(23.75f, 720f);

        clock.Tick(30f);

        Assert.Equal(0.75f, clock.Hour, 3);
        Assert.Equal(2, clock.Day);
    }

    [Fact]
    public void Advance_wraps_past_midnight_into_the_next_day()
    {
        var clock = new SimClock(20f, 720f);

        clock.Advance(8f);

        Assert.Equal(4f, clock.Hour, 3);
        Assert.Equal(2, clock.Day);
    }

    [Fact]
    public void TimeText_shows_hours_and_minutes_with_leading_zeros()
    {
        Assert.Equal("06:15", new SimClock(6.25f, 720f).TimeText);
    }

    [Fact]
    public void Night_runs_from_half_past_seven_to_half_past_five()
    {
        Assert.True(new SimClock(21.5f, 720f).IsNight);
        Assert.True(new SimClock(5f, 720f).IsNight);
        Assert.False(new SimClock(12f, 720f).IsNight);
        Assert.False(new SimClock(19.5f, 720f).IsNight);
    }
}
