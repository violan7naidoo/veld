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
    private readonly Game _game;

    public LiveDialogueView(Game game) => _game = game;

    public void Open(string speaker, string text, IReadOnlyList<string> choices, Action<int> onChoose)
    {
        _game.InputLocked = true;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        _game.Main?.DialogueBox.Show(speaker, text, new List<string>(choices), onChoose);
    }

    public void Close()
    {
        _game.Main?.DialogueBox.Close();
        _game.InputLocked = false;
        if (_game.Main != null && !_game.Main.PauseMenu.IsOpen) Input.MouseMode = Input.MouseModeEnum.Captured;
    }
}
