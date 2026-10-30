using Microsoft.Data.Sqlite;

namespace Daoris.Driver;

/// <summary>
/// A merge rule's press carried by the integration transport (INTEGRATE1g; the integration design §4.2's named legacy exception, C9, C18): the
/// press is admitted at the one door under <see cref="IntegrationRequests.Legacy"/> and its line run to rest at once, so the person's checkout is
/// only ever fast-forwarded to a merge made in the line's integration tree, with today's message and the repository's own hooks.
/// </summary>
internal static class IntegrationPresses
{
    /// <summary>
    /// Merge <paramref name="tip"/> of <paramref name="branch"/> into <paramref name="line"/> as the press's: true where the line now holds it,
    /// else the sentence the press is refused with (the conflict or the hook in today's words, or what holds it).
    /// </summary>
    public static async Task<(bool Merged, string? Refused)> MergeAsync(
        string home, DriverConfig config, string root, string repository, string? workspace, string line, string branch, string tip, CancellationToken ct)
    {
        try
        {
            var store = new IntegrationStore(home);
            var admitted = new IntegrationRequests(new HomeIntegrationRecords(home), store, () => config).Press(repository, workspace, branch, tip, line);
            if (admitted is not { Admitted: true, Request: { } request }) return (false, $"Nothing was merged: {admitted.Message}");

            var asks = new IntegrationSweepAsks(
                new IntegrationReplayAsk(repository, request.Workspace, root, line) { Config = config },
                new IntegrationPromoteAsk(repository, request.Workspace, root, line, config, home));
            var run = await new IntegrationLines(store, DaorisHome.Entry(home, HomeEntry.Trees)).RunAsync(asks, ct).ConfigureAwait(false);
            return store.Request(request.Id) switch
            {
                { ClosedWhy: IntegrationRequestCloses.Settled } => (true, null),
                { ClosedAt: not null } closed => (false, store.MemberOf(closed.Id)?.Said ?? $"its integration closed `{closed.ClosedWhy}`: nothing was merged."),
                _ => (false, $"`{branch}` waits to be merged into `{line}`, and is tried again at each look: {run.Said.LastOrDefault() ?? "its line's run did not reach it now."}"),
            };
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or SqliteException or IntegrationRefusedException)
        {
            return (false, $"the integration's record could not be kept ({error.Message}), so nothing was merged.");
        }
    }
}
