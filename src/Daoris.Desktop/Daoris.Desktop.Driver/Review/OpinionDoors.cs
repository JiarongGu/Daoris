using System.Text.Json;
using Daoris.Contracts;
using Daoris.Contracts.Review;
using static Daoris.Driver.Json;

namespace Daoris.Driver;

/// <summary>
/// The local host's opinion doors, as the driver asks them (XAGENT1c–XAGENT1e): a pass's ask, an opinion's read and its findings'
/// hand: a class over the service's client, reached as its <c>ServiceClient.Opinions</c> (PART8), a member the review declares over the client (<see cref="OpinionDoorsOfClient"/>).
/// </summary>
public sealed class OpinionDoors
{
    private readonly ServiceClient _service;

    internal OpinionDoors(ServiceClient service) => _service = service;

    /// <summary>
    /// Ask the local host for a pass (XAGENT1c, D155 point 11): it keeps the opinion and opens its reviewer's record in one
    /// transaction, a chat on no quest naming the opinion, in its own copy, as its account. A refusal is an answer, not an
    /// exception, and so is a host older than the door.
    /// </summary>
    public async Task<OpinionAsked> AskAsync(OpinionAskBody body, CancellationToken ct = default)
    {
        const string Door = "/api/opinions";
        var sent = await _service.SendAsync(body.Route(), ct).ConfigureAwait(false);
        var payload = sent.Reply.Payload;
        if (!IsJson(payload)) return new(null, null, $"the service at {_service.BaseUrl} has no `{Door}` door ({sent.Reply.Status}) — is it older than this driver?");
        if (!sent.Reply.Ok) return new(null, null, DriverHttp.ErrorOf(payload));

        if (sent.Answer is not { Opinion.Id: { } opinion, Session: { } session })
        {
            return new(null, null, $"the service at {_service.BaseUrl} answered `{Door}` with no opinion and no record.");
        }

        // The door the record opened by, for the session log (LOG1b): a reviewer's record is a chat that serves no quest.
        _service.RecordOpened(new SessionOpened(session.Id, OpinionPass.OpenedKind, session.Door ?? body.Adapter, session.Repository ?? body.Candidate.Repository)
        {
            Workspace = session.Workspace,
        });
        return new(opinion, session.Id, sent.Answer.Message ?? "");
    }

    private static bool IsJson(string payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// The opinion under <paramref name="id"/> as the local host answers it (XAGENT1c), with where its pass stands; null where
    /// there is none, or the host is older than the door.
    /// </summary>
    /// <exception cref="DriverException">The host refused for another reason.</exception>
    public async Task<OpinionView?> ReadAsync(string id, CancellationToken ct = default)
    {
        var sent = await _service.SendAsync(new ReadOpinion(id), ct).ConfigureAwait(false);
        if (sent.Reply.Status == (int)System.Net.HttpStatusCode.NotFound) return null;
        if (!sent.Reply.Ok) throw new DriverException(sent.Refusal);
        return OpinionViews.Read(sent.Answer);
    }

    /// <summary>
    /// Hand a first pass's findings to its working session (XAGENT1c's <c>POST /api/opinions/{id}/hand</c>; design §6.3): the
    /// host keeps them on its record as a word whose <c>by</c> names the opinion, in its fixed words, once, and answers the word (CHATFLOW1e): a
    /// chat run's runner takes it as its reserved turn's message, under the record's id, so it leaves the record once that turn is on the wire. A
    /// refusal is an answer, the host's sentence, not an exception, and so is a host older than the door. COMMS1b: the hand to the working session's
    /// line asks for a settled answer under the opinion's key, and its answer is read as the helper's and the partner's are
    /// (<see cref="SolicitedHand.Read"/>); a chat run's hand to the record it names asks none, as before.
    /// </summary>
    /// <param name="to">
    /// A chat run's record of the line to hand them to (CHATFLOW1e, the chat-flow design §6.1 (a)), live between turns or ended and newest;
    /// null for the working session itself.
    /// </param>
    public async Task<OpinionHand> HandWordAsync(string id, CancellationToken ct = default, string? to = null)
    {
        var route = new HandOpinion(id, to, to is null ? true : null);
        var door = RouteRequest.Of(route).Path;
        var (ok, status, payload, root) = await _service.SendJsonAsync(route, ct).ConfigureAwait(false);
        var answer = SolicitedHand.Read(ok, status, root, payload, door, _service.BaseUrl);
        if (!answer.Handed || root is not { ValueKind: JsonValueKind.Object } read) return new(answer, null, null);
        var opinion = read.TryGetProperty("opinion", out var kept)
            ? OpinionViews.Read(kept.Deserialize((System.Text.Json.Serialization.Metadata.JsonTypeInfo<OpinionResponse>)ContractJson.Options.GetTypeInfo(typeof(OpinionResponse))))
            : null;
        var word = read.TryGetProperty("said", out var said) ? SessionReads.ReadWord(said) : null;
        return new(answer, opinion, word);
    }
}

/// <summary>
/// What a hand of an opinion's findings came to: the door's answer as every solicited hand reads it, the opinion as it now stands and the word the
/// findings wait in, where the host answered them.
/// </summary>
public sealed record OpinionHand(SolicitedAnswer Answer, OpinionView? Opinion, SaidWordView? Word)
{
    /// <summary>Whether the findings are on the line or went there before: kept, waiting or settled.</summary>
    public bool Handed => Answer.Handed;

    public string Message => Answer.Message;
}

/// <summary>
/// The client's opinion doors, as the review's own member of it (FOUNDATION1 mod-iv, the design §14.2: the door classes are their owners', over
/// the service's client, which names none of them): each read hands a door over the client, which holds no state of its own.
/// </summary>
public static class OpinionDoorsOfClient
{
    extension(ServiceClient client)
    {
        /// <summary>The local host's opinion doors (XAGENT1c–XAGENT1e), over this client.</summary>
        public OpinionDoors Opinions => new(client);
    }
}
