using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// The second opinion's answer an Accept on the review carries (<c>WORKFLOW_NODE</c>'s <c>answers</c>, the gate's token the page drew): kept
/// at the gate of the session whose handback the review's record names last, before the Accept itself, as the landing's press kept it. A
/// token the gate no longer shows answers nothing (<see cref="OpinionPresses.PressedAsync"/>).
/// </summary>
internal static class AcceptAnswers
{
    public static async Task KeepAsync(DriverLoop loop, string run, string node, string? answers, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(answers) || loop.Service is not { } service) return;
        var records = new NodeRecordFiles(loop.Home);
        if (new GraphRuns(records).Find(run) is not { } bound || records.Read(bound.Unit)?.Statuses.GetValueOrDefault(node) is not { } status) return;
        if (ReviewRecords.Read(loop.Home, run, node, status.Attempt)?.Parts.LastOrDefault(part => part.Session is not null)?.Session is not { } session) return;
        await new OpinionPresses(loop.Home, service).AnswerAsync(session, answers, ct).ConfigureAwait(false);
    }
}
