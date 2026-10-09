namespace Daoris.Driver;

/// <summary>
/// The kind and workflow a new ask carries from a terminal (WORKFLOW1e): each null where none is chosen, and the person's words.
/// </summary>
public sealed record AskWorkflowComposed(string? Kind, string? Workflow, string? Words);

/// <summary>What <c>daoris-driver ask --set-workflow</c> was asked: the ask, the kind and workflow (neither clears), and the words.</summary>
public sealed record AskWorkflowAsk(string Ask, string? Kind, string? Workflow, string? Words);

/// <summary>
/// What the ask's workflow doors reach: the service, this machine's driver config (a kind is one the ask's workspace declares) and
/// its home (a workflow is one saved there whose newest version reads).
/// </summary>
public sealed record AskWorkflowWorld(ServiceClient Service, Func<DriverConfig> Config, string Home);

/// <summary>
/// The ask's kind and workflow at a terminal (WORKFLOW1e, D157 point 10; the workflow design §4.1 row 1, §4.3, D50):
/// <c>daoris-driver ask … --kind &lt;kind&gt; [--workflow &lt;id&gt;|current] [--workflow-words "…"]</c> with a new ask, and
/// <c>daoris-driver ask --set-workflow &lt;id&gt; [--kind &lt;kind&gt;] [--workflow &lt;id&gt;|current]|--clear ["…"]</c> on one.
/// The latest stands, and reaches every chain the ask publishes whose work has not started.
/// </summary>
/// <remarks>
/// <para><b>A person's door</b> (D156 §3): the service keeps the choice behind the person key, so a keyless call on a keyed host is
/// refused in the service's words, which this prints whole. An agent proposes a kind (the intake, WORKFLOW1i), never sets one.</para>
///
/// <para><b>A kind is one the ask's workspace declares, and a workflow one saved here whose newest version reads</b>, as the
/// composer will offer only those: the service holds no workflow and no kind, so this door reads them. <c>current</c> needs none.</para>
///
/// <para><b>Exit codes</b>: 0 kept, 1 refused (a kind not declared, a workflow not here, a service refusal), 2 the usage.</para>
/// </remarks>
public static class AskWorkflowCommand
{
    /// <summary>
    /// A new ask's <c>--kind</c>, <c>--workflow</c> and <c>--workflow-words</c>, read as the composer sends them, or null with why not:
    /// none chosen sends none; words go only with a choice.
    /// </summary>
    public static AskWorkflowComposed? Compose(string? kind, string? workflow, string? words, out string? problem)
    {
        problem = null;
        var said = string.IsNullOrWhiteSpace(words) ? null : words.Trim();
        if (kind is null && workflow is null)
        {
            if (said is null) return new AskWorkflowComposed(null, null, null);
            problem = "--workflow-words go with a kind or a workflow: --kind <kind>, --workflow <id>|current.";
            return null;
        }

        problem = Unfit(kind, workflow);
        return problem is null ? new AskWorkflowComposed(kind, workflow, said) : null;
    }

    /// <summary>
    /// An ask's <c>--set-workflow &lt;id&gt;</c> read, with its <c>--kind</c>, <c>--workflow</c> or <c>--clear</c> and the words after,
    /// or null with why not.
    /// </summary>
    public static AskWorkflowAsk? Read(string id, string? kind, string? workflow, bool clear, IReadOnlyList<string> words, out string? problem)
    {
        problem = null;
        if (id.TrimStart('#').Length == 0 || id.StartsWith('-') || clear == (kind is not null || workflow is not null))
        {
            problem = "--set-workflow takes one ask's id, then --kind <kind>, --workflow <id>|current, both, or --clear, then your words.";
            return null;
        }

        if (Unfit(kind, workflow) is { } unfit)
        {
            problem = unfit;
            return null;
        }

        var said = string.Join(" ", words).Trim();
        return new AskWorkflowAsk(id.TrimStart('#'), kind, workflow, said.Length == 0 ? null : said);
    }

    /// <summary>
    /// Why a kind or a workflow cannot be chosen for an ask in <paramref name="workspace"/>, or null where both can (design §4.2,
    /// §3.9): a kind its workspace declares, and a workflow saved here whose newest version reads, or <c>current</c>.
    /// </summary>
    public static string? Unchoosable(string? kind, string? workflow, string workspace, AskWorkflowWorld world)
    {
        if (kind is not null)
        {
            var named = RemoteTarget.Workspace(workspace);
            world.Config().WorkspaceWorkflows.TryGetValue(named, out var shared);
            if (shared?.KindOf(kind) is null)
            {
                var spelled = ShellWord.Of(named, "<workspace>");
                return $"{WorkflowSelection.UndeclaredInWorkspace(named, kind)} `daoris driver workflow kind {spelled} {kind} "
                       + "--label \"…\"` declares one. Nothing was kept.";
            }
        }

        if (workflow is null or WorkflowSelection.Current) return null;
        if (WorkflowStore.Load(world.Home, workflow) is not { } found)
        {
            return $"No workflow `{workflow}` is saved here: `daoris driver workflow list` names them. Nothing was kept.";
        }

        if (found.Read.Problem is { } unread) return $"`{workflow}` cannot be read: {unread} Nothing was kept.";
        var newest = found.Read.Versions[^1];
        return newest.Steps is null
            ? $"`{workflow}` v{newest.Version} cannot be read here: {newest.Problem} Nothing was kept."
            : null;
    }

    /// <summary>Set the choice on the ask, as its page's will, and print the service's sentence.</summary>
    public static async Task<int> RunAsync(AskWorkflowAsk ask, AskWorkflowWorld world, TextWriter output, CancellationToken ct = default)
    {
        // Judged in the ask's workspace; an ask the service does not hold is its to refuse.
        if ((ask.Kind ?? ask.Workflow) is not null && await world.Service.FindAskAsync(ask.Ask, ct).ConfigureAwait(false) is { } held
            && Unchoosable(ask.Kind, ask.Workflow, held.Workspace, world) is { } unchoosable)
        {
            output.WriteLine(unchoosable);
            return 1;
        }

        var answer = await world.Service.ChooseAskWorkflowAsync(ask.Ask, ask.Kind, ask.Workflow, ask.Words, ct).ConfigureAwait(false);
        output.WriteLine(answer.Message);
        return answer.Ok ? 0 : 1;
    }

    /// <summary>Why a kind or a workflow is not one by its shape, or null.</summary>
    private static string? Unfit(string? kind, string? workflow) =>
        kind is not null && !WorkflowSelection.IsKindId(kind) ? WorkflowSelection.KindIdProblem(kind)
        : workflow is not null && !WorkflowSelection.IsWorkflow(workflow) ? WorkflowSelection.WorkflowProblem(workflow)
        : null;
}
