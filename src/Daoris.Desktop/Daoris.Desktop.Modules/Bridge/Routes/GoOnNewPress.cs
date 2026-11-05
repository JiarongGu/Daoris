using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// *Go on in a new session*, the hand-over's press naming no agent (MSG1g, D137 §2.2): words a resume holds while the account their
/// session ran on cools go on in a new session at the driver's next look, handed them, without that conversation. The driver's
/// <see cref="GoOnNew"/> judges and keeps the person's choice, the same judge <c>daoris session hand-over &lt;id&gt;</c> calls; the loop is
/// nudged so its next look carries the words on. What cannot is <c>Kept: false</c> with its code for the page to word, and the driver's
/// sentence beside it. No session opens here, so its <c>SessionId</c> is null.
/// </summary>
internal static class GoOnNewPress
{
    public static async Task<object?> AskAsync(DriverLoop loop, string id, CancellationToken cancellationToken)
    {
        var service = loop.Service ?? throw DriverArguments.NotReady();
        var answer = await GoOnNew.AskAsync(
                service, loop.Home, (adapter, profile) => loop.Harnesses.CoolingOf(adapter ?? "", profile), id, DateTimeOffset.UtcNow, cancellationToken)
            .ConfigureAwait(false);
        if (answer.Sent) loop.Nudge();
        return new { SessionId = (string?)null, Kept = answer.Sent, answer.Why, answer.Message };
    }
}
