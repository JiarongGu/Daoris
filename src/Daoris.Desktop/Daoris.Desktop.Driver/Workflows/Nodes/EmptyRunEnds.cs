using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>
/// Why a run's end closed no quest and ended no session, or why a run at its end that a session waits on was not governed for its end to reach
/// it, by a code: written to the machine log once per change of reason for its run, as a landing's wait is (<see cref="LandWaits"/>), so an end
/// that leaves its sessions waiting shows why. Kept in memory: a restart says each once more.
/// </summary>
public static class EmptyRunEnds
{
    /// <summary>The machine log's event an empty end is written under.</summary>
    public const string Event = "run.end.empty";

    /// <summary>No door writes a run's end in this loop.</summary>
    public const string NoDoor = "no-door";

    /// <summary>It names no quest to close, and no session waits on the run.</summary>
    public const string Nothing = "nothing";

    /// <summary>The service refused to close its quests, so it moved no session.</summary>
    public const string Refused = "refused";

    /// <summary>The service took it, and nothing it named still waited.</summary>
    public const string Unmoved = "unmoved";

    /// <summary>A run at its end that a session waits on is not governed: its newest quest here was not read.</summary>
    public const string QuestUnread = "quest-unread";

    /// <summary>A run at its end that a session waits on is not governed: its chain came back here, and no start asked a pass for it yet.</summary>
    public const string NotAsked = "not-asked";

    /// <summary>A run at its end that a session waits on is not governed: the graph version it is bound to does not read.</summary>
    public const string VersionUnread = "version-unread";

    /// <summary>The session records did not read at the look, so no run at its end is known to have a session waiting on it.</summary>
    public const string SessionsUnread = "sessions-unread";

    // The last reason said for each run, by home and run.
    private static readonly ConcurrentDictionary<string, string> Last = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Whether <paramref name="reason"/> is not the last one said for the run, which it then is.</summary>
    public static bool Changed(string home, string run, string reason)
    {
        var key = $"{home}\n{run}";
        if (string.Equals(Last.GetValueOrDefault(key), reason, StringComparison.Ordinal)) return false;
        Last[key] = reason;
        return true;
    }

    /// <summary>The end as the machine log's sentence: the run, the reason's code and its words.</summary>
    /// <param name="detail">The service's refusal, for <see cref="Refused"/>.</param>
    public static string Sentence(string run, string reason, string? detail = null) =>
        (reason == SessionsUnread ? "runs at their end" : $"run {run}: its end closed no quest and ended no session") + $" ({reason}): " + reason switch
        {
            NoDoor => "no door writes a run's end in this loop, so its sessions wait.",
            Nothing => "it names no quest to close, and no session waits on the run.",
            Refused => $"the service refused to close its quests ({detail}), so no session of it moved.",
            Unmoved => "the service took it, and nothing it named still waited.",
            QuestUnread => "a session waits on it at its end, and its newest quest here was not read, so its end does not reach that session.",
            NotAsked => "a session waits on it, and its chain came back here with a newer quest that no start has asked a pass for yet.",
            VersionUnread => "a session waits on it at its end, and the graph version it is bound to does not read, so its end does not reach that session.",
            SessionsUnread => "this machine's session records did not read, so no session waiting on a run at its end is known; they are read again at the next look.",
            _ => reason,
        };
}
