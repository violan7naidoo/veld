using System;
using System.Collections.Generic;

namespace Veld.Core;

/// <summary>
/// Where a conversation is shown. Open is called once per node, so locking input inside it must be
/// idempotent; Close may be called when nothing is open and must be harmless.
/// </summary>
public interface IDialogueView
{
    void Open(string speaker, string text, IReadOnlyList<string> choices, Action<int> onChoose);
    void Close();
}
