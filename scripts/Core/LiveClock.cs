namespace Veld.Core;

/// <summary>The scene's DayNight behind IClock, read through Game on every call; zero and no-op until the scene is bound.</summary>
public sealed class LiveClock : IClock
{
    private readonly Game _game;

    public LiveClock(Game game) => _game = game;

    public float Hour => _game.DayNight?.Hour ?? 0f;
    public int Day => _game.DayNight?.Day ?? 0;
    public void Advance(float hours) => _game.DayNight?.Advance(hours);
}
