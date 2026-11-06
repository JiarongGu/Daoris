namespace Daoris.Driver;

/// <summary>
/// The person's ask of a helper as the window's doors judge it (the page's <c>SESSION_HELPER_ASK</c> and an Ask Daoris card's Apply):
/// the question is a question within the bound, the agent is one word, and the session is one of this machine's that may ask, in the
/// verb's sentences (<see cref="SessionsHelperCommand.Unfit"/>); what the files alone carry is <see cref="HelperPersonAsk"/>'s.
/// </summary>
public static class HelperPersonAskDoor
{
    public static async Task<HelperPersonAsked> AskAsync(
        ServiceClient service, HelperFiles files, ISessionEvents events, string id, string? question, string? agent, string? after, bool toPerson,
        CancellationToken ct)
    {
        static HelperPersonAsked Refused(string why, string sentence) => new(null, why, $"{sentence} Nothing was asked.");
        question = (question ?? "").Trim();
        agent = agent?.Trim();
        if (question.Length == 0) return Refused(HelperAskWhy.Blank, "A helper is asked a question, and none was given.");
        if (question.Length > SessionsHelperCommand.MaxQuestion)
        {
            return Refused(HelperAskWhy.TooLong,
                $"A question is at most {SessionsHelperCommand.MaxQuestion} characters, and this is {question.Length}.");
        }

        if (agent is not null && agent.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)))
        {
            return Refused(HelperAskWhy.Agent, $"An agent is named as one word, as it is listed in Settings, and `{agent}` is not.");
        }

        var asker = await service.RecordAsync(id, ct).ConfigureAwait(false);
        var refused = asker is null or { Teammate: true }
            ? (HelperAskWhy.NotHeld, $"No session `{id}` of this machine's is held to bring an answer back to.")
            : SessionsHelperCommand.Unfit(id, asker);
        if (refused is var (why, sentence)) return Refused(why, sentence);

        return HelperPersonAsk.Write(files, events, id, question, agent, after, toPerson, DateTimeOffset.UtcNow);
    }
}
