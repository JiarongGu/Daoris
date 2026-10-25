using System.Runtime.CompilerServices;

namespace Daoris.Driver;

/// <summary>
/// The workplace's answers to the two ports the service declares (FOUNDATION1 mod-iv, the design §14.3): its git read for the remote sync
/// (<see cref="ICheckoutReads"/>) and its lock let go when a session's record ends (<see cref="SessionEnds"/>). Installed when the
/// driver's assembly loads, before any client or sync can run, so every host, the loop and every test read and release as they always did
/// and no construction site hands them anything.
/// </summary>
internal sealed class WorkingTreeReads : ICheckoutReads
{
    [ModuleInitializer]
    internal static void Install()
    {
        CheckoutReads.Current = new WorkingTreeReads();
        SessionEnds.Ended = record => SessionLocks.LetGoEndedAsync(record);
    }

    public async Task<string?> LineAsync(string root, string repository, string? workspace, DriverConfig choices, CancellationToken ct) =>
        (await CanonicalLine.ResolveAsync(root, repository, workspace, choices, ct).ConfigureAwait(false)).Branch;

    public Task<TreeProvenance?> ProvenanceAsync(string root, CancellationToken ct) => WorkingTree.ProvenanceAsync(root, ct);

    public async Task<bool> CleanAsync(string root, CancellationToken ct) =>
        (await WorkingTree.CleanAsync(root, ct).ConfigureAwait(false)).Clean;

    public Task<bool> UnmodifiedAsync(string root, string path, CancellationToken ct) => WorkingTree.UnmodifiedAsync(root, path, ct);

    public Task<TreeRelation> RelationAsync(string root, string held, string head, CancellationToken ct) =>
        WorkingTree.RelationAsync(root, held, head, ct);
}
