namespace Daoris.Driver;

/// <summary>What <c>daoris-driver quest workflow</c> was asked: the quest, the kind and workflow (neither clears), and the words.</summary>
public sealed record QuestWorkflowAsk(string Quest, string? Kind, string? Workflow, string? Words);

/// <summary>
/// A quest's own kind and workflow at a terminal (WORKFLOW1e2, D157; the workflow design §4.1 row 1, §4.3, D50):
/// <c>daoris-driver quest workflow &lt;id&gt; [--kind &lt;kind&gt;] [--workflow &lt;id&gt;|current]|--clear ["…"]</c>, for a quest
/// published with no ask, before it starts. The latest stands, and its run binds it at its first start (§4.5).
/// </summary>
/// <remarks>
/// <para><b>A person's door</b> (D156 §3), as an ask's is (<see cref="AskWorkflowCommand"/>): the service keeps the choice behind the
/// person key, so a keyless call on a keyed host is refused in the service's words, which this prints whole. A quest an ask
/// published follows its ask's choice, set by <c>ask --set-workflow</c>; the service refuses it here, naming the ask.</para>
///
/// <para><b>A kind is one the quest's workspace declares, and a workflow one saved here whose newest version reads</b>, judged as the
/// ask's door judges them (<see cref="AskWorkflowCommand.Unchoosable"/>).</para>
///
/// <para><b>Exit codes</b>: 0 kept, 1 refused (a kind not declared, a workflow not here, a service refusal), 2 the usage.</para>
/// </remarks>
public static class QuestWorkflowCommand
{
    public const string Usage =
        "usage: daoris-driver quest workflow <id> [--kind <kind>] [--workflow <id>|current]|--clear [\"…\"]\n"
        + "       say what kind of task a quest no ask published is and which workflow its work follows, before it starts: a kind\n"
        + "       its workspace declares, a workflow saved here or current; the latest stands, and its run binds it at its first\n"
        + "       start. A quest an ask published follows its ask's: `ask --set-workflow` sets that.";

    /// <summary>Whether a <c>quest</c> line asks for a quest's kind and workflow.</summary>
    public static bool Asks(IReadOnlyList<string> args) => args is ["workflow", ..];

    /// <summary>
    /// The line read, or null with why not: one quest's id, then <c>--kind</c>, <c>--workflow</c>, both, or <c>--clear</c>, then the
    /// person's words, joined as said.
    /// </summary>
    public static QuestWorkflowAsk? Read(IReadOnlyList<string> args, out string? problem)
    {
        problem = null;
        string? kind = null, workflow = null;
        var clear = false;
        var words = new List<string>();
        if (args is not ["workflow", var id, ..] || id.TrimStart('#').Length == 0 || id.StartsWith('-'))
        {
            problem = "quest workflow takes one quest's id, then --kind <kind>, --workflow <id>|current, both, or --clear, then your words.";
            return null;
        }

        var rest = args.Skip(2).ToList();
        for (var i = 0; i < rest.Count; i++)
        {
            switch (rest[i])
            {
                case "--kind" when kind is null && i + 1 < rest.Count && !rest[i + 1].StartsWith("--", StringComparison.Ordinal):
                    kind = rest[++i];
                    break;
                case "--workflow" when workflow is null && i + 1 < rest.Count && !rest[i + 1].StartsWith("--", StringComparison.Ordinal):
                    workflow = rest[++i];
                    break;
                case "--clear" when !clear:
                    clear = true;
                    break;
                case var flag when flag.StartsWith("--", StringComparison.Ordinal):
                    problem = $"quest workflow does not take `{flag}` here: --kind <kind>, --workflow <id>|current, or --clear.";
                    return null;
                default:
                    words.Add(rest[i]);
                    break;
            }
        }

        if (clear == (kind is not null || workflow is not null))
        {
            problem = "quest workflow takes --kind <kind>, --workflow <id>|current, both, or --clear, never neither and never --clear with one.";
            return null;
        }

        if (kind is not null && !WorkflowSelection.IsKindId(kind))
        {
            problem = WorkflowSelection.KindIdProblem(kind);
            return null;
        }

        if (workflow is not null && !WorkflowSelection.IsWorkflow(workflow))
        {
            problem = WorkflowSelection.WorkflowProblem(workflow);
            return null;
        }

        var said = string.Join(" ", words).Trim();
        return new QuestWorkflowAsk(id.TrimStart('#'), kind, workflow, said.Length == 0 ? null : said);
    }

    /// <summary>Set the choice on the quest, as its page's will, and print the service's sentence.</summary>
    public static async Task<int> RunAsync(QuestWorkflowAsk ask, AskWorkflowWorld world, TextWriter output, CancellationToken ct = default)
    {
        // Judged in the quest's workspace; a quest the service does not hold is its to refuse.
        if ((ask.Kind ?? ask.Workflow) is not null && await world.Service.FindQuestAsync(ask.Quest, ct).ConfigureAwait(false) is { } held
            && AskWorkflowCommand.Unchoosable(ask.Kind, ask.Workflow, held.Workspace, world) is { } unchoosable)
        {
            output.WriteLine(unchoosable);
            return 1;
        }

        var answer = await world.Service.ChooseQuestWorkflowAsync(ask.Quest, ask.Kind, ask.Workflow, ask.Words, ct).ConfigureAwait(false);
        output.WriteLine(answer.Message);
        return answer.Ok ? 0 : 1;
    }
}
