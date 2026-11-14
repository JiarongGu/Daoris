namespace Daoris.Driver;

/// <summary>
/// The quests whose last start could not go (an account cooling, a plugin's hold, a sign-in missing), kept by the loop from one look
/// to the next so the plan puts them behind every quest that can start: a start that cannot go never holds a repository's turn or a
/// place under the cap from the one that can, and is tried again at each look it is the only one left, so it goes the moment it can.
/// </summary>
internal sealed class HeldStarts
{
    private readonly HashSet<string> _quests = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();

    /// <summary>The quests held now, as a look hands them to the planner.</summary>
    public IReadOnlySet<string> Quests
    {
        get { lock (_gate) return new HashSet<string>(_quests, StringComparer.OrdinalIgnoreCase); }
    }

    /// <summary>Keep or let go of each start's quest by what it came to: held and unopened is kept, a record open or an end lets it go.</summary>
    public void Settle(IEnumerable<StartCame> outcomes)
    {
        lock (_gate)
        {
            foreach (var each in outcomes)
            {
                if (each.Started.Quest is not { } quest) continue;
                if (each.Came is not { } came || came.Opened || came.Ended is not null) _quests.Remove(quest);
                else if (came.Held is not null || came.Cooling is not null || came.SignedOut is not null) _quests.Add(quest);
            }
        }
    }
}
