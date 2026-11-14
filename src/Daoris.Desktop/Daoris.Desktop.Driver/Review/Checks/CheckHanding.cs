namespace Daoris.Driver;

/// <summary>
/// How a verdict goes back to the session that asked: as Daoris's own word, kept under the check's key, which reopens the session that
/// ended waiting (<see cref="EndNoteCodes.EndedAwaitsCheck"/>) with the verdict. A done quest's verdict stays for the person; a green
/// check on a done quest, or one Daoris asked itself, owes no word.
/// </summary>
public static class CheckHanding
{
    /// <summary>The mark a verdict owed no word carries in place of a word's id.</summary>
    public const string Quiet = "quiet";

    /// <summary>
    /// Hand every finished verdict not handed yet. <paramref name="say"/> answers the kept word's id, or null where the host refused or
    /// lost it, which leaves the verdict for the next look. The ids handed, in order.
    /// </summary>
    public static async Task<IReadOnlyList<string>> HandAsync(
        CheckFiles files, CheckSay say, CancellationToken ct, Func<string, CancellationToken, Task<string?>> questStatus)
    {
        var handed = new List<string>();
        foreach (var ask in files.Asks())
        {
            if (files.ReadState(ask.Id) is not { Handed: null } state || !CheckStates.Finished(state.State)) continue;
            if (ask.By == CheckAsk.ByDaoris && state.State == CheckStates.Reached)
            {
                files.WriteState(state with { Handed = Quiet });
                continue;
            }

            if (await questStatus(ask.Session, ct).ConfigureAwait(false) == "Done")
            {
                if (state.State == CheckStates.Reached) files.WriteState(state with { Handed = Quiet });
                continue;
            }

            if (await say(ask.Session, CheckWaits.Words(ask, state), CheckWaits.Key(ask), ct).ConfigureAwait(false) is not { } word) continue;
            files.WriteState(state with { Handed = word });
            handed.Add(ask.Id);
        }

        return handed;
    }
}

/// <summary>Say a word as Daoris's to a session under a key: the kept word's id, or null where the host refuses or loses it.</summary>
public delegate Task<string?> CheckSay(string session, string text, string key, CancellationToken ct);
