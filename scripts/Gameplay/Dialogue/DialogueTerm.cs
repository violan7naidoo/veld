using System.Collections.Generic;

namespace Veld.Gameplay.Dialogue;

/// <summary>One name and its colon-separated arguments: "mission_step:parcel:1" is Name "mission_step", Args ["parcel", "1"].</summary>
public sealed class DialogueTerm
{
    public DialogueTerm(string name, string[] args)
    {
        Name = name;
        Args = args;
    }

    public string Name { get; }
    public string[] Args { get; }

    /// <summary>Splits a comma-separated list. Terms are trimmed and blank ones skipped, so "" gives none.</summary>
    public static List<DialogueTerm> Parse(string text)
    {
        var terms = new List<DialogueTerm>();
        if (string.IsNullOrEmpty(text)) return terms;
        foreach (var raw in text.Split(','))
        {
            var parts = raw.Trim().Split(':');
            if (parts[0].Length == 0) continue;
            terms.Add(new DialogueTerm(parts[0], parts[1..]));
        }
        return terms;
    }
}
