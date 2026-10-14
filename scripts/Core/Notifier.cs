using System;
using Godot;

namespace Veld.Core;

/// <summary>Feedback to the player: a toast line and the coin sound at a spot. The log is GD.Print in the game.</summary>
public sealed class Notifier : INotifier
{
    private readonly Action<string> _log;

    public Notifier(Action<string> log) => _log = log;

    public event Action<string>? Notified;
    public event Action<Vector3>? CoinsDropped;

    public void Notify(string message)
    {
        _log("[notify] " + message);
        Notified?.Invoke(message);
    }

    public void Coins(Vector3 at) => CoinsDropped?.Invoke(at);
}
