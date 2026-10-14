using System;
using System.Collections.Generic;
using Veld.Core;

namespace Veld.Tests.Fakes;

public sealed class FakeDialogueView : IDialogueView
{
    public sealed record Opened(string Speaker, string Text, IReadOnlyList<string> Choices);

    public List<Opened> Openings { get; } = new();
    public int Closes { get; private set; }
    public Action<int>? LastOnChoose { get; private set; }
    public Opened? Current => Openings.Count > 0 ? Openings[^1] : null;

    public void Open(string speaker, string text, IReadOnlyList<string> choices, Action<int> onChoose)
    {
        Openings.Add(new Opened(speaker, text, new List<string>(choices)));
        LastOnChoose = onChoose;
    }

    public void Close() => Closes++;
}
