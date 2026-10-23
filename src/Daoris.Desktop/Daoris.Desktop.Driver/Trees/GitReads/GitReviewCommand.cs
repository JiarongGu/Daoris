using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>What <c>daoris-driver git review</c> was asked: whose review, and in which form.</summary>
/// <param name="Session">The session whose review is read.</param>
/// <param name="Json">The review written as JSON, its files without their patches, rather than said.</param>
/// <param name="Time">Each git start the read made, with its node, budget, gate wait, creation and run, and <c>review.read</c> written.</param>
public sealed record GitReviewAsk(string Session, bool Json, bool Time);

/// <summary>Where the terminal's review reads: the session's record (its tree and base), the repositories' checkouts, and the home's trees.</summary>
/// <param name="Ground">The tree and base commit a session's record names (<see cref="ServiceClient.SessionGroundAsync"/>).</param>
/// <param name="CheckoutOf">A repository's checkout here, or null.</param>
/// <param name="Trees">The home's session trees.</param>
public sealed record GitReviewSources(
    Func<string, CancellationToken, Task<(string? Tree, string? BaseCommit)>> Ground,
    Func<string, CancellationToken, Task<string?>> CheckoutOf,
    SessionTrees Trees);

/// <summary>
/// <c>daoris-driver git review &lt;session&gt; [--json] [--time]</c> (GITAPP1a-i, D202 point 2, the git-app design P1.5, D50): the
/// terminal's door to Review's read, through the same code as <c>SESSION_DIFF</c> (<see cref="SessionReviews"/>), and the bench's verb
/// (§10.3): with <c>--time</c> it prints every git start the read made, by node and budget, with the gate's wait, the creation and the
/// run apart, and writes <c>review.read</c>.
/// </summary>
/// <remarks>
/// <para><b>Reads only.</b> Its git is Review's own reads; with <c>--time</c> it appends one machine-log line, which names no path,
/// and never prunes the log, so the host routes it before it opens its own.</para>
///
/// <para>Exit codes are the family's: 0 read · 1 nothing is reviewable, as the page's refusal says · 2 could not: the usage, or the
/// service did not answer.</para>
/// </remarks>
public static class GitReviewCommand
{
    public const string Usage =
        """
        usage: daoris-driver git review <session> [--json] [--time]
        """;

    /// <summary>Whether the words are this command's.</summary>
    public static bool Answers(IReadOnlyList<string> args) => args is ["review", ..];

    /// <summary>What the words ask, or null with what is wrong with them.</summary>
    public static GitReviewAsk? Read(IReadOnlyList<string> args, out string? problem)
    {
        problem = null;
        if (args is not ["review", ..])
        {
            problem = "`git` reads `review`: name it.";
            return null;
        }

        string? session = null;
        bool json = false, time = false;
        foreach (var word in args.Skip(1))
        {
            switch (word)
            {
                case "--json":
                    json = true;
                    break;
                case "--time":
                    time = true;
                    break;
                case { Length: > 0 } when !word.StartsWith('-') && session is null:
                    session = word.Trim();
                    break;
                default:
                    problem = $"`{word}` is not a word `git review` takes.";
                    return null;
            }
        }

        if (session is not { Length: > 0 })
        {
            problem = "`git review` takes a session's id.";
            return null;
        }

        return new(session, json, time);
    }

    /// <summary>Read the review and say it; the exit code is the family's.</summary>
    public static async Task<int> RunAsync(
        GitReviewAsk ask, GitReviewSources sources, MachineLog? log, TextWriter output, CancellationToken ct = default)
    {
        var (tree, baseCommit) = await sources.Ground(ask.Session, ct).ConfigureAwait(false);
        var (review, starts) = await SessionReviews.TimedAsync(
            ask.Session, tree, baseCommit, sources.Trees, sources.CheckoutOf, ask.Time ? log : null, ct).ConfigureAwait(false);

        if (ask.Json) output.WriteLine(Json(ask.Session, review, ask.Time ? starts : null));
        else
        {
            output.Write(Said(ask.Session, review));
            if (ask.Time) output.Write(Times(starts));
        }

        if (ask.Time && !ask.Json) output.WriteLine(LineJson(SessionReviews.Line(ask.Session, review, starts)));
        return review is null ? 1 : 0;
    }

    /// <summary>The review in a few lines.</summary>
    internal static string Said(string session, SessionReview? review)
    {
        var text = new StringBuilder();
        if (review is null)
        {
            text.AppendLine(Invariant($"{session}: nothing to review here: its record names no tree on this machine, its tree is gone with no landing, it names no base, or git could not read its range."));
            return text.ToString();
        }

        var files = review.Diff?.Files.Count ?? 0;
        text.AppendLine(Invariant($"{session}: {files} file{(files == 1 ? "" : "s")} from {Short(review.Diff?.Base)}, read from {(review.FromBranch ? "its landed branch" : "its tree")}"));
        if (review.Uncommitted is { Files.Count: > 0 } dirty) text.AppendLine(Invariant($"  uncommitted: {dirty.Files.Count} file{(dirty.Files.Count == 1 ? "" : "s")}"));
        if (review.Behind is { } behind) text.AppendLine(Invariant($"  {behind.Line}: {behind.Commits} behind, {behind.Ahead} ahead"));
        if (review.TreeBranch is { } branch) text.AppendLine(Invariant($"  branch: {branch}"));
        if (review.Landed is { } landed) text.AppendLine(Invariant($"  landed: {landed.Entry.Branch} ({landed.State})"));
        if (review.Offer is { } offer) text.AppendLine(Invariant($"  offers to land {offer.Commits} commit{(offer.Commits == 1 ? "" : "s")} of {offer.Branch}"));
        return text.ToString();
    }

    /// <summary>Each start, in the order the seam saw them, and the totals: what the bench reads (the git-app design §10.3).</summary>
    internal static string Times(GitCount count)
    {
        var starts = count.Starts;
        var text = new StringBuilder();
        text.AppendLine();
        text.AppendLine("  #  node  budget     runner    waited  created      ran  git");
        for (var at = 0; at < starts.Count; at++)
        {
            var start = starts[at];
            text.AppendLine(Invariant(
                $"{at + 1,3}  {start.Node,-4}  {Budget(start.Budget),-9}  {Runner(start.Runner),-8}  {Ms(start.WaitedTicks),6}  {Ms(start.CreatedTicks),7}  {Ms(start.RanTicks),7}  git {string.Join(' ', start.Arguments)}"));
        }

        text.AppendLine(Invariant(
            $"  {starts.Count} starts, depth {starts.Count} (one after another), {GitLaunched.Ms(count.ElapsedTicks):0} ms: waited {Ms(starts.Sum(start => start.WaitedTicks))}, created {Ms(starts.Sum(start => start.CreatedTicks))}, ran {Ms(starts.Sum(start => start.RanTicks))}"));
        return text.ToString();
    }

    /// <summary>The review as JSON: the files without their patches, and with <paramref name="starts"/> each start and the log line's fields.</summary>
    internal static string Json(string session, SessionReview? review, GitCount? starts)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            writer.WriteStartObject();
            writer.WriteString("session", session);
            writer.WriteBoolean("reviewable", review is not null);
            if (review is not null)
            {
                writer.WriteString("source", review.FromBranch ? "branch" : "tree");
                writer.WriteString("base", review.Diff?.Base ?? "");
                Files(writer, "files", review.Diff);
                if (review.Uncommitted is not null) Files(writer, "uncommitted", review.Uncommitted);
                if (review.Behind is { } behind)
                {
                    writer.WriteStartObject("behind");
                    writer.WriteString("line", behind.Line);
                    writer.WriteNumber("commits", behind.Commits);
                    writer.WriteNumber("ahead", behind.Ahead);
                    writer.WriteEndObject();
                }

                writer.WriteString("treeBranch", review.TreeBranch);
                writer.WriteBoolean("offer", review.Offer is not null);
            }

            if (starts is not null)
            {
                writer.WriteStartArray("starts");
                foreach (var start in starts.Starts)
                {
                    writer.WriteStartObject();
                    writer.WriteString("node", start.Node);
                    writer.WriteString("budget", Budget(start.Budget));
                    writer.WriteString("runner", Runner(start.Runner));
                    writer.WriteString("kind", start.Kind.ToString());
                    writer.WriteNumber("waitedMs", Math.Round(GitLaunched.Ms(start.WaitedTicks), 1));
                    writer.WriteNumber("createdMs", Math.Round(GitLaunched.Ms(start.CreatedTicks), 1));
                    writer.WriteNumber("ranMs", Math.Round(GitLaunched.Ms(start.RanTicks), 1));
                    writer.WriteStartArray("arguments");
                    foreach (var argument in start.Arguments) writer.WriteStringValue(argument);
                    writer.WriteEndArray();
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WritePropertyName("read");
                Fields(writer, SessionReviews.Line(session, review, starts));
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void Files(Utf8JsonWriter writer, string name, WorkingTree.TreeDiff? diff)
    {
        writer.WriteStartArray(name);
        foreach (var file in diff?.Files ?? [])
        {
            writer.WriteStartObject();
            writer.WriteString("path", file.Path);
            writer.WriteString("status", file.Status);
            if (file.Added is { } added) writer.WriteNumber("added", added);
            if (file.Removed is { } removed) writer.WriteNumber("removed", removed);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    /// <summary><c>review.read</c>'s fields on one line, as the bench keeps them.</summary>
    internal static string LineJson((string Key, object? Value)[] fields)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            Fields(writer, fields);
        }

        return "review.read " + Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void Fields(Utf8JsonWriter writer, (string Key, object? Value)[] fields)
    {
        writer.WriteStartObject();
        foreach (var (key, value) in fields)
        {
            switch (value)
            {
                case int number:
                    writer.WriteNumber(key, number);
                    break;
                case long number:
                    writer.WriteNumber(key, number);
                    break;
                default:
                    writer.WriteString(key, value?.ToString());
                    break;
            }
        }

        writer.WriteEndObject();
    }

    private static string Budget(GitBudget budget) => budget.ToString().ToLowerInvariant();

    private static string Runner(GitRunner runner) => runner.ToString().ToLowerInvariant();

    private static string Ms(long ticks) => GitLaunched.Ms(ticks).ToString("0", CultureInfo.InvariantCulture);

    private static string Short(string? commit) => string.IsNullOrEmpty(commit) ? "no base" : TextBounds.ShortCommit(commit, 8);

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
