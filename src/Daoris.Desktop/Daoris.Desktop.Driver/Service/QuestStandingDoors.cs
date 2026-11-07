using Daoris.Contracts.Knowledge;
using Daoris.Contracts.Quests;
using Daoris.Contracts.Sync;

namespace Daoris.Driver;

/// <summary>
/// The doors for where a quest stands on this machine: its claim (the service's sync routes), its handback (the quest file routes), and what the
/// host knows of the closure graph's operations (the service's status). A class over the client, reached as its <see cref="ServiceClient.Standing"/>
/// and by the client's own doors of the same names.
/// </summary>
public sealed class QuestStandingDoors
{
    private readonly ServiceClient _service;

    internal QuestStandingDoors(ServiceClient service) => _service = service;

    /// <summary>Where a circle stands on this machine, read from its host, without reaching the remote.</summary>
    public async Task<SyncStanding> SyncAsync(string workspace, CancellationToken ct = default)
    {
        var standing = (await _service.SendAsync(new ReadSyncStanding(workspace), ct).ConfigureAwait(false)).Answered();
        return new SyncStanding(
            standing.Workspace, standing.Wired, standing.Ahead, standing.Behind, standing.Conflicts, standing.Synced, standing.Tried,
            standing.Problem,
            // Absent where no pass heard the handshake, which is never read as every machine declared.
            standing.Undeclared);
    }

    /// <summary>
    /// Where this machine's claim on a quest stands: none, held, unconfirmed or lost — how the driver learns that a session it is
    /// running took a quest another machine took first.
    /// </summary>
    public async Task<string> ClaimAsync(string id, CancellationToken ct = default) =>
        (await ClaimTakeAsync(id, ct).ConfigureAwait(false)).Claim;

    /// <summary>
    /// <see cref="ClaimAsync"/>, with why a lost claim lost where the host says: <c>source-reopened</c> where the take lost to
    /// the person's reopen of the work its step grew from; null otherwise, and from an older host.
    /// </summary>
    public async Task<(string Claim, string? Why)> ClaimStandingAsync(string id, CancellationToken ct = default)
    {
        var (claim, why, _, _) = await ClaimTakeAsync(id, ct).ConfigureAwait(false);
        return (claim, why);
    }

    /// <summary>
    /// <see cref="ClaimStandingAsync"/>, with the person's bring-forward that made the take where the host names one:
    /// what a look carries a stood-down session on by. Null from an older host, and for any other take. And when the take
    /// happened, null from an older host.
    /// </summary>
    public async Task<(string Claim, string? Why, ForwardTake? Forward, DateTimeOffset? Taken)> ClaimTakeAsync(string id, CancellationToken ct = default)
    {
        var claim = (await _service.SendAsync(new ReadClaim(id), ct).ConfigureAwait(false)).Answered();
        return (claim.Claim, claim.Why, claim.Forward is { } forward ? new ForwardTake(forward.Machine, forward.Sequence, forward.At, forward.Standing) : null, claim.Taken);
    }

    /// <summary>
    /// One quest's handback as the service answers it (<c>GET /api/handbacks/{id}</c>), by the quest's id or the id of the session
    /// that served it: what <c>sessions handback</c> prints. Null from a host without the door, which answers the route not found; the door
    /// answers every id, one naming nothing as not found in its own answer, so its own answer is never a 404.
    /// </summary>
    /// <exception cref="DriverException">The host answered the door with a failure.</exception>
    public async Task<HandbackResponse?> HandbackAsync(string id, CancellationToken ct = default)
    {
        var sent = await _service.SendAsync(new ReadHandback(id.TrimStart('#')), ct).ConfigureAwait(false);
        if (sent.Reply.Status == (int)System.Net.HttpStatusCode.NotFound) return null;
        return sent.Answer ?? throw new DriverException($"the service would not read `{id}`'s handback: {sent.Reply.Payload}");
    }

    /// <summary>
    /// Whether this client's host knows a closure graph's operations and doors (by the handshake): <c>/api/status</c>'s
    /// <c>closure</c> and <c>closeOutcome</c>, the outcome door's recovery: both must be answered. An older host answers none,
    /// or <c>closure</c> alone, and is not asked to keep what it cannot recover.
    /// </summary>
    public async Task<bool> KnowsClosureAsync(CancellationToken ct = default) =>
        (await _service.SendAsync(new ReadStatus(), ct).ConfigureAwait(false)).Answered() is { Closure: true, CloseOutcome: true };

    /// <summary>
    /// Whether this client's host judges a bind in a workspace synced through a remote by the remote's handshake:
    /// <c>/api/status</c>'s <c>closeRemote</c>, beside the two <see cref="KnowsClosureAsync"/> reads. An older host answers none, and is
    /// asked no bind there.
    /// </summary>
    public async Task<bool> KnowsRemoteClosureAsync(CancellationToken ct = default) =>
        (await _service.SendAsync(new ReadStatus(), ct).ConfigureAwait(false)).Answered() is { Closure: true, CloseOutcome: true, CloseRemote: true };
}
