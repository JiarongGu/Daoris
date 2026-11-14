using System.Text.Json;
using Daoris.Contracts.Participants;
using static Daoris.Driver.Json;

namespace Daoris.Driver;

/// <summary>What the host answered a helper's record being opened: the record, or why not, in the host's sentence.</summary>
/// <param name="Session">The helper's session id; null where it is refused.</param>
/// <param name="Message">The host's sentence: its yes, or its refusal verbatim.</param>
public sealed record HelperOpened(string? Session, string Message)
{
    public bool Opened => Session is not null;
}

/// <summary>
/// The local host's helper doors, as the driver asks them (<c>POST /api/helpers</c> and <c>/hand</c>): a class over
/// the service's client, reached as its <c>ServiceClient.Helpers</c>, a member the participants declare over the client (<see cref="HelperDoorsOfClient"/>).
/// </summary>
public sealed class HelperDoors
{
    private readonly ServiceClient _service;

    internal HelperDoors(ServiceClient service) => _service = service;

    /// <summary>
    /// Open a helper's record: a chat on no quest that serves <paramref name="asker"/>, in the asker's repository
    /// and workspace, in <paramref name="tree"/>, the clone the driver made, as <paramref name="adapter"/> and its account. A
    /// refusal is an answer, the host's sentence, not an exception, and so is a host older than the door.
    /// </summary>
    public async Task<HelperOpened> OpenAsync(
        string asker, string adapter, string tree, string? account, string? doorVersion, string? baseCommit, CancellationToken ct = default,
        string? scoutIn = null)
    {
        var route = new OpenHelper(asker, adapter, tree, account, doorVersion, baseCommit, scoutIn);
        var door = RouteRequest.Of(route).Path;
        var (ok, status, payload, root) = await _service.SendJsonAsync(route, ct).ConfigureAwait(false);
        if (root is not { } answer) return new(null, $"the service at {_service.BaseUrl} has no `{door}` door ({status}) — is it older than this driver?");
        if (!ok) return new(null, Text(answer, "error") ?? payload);

        var session = answer.ValueKind == JsonValueKind.Object && answer.TryGetProperty("session", out var record) ? record : default;
        var id = Text(session, "id");
        if (id is null) return new(null, $"the service at {_service.BaseUrl} answered `{door}` with no record.");

        // The door the record opened by, for the session log: a helper's record is a chat that serves another session.
        _service.RecordOpened(new SessionOpened(id, HelperPass.OpenedKind, Text(session, "door") ?? adapter, Text(session, "repository") ?? "")
        {
            Workspace = Text(session, "workspace"),
        });
        return new(id, Text(answer, "message") ?? "");
    }

    /// <summary>
    /// Hand a helper's answer to the session it serves (<c>POST /api/helpers/{helper}/hand</c>): the host keeps <paramref name="text"/>
    /// on its record as a word marked as the helper's, once, which reopens it at the driver's next look. A refusal is an
    /// answer, the host's sentence, not an exception, and so is a host older than the door.
    /// </summary>
    /// <param name="running">
    /// This driver runs the working asker and hands an urgent ask's answer for its run's next step, kept on its running record;
    /// a host from before refuses it as running, and the answer waits for the asker's end.
    /// </param>
    /// <param name="level">The ask's level, kept beside the word: <c>urgent</c> where the ask is, else none.</param>
    /// <remarks>
    /// The desk keeps the word before its answer is made, so only a refusal it gave says
    /// nothing is kept (<see cref="SolicitedHand.Read"/>). No answer at all (a timeout, a broken connection) is
    /// <see cref="SolicitedFate.Uncertain"/>: the word may wait on the line. A word already waiting is answered with the record that
    /// holds it, <see cref="SolicitedFate.Waiting"/>.
    /// </remarks>
    public async Task<SolicitedAnswer> HandAsync(
        string helper, string text, CancellationToken ct = default, bool running = false, string? level = null)
    {
        // The hand reads a settled answer, a key its line settled.
        var route = new HandHelper(helper, text, running ? true : null, level is { Length: > 0 } ? level : null, true);
        var door = RouteRequest.Of(route).Path;

        bool ok;
        int status;
        string payload;
        JsonElement? root;
        try
        {
            (ok, status, payload, root) = await _service.SendJsonAsync(route, ct).ConfigureAwait(false);
        }
        catch (Exception lost) when (lost is HttpRequestException || (lost is OperationCanceledException && !ct.IsCancellationRequested))
        {
            return SolicitedHand.Lost(door, _service.BaseUrl, lost.Message);
        }

        // Settled before a word: the key's word left a record of the asker's line, so nothing is kept and there is no word.
        return SolicitedHand.Read(ok, status, root, payload, door, _service.BaseUrl);
    }
}

/// <summary>
/// The client's helper doors, as the participants' own member of it: the door classes are their owners',
/// over the service's client, which names none of them; each read hands a door over the client, which holds no state of its own.
/// </summary>
public static class HelperDoorsOfClient
{
    extension(ServiceClient client)
    {
        /// <summary>The local host's helper doors, over this client.</summary>
        public HelperDoors Helpers => new(client);
    }
}
