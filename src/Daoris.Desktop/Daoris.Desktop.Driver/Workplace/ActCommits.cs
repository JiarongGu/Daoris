using System.Text;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// The commit step's plan, its effect and its postconditions. The effect is one <c>git commit</c>
/// in the act's job, so its hooks and their children are ended with it; it is never run again, and no hook is skipped. Its postconditions read
/// <c>HEAD</c> against the old tip and the plan's parents, and its message against the plan's, never the exit code.
/// </summary>
internal static class ActCommits
{
    /// <summary>The hooks a plain commit may run, in git's order; an amend adds <see cref="Rewrite"/>.</summary>
    public static readonly string[] CommitHooks = ["pre-commit", "prepare-commit-msg", "commit-msg", "post-commit", "reference-transaction"];

    public const string Rewrite = "post-rewrite";

    /// <summary>
    /// A commit step: <paramref name="author"/> as <c>-c</c> words, the <paramref name="message"/> as cleaned, the tip it starts from, the tree the
    /// index held, the parents the new commit must have, and the hooks the plan named.
    /// </summary>
    public static ActStepPlan Step(
        IReadOnlyList<string> author, string message, string? head, string? branch, bool amend, string tree, IReadOnlyList<string> parents, IReadOnlyList<string> hooks)
    {
        var said = new JsonObject
        {
            ["author"] = new JsonArray([.. author.Select(each => (JsonNode)each)]), ["message"] = message, ["amend"] = amend,
        };
        var read = new JsonObject
        {
            ["head"] = head, ["branch"] = branch, ["amend"] = amend, ["message"] = message, ["tree"] = tree,
            ["parents"] = new JsonArray([.. parents.Select(each => (JsonNode)each)]), ["hooks"] = new JsonArray([.. hooks.Select(each => (JsonNode)each)]),
        };
        return new ActStepPlan(ActStepKinds.Commit, said.ToJsonString(), read.ToJsonString());
    }

    /// <summary>The commit, run once in the act's job with the message on its input; its words are git's and its hooks'.</summary>
    public static async Task<ActStepEnd> EffectAsync(TreeLock held, ActLaunch launch, ActStep step)
    {
        _ = held;
        var plan = JsonNode.Parse(step.Arguments)!;
        var who = plan["author"]!.AsArray().Select(each => each!.GetValue<string>()).ToList();
        var message = plan["message"]!.GetValue<string>();
        string[] commit = plan["amend"]!.GetValue<bool>()
            ? [.. who, "commit", "--amend", "--cleanup=whitespace", "-F", "-"]
            : [.. who, "commit", "--cleanup=whitespace", "-F", "-"];
        var run = await launch.HeldGitAsync(launch.Root, step.Ordinal, commit, message + "\n", null).ConfigureAwait(false);
        var words = string.Join(' ', new[] { run.Stderr, run.Stdout }.Select(each => each.Trim()).Where(each => each.Length > 0));
        return run.End(run.Exit == 0 ? "git made the commit" : $"git refused the commit ({ContentHolds.Said(words)})");
    }

    /// <summary>What the step's postconditions read: done where HEAD stands on the plan's parents, not done where it stands where it began, else unknown.</summary>
    public static async Task<string?> ReadAsync(string root, JsonNode plan, CancellationToken ct)
    {
        var head = plan["head"]?.GetValue<string>();
        var now = await HeadAsync(root, ct).ConfigureAwait(false);
        if (now.Unread) return null;
        if (now.Id == head) return ActOutcomes.NotDone;
        if (now.Id is null) return ActOutcomes.Unknown;

        var parents = plan["parents"]!.AsArray().Select(each => each!.GetValue<string>()).ToList();
        if (await ParentsAsync(root, now.Id, ct).ConfigureAwait(false) is not { } stand) return null;
        if (!stand.SequenceEqual(parents, StringComparer.Ordinal)) return ActOutcomes.Unknown;

        // A message another hand wrote on the same parents is no proof; one a hook may rewrite is read as the hook's.
        if (await MessageAsync(root, now.Id, ct).ConfigureAwait(false) is not { } message) return null;
        var rewrites = plan["hooks"]!.AsArray().Select(each => each!.GetValue<string>()).Any(each => each is "commit-msg" or "prepare-commit-msg");
        return Normalize(message) == Normalize(plan["message"]!.GetValue<string>()) || rewrites ? ActOutcomes.Done : ActOutcomes.Unknown;
    }

    /// <summary>The words that say what a hook changed in the commit made: its files that differ from the tree the index held, and its message.</summary>
    public static async Task<string?> RewriteWordsAsync(string root, string? tree, string message, CancellationToken ct)
    {
        var now = await HeadAsync(root, ct).ConfigureAwait(false);
        if (now.Id is null || tree is null) return null;
        var said = new List<string>();
        var (code, names, _) = await WorkingTree.GitAsync(root, ["diff-tree", "-r", "--name-only", "--no-renames", "-z", tree, now.Id + "^{tree}"], ct).ConfigureAwait(false);
        if (code == 0 && names.Split('\0', StringSplitOptions.RemoveEmptyEntries) is { Length: > 0 } files)
        {
            said.Add($"a hook changed what was committed: {string.Join(", ", files.Take(5).Select(each => $"`{each}`"))}{(files.Length > 5 ? $" and {files.Length - 5} more" : "")} differ from what was staged");
        }

        if (await MessageAsync(root, now.Id, ct).ConfigureAwait(false) is { } written && Normalize(written) != Normalize(message))
        {
            said.Add("a hook changed the message");
        }

        return said.Count == 0 ? null : string.Join("; ", said);
    }

    /// <summary>The commit HEAD names, null on an unborn branch; <c>Unread</c> where git could not say.</summary>
    public static async Task<(string? Id, bool Unread)> HeadAsync(string root, CancellationToken ct)
    {
        var (code, said, _) = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", "HEAD^{commit}"], environment: null, GitBounds.Question, ct).ConfigureAwait(false);
        return code == 0 && WorkingTree.IsCommitId(said.Trim()) ? (said.Trim(), false) : code == 1 ? (null, false) : (null, true);
    }

    /// <summary>The ids of <paramref name="commit"/>'s parents, first first; null where git could not say.</summary>
    public static async Task<IReadOnlyList<string>?> ParentsAsync(string root, string commit, CancellationToken ct)
    {
        var (code, said, _) = await WorkingTree.GitAsync(root, ["rev-list", "--parents", "-n", "1", commit], environment: null, GitBounds.Question, ct).ConfigureAwait(false);
        return code != 0 ? null : said.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1).ToList();
    }

    /// <summary>A commit's tree, parents and whole message from one <c>log</c>, where a plan asked each of the three; null where git could not say.</summary>
    public static async Task<(string Tree, IReadOnlyList<string> Parents, string Message)?> FactsAsync(string root, string commit, CancellationToken ct)
    {
        var (code, said, _) = await WorkingTree.GitAsync(root, ["log", "-1", "--format=%T%x00%P%x00%B", "--no-show-signature", commit], environment: null, GitBounds.Question, ct).ConfigureAwait(false);
        var parts = said.Split('\0', 3);
        return code != 0 || parts.Length != 3 || !WorkingTree.IsCommitId(parts[0].Trim()) ? null : (parts[0].Trim(), parts[1].Split(' ', StringSplitOptions.RemoveEmptyEntries), parts[2]);
    }

    /// <summary>The whole message of <paramref name="commit"/>; null where git could not say.</summary>
    public static async Task<string?> MessageAsync(string root, string commit, CancellationToken ct)
    {
        var (code, said, _) = await WorkingTree.GitAsync(root, ["log", "-1", "--format=%B", "--no-show-signature", commit], environment: null, GitBounds.Question, ct).ConfigureAwait(false);
        return code != 0 ? null : said;
    }

    /// <summary>
    /// The tree the index holds, made on a copy of the index so the index itself is never written; null where the index cannot be made a tree
    /// (an unresolved file stands in it).
    /// </summary>
    public static async Task<string?> TreeAsync(string root, string gitDir, CancellationToken ct)
    {
        var copy = Path.Combine(gitDir, $"index.daoris-plan-{Guid.NewGuid():N}");
        try
        {
            var index = Path.Combine(gitDir, "index");
            if (File.Exists(index)) File.Copy(index, copy, overwrite: true);
            var environment = new Dictionary<string, string>(StringComparer.Ordinal) { ["GIT_INDEX_FILE"] = copy };
            var (code, said, _) = await WorkingTree.GitAsync(root, ["write-tree"], environment, GitBounds.Question, ct).ConfigureAwait(false);
            return code == 0 && said.Trim().Length > 0 ? said.Trim() : null;
        }
        finally
        {
            try
            {
                File.Delete(copy);
            }
            catch (IOException)
            {
                // A copy left in the git directory is named for its act and harmless.
            }
        }
    }

    /// <summary>A message as <c>--cleanup=whitespace</c> leaves it: lines trimmed at the end, no blank line at either end, runs of blank lines as one.</summary>
    public static string Normalize(string message)
    {
        var lines = new List<string>();
        foreach (var line in message.Replace("\r\n", "\n").Split('\n').Select(each => each.TrimEnd()))
        {
            if (line.Length == 0 && (lines.Count == 0 || lines[^1].Length == 0)) continue;
            lines.Add(line);
        }

        while (lines.Count > 0 && lines[^1].Length == 0) lines.RemoveAt(lines.Count - 1);
        return new StringBuilder().AppendJoin('\n', lines).ToString();
    }
}
