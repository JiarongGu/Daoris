using System.Text.Json;
using Daoris.Contracts;
using Daoris.Contracts.Sessions;
using static Daoris.Driver.RefusalWords;

namespace Daoris.Driver;

/// <summary>
/// The doors that keep a fact on a session's record after its open (the service's session keep routes): the access mode of an Ask Daoris
/// conversation, the setup a resumed session runs under, the downloaded versions a start was handed, and the actor provenance. A class over the
/// client, reached as its <see cref="ServiceClient.Keeps"/> and by the client's own doors of the same names. A refusal is the ledger's sentence
/// (<see cref="DriverException"/>).
/// </summary>
public sealed class SessionKeepDoors
{
    private readonly ServiceClient _service;

    internal SessionKeepDoors(ServiceClient service) => _service = service;

    private async Task KeepAsync(IRoute<SessionActionResponse> route, string refused, CancellationToken ct)
    {
        var sent = await _service.SendAsync(route, ct).ConfigureAwait(false);
        if (!sent.Reply.Ok) throw new DriverException($"{refused}: {ErrorWords(sent.Reply.Payload) ?? sent.Reply.Payload}");
    }

    /// <summary>Record the access mode an Ask Daoris conversation going on runs under (HELPMODE1a2, D183 point 3), where the person's mode changed since it last ran.</summary>
    public Task HelpModeAsync(string sessionId, string mode, CancellationToken ct = default) =>
        KeepAsync(new KeepHelpMode(sessionId, mode), $"the service refused recording that Ask Daoris `{sessionId}` goes on {mode}", ct);

    /// <summary>Record the access setup a resumed driven session now runs under (AUTONOMY1b2, D184 point 1), where the person's setup changed since it started.</summary>
    public Task AccessAsync(string sessionId, IReadOnlyDictionary<string, string> access, CancellationToken ct = default) =>
        KeepAsync(new KeepAccess(sessionId, access), $"the service refused recording the setup session `{sessionId}` goes on under", ct);

    /// <summary>
    /// Record the downloaded versions a start of this session was handed (KEEP1b, D195 point 6; the cleanup design §3.5),
    /// <c>&lt;agent|tool&gt;/&lt;name&gt;/&lt;version&gt;</c> (<see cref="DownloadClearing.Handed"/>), which the record adds to those of its earlier
    /// starts: the downloads' rule keeps each while the session runs.
    /// </summary>
    public Task ResolvedAsync(string sessionId, IReadOnlyList<string> versions, CancellationToken ct = default) =>
        KeepAsync(new KeepResolved(sessionId, [.. versions]), $"the service refused recording the versions session `{sessionId}` was handed", ct);

    /// <summary>
    /// D205 §9.3: a record's provenance after its open, through the loopback-only later door, with no revision. A record this machine no
    /// longer has is no failure; any other refusal throws.
    /// </summary>
    public async Task ActorAsync(string id, string? choice = null, ActorAsked? asked = null, ActorRan? ran = null,
        object? changed = null, CancellationToken ct = default, ActorChosen? chosen = null)
    {
        JsonElement? Wired(object? value) => value is null ? null : JsonSerializer.SerializeToElement(value, ActorWire.Json);
        var sent = await _service.SendAsync(new KeepActor(id, choice, Wired(asked), Wired(changed), Wired(ran), Wired(chosen)), ct).ConfigureAwait(false);
        if (sent.Reply.Ok) _service.RecordActorKept(id);
        else if (sent.Reply.Status != 404) throw new DriverException($"the service refused actor provenance: {sent.Reply.Payload}");
    }

    /// <summary>
    /// <see cref="ActorAsync"/> where a start or a composer change keeps it: reporting never changes what runs (D205 row b), so a refusal or an
    /// unreachable service is logged and the run goes on. A cancellation still cancels.
    /// </summary>
    public async Task<bool> TryActorAsync(string id, MachineLog? log, string? choice = null, ActorAsked? asked = null,
        object? changed = null, CancellationToken ct = default, ActorChosen? chosen = null)
    {
        try
        {
            await ActorAsync(id, choice, asked, changed: changed, ct: ct, chosen: chosen).ConfigureAwait(false);
            return true;
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            log?.Write("warn", "actor.keep-failed", [("session", id)]);
            return false;
        }
    }
}
