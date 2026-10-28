namespace Daoris.Driver;

/// <summary>
/// <c>daoris-driver sessions helper &lt;session&gt; "&lt;question&gt;" [--agent &lt;name&gt;]</c> (MIXAGENT2a-ii, D163 point 2; the mixed
/// agents design §3, §5): the person's own door to a helper. It writes the ask a connector's <c>helper_ask</c> writes (the same file
/// under the home's <c>helpers</c> folder), with <c>askedBy: person</c>, for the driver's look to take; the person is the one asking, so
/// the rule's <c>ask</c> and <c>off</c> do not hold it and the driver starts it within the limits.
/// </summary>
/// <remarks>
/// <para>It judges only what a reader of the file cannot know: that the session is one of this machine's that may ask (no helper, no
/// reviewer, no intake: a helper cannot ask for a helper), that the question is a question within the bound, and that an agent is one
/// word. Everything else is the driver's, at its next look.</para>
/// <para>Exit codes are the family's: 0 asked · 1 refused, a policy answer · 2 could not, the usage among them.</para>
/// </remarks>
public static class SessionsHelperCommand
{
    /// <summary>The longest question kept, in characters: the service's <c>HelperBox.MaxQuestion</c>, which it holds the same.</summary>
    public const int MaxQuestion = 4000;

    private const string UsageProblem =
        "`helper` takes one session's id, the question in quotes, and `--agent <name>` where one agent is wanted.";

    /// <summary>What the words ask: <c>helper &lt;id&gt; "…" [--agent &lt;name&gt;]</c>, or null with what is wrong with them.</summary>
    public static SessionsAsk? Read(IReadOnlyList<string> args, out string? problem)
    {
        problem = null;
        switch (args)
        {
            case ["helper", "--approve", var ask] when Word(ask):
                return new SessionsAsk("helper-approve") { Ids = [ask] };
            case ["helper", "--decline", var ask] when Word(ask):
                return new SessionsAsk("helper-decline") { Ids = [ask] };
            case ["helper", "--approve" or "--decline", ..]:
                problem = "`helper --approve` and `helper --decline` take one ask's id, as `#<ask>` is said on its session's page.";
                return null;
            case ["helper", var id, var question] when Word(id) && question.Trim().Length > 0:
                return new SessionsAsk("helper") { Ids = [id], Text = question.Trim() };
            case ["helper", var id, var question, "--agent", var agent] when Word(id) && question.Trim().Length > 0 && Word(agent):
                return new SessionsAsk("helper") { Ids = [id], Text = question.Trim(), Agent = agent.Trim() };
        }

        problem = UsageProblem;
        return null;
    }

    /// <summary>Write the ask for the driver's look, or say why not.</summary>
    public static async Task<int> RunAsync(SessionsWorld world, SessionsAsk ask, TextWriter output, CancellationToken ct = default)
    {
        var (id, question, agent) = (ask.Ids[0], ask.Text ?? "", ask.Agent);
        if (question.Length > MaxQuestion)
        {
            output.WriteLine($"sessions: a question is at most {MaxQuestion} characters, and this is {question.Length}. Nothing was asked.");
            return 1;
        }

        if (agent is not null && agent.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)))
        {
            output.WriteLine($"sessions: an agent is named as one word, as it is listed in Settings, and `{agent}` is not. Nothing was asked.");
            return 1;
        }

        PriorSession? asker;
        try
        {
            asker = await world.Service.RecordAsync(id, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is HttpRequestException or DriverException or System.Text.Json.JsonException)
        {
            output.WriteLine($"sessions: the service did not answer ({error.Message}), so nothing was asked.");
            return 2;
        }

        // The box's words for each, so an asker reads the same sentence from either door.
        var refused = asker switch
        {
            null => $"no session `{id}` of this machine's is held to bring an answer back to.",
            { Serves: { } serves } => $"session `{id}` is itself a helper for session `{serves}`, and a helper cannot ask for a helper.",
            { Opinion: { } opinion } => $"session `{id}` reads another session's work for second opinion `{opinion}`, and a reviewer cannot ask for a helper.",
            { Ask: { Length: > 0 } intake } => $"session `{id}` is the intake for ask `#{intake}`, which does no work in a repository, and an intake cannot ask for a helper.",
            _ => null,
        };
        if (refused is not null)
        {
            output.WriteLine($"sessions: {refused} Nothing was asked.");
            return 1;
        }

        string asked;
        try
        {
            asked = new HelperFiles(world.Home).WriteAsk(id, question, agent, DateTimeOffset.UtcNow);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            output.WriteLine($"sessions: the ask could not be written under the home ({error.Message}), so nothing was asked.");
            return 2;
        }

        output.WriteLine(
            $"sessions: asked `#{asked}` for session {id}: the driver takes it at its next look and starts a helper on another agent, "
            + "read-only, within the limits; its answer comes back to that session as its next turn, marked as the helper's.");
        return 0;
    }

    /// <summary>
    /// <c>helper --approve|--decline &lt;ask&gt;</c> (HELPERTERM1): the person's yes or no to an agent's ask that waits on it, by the
    /// judge the page's presses use (<see cref="HelperPersonWord"/>). The driver's look reads the file at its next look; the loop
    /// is not nudged from here, and the line says so. Exit 0 where the word stands, 1 where the judge refuses it.
    /// </summary>
    public static Task<int> RunWordAsync(SessionsWorld world, SessionsAsk ask, TextWriter output)
    {
        var id = ask.Ids[0].TrimStart('#');
        var files = new HelperFiles(world.Home);
        try
        {
            var answer = ask.Verb == "helper-approve"
                ? HelperPersonWord.Approve(files, null, id)
                : HelperPersonWord.Decline(files, new SessionEvents(DaorisHome.Entry(world.Home, HomeEntry.Sessions)), null, null, id);
            output.WriteLine($"sessions: {answer.Message}{(answer.Stands ? "" : " Nothing was changed.")}");
            return Task.FromResult(answer.Stands ? 0 : 1);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            output.WriteLine($"sessions: the word could not be written under the home ({error.Message}), so nothing was changed.");
            return Task.FromResult(2);
        }
    }

    private static bool Word(string word) => word.Trim().Length > 0 && !word.StartsWith('-');
}
