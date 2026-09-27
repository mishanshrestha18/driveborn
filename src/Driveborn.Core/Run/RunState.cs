using Driveborn.Core.Model;

namespace Driveborn.Core.Run;

/// <summary>Why a descent ended.</summary>
public enum RunOutcome
{
    InProgress = 0,
    Extracted,
    Died,
    Abandoned
}

/// <summary>
/// One descent. Loot in <see cref="Player.Carried"/> only becomes permanent on
/// extraction - dying loses the haul but keeps XP and map knowledge. That is the
/// tension the whole session is built around.
/// </summary>
public sealed class RunState
{
    public required string TerritoryRoot { get; init; }
    public required DateTime StartedUtc { get; init; }
    public required Player Player { get; init; }

    public Room? CurrentRoom { get; set; }
    public RunOutcome Outcome { get; set; } = RunOutcome.InProgress;

    public int Turn { get; set; } = 1;
    public int DeepestFloor { get; set; }
    public int RoomsCleared { get; set; }
    public int EntitiesFelled { get; set; }

    /// <summary>Folders visited this run, in order, so the UI can draw a breadcrumb.</summary>
    public List<string> Path { get; } = new();

    /// <summary>Rolling combat log, newest last.</summary>
    public List<string> Log { get; } = new();

    public bool IsOver => Outcome != RunOutcome.InProgress;

    /// <summary>Extraction is only allowed from a territory root - you must walk out.</summary>
    public bool CanExtractHere =>
        CurrentRoom is not null &&
        CurrentRoom.FolderPath.Equals(TerritoryRoot, StringComparison.OrdinalIgnoreCase);

    public int CarriedValue => Player.Carried.Sum(r => r.Value);

    public void Say(string message)
    {
        Log.Add(message);
        if (Log.Count > 200) Log.RemoveRange(0, Log.Count - 200);
    }
}

/// <summary>Result of a single player action.</summary>
public sealed record ActionResult(bool Ok, string Message)
{
    public static ActionResult Fail(string why) => new(false, why);
    public static ActionResult Done(string what) => new(true, what);
}
