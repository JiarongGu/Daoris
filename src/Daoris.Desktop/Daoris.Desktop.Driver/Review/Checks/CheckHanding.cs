namespace Daoris.Driver;

/// <summary>How a verdict goes back to its asker: Daoris's own word under the check's key, which reopens the session that ended waiting (<see cref="EndNoteCodes.EndedAwaitsCheck"/>); every verdict is the asker's, none is owed to anyone else.</summary>
public static class CheckHanding
{
    /// <summary>Hand every finished verdict not handed yet: <paramref name="say"/> answers the kept word's id, or null where the host refused it, which leaves the verdict for the next look. The ids handed, in order.</summary>
    public static async Task<IReadOnlyList<string>> HandAsync(CheckFiles files, CheckSay say, CancellationToken ct)
    {
        var handed = new List<string>();
        foreach (var ask in files.Asks())
        {
            if (files.ReadState(ask.Id) is not { Handed: null } state || !CheckStates.Finished(state.State)) continue;
            if (await say(ask.Session, CheckWaits.Words(ask, state), CheckWaits.Key(ask), ct).ConfigureAwait(false) is not { } word) continue;
            files.WriteState(state with { Handed = word });
            handed.Add(ask.Id);
        }

        return handed;
    }
}

/// <summary>Say a word as Daoris's to a session under a key: the kept word's id, or null where the host refuses or loses it.</summary>
public delegate Task<string?> CheckSay(string session, string text, string key, CancellationToken ct);
