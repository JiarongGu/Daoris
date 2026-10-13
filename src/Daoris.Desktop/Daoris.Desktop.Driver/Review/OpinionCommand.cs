namespace Daoris.Driver;

/// <summary>
/// What <c>daoris-driver opinion</c> was asked (XAGENT1f): the verb, the session or opinion it names, and what goes with it.
/// </summary>
/// <param name="Verb"><c>ask</c>, <c>show</c>, <c>stop</c>, <c>anyway</c> or <c>myself</c>.</param>
/// <param name="Id">The working session, or for <c>stop</c> the opinion, or for <c>show</c> either.</param>
public sealed record OpinionCommandAsk(string Verb, string Id)
{
    /// <summary>For <c>ask</c>: one of the rule's reviewers, named.</summary>
    public string? Reviewer { get; init; }

    /// <summary>For <c>ask</c>: the working agent's own, in a fresh conversation.</summary>
    public bool SameAgent { get; init; }

    /// <summary>For <c>ask</c>: a struck quest's <i>Ask another agent for help</i> (§2.1's failure occasion).</summary>
    public bool Failure { get; init; }

    /// <summary>The person's words: every word after the verb's own, joined as said.</summary>
    public string? Words { get; init; }
}

/// <summary>
/// The terminal's doors to the second opinion's gate (XAGENT1f, D155 point 10; the second-agent design §8.5, §9, D50):
/// <c>daoris-driver opinion ask|show|stop|anyway|myself</c>, the screen's presses' twin, each through the driver's own press
/// (<see cref="OpinionPresses"/>) so both say and keep the same, and each printing what the gate says after it.
/// <c>sessions say</c> is <i>Send back…</i>.
/// </summary>
/// <remarks>
/// <para><b>The person's alone</b>: no connector tool and no Ask Daoris card reaches these (§9, D110).</para>
///
/// <para><b>Exit codes</b>: 0 taken, 1 refused (no such session or opinion, nothing waits, a rule that names no reviewer), 2 the
/// usage.</para>
/// </remarks>
public static class OpinionCommand
{
    public const string Usage =
        "usage: daoris-driver opinion ask <session> [--reviewer <adapter>] [--same-agent] [\"…\"]  ·  opinion show <session|opinion>\n"
        + "       opinion stop <opinion>  ·  opinion anyway <session> [\"…\"]  ·  opinion myself <session> [\"…\"]\n"
        + "       ask another agent to read a session's work before it lands, one of its rule's reviewers by name, or the working\n"
        + "       agent in a fresh conversation, from the driver's next look; say where its second opinion stands, its findings and\n"
        + "       their answers; stop a reviewer reading; go on without a settled opinion; or record your own reading in its place.\n"
        + "       Each prints what the gate says after it. `sessions say` sends the work back with your words.";

    /// <summary>Whether a line asks for the second opinion's doors.</summary>
    public static bool Asks(IReadOnlyList<string> args) => args is ["opinion", ..];

    /// <summary>The line read (its words after <c>opinion</c>), or null with why not.</summary>
    public static OpinionCommandAsk? Read(IReadOnlyList<string> args, out string? problem)
    {
        problem = null;
        if (args is not [var verb, var id, ..] || !Id(id))
        {
            problem = "`opinion` takes `ask`, `show`, `stop`, `anyway` or `myself`, then a session's id (an opinion's, for `stop`).";
            return null;
        }

        var rest = args.Skip(2).ToList();
        switch (verb)
        {
            case "show" or "stop" when rest.Count > 0:
                problem = $"`opinion {verb}` takes one id and nothing after it.";
                return null;
            case "show" or "stop":
                return new OpinionCommandAsk(verb, id.TrimStart('#'));
            case "anyway" or "myself":
                return new OpinionCommandAsk(verb, id.TrimStart('#')) { Words = Joined(rest) };
            case "ask":
            {
                string? reviewer = null;
                bool same = false, failure = false;
                var said = new List<string>();
                for (var at = 0; at < rest.Count; at++)
                {
                    switch (rest[at])
                    {
                        case "--reviewer" when at + 1 < rest.Count && !rest[at + 1].StartsWith('-'):
                            reviewer = rest[++at];
                            break;
                        case "--reviewer":
                            problem = "`--reviewer` names an adapter, one of the rule's reviewers.";
                            return null;
                        case "--same-agent":
                            same = true;
                            break;
                        case "--failure":
                            failure = true;
                            break;
                        default:
                            if (rest[at].StartsWith("--", StringComparison.Ordinal))
                            {
                                problem = $"`{rest[at]}` is not a flag `opinion ask` takes: `--reviewer <adapter>`, `--same-agent`.";
                                return null;
                            }

                            said.Add(rest[at]);
                            break;
                    }
                }

                if (reviewer is not null && same)
                {
                    problem = "name a reviewer or ask the same agent, not both.";
                    return null;
                }

                return new OpinionCommandAsk(verb, id.TrimStart('#')) { Reviewer = reviewer, SameAgent = same, Failure = failure, Words = Joined(said) };
            }

            default:
                problem = $"`{verb}` is not an `opinion` verb: `ask`, `show`, `stop`, `anyway` or `myself`.";
                return null;
        }
    }

    /// <summary>Press as asked, through the driver's own presses, and say what the gate says after it.</summary>
    /// <param name="processes">The sessions this machine runs, for <c>stop</c>.</param>
    public static async Task<int> RunAsync(
        OpinionCommandAsk ask, OpinionPresses presses, ServiceClient service, SessionProcesses processes, TextWriter output, CancellationToken ct = default)
    {
        OpinionPressed pressed;
        switch (ask.Verb)
        {
            case "stop":
                pressed = await presses.StopAsync(ask.Id, processes, ct).ConfigureAwait(false);
                output.WriteLine($"opinion: {pressed.Message}");
                return pressed.Done ? 0 : 1;
            case "show":
            {
                // An opinion's id shows that opinion and the gate of the work it read; a session's shows its work's gate.
                var named = await service.ReadOpinionAsync(ask.Id, ct).ConfigureAwait(false);
                pressed = await presses.GateAsync(named?.Working ?? ask.Id, ct).ConfigureAwait(false);
                if (pressed.Gate is not { } gate)
                {
                    output.WriteLine($"opinion: {pressed.Message}");
                    return 1;
                }

                var opinion = named ?? gate.Opinion.Opinion;
                foreach (var line in Show(gate.Opinion, opinion)) output.WriteLine(line);
                return 0;
            }

            case "ask":
                pressed = await presses.AskAsync(ask.Id, ask.Reviewer, ask.SameAgent, ask.Words, ask.Failure ? "failure" : "asked", ct).ConfigureAwait(false);
                break;
            case "anyway":
                pressed = await presses.AnywayAsync(ask.Id, ask.Words, ReviewDoors.Terminal, ct).ConfigureAwait(false);
                break;
            default:
                pressed = await presses.MyselfAsync(ask.Id, ask.Words, ReviewDoors.Terminal, ct).ConfigureAwait(false);
                break;
        }

        output.WriteLine($"opinion: {pressed.Message}");
        if (pressed.Done && pressed.Gate is { } after && ask.Verb == "ask") output.WriteLine($"opinion: {after.Opinion.Says}");
        return pressed.Done ? 0 : 1;
    }

    /// <summary>
    /// What <c>opinion show</c> says (§8.5, §9): the gate's sentence, then the opinion it speaks of, its reviewer and how it stands
    /// to the work, what it read and could not, and each finding with the working session's answer beside it. Claims, said as
    /// such: nothing here calls a finding true.
    /// </summary>
    public static IReadOnlyList<string> Show(OpinionGateState gate, OpinionView? opinion)
    {
        var lines = new List<string> { $"opinion: {gate.Says}" };
        if (opinion is null) return lines;

        lines.Add($"  second opinion `{opinion.Id}` ({opinion.Pass}), by {opinion.Who}, {Label(opinion.Label)}, on `{opinion.Repository}` at "
            + $"`{Short(opinion.Tip)}`: {opinion.State}.");
        if (opinion.Read is { Length: > 0 } read) lines.Add($"  it read: {read}");
        if (opinion.Limits is { Length: > 0 } limits) lines.Add($"  it could not tell: {limits}");
        if (opinion.Findings is { Count: 0 }) lines.Add($"  {opinion.Who} raised nothing in what it read.");
        foreach (var finding in opinion.Findings ?? [])
        {
            lines.Add($"  {finding.Number}. {finding.Weight}, {finding.Sure}, at {finding.Where}: {finding.Claim}");
            if (finding.Consequence.Length > 0) lines.Add($"     if so: {finding.Consequence}");
            lines.Add($"     answered: {Answer(opinion.AnswerTo(finding.Number), gate.Answers?.Findings.FirstOrDefault(row => row.Finding == finding.Number))}");
        }

        return lines;
    }

    private static string Answer(OpinionAnswerView? said, OpinionAnswerRead? read) => (said, read) switch
    {
        (_, { Counts: OpinionViews.Fixed, Fix: { } fix }) => $"fixed at `{Short(fix)}`, its commit read from git as one the turn made",
        (_, { Why: { } why }) => OpinionAnswerWhy.Said(why),
        ({ Said: OpinionViews.Rejected, Evidence: { } evidence }, _) => $"rejected: {evidence}",
        ({ Said: OpinionViews.Unresolved, Why: { } why }, _) => $"unresolved: {why}",
        ({ } any, _) => any.Said,
        _ => "not yet",
    };

    private static string Label(string label) => label switch
    {
        ReviewerLabels.AnotherMaker => "another maker's agent",
        ReviewerLabels.SameAgent => "the same agent, fresh: not an independent reading",
        _ => "maker not declared",
    };

    private static string? Joined(IReadOnlyList<string> words) => string.Join(" ", words).Trim() is { Length: > 0 } said ? said : null;

    private static bool Id(string word) => word.TrimStart('#').Length > 0 && !word.StartsWith('-');

    private static string Short(string commit) => commit.Length > 8 ? commit[..8] : commit;
}
