namespace Daoris.Driver;

/// <summary>
/// What a check runs under, its blob in <paramref name="Blob"/>. Under restricted access, always the declaration the person accepted, read
/// from its blob, never the work's file, and it holds only where the work's own change alters what runs: <paramref name="Held"/> says so, the
/// work's blob in <paramref name="Work"/>. Under full access, the work's own declaration as it committed it, holding nothing
/// (<see cref="DeclarationMode"/>). <paramref name="Problem"/> is a read that failed, which makes the check unknown and holds nothing as changed.
/// </summary>
/// <param name="Text">The declaration's text; null where its blob is <c>none</c> (the line held none).</param>
internal sealed record CheckDeclaration(string? Text, string? Blob = null, string? Held = null, string? Work = null, string? Problem = null)
{
    /// <summary>What a held check's person can do, said at the end of each hold.</summary>
    private const string Doors = "once the change lands, accept it on its repository's Setup page and the check runs again by itself, or answer the held check with It happened";

    /// <summary>
    /// The declaration a check of <paramref name="commit"/> in <paramref name="checkout"/> runs under, <paramref name="accepted"/> being the
    /// blob the person accepted and <paramref name="mode"/> the repository's (<see cref="DeclarationMode"/>). Under full access the work's
    /// own, read at its commit, whatever it changed: its agents run any command in their own sessions, so a command change of its own runs.
    /// Under restricted access the work's declaration is read at its commit and where it left its line (the merge base with the local
    /// branch of the line <paramref name="lineOf"/> names, the line the acceptance look reads): the check runs under the accepted one where the work's
    /// runs nothing the accepted does not, or where the work's own change (its declaration against the one it left) alters no command
    /// (<see cref="DeclarationCommands.Keeps"/>), so a branch that predates a declaration change on the line, or merged it since, is checked
    /// as accepted. Only a work whose own change alters a command holds: Daoris starts only the commands the person accepted (they run the
    /// work's own code). A line git cannot name, or no merge base, counts as its own change and is said apart.
    /// </summary>
    internal static async Task<CheckDeclaration> OfAsync(
        string checkout, string commit, string accepted, string mode, Func<CancellationToken, Task<string?>> lineOf, CancellationToken ct)
    {
        if (await IntegrationAcceptance.BlobAtAsync(checkout, commit, ct).ConfigureAwait(false) is not { } work)
        {
            return new(null, Problem: $"git could not read {TextBounds.ShortCommit(commit, 7)}'s daoris.gates.json here");
        }

        if (mode == DeclarationMode.Full && work != accepted)
        {
            var own = work == IntegrationAcceptance.None ? null : await IntegrationAcceptance.TextAsync(checkout, work, ct).ConfigureAwait(false);
            return own is null && work != IntegrationAcceptance.None
                ? new(null, Problem: $"{TextBounds.ShortCommit(commit, 7)}'s daoris.gates.json could not be read here")
                : new(own, work);
        }

        string? text = null;
        if (accepted != IntegrationAcceptance.None && (text = await IntegrationAcceptance.TextAsync(checkout, accepted, ct).ConfigureAwait(false)) is null)
        {
            return new(null, Problem: $"the accepted declaration {TextBounds.ShortCommit(accepted, 7)} could not be read here");
        }

        if (work == accepted) return new(text, accepted);
        var mine = await IntegrationAcceptance.TextAsync(checkout, work, ct).ConfigureAwait(false);
        if (DeclarationCommands.Keeps(text, mine)) return new(text, accepted);

        // The line is asked only here, where the work's own change is what decides.
        var line = await lineOf(ct).ConfigureAwait(false);
        var left = line is null ? null : await LeftAsync(checkout, line, commit, ct).ConfigureAwait(false);
        var before = left is null ? null : await IntegrationAcceptance.BlobAtAsync(checkout, left, ct).ConfigureAwait(false);
        if (before is null)
        {
            return new(text, accepted, Work: work, Held:
                $"Daoris could not tell where this work left its line ({(line is null ? "no line is set and git names none" : $"`{line}` and it share no commit here")}), "
                + $"so its change to daoris.gates.json cannot be told from the line's, and Daoris starts only the commands you accepted "
                + $"({DeclarationMode.Said(DeclarationMode.Restricted)}): {Doors}");
        }

        var was = await IntegrationAcceptance.TextAsync(checkout, before, ct).ConfigureAwait(false);
        if (before == work || DeclarationCommands.Keeps(was, mine)) return new(text, accepted);

        var said = DeclarationChange.Between(was, mine).Said();
        return new(text, accepted, Work: work, Held:
            $"this work's own change to daoris.gates.json alters the commands it runs{(said.Length > 0 ? $" ({said})" : "")}, "
            + $"and Daoris starts only the commands you accepted ({DeclarationMode.Said(DeclarationMode.Restricted)}), so its check waits: {Doors}");
    }

    /// <summary>
    /// The line a check of <paramref name="repository"/> in <paramref name="checkout"/> judges its work against: the one the acceptance look
    /// reads (<see cref="CanonicalLine.ResolveAsync(string, string, string?, DriverConfig, CancellationToken)"/>); null where the choices do
    /// not read or no line is set and git names none.
    /// </summary>
    internal static async Task<string?> LineAsync(string home, string repository, string checkout, CancellationToken ct)
    {
        DriverConfig config;
        try
        {
            config = DriverConfig.Load(DriverConfig.ResolvePath(home));
        }
        catch (DriverConfigUnreadableException)
        {
            return null;
        }

        var workspace = GitFolders.RepositoryOf(home, checkout).Workspace;
        return (await CanonicalLine.ResolveAsync(checkout, repository, workspace, config, ct).ConfigureAwait(false)).Branch;
    }

    /// <summary>Where <paramref name="commit"/> left <paramref name="line"/>: the merge base with its local branch here; null where git names none.</summary>
    private static async Task<string?> LeftAsync(string checkout, string line, string commit, CancellationToken ct)
    {
        if (!BranchName.IsValid(line)) return null;
        var (code, stdout, _) = await WorkingTree.GitAsync(checkout, ["merge-base", $"refs/heads/{line}", commit], ct).ConfigureAwait(false);
        return code == 0 && stdout.Trim() is { Length: > 0 } based ? based : null;
    }
}
