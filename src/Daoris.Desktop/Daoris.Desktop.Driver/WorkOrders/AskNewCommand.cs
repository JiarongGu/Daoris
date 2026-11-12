namespace Daoris.Driver;

/// <summary>A new ask as <c>daoris ask "…"</c> makes it: the workspace it is made at, its words, and what it carries.</summary>
/// <param name="To">The receiver it is published to at once as a quest; null, and its intake answers it.</param>
/// <param name="Files">Files on this machine by their full paths, read when the ask is made.</param>
public sealed record AskNew(
    string Workspace, string Sentence, string? To = null, IReadOnlyList<string>? Links = null, IReadOnlyList<string>? Files = null,
    string? Review = null, string? ReviewWords = null, string? Kind = null, string? Workflow = null, string? WorkflowWords = null, string? Short = null,
    IReadOnlyList<Daoris.Contracts.Quests.QuestRequirementWire>? Requirements = null, string? Through = null)
{
    public AskCarried Carried => new(Requirements ?? [], string.IsNullOrWhiteSpace(Through) ? null : Through.Trim());
}

/// <summary>
/// <c>daoris ask [--workspace &lt;name&gt;] … "…"</c>: an ask made at a workspace, through the service's ask door as the person's. The
/// person's press of the running Daoris (<c>ASK_NEW</c>), confirmed in its window, or Ask Daoris's delegate press, which runs as its
/// access says; alone where none runs, the direct post. The page's composer is the other door.
/// </summary>
/// <remarks>Exit codes: 0 asked, 1 refused (an environment or a kind not declared, a service refusal), 2 the usage.</remarks>
public static class AskNewCommand
{
    public static Task<int> PressAsync(
        AskNew ask, AskReviewWorld reviews, AskWorkflowWorld workflows, TextWriter output, InstanceWorld? instance, CancellationToken ct = default) =>
        InstanceDoor.ThroughAsync(instance, PersonPresses.AskNew, InstanceDoor.Said(
                ("workspace", ask.Workspace), ("sentence", ask.Sentence), ("to", ask.To), ("links", Some(ask.Links)), ("files", Some(ask.Files)),
                ("review", ask.Review), ("reviewWords", ask.ReviewWords), ("kind", ask.Kind), ("workflow", ask.Workflow),
                ("workflowWords", ask.WorkflowWords), ("short", ask.Short),
                ("requirements", ask.Requirements is { Count: > 0 } required ? required : null), ("through", ask.Carried.Through)),
            () => RunAsync(ask, reviews, workflows, output, ct), output, ct);

    /// <summary>Make the ask and print the service's sentence, with the ask and the quest it names.</summary>
    public static async Task<int> RunAsync(AskNew ask, AskReviewWorld reviews, AskWorkflowWorld workflows, TextWriter output, CancellationToken ct = default)
    {
        var registry = (await reviews.Service.RegistryAsync(ct).ConfigureAwait(false)).Select(row => (row.Repository, (string?)row.Workspace));
        if (Unfit(ask, reviews.Config(), workflows.Home, registry, out var usage) is { } unfit)
        {
            output.WriteLine(unfit);
            return usage ? 2 : 1;
        }

        // Read here, on the machine that has them, and sent whole to the service, which keeps them under this machine's home.
        var read = new List<(string Name, byte[] Content)>();
        foreach (var path in ask.Files ?? [])
        {
            if (!File.Exists(path))
            {
                output.WriteLine($"`{path}` is not a file on this machine. Nothing was asked.");
                return 1;
            }

            read.Add((Path.GetFileName(path), await File.ReadAllBytesAsync(path, ct).ConfigureAwait(false)));
        }

        var review = AskReviewCommand.Compose(ask.Review, ask.ReviewWords, out _)!;
        var workflow = AskWorkflowCommand.Compose(ask.Kind, ask.Workflow, ask.WorkflowWords, out _)!;
        var answer = await reviews.Service.AskAsync(ask.Workspace, ask.Sentence.Trim(), ask.Links ?? [], read, ask.To, ct,
            review: review.Choice, reviewWords: review.Words, workflow: workflow, shortTitle: ask.Short, carried: ask.Carried).ConfigureAwait(false);
        output.WriteLine(answer.Message);
        if (answer.AskId is not null) output.WriteLine($"  ask    #{answer.AskId}");
        if (answer.QuestId is not null) output.WriteLine($"  quest  #{answer.QuestId}");
        return answer.Ok ? 0 : 1;
    }

    /// <summary>
    /// Why the ask cannot be made as it stands, or null: its words, the review choice and the kind and workflow by their shape
    /// (<paramref name="usage"/>), then a named environment and a kind its workspace does not declare, and a workflow that does not read.
    /// </summary>
    public static string? Unfit(
        AskNew ask, DriverConfig config, string home, IEnumerable<(string Repository, string? Workspace)> registry, out bool usage)
    {
        usage = true;
        if (ask.Sentence.Trim().Length == 0) return "an ask needs its words.";
        if (AskReviewCommand.Compose(ask.Review, ask.ReviewWords, out var reviewProblem) is not { } review) return reviewProblem;
        if (AskWorkflowCommand.Compose(ask.Kind, ask.Workflow, ask.WorkflowWords, out var workflowProblem) is not { } workflow) return workflowProblem;
        usage = false;
        if (review.Choice is { } choice && AskReviewCommand.Undeclared(choice, ask.Workspace, config, registry) is { } undeclared) return undeclared;
        return AskWorkflowCommand.Unchoosable(workflow.Kind, workflow.Workflow, ask.Workspace, config, home);
    }

    private static IReadOnlyList<string>? Some(IReadOnlyList<string>? values) => values is { Count: > 0 } ? values : null;
}
