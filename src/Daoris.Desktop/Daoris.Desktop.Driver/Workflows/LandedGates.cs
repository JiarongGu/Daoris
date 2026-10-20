using System.Text;

namespace Daoris.Driver;

/// <summary>
/// The line's tip as a landed route reads it (FABRIC-PIPELINE1a): its line, the commit at its tip, and git's blob id of the
/// <c>daoris.gates.json</c> it holds there, or <c>none</c> where it holds none.
/// </summary>
public sealed record LandedTip(string Line, string Commit, string Gates);

/// <summary>
/// The declaration a landed route runs unasked, accepted by its blob (FABRIC-PIPELINE1a, D192 point 2, the pipeline design §4.2): the
/// person accepts the <c>daoris.gates.json</c> at the line's tip, kept as the blob id git gives it, so any edit of the file (a command, a
/// folder, a gate, a <c>heavy</c> or <c>clock</c> mark, the runner) asks again, and a landed edit of it never runs by itself. Read as
/// git objects with no checkout, so what the person has in flight there is neither read nor touched.
/// </summary>
/// <remarks>
/// A blob is git's digest of the file's bytes, so no twin computes a canonical form of the declaration; an edit that changes nothing
/// that runs (a key moved) asks again too, the price the design names (§6). The CLI's <c>use … --landed</c> reads it the same way.
/// </remarks>
public static class LandedGates
{
    /// <summary>The declaration's name at a repository's top.</summary>
    public const string File = "daoris.gates.json";

    /// <summary>The acceptance names the tip's declaration.</summary>
    public const string Accepted = "accepted";

    /// <summary>The tip's declaration is not the one accepted: nothing runs until the person accepts it again.</summary>
    public const string Changed = "changed";

    /// <summary>A route naming a verification pipeline with no gates accepted yet.</summary>
    public const string Unaccepted = "unaccepted";

    /// <summary>The tip could not be read here: no checkout, or a line that names no commit.</summary>
    public const string Unread = "unread";

    /// <summary>The route names no verification, so there is nothing to accept.</summary>
    public const string NoVerification = "none";

    /// <summary>How much of a declaration is read for the person to see: one is far smaller.</summary>
    private const int TextLimit = 256 * 1024;

    /// <summary>
    /// The tip of <paramref name="line"/> in the checkout at <paramref name="root"/> and its declaration's blob, or why it could not be
    /// read: no repository there, or a line that names no commit.
    /// </summary>
    public static async Task<(LandedTip? Tip, string? Problem)> ReadAsync(string root, string line, CancellationToken ct = default)
    {
        // 🔴 git walks UP (FIX-LOG): a folder that is not a repository's top would answer for the one above it.
        if (!await WorkingTree.IsTopLevelAsync(root, ct).ConfigureAwait(false)) return (null, "its checkout here is not a git repository");
        if (!BranchName.IsValid(line)) return (null, $"its line `{line}` is not a branch name git would take");

        var (found, sha, _) = await WorkingTree.GitAsync(
            root, ["rev-parse", "--verify", "--quiet", $"refs/heads/{line}^{{commit}}"], ct).ConfigureAwait(false);
        var commit = sha.Trim();
        if (found != 0 || !WorkingTree.IsCommitId(commit)) return (null, $"its line `{line}` names no commit here");

        // The tip's entry of that name at the top, `<mode> <type> <object>\t<name>`: a blob alone is a declaration, never a folder.
        var (listed, entry, problem) = await WorkingTree.GitAsync(root, ["ls-tree", "--full-tree", "-z", commit, "--", File], ct).ConfigureAwait(false);
        if (listed != 0) return (null, $"git could not list `{line}` at `{TextBounds.ShortCommit(commit, 7)}`: {problem.Trim()}");
        var parts = entry.Split('\t', 2)[0].Split(' ');
        var gates = parts is [_, "blob", var blob] && IsBlob(blob) ? blob : WorkflowSelection.NoGates;
        return (new LandedTip(line, commit, gates), null);
    }

    /// <summary>The declaration's text at <paramref name="blob"/>, for the person to read before accepting it; null for <c>none</c>.</summary>
    public static async Task<string?> TextAsync(string root, string blob, CancellationToken ct = default)
    {
        if (!IsBlob(blob)) return null;
        return await WorkingTree.GitBytesAsync(root, ["cat-file", "blob", blob], TextLimit, ct).ConfigureAwait(false) is { } read
            ? Encoding.UTF8.GetString(read.Bytes, 0, read.Count)
            : null;
    }

    /// <summary>A whole blob id as git writes one: 40 lower-case hex, or 64 in a SHA-256 repository.</summary>
    private static bool IsBlob(string text) => text != WorkflowSelection.NoGates && WorkflowSelection.IsGates(text);

    /// <summary>
    /// Where a route's acceptance stands against the tip (the pipeline design §4.2): <see cref="Accepted"/>, <see cref="Changed"/>,
    /// <see cref="Unaccepted"/>, <see cref="Unread"/>, or <see cref="NoVerification"/> where the route names none.
    /// </summary>
    public static string StateOf(WorkflowLandedSelected route, LandedTip? tip)
    {
        if (route.Workflow == WorkflowSelection.Current) return NoVerification;
        if (tip is null) return Unread;
        if (route.Gates is null) return Unaccepted;
        return route.Gates == tip.Gates ? Accepted : Changed;
    }
}
