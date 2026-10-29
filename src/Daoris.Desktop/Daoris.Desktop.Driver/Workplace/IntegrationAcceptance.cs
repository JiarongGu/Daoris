using System.Text;

namespace Daoris.Driver;

/// <summary>
/// The owner's acceptance of a repository's integration declaration (INTEGRATE1d; D215 point 2, the integration design §4.1, §4.3): git's
/// blob id of the <c>daoris.gates.json</c> the person accepted for integration, kept by this machine in <c>driver.json</c>'s
/// <c>integrationGates</c> (<see cref="IntegrationGatesKey"/>), apart from the landed route's (D192), so a repository needs no
/// verification pipeline to integrate. Only the accepted blob's text is read for what runs: a member that changes the declaration
/// integrates under the line's accepted one, and its own blob waits for the owner.
/// </summary>
/// <remarks>
/// The states are pure (<see cref="StateOf"/>); the reads beside them ask git of the checkout's objects alone, never its working files,
/// so what the person has in flight there is neither read nor touched. Their consumers (INTEGRATE1d's second part): the window's door,
/// which accepts the tip's blob; GENREGEN1's writers and the line rules' writers in a landing's replay; and the tidy's declared
/// scratch (D218's note). Each reads the accepted blob's text only where it is the declaration at the commit it acts on.
/// </remarks>
public static class IntegrationAcceptance
{
    /// <summary>The acceptance of a line that held no declaration, as <c>integrationGates</c> keeps it.</summary>
    public const string None = WorkflowKey.NoGates;

    /// <summary>How much of a declaration is read: one is far smaller, and one past it does not read, so it declares nothing.</summary>
    private const int TextLimit = 256 * 1024;

    // The four states are the landed route's (LandedGates in Workflows, which this does not name), so a page reads both alike.

    /// <summary>The acceptance names the tip's declaration.</summary>
    public const string Accepted = "accepted";

    /// <summary>The tip's declaration is not the one accepted: integration goes on under the last accepted blob, and the new one waits for the owner.</summary>
    public const string Changed = "changed";

    /// <summary>No declaration has been accepted for integration here yet: nothing it declares runs.</summary>
    public const string Unaccepted = "unaccepted";

    /// <summary>The tip could not be read here: no checkout, or a line that names no commit.</summary>
    public const string Unread = "unread";

    /// <summary>The blob this machine accepted for <paramref name="repository"/>'s integration, its name trimmed and matched in any case; null for none.</summary>
    public static string? Of(DriverConfig config, string repository) =>
        config.IntegrationGates.TryGetValue(repository.Trim(), out var accepted) ? accepted : null;

    /// <summary>
    /// Where an acceptance stands against the line's tip, in the shape of the landed route's: <see cref="Unread"/> where the tip's
    /// blob could not be read (null), whatever was accepted; <see cref="Unaccepted"/> where nothing was; <see cref="Accepted"/> where
    /// the accepted blob is the tip's (<c>none</c> for a line that holds no declaration, accepted as such); else <see cref="Changed"/>.
    /// </summary>
    public static string StateOf(string? accepted, string? tip)
    {
        if (tip is null) return Unread;
        if (accepted is null) return Unaccepted;
        return string.Equals(accepted, tip, StringComparison.Ordinal) ? Accepted : Changed;
    }

    /// <summary>
    /// The declaration's blob at <paramref name="commit"/> (a commit's id or a full ref) in the checkout at <paramref name="root"/>: its
    /// blob id, <see cref="None"/> where the commit holds no such file at its top (a folder of that name is none either), or null where
    /// git cannot say, so nothing is read as accepted.
    /// </summary>
    internal static async Task<string?> BlobAtAsync(string root, string commit, CancellationToken ct)
    {
        var (listed, entry, _) = await WorkingTree.GitAsync(
            root, ["ls-tree", "--full-tree", "-z", commit, "--", GeneratedWriters.DeclarationFile], ct).ConfigureAwait(false);
        if (listed != 0) return null;
        // `<mode> <type> <object>\t<name>`: a blob alone is a declaration.
        var line = entry.TrimEnd('\0');
        var tab = line.IndexOf('\t');
        return tab >= 0 && line[..tab].Split(' ') is [_, "blob", var blob] && IntegrationGatesKey.IsAcceptance(blob) && blob != None ? blob : None;
    }

    /// <summary>The declaration's blob at the tip of <paramref name="line"/> here, as <see cref="BlobAtAsync"/> reads it; null for a name git would not take.</summary>
    public static async Task<string?> TipAsync(string root, string line, CancellationToken ct = default) =>
        BranchName.IsValid(line) ? await BlobAtAsync(root, $"refs/heads/{line}", ct).ConfigureAwait(false) : null;

    /// <summary>The text of the declaration at <paramref name="blob"/>, for the person to read before accepting it; null for <see cref="None"/> or one git cannot give.</summary>
    public static async Task<string?> TextAsync(string root, string blob, CancellationToken ct = default)
    {
        if (blob == None || !IntegrationGatesKey.IsAcceptance(blob)) return null;
        return await WorkingTree.GitBytesAsync(root, ["cat-file", "blob", blob], TextLimit, ct).ConfigureAwait(false) is { } read
            ? Encoding.UTF8.GetString(read.Bytes, 0, read.Count)
            : null;
    }

    /// <summary>
    /// The text of the declaration <paramref name="repository"/>'s integration acceptance names, read only where that blob is the one at
    /// <paramref name="commit"/> (the design §4.3: the accepted blob alone, at the commit it acts on); null where nothing is accepted,
    /// the declaration there is another (it waits for the owner), git cannot say, or the line held none.
    /// </summary>
    internal static async Task<string?> AcceptedTextAsync(DriverConfig config, string root, string repository, string commit, CancellationToken ct)
    {
        if (Of(config, repository) is not { } accepted || accepted == None) return null;
        if (StateOf(accepted, await BlobAtAsync(root, commit, ct).ConfigureAwait(false)) != Accepted) return null;
        return await TextAsync(root, accepted, ct).ConfigureAwait(false);
    }
}
