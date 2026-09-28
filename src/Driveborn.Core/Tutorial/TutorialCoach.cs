namespace Driveborn.Core.Tutorial;

/// <summary>
/// Things the player can do that the coach reacts to. Steps advance on the
/// player's own actions rather than on a Next button, so the lesson is always
/// attached to something they just did.
/// </summary>
public enum TutorialTrigger
{
    DescentStarted,
    Moved,
    Selected,
    Struck,
    StruckMatched,
    Killed,
    TurnEnded,
    TookDamage,
    Extracted
}

/// <summary>One coach card.</summary>
public sealed record TutorialStep(
    string Title,
    string Body,
    TutorialTrigger[] AdvanceOn)
{
    /// <summary>Steps with no trigger are advanced by the Next button alone.</summary>
    public bool IsManual => AdvanceOn.Length == 0;
}

/// <summary>
/// The guided first descent. It teaches on the player's real folder rather than
/// a sandbox, because the whole point of the game is that the dungeon is theirs -
/// a fake training room would teach the wrong thing.
///
/// Every step can also be advanced manually, so a player whose first room happens
/// to lack (say) a matching target is never stuck.
/// </summary>
public sealed class TutorialCoach
{
    private static readonly TutorialStep[] Steps =
    {
        new("This room is a real folder",
            "The grid you are looking at is one folder on your disk. Its name is in the bar above. " +
            "Every square on it is one real file inside that folder - nothing here is invented.",
            Array.Empty<TutorialTrigger>()),

        new("You are the cyan dot",
            "Move with W A S D or the arrow keys. Try a step now.",
            new[] { TutorialTrigger.Moved }),

        new("Click a square to see what it really is",
            "Click any square. The panel on the right shows the actual file behind it - its full path, " +
            "its size and how old it is. The game never lies about what a monster is.",
            new[] { TutorialTrigger.Selected }),

        new("Size and age decide the fight",
            "A big file has more health and takes up more squares. A file you saved today is HOT and hits " +
            "you twice a turn. A file older than three years is DORMANT - it will not attack at all until " +
            "you touch it, and striking it while it sleeps does triple damage.",
            Array.Empty<TutorialTrigger>()),

        new("Attack with 1, 2 or 3",
            "Stand next to something and press 1, 2 or 3 to hit it with that parser. Each strike costs " +
            "cycles - the three bars on the left. When they run out, your turn ends and everything in the " +
            "room gets to act.",
            new[] { TutorialTrigger.Struck }),

        new("The parser has to match",
            "Look at the log. A matched parser ignores armour and hits for roughly six times as much as " +
            "the wrong one. The inspector tells you which parser counters the target. This is the whole " +
            "strategy: know what kind of files live in a folder before you walk into it.",
            new[] { TutorialTrigger.StruckMatched, TutorialTrigger.Killed }),

        new("Doors are subfolders",
            "The orange squares on the walls are subfolders, listed on the right with how many things are " +
            "inside. Step onto one to go deeper. Deeper means a higher floor number, tougher files and " +
            "better loot - but you are further from the way out.",
            Array.Empty<TutorialTrigger>()),

        new("Loot only counts if you walk out",
            "Anything you pick up is only being carried. Walk back to the folder you started in and press " +
            "X to extract and keep it. If your integrity hits zero first, the haul is gone - you keep your " +
            "experience and everything you mapped, but not the loot.",
            new[] { TutorialTrigger.Extracted }),

        new("Come back tomorrow",
            "Whenever you save, download or edit something, that folder becomes tomorrow's Rift: double " +
            "loot, one day only. You do not grind for new levels - your ordinary computer use makes them.",
            Array.Empty<TutorialTrigger>())
    };

    private int _index;

    public bool IsActive { get; private set; }
    public bool IsFinished => _index >= Steps.Length;

    public TutorialStep? Current => IsActive && !IsFinished ? Steps[_index] : null;

    public int StepNumber => Math.Min(_index + 1, Steps.Length);
    public int StepCount => Steps.Length;

    /// <summary>Raised whenever the visible card changes, including on finish.</summary>
    public event Action? Changed;

    public void Start()
    {
        _index = 0;
        IsActive = true;
        Changed?.Invoke();
    }

    public void Stop()
    {
        IsActive = false;
        Changed?.Invoke();
    }

    /// <summary>Advances if the current step was waiting for this action.</summary>
    public void Notify(TutorialTrigger trigger)
    {
        if (Current is not { } step) return;
        if (step.IsManual) return;
        if (!step.AdvanceOn.Contains(trigger)) return;

        Advance();
    }

    /// <summary>The Next button, and the fallback for any step the player cannot satisfy.</summary>
    public void Advance()
    {
        if (!IsActive) return;

        _index++;
        if (IsFinished) IsActive = false;
        Changed?.Invoke();
    }
}
