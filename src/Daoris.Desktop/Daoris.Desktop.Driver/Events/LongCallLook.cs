namespace Daoris.Driver;

/// <summary>A call or a background task of a light session past its bound, found by the watcher's look and told once (<see cref="QuietWord"/>).</summary>
/// <param name="Key">The word's key, <c>long-call/&lt;session&gt;/call/&lt;start&gt;</c> or <c>/task/&lt;id&gt;</c>.</param>
/// <param name="Title">The call's title or the task's name.</param>
/// <param name="Call">The running call it is, to be marked told when its word is kept; null for a background task.</param>
public sealed record LongCallStep(string Session, string Key, string Title, TimeSpan Ran, TimeSpan Bound, RunningCall? Call)
{
    /// <summary>Whether it is a background task, not a call.</summary>
    public bool Background => Call is null;
}

/// <summary>
/// A light session's long calls, as the watcher's one look sees them: a running
/// call (<see cref="RunningCalls"/>) or a background task past the pipeline's <see cref="CallBound"/> is found once, by its start or its id.
/// A call held for the person's answer is their wait and found never. Found only where the door takes an urgent word at its next step: where
/// it takes words at its turn's end the call has ended before a word could reach it, and the rail's time says it instead.
/// </summary>
public sealed class LongCallLook(TimeSpan bound, Func<IReadOnlyList<RunningTask>>? background, Func<bool> reaches)
{
    /// <summary>The kind a long call's key names (<see cref="DaorisWord.Key"/>): <c>long-call/&lt;session&gt;/…</c>.</summary>
    public const string Kind = "long-call";

    private readonly HashSet<string> _found = new(StringComparer.Ordinal);

    /// <summary>The bound.</summary>
    public TimeSpan Bound => bound;

    /// <summary>What is newly past the bound at <paramref name="now"/>, each found once.</summary>
    public IReadOnlyList<LongCallStep> Find(string session, RunningCalls calls, DateTimeOffset now)
    {
        if (!reaches()) return [];

        var found = new List<LongCallStep>();
        if (calls.Calling(session) is { Waiting: false, Title.Length: > 0 } call && now - call.Since >= bound)
        {
            var key = $"{Kind}/{session}/call/{call.Since.UtcTicks}";
            if (_found.Add(key)) found.Add(new LongCallStep(session, key, call.Title, now - call.Since, bound, call));
        }

        foreach (var task in background?.Invoke() ?? [])
        {
            if (now - task.Since < bound) continue;
            var key = $"{Kind}/{session}/task/{task.Id}";
            if (_found.Add(key)) found.Add(new LongCallStep(session, key, task.Name, now - task.Since, bound, null));
        }

        return found;
    }
}
