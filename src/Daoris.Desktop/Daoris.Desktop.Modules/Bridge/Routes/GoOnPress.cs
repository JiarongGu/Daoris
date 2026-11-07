using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// The hand-over's press naming no agent (MSG1g, D137 §2.2; D189 point 3, ONEWAY1j): the session goes on where it can, the walk's choice
/// taking the words. The driver's <see cref="GoOnNew"/> judges first: words a resume holds while the account their session ran on cools go
/// on in a new session at the driver's next look, handed them, without that conversation, the same judge
/// <c>daoris session hand-over &lt;id&gt;</c> calls. Where that judge says the session is a conversation, a closed quest's, or Ask Daoris's,
/// which nothing carries on by itself, the press is *Go on with Daoris's brief*: a new part in its tree on its agent and account, handed
/// the brief and then the words (<see cref="ChatRunner.GoOnBriefedAsync"/>). One answer for both, <c>{sessionId, kept, why, message}</c>:
/// <c>kept</c> is that the choice is kept (a go-on-new) or the new part was opened (a brief), <c>sessionId</c> is the new part where one
/// opened and null where the next look opens it, and a refusal is <c>kept: false</c> with its code and the driver's sentence.
/// </summary>
internal static class GoOnPress
{
    public static async Task<object?> AskAsync(
        DriverLoop loop, string id, DriverConfig config, Func<string, string, Task> onEnded, CancellationToken cancellationToken)
    {
        var service = loop.Service ?? throw DriverArguments.NotReady();
        var answer = await GoOnNew.AskAsync(
                service, loop.Home, (adapter, profile, model) => loop.Harnesses.CoolingOf(adapter ?? "", profile, model), id, DateTimeOffset.UtcNow, cancellationToken)
            .ConfigureAwait(false);
        if (answer.Why is GoOnNew.Conversation or GoOnNew.Closed or WordsNever.Help)
        {
            var chat = loop.Chat ?? throw DriverArguments.NotReady();
            var moved = await chat.GoOnBriefedAsync(id, config, onEnded, cancellationToken).ConfigureAwait(false);
            loop.Nudge();
            return new { moved.SessionId, Kept = moved.Sent, moved.Why, moved.Message };
        }

        if (answer.Sent) loop.Nudge();
        return new { SessionId = (string?)null, Kept = answer.Sent, answer.Why, answer.Message };
    }
}
