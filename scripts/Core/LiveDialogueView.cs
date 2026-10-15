using System;
using System.Collections.Generic;
using Godot;

namespace Veld.Core;

/// <summary>
/// The on-screen DialogueBox behind IDialogueView. Opening a conversation locks the game's input and
/// shows the mouse; closing it unlocks and recaptures the mouse unless the pause menu is up.
/// </summary>
public sealed class LiveDialogueView : IDialogueView
{
    private readonly Main _main;
    private readonly UiState _ui;

    public LiveDialogueView(Main main, UiState ui)
    {
        _main = main;
        _ui = ui;
    }

    public void Open(string speaker, string text, IReadOnlyList<string> choices, Action<int> onChoose)
    {
        _ui.InputLocked = true;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        _main.DialogueBox.Show(speaker, text, new List<string>(choices), onChoose);
    }

    public void Close()
    {
        _main.DialogueBox.Close();
        _ui.InputLocked = false;
        if (!_main.PauseMenu.IsOpen) Input.MouseMode = Input.MouseModeEnum.Captured;
    }
}
