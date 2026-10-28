using System.Globalization;
using static Daoris.Driver.TreeGit;

namespace Daoris.Driver;

/// <summary>
/// What the landing gate asks of the workflows: the gate's assembly, which reads the run's process, judges its workflow part, reads the
/// review and the second opinion over it and judges the graph's part, and the process a quest's work follows. Workflows keeps each and
/// implements this, installed when the driver loads (<c>Workflows/WorkplaceWorkflowsWiring.cs</c>); each does exactly what
/// <see cref="TreeGates"/> did before it was a port, in the same order, reading the workplace's own parts back through
/// <see cref="TreeGates"/> (<c>ChangedAsync</c>, its context) and the review's second opinion through its <c>OpinionAsync</c>.
/// </summary>
internal interface IWorkplaceGateWorkflows
{
    /// <summary>
    /// The whole landing gate for the tree (see <see cref="TreeGates.GateAsync"/>). The world is the review's <c>IOpinionWorld</c>, which
    /// every world the look and the doors hand the gate is: the second opinion reads its opinions through it.
    /// </summary>
    Task<IWorkplaceGate> GateAsync(
        TreeGates gates, string path, string? quest, IReviewWorld world, string? session, CancellationToken ct, IWorkplaceProcess? process,
        string? tip, string? run);

    /// <summary>The process a quest's work follows (see <see cref="TreeGates.ProcessAsync"/>).</summary>
    Task<IWorkplaceProcess> ProcessAsync(
        TreeGates gates, DriverConfig config, string repository, string? workspace, string? quest, IReviewWorld world, CancellationToken ct, string? run);
}

internal static class WorkplaceGateWorkflows
{
    private static IWorkplaceGateWorkflows? _current;

    internal static IWorkplaceGateWorkflows Current
    {
        get => _current ?? throw new InvalidOperationException("no workplace gate operations are installed: Workflows installs them when the driver loads");
        set => _current = value;
    }
}

/// <summary>
/// The landing gate for a tree (XAGENT1f; PART10k, out of <c>SessionTrees.Opinion.cs</c>): the run's process, and the paths a chain
/// changed and the fork point they read from, over the context alone; the workflows assemble the gate over them and the review reads
/// the second opinion's part (<c>TreeGateOpinions</c>). <see cref="SessionTrees"/> keeps the public calls, one line each.
/// </summary>
internal sealed class TreeGates(TreeContext context)
{
    /// <summary>The context the gate reads, which the workflows' assembly of the gate reads the tree's owner, home and branches by.</summary>
    internal TreeContext Context => context;

    /// <summary>
    /// The whole landing gate for this tree (XAGENT1f, D155 point 9; the second-agent design §7): D154's look as
    /// the review's <c>ReviewAsync</c> reads it, and the second opinion before it. Every door that lands reads it here, once, and hands it
    /// to <see cref="SessionTrees.LandAsync"/>, which refuses the first part that holds.
    /// </summary>
    /// <param name="session">The working session the door lands, which the terminal's doors the gate names say.</param>
    /// <param name="process">
    /// The run's process, where the door read it already (WORKFLOW1f: the look's automatic landing reads it for the switch first);
    /// otherwise it is read here, once, for every part.
    /// </param>
    /// <param name="tip">
    /// The commit to judge the work at (FABRIC-CLOSE1h16, D173's CLOSELAND1 note): where a landing's push is judged, the one its branch
    /// was judged and made at (<see cref="LandingGate.Tip"/>), so a commit made on the tree after it, which the push does not carry, holds
    /// nothing back. Null judges the tree's <c>HEAD</c>, as every door before the landing does.
    /// </param>
    /// <param name="run">A chat run's key where the work serves no quest (CHATFLOW1c), whose bound process is read in place of the rules live.</param>
    internal Task<IWorkplaceGate> GateAsync(
        string path, string? quest, IReviewWorld world, string? session = null, CancellationToken ct = default, IWorkplaceProcess? process = null,
        string? tip = null, string? run = null) =>
        WorkplaceGateWorkflows.Current.GateAsync(this, path, quest, world, session, ct, process, tip, run);

    /// <summary>
    /// The process a quest's work here follows (WORKFLOW1f, <see cref="WorkflowProcesses.ReadAsync"/>): its run's bound version, or the
    /// rules live. A conversation's work, which serves no quest, reads its chat run's (CHATFLOW1c, the chat-flow design §4.2), so the view
    /// and the gate decide by one process; one with no chat run named reads the rules live.
    /// </summary>
    /// <param name="run">The chat run's key, <c>chat-&lt;first&gt;</c>, where the work serves no quest; null for none.</param>
    internal Task<IWorkplaceProcess> ProcessAsync(
        DriverConfig config, string repository, string? workspace, string? quest, IReviewWorld world, CancellationToken ct = default,
        string? run = null) =>
        WorkplaceGateWorkflows.Current.ProcessAsync(this, config, repository, workspace, quest, world, ct, run);

    /// <summary>
    /// The paths the work changed, from where it leaves its line to its tree's tip (§4.4), or to <paramref name="tip"/> where a push is
    /// judged (FABRIC-CLOSE1h16); null where git cannot say.
    /// </summary>
    internal async Task<IReadOnlyList<string>?> ChangedAsync(string tree, CancellationToken ct, string? tip = null)
    {
        var at = tip ?? "HEAD";
        if (await ForkPointAsync(tree, ct, at).ConfigureAwait(false) is not { } fork) return null;
        var (code, names, _) = await WorkingTree.GitAsync(tree, ["--no-optional-locks", "diff", "--name-only", "--no-renames", "-z", fork, at], ct).ConfigureAwait(false);
        return code == 0 ? [.. names.Split('\0', StringSplitOptions.RemoveEmptyEntries)] : null;
    }

    /// <summary>The name a chain's gate is kept under: its first quest's id, or the quest's own where no chain was read.</summary>
    internal static string OpinionChain(IReadOnlyList<QuestView> chain, string quest) => chain.Count > 0 ? chain[0].Id : quest.TrimStart('#');

    /// <summary>
    /// How many commits <c>HEAD</c>, or <paramref name="tip"/> where a push is judged (FABRIC-CLOSE1h16), holds that <paramref name="from"/>
    /// does not; null where git cannot say.
    /// </summary>
    internal static async Task<int?> SinceAsync(string tree, string from, CancellationToken ct, string? tip = null)
    {
        if (!EvidenceCodes.IsObjectId(from) || !Directory.Exists(tree)) return null;
        var (code, count, _) = await WorkingTree.GitAsync(tree, ["rev-list", "--count", $"{from}..{tip ?? "HEAD"}"], ct).ConfigureAwait(false);
        return code == 0 && int.TryParse(count.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var since) ? since : null;
    }

    /// <summary>
    /// Where a tree's work leaves its repository's line (XAGENT1f, design §4): the commit a landing's candidate starts from, so one
    /// opinion reads the chain's whole work there. Null where git cannot name the line or where they meet.
    /// </summary>
    /// <param name="at">The commit whose work is read, the tree's <c>HEAD</c> unless a push is judged at its branch's (FABRIC-CLOSE1h16).</param>
    internal async Task<string?> ForkPointAsync(string tree, CancellationToken ct = default, string at = "HEAD")
    {
        var full = Path.GetFullPath(tree);
        if (!Directory.Exists(full)) return null;
        var (code, commonDir, _) = await WorkingTree.GitAsync(full, ["rev-parse", "--path-format=absolute", "--git-common-dir"], ct).ConfigureAwait(false);
        if (code != 0) return null;
        var root = Path.GetDirectoryName(commonDir.Trim())!;
        var (workspace, repository) = context.OwnerOf(full);
        var line = (await context.LineAsync(root, repository, workspace, ct).ConfigureAwait(false)).Branch;
        var against = line is null ? null : await ComparableAsync(root, line, ct).ConfigureAwait(false);
        if (against is null) return null;
        var (forkCode, fork, _) = await WorkingTree.GitAsync(full, ["merge-base", against, at], ct).ConfigureAwait(false);
        return forkCode == 0 && fork.Trim() is { Length: > 0 } found && EvidenceCodes.IsObjectId(found) ? found : null;
    }

    /// <summary>The repository's own checkout a tree belongs to (its git common folder's parent); null where git cannot say.</summary>
    internal static async Task<string?> CheckoutOfTreeAsync(string tree, CancellationToken ct = default)
    {
        if (!Directory.Exists(tree)) return null;
        var (code, commonDir, _) = await WorkingTree.GitAsync(tree, ["rev-parse", "--path-format=absolute", "--git-common-dir"], ct).ConfigureAwait(false);
        return code == 0 && commonDir.Trim() is { Length: > 0 } common ? Path.GetDirectoryName(common) : null;
    }
}
