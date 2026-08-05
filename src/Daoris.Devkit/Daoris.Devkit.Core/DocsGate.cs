namespace Daoris.Devkit;

/// <summary>
/// Documents that are supposed to describe code, checked against the code they describe.
/// </summary>
/// <remarks>
/// <para>Freshness is judged by the LAST COMMIT that touched each side, not by file modification times.
/// A checkout sets every mtime to the moment of the clone, so an mtime comparison reports every document
/// as current on a fresh clone and as stale after a rebase — noise in both directions, which is how a
/// gate teaches people to ignore it.</para>
///
/// <para>It reports rather than blocks by default in one specific case: a document that has never been
/// committed alongside anything it describes is more likely to be newly declared than stale. The first
/// run of a new declaration should not fail the build.</para>
/// </remarks>
public sealed class DocsGate(IGitHistory history) : IGate
{
    public string Name => "docs";

    public GateResult Run(GateContext context)
    {
        var tracked = context.Declaration.Docs.Tracked ?? [];
        var grace = context.Declaration.Docs.GraceDays;
        if (tracked.Count == 0)
        {
            return GateResult.Skip(Name, "no 'docs.tracked' declared — nothing claims to describe anything");
        }

        var problems = new List<string>();
        foreach (var entry in tracked)
        {
            if (!File.Exists(context.Path(entry.Document)))
            {
                problems.Add($"{entry.Document}: declared as a tracked document but does not exist");
                continue;
            }

            var documentAt = history.LastCommitDate(entry.Document);
            if (documentAt is null)
            {
                // Never committed: it is new, and a new document cannot be stale.
                continue;
            }

            foreach (var described in entry.Describes)
            {
                var codeAt = history.LastCommitDate(described);
                if (codeAt is null) continue;

                // Compared by DAY, not by instant. Within one working session a document and the code
                // it describes are edited in whatever order the work happened, often in separate
                // commits minutes apart — and a gate that fires on that ordering fires on every normal
                // session. It would be technically correct and completely useless, which is how a gate
                // teaches people to pass `--no-verify`.
                var behind = (codeAt.Value.Date - documentAt.Value.Date).TotalDays;
                if (behind <= grace) continue;

                problems.Add(
                    $"{entry.Document} last changed {documentAt:yyyy-MM-dd}, but {described} changed "
                    + $"{codeAt:yyyy-MM-dd} ({behind:0} days later) — the document is supposed to describe it");
            }
        }

        return problems.Count == 0
            ? GateResult.Pass(Name, $"{tracked.Count} document(s) keeping up")
            : GateResult.Fail(Name, string.Join('\n', problems));
    }
}

/// <summary>When a path last changed, according to history rather than the filesystem.</summary>
public interface IGitHistory
{
    /// <summary>The author date of the last commit touching this path, or null if it has never been committed.</summary>
    DateTimeOffset? LastCommitDate(string relativePath);
}

/// <summary>git log, through the command line.</summary>
public sealed class CommandLineGitHistory(string repositoryRoot) : IGitHistory
{
    public DateTimeOffset? LastCommitDate(string relativePath)
    {
        var result = Process.Run(
            "git", ["log", "-1", "--format=%aI", "--", relativePath], repositoryRoot);

        if (result.ExitCode != 0) return null;
        var text = result.Output.Trim();
        return DateTimeOffset.TryParse(text, out var when) ? when : null;
    }
}
