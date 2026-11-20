namespace Daoris.Driver;

/// <summary>
/// What a check runs under: always the declaration the person accepted, read from its blob, never the work's file. It holds only where the
/// work's own change alters what runs: <paramref name="Held"/> says so, the work's blob in <paramref name="Work"/>; <paramref name="Problem"/> is
/// a read that failed, which makes the check unknown and holds nothing as changed.
/// </summary>
/// <param name="Text">The accepted declaration's text; null where the accepted blob is <c>none</c> (the line held none).</param>
internal sealed record CheckDeclaration(string? Text, string? Held = null, string? Work = null, string? Problem = null)
{
    /// <summary>
    /// The declaration a check of <paramref name="commit"/> in <paramref name="checkout"/> runs under, <paramref name="accepted"/> being the
    /// blob the person accepted. The work's declaration is read at its commit and where it left its line (the merge base with the line's
    /// tip here): the check runs under the accepted one where the work's runs nothing the accepted does not, or where the work's own change
    /// (its declaration against the one it left) alters no command (<see cref="DeclarationCommands.Keeps"/>), so a branch that predates a
    /// declaration change on the line, or merged it since, is checked as accepted. Only a work whose own change alters a command holds,
    /// since a work never runs a command the person did not accept; a line git cannot name, or no merge base, counts as its own change.
    /// </summary>
    internal static async Task<CheckDeclaration> OfAsync(string checkout, string commit, string accepted, CancellationToken ct)
    {
        if (await IntegrationAcceptance.BlobAtAsync(checkout, commit, ct).ConfigureAwait(false) is not { } work)
        {
            return new(null, Problem: $"git could not read {TextBounds.ShortCommit(commit, 7)}'s daoris.gates.json here");
        }

        string? text = null;
        if (accepted != IntegrationAcceptance.None && (text = await IntegrationAcceptance.TextAsync(checkout, accepted, ct).ConfigureAwait(false)) is null)
        {
            return new(null, Problem: $"the accepted declaration {TextBounds.ShortCommit(accepted, 7)} could not be read here");
        }

        if (work == accepted) return new(text);
        var mine = await IntegrationAcceptance.TextAsync(checkout, work, ct).ConfigureAwait(false);
        if (DeclarationCommands.Keeps(text, mine)) return new(text);

        var left = await LeftAsync(checkout, commit, ct).ConfigureAwait(false);
        var before = left is null ? null : await IntegrationAcceptance.BlobAtAsync(checkout, left, ct).ConfigureAwait(false);
        var was = before is null ? null : await IntegrationAcceptance.TextAsync(checkout, before, ct).ConfigureAwait(false);
        if (before is not null && (before == work || DeclarationCommands.Keeps(was, mine))) return new(text);

        var said = before is null ? "" : DeclarationChange.Between(was, mine).Said();
        return new(text, Work: work, Held:
            $"this work's own change to daoris.gates.json alters the commands it runs{(said.Length > 0 ? $" ({said})" : "")}, "
            + "so its check waits until you accept that change: a work never runs a command you did not accept");
    }

    /// <summary>Where <paramref name="commit"/> left its line: the merge base with the line's tip here; null where git names no line or base.</summary>
    private static async Task<string?> LeftAsync(string checkout, string commit, CancellationToken ct)
    {
        if (await HandbackReading.LineAsync(checkout, ct).ConfigureAwait(false) is not { } line) return null;
        var (code, stdout, _) = await WorkingTree.GitAsync(checkout, ["merge-base", line, commit], ct).ConfigureAwait(false);
        return code == 0 && stdout.Trim() is { Length: > 0 } based ? based : null;
    }
}
