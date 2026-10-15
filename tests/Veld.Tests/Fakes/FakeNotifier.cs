using System.Collections.Generic;
using Godot;
using Veld.Sim;

namespace Veld.Tests.Fakes;

public sealed class FakeNotifier : INotifier
{
    public List<string> Messages { get; } = new();
    public List<Vector3> CoinsAt { get; } = new();

    public void Notify(string message) => Messages.Add(message);
    public void Coins(Vector3 at) => CoinsAt.Add(at);
}
