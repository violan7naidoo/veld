namespace Veld.Sim;

/// <summary>Game time: hour of day and day number, advanced by the tick at the map's day length.</summary>
public sealed class SimClock : IClock
{
    public SimClock(float startHour, float dayLengthSeconds)
    {
        Hour = startHour;
        DayLengthSeconds = dayLengthSeconds;
    }

    public float Hour { get; set; }
    public int Day { get; set; } = 1;
    public float DayLengthSeconds { get; }

    public string TimeText => $"{(int)Hour:00}:{(int)((Hour % 1f) * 60f):00}";
    public bool IsNight => Hour < 5.5f || Hour > 19.5f;

    public void Advance(float hours)
    {
        Hour += hours;
        while (Hour >= 24f) { Hour -= 24f; Day++; }
    }

    public void Tick(float dt) => Advance(dt * 24f / DayLengthSeconds);
}
