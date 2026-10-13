using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>What <c>daoris-driver git branches</c> was asked: which repositories, and in which form.</summary>
/// <param name="Repository">The one repository named, which is listed whatever it holds (D112); null for the scope's.</param>
/// <param name="All">Every repository with a checkout here, not only those holding Daoris's branches.</param>
/// <param name="Json">The list whole, written, rather than said.</param>
public sealed record GitBranchesAsk
{
    public string? Repository { get; init; }

    public bool All { get; init; }

    public bool Json { get; init; }
}

/// <summary>Where the terminal's list reads: the registry's rows (each repository's checkout here), the person's choices, the home, and the session records that name session branches.</summary>
public sealed record GitBranchesSources(IReadOnlyList<RepoView> Registry, DriverConfig Config, string Home)
{
    /// <summary>The session records, closed ones included; null where they could not be read.</summary>
    public IReadOnlyList<SessionRecord>? Sessions { get; init; }

    /// <summary>Why the session records could not be read, or null.</summary>
    public string? SessionsUnread { get; init; }
}

/// <summary>
/// <c>daoris-driver git branches</c> (GIT1c, D147 §3.3, D50): the terminal's read door to the branch list, each repository's
/// line and branches by kind, and the git each read ran, ready to copy, since a read's terminal door is git itself. In the
/// library, so its words are held by a test.
/// </summary>
/// <remarks>
/// <para><b>Reads only.</b> The list is <see cref="GitBranches.ListAsync"/>'s, which writes nothing; the host routes the verb
/// before it opens its machine log, whose open prunes old files.</para>
///
/// <para><b>Which repositories</b> is D112's, as <c>trees sync</c> takes them: those holding a branch of Daoris's, the rest
/// named in one line; <c>--all</c> lists every one, and <c>--repository</c> the one named.</para>
///
/// <para>Exit codes are the family's: 0 listed, each repository git could not read said on its row · 1 the repository named has
/// no checkout here · 2 could not: the usage, or the service did not answer.</para>
/// </remarks>
public static class GitBranchesCommand
{
    public const string Usage =
        """
        usage: daoris-driver git branches [--repository <name>] [--all] [--json]
        """;

    /// <summary>What the words ask, or null with what is wrong with them.</summary>
    public static GitBranchesAsk? Read(IReadOnlyList<string> args, out string? problem)
    {
        problem = null;
        if (args is not ["branches", ..])
        {
            problem = args.Count == 0 ? "`git` reads `branches`: name it." : $"`git` reads `branches`, not `{args[0]}`.";
            return null;
        }

        var ask = new GitBranchesAsk();
        for (var at = 1; at < args.Count; at++)
        {
            switch (args[at])
            {
                case "--all":
                    ask = ask with { All = true };
                    break;
                case "--json":
                    ask = ask with { Json = true };
                    break;
                case "--repository" when at + 1 < args.Count && args[at + 1].Trim().Length > 0 && !args[at + 1].StartsWith('-'):
                    ask = ask with { Repository = args[++at].Trim() };
                    break;
                case "--repository":
                    problem = "`--repository` takes a repository's name.";
                    return null;
                default:
                    problem = $"`{args[at]}` is not a word `git branches` takes.";
                    return null;
            }
        }

        return ask;
    }

    /// <summary>The repositories the words take (D112): every one with <c>--all</c>, the one named, else those holding Daoris's branches.</summary>
    public static SyncScope Scope(GitBranchesAsk ask) =>
        ask.All ? SyncScope.Everything : ask.Repository is { } named ? SyncScope.Named([named]) : SyncScope.Held;

    /// <summary>Read the list from each checkout here and say it, or write it whole.</summary>
    public static async Task<int> RunAsync(GitBranchesAsk ask, GitBranchesSources sources, TextWriter output, CancellationToken ct = default)
    {
        var rows = sources.Registry
            .Where(row => !string.IsNullOrWhiteSpace(row.Root))
            .Where(row => ask.Repository is null || string.Equals(row.Repository, ask.Repository, StringComparison.OrdinalIgnoreCase))
            .OrderBy(row => row.Repository, StringComparer.Ordinal)
            .Select(row => (row.Repository, (string?)row.Workspace, row.Root))
            .ToList();
        if (ask.Repository is { } named && !rows.Any(row => Directory.Exists(row.Root)))
        {
            output.WriteLine($"git: `{named}` has no checkout here, so it has no branches here to list.");
            return 1;
        }

        var list = await GitBranches.ListAsync(rows, sources.Config, sources.Home, sources.Sessions, Scope(ask), ct).ConfigureAwait(false);
        if (ask.Json) output.WriteLine(Json(list, sources.SessionsUnread));
        else output.Write(Say(list, sources.SessionsUnread, DateTimeOffset.UtcNow));
        return 0;
    }

    /// <summary>The list in words: each repository's line, its branches under their kinds, and the git that answered.</summary>
    /// <param name="sessionsUnread">Why the session records could not be read, said once first; null where they were.</param>
    public static string Say(GitBranchList list, string? sessionsUnread, DateTimeOffset now)
    {
        var text = new StringBuilder();
        if (sessionsUnread is not null) text.Append($"git: sessions are not named: {sessionsUnread.TrimEnd('.')}.\n");
        if (list.Repositories.Count == 0)
        {
            text.Append(list.Apart.Count == 0 ? "git: no repository has a checkout here.\n" : "git: no repository with a checkout here holds a branch of Daoris's.\n");
        }

        foreach (var repository in list.Repositories)
        {
            text.Append($"{repository.Repository}  {repository.Workspace}\n");
            text.Append($"  {Head(repository.Line)}; {Fetched(repository.Fetch, now)}\n");
            if (repository.Missing.Contains(GitRefs.WorktreePath))
            {
                text.Append($"  this Git does not say which tree holds a branch (`%({GitRefs.WorktreePath})` came in Git {GitRefs.WorktreePathSince}), so `git worktree list` said it\n");
            }

            if (repository.Missing.Contains(GitRefs.AheadBehind))
            {
                text.Append($"  this Git counts each branch on its own: `%({GitRefs.AheadBehind})` came in Git {GitRefs.AheadBehindSince}\n");
            }

            if (repository.Problem is { } problem) text.Append($"  git could not list its branches: {problem}\n");

            foreach (var (kind, label) in new[] { (GitBranchKind.Session, "sessions' branches"), (GitBranchKind.Landed, "landed"), (GitBranchKind.Yours, "yours") })
            {
                var branches = repository.Branches.Where(branch => branch.Kind == kind).ToList();
                if (branches.Count == 0) continue;
                text.Append($"  {label} ({branches.Count})\n");
                foreach (var branch in branches) text.Append($"    {Row(branch, sessionsUnread is null)}\n");
            }

            var origin = repository.Branches.Count(branch => branch.Kind == GitBranchKind.Origin);
            if (origin > 0) text.Append($"  origin's: {origin} branch{(origin == 1 ? "" : "es")} with no local branch here; `git branch -r` lists them\n");

            if (repository.Commands.Count > 0)
            {
                text.Append("  read by, in its checkout:\n");
                foreach (var command in repository.Commands) text.Append($"    {command}\n");
            }
        }

        if (list.Apart.Count > 0)
        {
            text.Append($"git: not listed, since they hold no branch of Daoris's ({list.Apart.Count}): "
                + string.Join(", ", list.Apart.Select(each => each.Repository))
                + ". `--all` lists them, and `--repository <name>` one of them.\n");
        }

        return text.ToString();
    }

    /// <summary>The line's head: which, what set it, its commit, and how it stands to origin's copy.</summary>
    private static string Head(GitLine line)
    {
        var said = CanonicalLine.Describe(new Line(line.Branch, line.Source));
        if (line.Branch is null) return said;

        var standing = (line.Commit, line.Origin) switch
        {
            (null, null) => "neither this checkout nor origin's copy holds a branch of that name",
            (null, { } origin) => $"this checkout has no branch of that name; origin's copy is at {Short(origin)}, and branches count from it",
            (_, null) => "origin holds no copy of it here",
            _ => (line.Ahead, line.Behind) switch
            {
                (0, 0) => "in step with origin's copy",
                ({ } ahead, 0) => $"{ahead} ahead of origin's copy",
                (0, { } behind) => $"{behind} behind origin's copy",
                ({ } ahead, { } behind) => $"{ahead} ahead of origin's copy and {behind} behind",
                _ => "origin's copy differs, and git could not count how",
            },
        };
        return $"line {said}{(line.Commit is { } commit ? $", at {Short(commit)}" : "")}: {standing}";
    }

    private static string Fetched(GitFetch? fetch, DateTimeOffset now) => fetch switch
    {
        null => "never fetched here",
        { Heard: true } => SyncWords.LastFetched(fetch.At, now),
        _ => $"a fetch was tried {SyncWords.LastFetched(fetch.At, now)["last fetched ".Length..]} and heard nothing",
    };

    /// <summary>One branch: its name, its commit, its distance from the line, and what its kind says of it.</summary>
    private static string Row(GitBranch branch, bool sessionsRead)
    {
        var parts = new List<string> { branch.Name, Short(branch.Commit) };
        if (Distance(branch.Ahead, branch.Behind) is { } distance) parts.Add(distance);

        var details = new List<string>();
        if (branch.Session is { } session)
        {
            if (session.Session is { } id)
            {
                details.Add($"session {id}{(session.State is { Length: > 0 } state ? $" ({state})" : "")}{(session.Quest is { } quest ? $", quest #{quest}" : "")}");
            }
            else if (sessionsRead) details.Add("no session record names its tree");
            if (session.GrewFrom is { } grew) details.Add($"grew from `{grew}`");
            details.Add(branch.Worktree is { } tree ? $"its tree {tree}" : "no tree holds it");
        }
        else if (branch.Landed is { } landed)
        {
            details.Add($"landed for session {landed.Session}{(landed.Quest is { } quest ? $", quest #{quest}" : "")}{(landed.Title is { } title ? $" \"{title}\"" : "")}");
            details.Add(OriginWords(landed));
            if (landed.PullRequest is { } request) details.Add($"pull request {request}");
            // PLUGHOOK1c: what its plugin last answered, with when, as kept; the list never asks.
            if (PullRequestWords.Row(landed.PullRequestState, null, landed.PullRequestAskFailed, null) is { Length: > 0 } kept) details.Add(kept[2..]);
            if (branch.Worktree is { } tree) details.Add($"checked out in {tree}");
        }
        else if (branch.Worktree is { } tree)
        {
            details.Add($"checked out in {tree}");
        }

        if (details.Count > 0) parts.Add(string.Join("; ", details));
        return string.Join("  ", parts);
    }

    private static string OriginWords(GitLandedBranch landed)
    {
        var pushed = !landed.Pushed ? "" : landed.Plugin is { } plugin ? $"pushed by {plugin}, " : "pushed, ";
        return landed.Origin switch
        {
            GitOrigin.InStep => $"{pushed}in step with origin's copy",
            GitOrigin.Ahead => $"{pushed}{landed.OriginAhead} ahead of origin's copy",
            GitOrigin.Behind => $"{pushed}{landed.OriginBehind} behind origin's copy",
            GitOrigin.Diverged => $"{pushed}{landed.OriginAhead} ahead of origin's copy and {landed.OriginBehind} behind",
            GitOrigin.Gone => $"{pushed}and gone from origin since",
            GitOrigin.NotPushed => "not on origin",
            _ => $"{pushed}origin's copy differs, and git could not count how",
        };
    }

    private static string? Distance(int? ahead, int? behind) => (ahead, behind) switch
    {
        (0, 0) => "on the line",
        ({ } more, 0) => $"{more} ahead",
        (0, { } fewer) => $"{fewer} behind",
        ({ } more, { } fewer) => $"{more} ahead, {fewer} behind",
        _ => null,
    };

    private static string Short(string commit) => commit.Length > 8 ? commit[..8] : commit;

    /// <summary>
    /// The list whole, as <c>--json</c> writes it: every repository listed with every field the reader answers, origin's
    /// branches included, the repositories left apart by name, and why the session records were not read.
    /// </summary>
    public static string Json(GitBranchList list, string? sessionsUnread)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteStartArray("repositories");
            foreach (var repository in list.Repositories)
            {
                writer.WriteStartObject();
                writer.WriteString("repository", repository.Repository);
                writer.WriteString("workspace", repository.Workspace);
                writer.WriteStartObject("line");
                writer.WriteString("branch", repository.Line.Branch);
                writer.WriteString("source", repository.Line.Source);
                writer.WriteString("commit", repository.Line.Commit);
                writer.WriteString("origin", repository.Line.Origin);
                Number(writer, "ahead", repository.Line.Ahead);
                Number(writer, "behind", repository.Line.Behind);
                writer.WriteEndObject();
                if (repository.Fetch is { } fetch)
                {
                    writer.WriteStartObject("fetch");
                    writer.WriteString("at", fetch.At.ToString("O", CultureInfo.InvariantCulture));
                    writer.WriteBoolean("heard", fetch.Heard);
                    writer.WriteEndObject();
                }
                else
                {
                    writer.WriteNull("fetch");
                }

                writer.WriteBoolean("holds", repository.Holds);
                Strings(writer, "missing", repository.Missing);
                writer.WriteString("problem", repository.Problem);
                Strings(writer, "commands", repository.Commands);
                writer.WriteStartArray("branches");
                foreach (var branch in repository.Branches) Branch(writer, branch);
                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            Strings(writer, "apart", [.. list.Apart.Select(each => each.Repository)]);
            writer.WriteString("sessionsUnread", sessionsUnread);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray()).Replace("\r\n", "\n");
    }

    private static void Branch(Utf8JsonWriter writer, GitBranch branch)
    {
        writer.WriteStartObject();
        writer.WriteString("name", branch.Name);
        writer.WriteString("kind", branch.Kind);
        writer.WriteString("commit", branch.Commit);
        writer.WriteString("at", branch.At?.ToString("O", CultureInfo.InvariantCulture));
        writer.WriteString("subject", branch.Subject);
        writer.WriteString("worktree", branch.Worktree);
        Number(writer, "ahead", branch.Ahead);
        Number(writer, "behind", branch.Behind);
        if (branch.Session is { } session)
        {
            writer.WriteStartObject("session");
            writer.WriteString("session", session.Session);
            writer.WriteString("state", session.State);
            writer.WriteString("quest", session.Quest);
            writer.WriteString("grewFrom", session.GrewFrom);
            writer.WriteString("from", session.From);
            writer.WriteEndObject();
        }
        else
        {
            writer.WriteNull("session");
        }

        if (branch.Landed is { } landed)
        {
            writer.WriteStartObject("landed");
            writer.WriteString("session", landed.Session);
            writer.WriteString("landedAt", landed.LandedAt.ToString("O", CultureInfo.InvariantCulture));
            writer.WriteString("origin", landed.Origin);
            writer.WriteString("quest", landed.Quest);
            writer.WriteString("title", landed.Title);
            writer.WriteString("plugin", landed.Plugin);
            writer.WriteBoolean("pushed", landed.Pushed);
            writer.WriteString("pullRequest", landed.PullRequest);
            writer.WriteString("pushedTip", landed.PushedTip);
            writer.WriteString("originCommit", landed.OriginCommit);
            Number(writer, "originAhead", landed.OriginAhead);
            Number(writer, "originBehind", landed.OriginBehind);
            // PLUGHOOK1c (D148 point 6): the kept answer whole, as the landing record keeps it, and the failed ask beside it.
            if (landed.PullRequestState is { } kept)
            {
                writer.WriteStartObject("pullRequestState");
                writer.WriteString("state", kept.State);
                writer.WriteString("pullRequest", kept.PullRequest);
                writer.WriteString("mergeCommit", kept.MergeCommit);
                writer.WriteString("sourceCommit", kept.SourceCommit);
                writer.WriteString("target", kept.Target);
                writer.WriteString("how", kept.How);
                writer.WriteString("at", kept.At?.ToString("O", CultureInfo.InvariantCulture));
                writer.WriteString("message", kept.Message);
                writer.WriteString("plugin", kept.Plugin);
                writer.WriteString("askedAt", kept.AskedAt == DateTimeOffset.MinValue ? null : kept.AskedAt.ToString("O", CultureInfo.InvariantCulture));
                writer.WriteEndObject();
            }
            else
            {
                writer.WriteNull("pullRequestState");
            }

            if (landed.PullRequestAskFailed is { } failed)
            {
                writer.WriteStartObject("pullRequestAskFailed");
                writer.WriteString("code", failed.Code);
                writer.WriteString("plugin", failed.Plugin);
                writer.WriteString("at", failed.At.ToString("O", CultureInfo.InvariantCulture));
                writer.WriteEndObject();
            }
            else
            {
                writer.WriteNull("pullRequestAskFailed");
            }

            writer.WriteEndObject();
        }
        else
        {
            writer.WriteNull("landed");
        }

        writer.WriteEndObject();
    }

    private static void Number(Utf8JsonWriter writer, string name, int? value)
    {
        if (value is { } number) writer.WriteNumber(name, number);
        else writer.WriteNull(name);
    }

    private static void Strings(Utf8JsonWriter writer, string name, IReadOnlyList<string> values)
    {
        writer.WriteStartArray(name);
        foreach (var value in values) writer.WriteStringValue(value);
        writer.WriteEndArray();
    }
}
