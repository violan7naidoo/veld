using System;
using System.Collections.Generic;
using Godot;
using Veld.Gameplay;

namespace Veld.UI;

/// <summary>Bottom-of-screen conversation panel: speaker name, line of text and a column of choice buttons (1-9 also work).</summary>
public partial class DialogueBox : Control
{
    private PanelContainer _panel = null!;
    private Label _speaker = null!, _text = null!;
    private VBoxContainer _choices = null!;
    private Action<int>? _onChoose;
    private DialogueSystem _dialogue = null!;

    public bool IsOpen => Visible;

    /// <summary>Call before adding to the tree.</summary>
    public void Init(DialogueSystem dialogue) => _dialogue = dialogue;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        Visible = false;

        _panel = new PanelContainer();
        _panel.SetAnchorsPreset(LayoutPreset.CenterBottom);
        _panel.AnchorLeft = 0.5f; _panel.AnchorRight = 0.5f; _panel.AnchorTop = 1; _panel.AnchorBottom = 1;
        _panel.OffsetLeft = -460; _panel.OffsetRight = 460; _panel.OffsetTop = -300; _panel.OffsetBottom = -40;
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.12f, 0.09f, 0.06f, 0.92f), BorderColor = new Color(0.8f, 0.7f, 0.5f),
            BorderWidthLeft = 2, BorderWidthRight = 2, BorderWidthTop = 2, BorderWidthBottom = 2,
            CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
            ContentMarginLeft = 22, ContentMarginRight = 22, ContentMarginTop = 16, ContentMarginBottom = 16,
        };
        _panel.AddThemeStyleboxOverride("panel", style);
        AddChild(_panel);

        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 10);
        _panel.AddChild(v);
        _speaker = new Label();
        _speaker.AddThemeFontSizeOverride("font_size", 22);
        _speaker.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.5f));
        v.AddChild(_speaker);
        _text = new Label { AutowrapMode = TextServer.AutowrapMode.Word, CustomMinimumSize = new Vector2(0, 90) };
        _text.AddThemeFontSizeOverride("font_size", 20);
        _text.AddThemeColorOverride("font_color", new Color(0.95f, 0.9f, 0.8f));
        v.AddChild(_text);
        _choices = new VBoxContainer();
        _choices.AddThemeConstantOverride("separation", 4);
        v.AddChild(_choices);
    }

    public void Show(string speaker, string text, List<string> choices, Action<int> onChoose)
    {
        _onChoose = onChoose;
        _speaker.Text = speaker;
        _text.Text = text;
        foreach (var c in _choices.GetChildren()) c.QueueFree();
        for (int i = 0; i < choices.Count; i++)
        {
            int index = i;
            var b = new Button { Text = $"{i + 1}.  {choices[i]}", Alignment = HorizontalAlignment.Left };
            b.AddThemeFontSizeOverride("font_size", 18);
            b.Pressed += () => Pick(index);
            _choices.AddChild(b);
        }
        Visible = true;
    }

    private void Pick(int index)
    {
        var cb = _onChoose;
        cb?.Invoke(index);
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (!Visible || @event is not InputEventKey k || !k.Pressed || k.Echo) return;
        int n = (int)k.Keycode - (int)Key.Key1;
        if (n >= 0 && n < _choices.GetChildCount()) { Pick(n); GetViewport().SetInputAsHandled(); }
    }

    public void Close()
    {
        if (!Visible) return;
        Visible = false;
        _onChoose = null;
        _dialogue.OnBoxClosed();
    }
}
