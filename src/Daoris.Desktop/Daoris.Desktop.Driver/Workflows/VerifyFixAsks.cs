using System.Text;

namespace Daoris.Driver;

/// <summary>
/// A failed verification becomes work (FABRIC-PIPELINE1f; the pipeline design §4.5): <i>Ask for a fix…</i> publishes one ask per failed gate to
/// the repository, in Daoris's fixed words naming the line, the gate and the run, with the gate's log, its last 256 KiB, as the ask's file and
/// the kind the check's <c>ask</c> option names as its choice, so the start node routes it into the development pipeline chosen for that kind.
/// </summary>
/// <remarks>
/// An ask is sent with no key of the sender's, so a restart between its send and its answer could not tell whether it was made. Before it
/// publishes, the look reads the workspace's asks for one whose words are Daoris's own and name this line and gate: one naming this run is the
/// ask already made; another still open takes the new failure on its channel, so there is one per gate; none, it publishes. A person's words
/// are never read, and nothing in the service changes.
/// </remarks>
public static class VerifyFixAsks
{
    /// <summary>The words every fix ask opens with, which the look finds it by.</summary>
    public const string Opening = "Daoris verification fix: ";

    /// <summary>How much of a gate's log goes with the ask.</summary>
    public const int LogBytes = 256 * 1024;

    /// <summary>How many commits of the batch the ask names.</summary>
    private const int Commits = 20;

    /// <summary>What a press came to: whether an ask stands for the failure now, and the sentence the person reads.</summary>
    public sealed record Pressed(bool Done, string Message, string? Ask = null);

    /// <summary>The first line of an ask for <paramref name="gate"/> failing on <paramref name="scope"/> in <paramref name="run"/>.</summary>
    public static string First(string scope, string gate, string run) => $"{Opening}{Key(scope, gate)}{RunMark}{run}.";

    private const string RunMark = ", run ";

    private static string Key(string scope, string gate) => $"{(scope.Contains(' ') ? $"`{scope}`" : scope)}'s gate {gate}";

    /// <summary>The line a landed run is on, or the branch or commit a person asked of it: what the ask names as where it failed.</summary>
    public static string ScopeOf(VerifyRun run) => run.Line ?? run.Branch ?? TextBounds.ShortCommit(run.Commit, 7);

    /// <summary>
    /// The ask for the failed check <paramref name="node"/> of <paramref name="unit"/>'s attempt <paramref name="attempt"/>: found if it was made,
    /// added to its channel if its gate failed again, else published. A refusal is an answer with its sentence.
    /// </summary>
    public static async Task<Pressed> AskAsync(ServiceClient service, NodeRecordFiles records, string unit, string node, int attempt, CancellationToken ct)
    {
        var record = records.Read(unit);
        var held = record?.Attempts.FirstOrDefault(each => each.Node == node && each.Attempt == attempt);
        if (record is null || held is null || held.Plugin != NodeKinds.Check || !WorkPerformer.KeptFailure(record, held))
        {
            return new(false, "that is not a failed verification held for you");
        }

        var run = VerifyRuns.All(records.Home).FirstOrDefault(each => RunUnits.IdOf(each.Run) == unit);
        if (run is null) return new(false, "the verification's run is not bound here");
        if (CheckResults.Newest(records.Home, run.Run, record, node) is not { Failed: { } gate } result) return new(false, "its newest result names no failed gate to ask about");

        var scope = ScopeOf(run);
        var first = First(scope, gate, run.Run);
        var made = (await service.EveryAskAsync(ct).ConfigureAwait(false))
            .Where(ask => string.Equals(RemoteTarget.Workspace(ask.Workspace), RemoteTarget.Workspace(run.Workspace), StringComparison.OrdinalIgnoreCase)
                          && ask.Sentence.StartsWith(Opening + Key(scope, gate) + RunMark, StringComparison.Ordinal))
            .ToList();
        if (made.FirstOrDefault(ask => ask.Sentence.StartsWith(first, StringComparison.Ordinal)) is { } already)
        {
            return new(true, $"the ask for this failure is made already: #{already.Id}", already.Id);
        }

        if (made.FirstOrDefault(ask => !Is(ask.State, "Closed") && !Is(ask.State, "Done")) is { } open)
        {
            var posted = await service.ChannelPostAsync(open.Id, Again(run, scope, gate), to: null, re: null, ct, urgent: false).ConfigureAwait(false);
            return posted.Ok
                ? new(true, $"the gate {gate} failed again, which is on the channel of ask #{open.Id}", open.Id)
                : new(false, $"the channel of ask #{open.Id} would not take it: {posted.Code}");
        }

        var files = LogOf(result.Gates.FirstOrDefault(each => each.Name == gate)?.Log, gate) is { } log ? new[] { log } : [];
        var kind = NodeOf(records.Home, run, node) is { } declared ? NodeOptions.Text(declared, "ask") : null;
        var composed = kind is not null && AskWorkflowCommand.Compose(kind, null, null, out _) is { } chosen ? chosen : null;
        var published = await service.AskAsync(
            run.Workspace, await SentenceAsync(run, scope, gate, result, first, ct).ConfigureAwait(false), [], files, run.Repository, ct,
            workflow: composed, shortTitle: Short(scope, gate)).ConfigureAwait(false);
        return published.Ok
            ? new(true, $"asked for a fix of the gate {gate}: #{published.AskId}", published.AskId)
            : new(false, published.Message);
    }

    private static bool Is(string state, string word) => string.Equals(state, word, StringComparison.OrdinalIgnoreCase);

    private static string Short(string scope, string gate)
    {
        var title = $"Fix {gate} on {scope}";
        return title.Length <= 40 ? title : title[..40];
    }

    private static string Again(VerifyRun run, string scope, string gate) =>
        $"The gate {gate} failed again on {scope} at {TextBounds.ShortCommit(run.Commit, 7)} (run {run.Run}). Its log is on the run.";

    /// <summary>The check's declaration in the version the run is bound to; null where it no longer reads.</summary>
    private static NodeDeclaration? NodeOf(string home, VerifyRun run, string node) =>
        new WorkflowFiles(home).Load(run.Workflow)?.Read.Versions.FirstOrDefault(each => each.Version == run.Version) is { Graph: { } graph }
            ? graph.Nodes.FirstOrDefault(each => each.Id == node)
            : null;

    private static async Task<string> SentenceAsync(VerifyRun run, string scope, string gate, VerifyResult result, string first, CancellationToken ct)
    {
        var text = new StringBuilder(first).AppendLine().AppendLine();
        text.AppendLine($"This ask is Daoris's, not a person's: its verification of {run.Repository}'s {scope} failed the gate {gate} at {TextBounds.ShortCommit(run.Commit, 7)}.");
        if (result.Gates.FirstOrDefault(each => each.Name == gate)?.Exit is { } exit) text.AppendLine($"The gate exited {exit}; its log is attached.");
        if (run.Base is { } from)
        {
            var (code, log, _) = await WorkingTree.GitAsync(run.Root, ["log", "--format=%h %s", $"-{Commits}", $"{from}..{run.Commit}"], ct).ConfigureAwait(false);
            if (code == 0 && log.Trim().Length > 0)
            {
                text.AppendLine().AppendLine($"The batch since {TextBounds.ShortCommit(from, 7)}, newest first:");
                foreach (var line in log.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) text.AppendLine($"- {line}");
            }
        }

        return text.ToString().TrimEnd();
    }

    /// <summary>A gate's log, its last <see cref="LogBytes"/>, as the ask's file; null where it is not kept here.</summary>
    private static (string Name, byte[] Content)? LogOf(string? path, string gate)
    {
        try
        {
            if (path is not { Length: > 0 } || !File.Exists(path)) return null;
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var take = (int)Math.Min(stream.Length, LogBytes);
            stream.Seek(-take, SeekOrigin.End);
            var content = new byte[take];
            stream.ReadExactly(content);
            var name = new string([.. gate.Select(letter => char.IsAsciiLetterOrDigit(letter) ? letter : '-')]);
            return ($"{name}.log", content);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
