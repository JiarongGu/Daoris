namespace Daoris.Driver;

/// <summary>What an answer to a superseded commit did: done, or why not (a code the page words), with the driver's sentence.</summary>
public sealed record IntegrationAnswered(bool Done, string? Why, string Message);

/// <summary>
/// A superseded commit's disposition answered: <i>superseded</i> marks it answered, its work kept on
/// its recovery ref, which goes only by the clean-up's rules; <i>rebase it</i> tells the member's session, through the say door as Daoris's words keyed
/// by the request and the commit, to rebase it onto the line in its own tree, and marks it answered once the words are kept. Nothing is
/// discarded, and no branch or ref is touched.
/// </summary>
public static class IntegrationDispositions
{
    /// <summary>The say door's key for a rebase asked of a superseded commit: one telling for each request and commit.</summary>
    public static string KeyOf(string request, string commit) => $"superseded/{request}/{commit}";

    /// <summary>Answer <paramref name="commit"/> of <paramref name="request"/> by <paramref name="answer"/>; <paramref name="say"/> tells its session where it is <i>rebase it</i>.</summary>
    public static async Task<IntegrationAnswered> AnswerAsync(
        IntegrationStore store, string request, string commit, string answer, RebaseTelling.Say? say, CancellationToken ct = default)
    {
        if (!IntegrationAnswers.All.Contains(answer))
        {
            return new IntegrationAnswered(false, "answer", $"`{answer}` is no answer to a superseded commit: one of {string.Join(", ", IntegrationAnswers.All)}.");
        }

        var named = TextBounds.ShortCommit(commit, 8);
        if (store.Dispositions(request).FirstOrDefault(each => each.Commit.StartsWith(commit, StringComparison.OrdinalIgnoreCase) && each.Pending) is not { } pending
            || store.Request(request) is not { } asked)
        {
            return new IntegrationAnswered(false, "gone", $"Commit {named} of request {request} awaits no disposition: nothing was changed.");
        }

        if (answer == IntegrationAnswers.Rebase)
        {
            if (asked.Session is not { Length: > 0 } session || say is null)
            {
                return new IntegrationAnswered(false, "no-session",
                    $"Request {request}'s work has no session here to ask to rebase {named}: nothing was changed. Answer *superseded*, or rebase it by hand from `{pending.Reference}`.");
            }

            SayAnswer? said;
            try
            {
                said = await say(session, RebaseWords(asked, pending), KeyOf(request, pending.Commit), ct).ConfigureAwait(false);
            }
            catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException)
            {
                said = new SayAnswer(false, error.Message);
            }

            if (said is not { Kept: true })
            {
                return new IntegrationAnswered(false, "untold",
                    $"Session `{session}` could not be asked to rebase {named} ({(said?.Message ?? "nothing reached its conversation").TrimEnd('.')}): nothing was changed.");
            }
        }

        if (!store.Answer(request, pending.Commit, answer))
        {
            return new IntegrationAnswered(false, "gone", $"Commit {named} of request {request} was answered already: nothing more was changed.");
        }

        return new IntegrationAnswered(true, null, answer == IntegrationAnswers.Rebase
            ? $"Session `{asked.Session}` is asked to rebase {named} onto `{asked.Line}` in its own tree; the commit's work stays kept on `{pending.Reference}`."
            : $"Commit {named} is answered superseded; its work stays kept on `{pending.Reference}` until D102's rules remove it.");
    }

    /// <summary>The words a session is told: which commit, what superseded it in which files, where its work is kept, and what to do.</summary>
    public static string RebaseWords(IntegrationRequest request, IntegrationDisposition pending) =>
        $"Your commit {TextBounds.ShortCommit(pending.Commit, 8)} (\"{pending.Subject}\") on `{request.Branch}` came after its integration request froze, and `{request.Line}` "
        + $"has since changed the same lines in commit {TextBounds.ShortCommit(pending.By, 8)}"
        + (pending.Files.Count > 0 ? $" ({string.Join(", ", pending.Files.Select(file => $"`{file}`"))})" : "")
        + $". The owner asks you to rebase it: bring `{request.Line}` into your tree, replay the commit onto it, settle each conflict, run what the change "
        + $"reaches, commit, and finish again so it is requested anew. Its work is kept on `{pending.Reference}` meanwhile.";
}
