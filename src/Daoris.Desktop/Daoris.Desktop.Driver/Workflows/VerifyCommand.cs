namespace Daoris.Driver;

/// <summary>What <c>daoris-driver verify</c> was asked: a repository, and a branch or a commit of it, or neither for its checkout's HEAD.</summary>
public sealed record VerifyAsk(string Repository)
{
    public string? Branch { get; init; }

    public string? Commit { get; init; }
}

/// <summary>
/// <c>daoris-driver verify &lt;repository&gt; [--branch &lt;name&gt; | --commit &lt;sha&gt;]</c> (FABRIC-PIPELINE1d; the pipeline design §4.3): a person's
/// verification of a branch or a commit, a run of its own under the repository's landed route and the gates the person accepted, bound under
/// the home's <c>workflows/lines/</c> for the running driver to verify. The line's batching never counts it.
/// </summary>
public static class VerifyCommand
{
    public const string Usage = "usage: daoris-driver verify <repository> [--branch <name> | --commit <sha>]";

    /// <summary>The words after <c>verify</c>, or null with what is wrong with them.</summary>
    public static VerifyAsk? Read(IReadOnlyList<string> args, out string? problem)
    {
        problem = null;
        if (args.Count == 0 || args[0].StartsWith('-'))
        {
            problem = "`verify` takes the repository to verify first.";
            return null;
        }

        string? branch = null, commit = null;
        for (var at = 1; at < args.Count; at++)
        {
            if (args[at] is not ("--branch" or "--commit") || at + 1 >= args.Count || args[at + 1].StartsWith('-'))
            {
                problem = $"`{args[at]}` is not a word `verify` takes: it takes `--branch <name>` or `--commit <sha>`.";
                return null;
            }

            if (args[at] == "--branch") branch = args[++at];
            else commit = args[++at];
        }

        if (branch is not null && commit is not null)
        {
            problem = "`verify` verifies a branch or a commit, not both.";
            return null;
        }

        return new VerifyAsk(args[0].Trim()) { Branch = branch, Commit = commit };
    }

    /// <summary>Bind the person's run in <paramref name="checkout"/>, saying on <paramref name="output"/> what was bound and where its verdict shows.</summary>
    /// <returns>0 bound · 1 refused: no verification pipeline, no gates accepted, no version that reads · 2 the commit could not be read.</returns>
    public static async Task<int> BindAsync(
        string home, string checkout, VerifyAsk ask, WorkflowLandedSelected route, string? workspace, TextWriter output, CancellationToken ct)
    {
        if (route.Workflow == WorkflowSelection.Current)
        {
            output.WriteLine($"verify: `{ask.Repository}`'s landed route names no verification pipeline: choose one on its Workflow tab first.");
            return 1;
        }

        if (route.Gates is not { } accepted)
        {
            output.WriteLine($"verify: the gates `{ask.Repository}`'s verification runs are not accepted yet: read them and press Accept the gates on its Workflow tab.");
            return 1;
        }

        var version = new WorkflowFiles(home).Load(route.Workflow)?.Read.Versions.LastOrDefault(each => each.Problem is null && each.Graph is not null && each.Digest is not null);
        if (version is null)
        {
            output.WriteLine($"verify: `{route.Workflow}` has no version here that reads, so nothing can be verified under it.");
            return 1;
        }

        var named = ask.Commit ?? (ask.Branch is { } branch ? $"refs/heads/{branch}" : "HEAD");
        var (code, sha, problem) = await WorkingTree.GitAsync(checkout, ["rev-parse", "--verify", "--quiet", named + "^{commit}"], ct).ConfigureAwait(false);
        var commit = sha.Trim();
        if (code != 0 || !WorkingTree.IsCommitId(commit))
        {
            output.WriteLine($"verify: `{ask.Commit ?? ask.Branch ?? "HEAD"}` names no commit in {checkout}{(problem.Trim() is { Length: > 0 } said ? $": {said}" : "")}.");
            return 2;
        }

        var run = new VerifyRun(VerifyRuns.NewId(), ask.Repository, workspace ?? "", checkout, route.Workflow, version.Version, version.Digest!, commit, VerifyRuns.Person, DateTimeOffset.UtcNow)
        {
            Branch = ask.Branch, Accepted = accepted,
        };
        VerifyRuns.Bind(home, run);
        output.WriteLine(
            $"verify: `{ask.Repository}` at {TextBounds.ShortCommit(commit, 7)}{(ask.Branch is { } on ? $" ({on})" : "")}: run {run.Run}, under `{route.Workflow}` v{version.Version}. "
            + "The running driver verifies it after any verification ahead of it, beside the sessions; its verdict shows on the run's Workflow view.");
        return 0;
    }
}
