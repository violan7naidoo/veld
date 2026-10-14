namespace Veld.Gameplay.Dialogue;

/// <summary>Registered twice: "wanted" holds while the level is above zero, "not_wanted" while it is zero.</summary>
public sealed class WantedCondition : IDialogueCondition
{
    private readonly bool _wanted;

    public WantedCondition(string name, bool wanted)
    {
        Name = name;
        _wanted = wanted;
    }

    public string Name { get; }

    public bool Holds(string[] args, DialogueContext context) => _wanted ? context.Wanted.Level > 0 : context.Wanted.Level == 0;
}
