using System.Text.Json;

namespace Daoris.Driver;

/// <summary>What a start's landing is named for, as its session is told it.</summary>
internal static class StartLanding
{
    /// <summary>
    /// A chain's step lands on its chain's branch, named for the chain's first quest, so it is told that branch, which its done moves
    /// on. A quest that follows nothing is its own; a service that cannot walk the chain leaves the step its own name, since a start
    /// never waits on a sentence.
    /// </summary>
    /// <param name="find">One quest as the service answers it, closed ones included.</param>
    public static async Task<LandingSubject> SubjectAsync(
        string session, QuestView quest, Func<string, Task<QuestView?>> find, CancellationToken ct)
    {
        var own = LandingSubject.Of(session, quest);
        if (quest.Parent is not { Length: > 0 }) return own;
        try
        {
            var chain = await LandingRules.SubjectAsync(session, quest.Id, find, opening: null).ConfigureAwait(false);
            // A service that no longer answers for the step itself names it with no title: its own is truer.
            return chain.Title is null ? own : chain;
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or JsonException
                                          || (error is OperationCanceledException && !ct.IsCancellationRequested))
        {
            return own;
        }
    }
}
