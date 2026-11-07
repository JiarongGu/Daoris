namespace Daoris.Driver;

/// <summary>
/// <c>daoris session helper &lt;session&gt; "&lt;question&gt;" [--agent &lt;name&gt;]</c>:
/// the person's own door to a helper. It writes the ask a connector's <c>helper_ask</c> writes (the same file
/// under the home's <c>helpers</c> folder), with <c>askedBy: person</c>, for the driver's look to take; the person is the one asking, so
/// the rule's <c>ask</c> and <c>off</c> do not hold it and the driver starts it within the limits.
/// </summary>
/// <remarks>
/// <para>It judges only what a reader of the file cannot know: that the session is one of this machine's that may ask (the start layer's
/// table, <see cref="StartAdmission"/>: no helper, reviewer, intake or Ask Daoris), that the question is a question within the bound, and that an agent is one
/// word. Everything else is the driver's, at its next look.</para>
/// <para>Exit codes are the family's: 0 asked · 1 refused, a policy answer · 2 could not, the usage among them.</para>
/// </remarks>
public static class SessionsHelperCommand
{
    /// <summary>The longest question kept, in characters: the service's <c>HelperBox.MaxQuestion</c>, which it holds the same.</summary>
    public const int MaxQuestion = 4000;

    private const string UsageProblem =
        "`helper` takes one session's id, the question in quotes, and `--agent <name>` where one agent is wanted.";

    /// <summary>
    /// What the words ask: <c>helper &lt;id&gt; "…" [--agent &lt;name&gt;] [--after &lt;ask&gt;] [--to-me]</c> or <c>pass &lt;ask&gt;</c>, or null with
    /// what is wrong with them.
    /// </summary>
    public static TAsk? Read<TAsk>(IReadOnlyList<string> args, out string? problem, ParticipantsAskMaker<TAsk> make)
        where TAsk : class
    {
        problem = null;
        switch (args)
        {
            case ["pass", var asked] when Word(asked):
                return make("pass", [asked.Trim().TrimStart('#')], null, null, [], null, false);
            case ["pass", ..]:
                problem = "`pass` takes one ask's id, as `#<ask>` is said on its member's thread: the helper's answer kept for you, passed to its session.";
                return null;
            case ["helper", "--approve", var ask] when Word(ask):
                return make("helper-approve", [ask], null, null, [], null, false);
            case ["helper", "--decline", var ask] when Word(ask):
                return make("helper-decline", [ask], null, null, [], null, false);
            case ["helper", "--approve" or "--decline", ..]:
                problem = "`helper --approve` and `helper --decline` take one ask's id, as `#<ask>` is said on its session's page.";
                return null;
            case ["helper", var id, var question, ..] when Word(id) && question.Trim().Length > 0:
            {
                string? agent = null, after = null;
                var toMe = false;
                var rest = args.Skip(3).ToList();
                for (var at = 0; at < rest.Count; at++)
                {
                    switch (rest[at])
                    {
                        case "--agent" when at + 1 < rest.Count && Word(rest[at + 1]) && agent is null:
                            agent = rest[++at].Trim();
                            break;
                        case "--after" when at + 1 < rest.Count && Word(rest[at + 1]) && after is null:
                            after = rest[++at].Trim().TrimStart('#');
                            break;
                        case "--to-me" when !toMe:
                            toMe = true;
                            break;
                        default:
                            problem = UsageProblem;
                            return null;
                    }
                }

                return make("helper", [id], question.Trim(), agent, [], after, toMe);
            }
        }

        problem = UsageProblem;
        return null;
    }

    /// <summary>
    /// <c>pass &lt;ask&gt;</c>: a helper's answer kept for the person handed to the session that
    /// asked, by the judge the page's <i>Pass to the lead</i> uses (<see cref="HelperPassToLead"/>). The driver's look hands it at its
    /// next look. Exit 0 where it is passed, 1 where the judge refuses it.
    /// </summary>
    public static Task<int> RunPassAsync(IParticipantsWorld world, IParticipantsAsk ask, TextWriter output)
    {
        try
        {
            var answer = HelperPassToLead.Pass(new HelperFiles(world.Home), null, ask.Ids[0], DateTimeOffset.UtcNow);
            output.WriteLine($"sessions: {answer.Message}{(answer.Stands ? "" : " Nothing was changed.")}");
            return Task.FromResult(answer.Stands ? 0 : 1);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            output.WriteLine($"sessions: the word could not be written under the home ({error.Message}), so nothing was changed.");
            return Task.FromResult(2);
        }
    }

    /// <summary>The session that asked <paramref name="ask"/>, or null where this machine holds no such ask: what the press names beside the ask.</summary>
    public static string? AskerOf(string home, string ask) =>
        new HelperFiles(home).Asks().FirstOrDefault(each => each.Id == ask.Trim().TrimStart('#'))?.Asker;

    /// <summary>Write the ask for the driver's look, or say why not.</summary>
    public static async Task<int> RunAsync(IParticipantsWorld world, IParticipantsAsk ask, TextWriter output, CancellationToken ct = default)
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

        var refused = asker is null ? $"No session `{id}` of this machine's is held to bring an answer back to." : Unfit(id, asker)?.Sentence;
        if (refused is not null)
        {
            output.WriteLine($"sessions: {char.ToLowerInvariant(refused[0])}{refused[1..]} Nothing was asked.");
            return 1;
        }

        HelperPersonAsked asked;
        try
        {
            // The person's ask keeps `helper.person-asked` on the session, their word on the line, as the page's door does.
            asked = HelperPersonAsk.Write(
                new HelperFiles(world.Home), WorkplaceEventRecords.Current.Open(DaorisHome.Entry(world.Home, HomeEntry.Sessions)),
                id, question, agent, ask.After, ask.ToMe, DateTimeOffset.UtcNow);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            output.WriteLine($"sessions: the ask could not be written under the home ({error.Message}), so nothing was asked.");
            return 2;
        }

        output.WriteLine($"sessions: {asked.Message}");
        return asked.Asked is null ? 1 : 0;
    }

    /// <summary>
    /// Why the start layer refuses <paramref name="asker"/> a helper (<see cref="StartAdmission.HelperAsker"/>), by its code
    /// and in the sentence the page and this verb both say, the connector's words; null where its work may ask.
    /// </summary>
    public static (string Why, string Sentence)? Unfit(string id, PriorSession asker) =>
        StartAdmission.HelperAsker(StartAdmission.Marks(asker)) switch
        {
            null => null,
            StartAdmission.Helper => (HelperAskWhy.Helper, $"Session `{id}` is itself a helper for session `{asker.Serves}`, and a helper cannot ask for a helper."),
            StartAdmission.Reviewer => (HelperAskWhy.Reviewer,
                $"Session `{id}` reads another session's work for second opinion `{asker.Opinion}`, and a reviewer cannot ask for a helper."),
            StartAdmission.Intake => (HelperAskWhy.Intake,
                $"Session `{id}` is the intake for ask `#{asker.Ask}`, which does no work in a repository, and an intake cannot ask for a helper."),
            _ => (HelperAskWhy.AskDaoris, $"Session `{id}` is Ask Daoris's conversation, and Ask Daoris cannot ask for a helper."),
        };

    /// <summary>
    /// <c>helper --approve|--decline &lt;ask&gt;</c>: the person's yes or no to an agent's ask that waits on it, by the
    /// judge the page's presses use (<see cref="HelperPersonWord"/>). The driver's look reads the file at its next look; the loop
    /// is not nudged from here, and the line says so. Exit 0 where the word stands, 1 where the judge refuses it.
    /// </summary>
    public static Task<int> RunWordAsync(IParticipantsWorld world, IParticipantsAsk ask, TextWriter output)
    {
        var id = ask.Ids[0].TrimStart('#');
        var files = new HelperFiles(world.Home);
        try
        {
            var answer = ask.Verb == "helper-approve"
                ? HelperPersonWord.Approve(files, null, id)
                : HelperPersonWord.Decline(files, WorkplaceEventRecords.Current.Open(DaorisHome.Entry(world.Home, HomeEntry.Sessions)), null, null, id);
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
