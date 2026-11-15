using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>
/// <c>session.opening</c>: a driven session's open up to its agent's spawn, each step timed, so a slow open names its slow step. Written
/// once per run at the hand, before the hold starts the agent: <c>ms</c> from the run's first stage, and <c>stages</c> the compact line
/// <c>watch.looked</c> carries (<see cref="LookStages.Take"/>): each run stage as <c>name=ms</c> in the order it began, the files stage's
/// own steps as <c>files.*</c>, the hand's preparation as <c>hand</c>, a step under 10 ms left out. Names only, never a path. The
/// agent's own start, from its spawn to its first prompt, is how long <c>session.opened</c> comes after it.
/// </summary>
internal static class OpeningLine
{
    internal const string Event = "session.opening";

    internal static AccountLine Of(string session, string door, long ms, string? stages) =>
        new(Event, [("session", session), ("door", door), ("ms", ms), ("stages", stages)]);

    /// <summary>The hand's own step ended and every step of the run said, once.</summary>
    /// <param name="began">When the run began, as <see cref="Stopwatch.GetTimestamp"/> read it.</param>
    internal static void Say(Action<AccountLine> said, LookStages steps, long began, IDisposable hand, string session, string door)
    {
        hand.Dispose();
        said(Of(session, door, (long)Stopwatch.GetElapsedTime(began).TotalMilliseconds, steps.Take()));
    }
}
