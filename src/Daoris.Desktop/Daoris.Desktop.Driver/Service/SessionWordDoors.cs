using Daoris.Contracts.Sessions;
using static Daoris.Driver.RefusalWords;

namespace Daoris.Driver;

/// <summary>
/// The doors for the person's words on a session record (the service's session word routes): what the person adds to a running session, what
/// they say to a parked or ended one, and the take-back, the move, the attempt and the take of those words. A class over the client, reached as
/// its <see cref="ServiceClient.Words"/> and by the client's own doors of the same names. Whether a word was kept is an answer, never an
/// exception: a refusal carries the service's word beside its sentence, and a host without the door says so.
/// </summary>
public sealed class SessionWordDoors
{
    private readonly ServiceClient _service;

    internal SessionWordDoors(ServiceClient service) => _service = service;

    private string NoDoor(string what, ServiceReply reply) =>
        $"the service at {_service.BaseUrl} has no door for {what} ({reply.Status}) — is it older than this driver?";

    /// <summary>
    /// Tell the service what the person added to a running session (DRIFT1a2, D133 §1), which keeps it on the ask that session's work is for:
    /// the words verbatim, by the session's own id. <c>kept: false</c> is a session on no ask, with the service's sentence, and a refusal or a
    /// host without the door is false with a sentence saying which.
    /// </summary>
    public async Task<(bool Kept, string Message)> AddedAsync(string id, string text, CancellationToken ct = default)
    {
        var sent = await _service.SendAsync(new AddToSession(id, text), ct).ConfigureAwait(false);
        if (sent.Answer is { } added) return (added.Kept, added.Message ?? "");
        if (sent.Reply.Ok || Refused(sent.Reply) is not { } refusal) return (false, NoDoor("what the person adds", sent.Reply));
        return (false, refusal.Error ?? sent.Reply.Payload);
    }

    /// <summary>
    /// Keep the person's words on a parked or ended session of this machine's for it to go on with (MSG1a's say door, MSG1d, D137 §5.3): the
    /// words as said, and the names of what they gave with them, never where a file is kept. The kept word comes back with the id the record's
    /// events say again where the session took it.
    /// </summary>
    /// <param name="from">
    /// Their author where that is not the person (COWORK1b, <see cref="DaorisWords.From"/>): kept as Daoris's, never read as the person's.
    /// </param>
    /// <param name="key">The producer's key (COWORK1b): a say under a key the record holds is answered with the word kept.</param>
    /// <param name="level">When the sender asked the words to reach a working session (COWORK1c), kept beside them; null keeps none.</param>
    /// <param name="running">
    /// This driver runs the session and keeps the words for its run (COWORK1c, the message-urgency design §6): a running record keeps them
    /// only so, and a service from before refuses them as <c>running</c>, as it always did.
    /// </param>
    /// <param name="supervised">
    /// The supervisor's release (SUPERVISOR1a-i, D208; the supervisor design §3.4): its decision, question and choice, sent with no text, since
    /// the service composes the word from the choice. Null for every other say.
    /// </param>
    /// <param name="ending">
    /// The shell keeps the words on a record that winds up or moves (FOUNDATION1o, the foundation design §3.4 rule 4), for its next part to go on with.
    /// </param>
    /// <param name="question">The question id a person's press answers (COMMS1e), kept on the word.</param>
    public async Task<SayAnswer> SayAsync(
        string id, string text, IReadOnlyList<string>? files = null, CancellationToken ct = default, string? channel = null,
        string? from = null, string? key = null, string? level = null, bool running = false,
        (string Decision, string Question, string Choice)? supervised = null, bool ending = false, string? question = null)
    {
        // COMMS1e: the person's press names its question, which the host's say door checks.
        var presses = question is { Length: > 0 } && supervised is null;
        var request = new SaySession(
            id, supervised is null ? text : null,
            files is { Count: > 0 } ? files : null,
            // The channel message these words are, which the ask holds already; an older service ignores it.
            channel is { Length: > 0 } ? channel : null,
            from is { Length: > 0 } ? from : null,
            key is { Length: > 0 } ? key : null,
            level is { Length: > 0 } ? level : null,
            running ? true : null,
            supervised?.Decision,
            supervised is { } release ? release.Question : presses ? question : null,
            supervised?.Choice,
            // Every keyed say reads a settled answer, the supervisor's release excepted, whose door reads no settlement. A host before the
            // row ignores the field and answers as it did.
            key is { Length: > 0 } && supervised is null ? true : null,
            ending ? true : null);
        var sent = await _service.SendAsync(request, ct).ConfigureAwait(false);
        if (sent.Answer is { } said)
        {
            // Settled before a word (row p): a success naming the record the key's word left carries no `said`, so it is read first and no
            // reader below it finds a word to act on.
            if (said.Settled is { Length: > 0 } settled) return new SayAnswer(true, said.Message ?? "") { Settled = settled };
            return new SayAnswer(true, said.Message ?? "") { Word = said.Said is { } word ? SessionReads.ReadWord(word) : null };
        }

        if (sent.Reply.Ok || Refused(sent.Reply) is not { } refusal) return new SayAnswer(false, NoDoor("words to a session that is not running", sent.Reply));
        // FOUNDATION1d-ii (the design §3.4 rule 2): `continued` names the line's head beside its word, at the status of every other refusal.
        return new SayAnswer(false, refusal.Error ?? sent.Reply.Payload)
        {
            Refusal = refusal.Refusal,
            Head = refusal.Refusal == SayAnswer.Continued && refusal.Head is { Length: > 0 } head ? head : null,
        };
    }

    /// <summary>
    /// Take back one of the person's words waiting on a record before a session took it, or with <paramref name="text"/> reword it (SAIDBACK1a's
    /// take-back door, D137 §2.4): the old word leaves, and a reword's new words wait last under a new id, which comes back. A word no longer
    /// waiting is <c>taken</c>.
    /// </summary>
    public async Task<TakeBackAnswer> TakeBackAsync(string id, string word, string? text = null, CancellationToken ct = default)
    {
        var sent = await _service.SendAsync(new TakeBackSaid(id, word, text), ct).ConfigureAwait(false);
        if (sent.Answer is { } back)
        {
            return new TakeBackAnswer(back.Done, back.Message ?? "", back.Why, back.Said is { } said ? SessionReads.ReadWord(said) : null);
        }

        if (sent.Reply.Ok || Refused(sent.Reply) is not { } refusal) return new TakeBackAnswer(false, NoDoor("taking back words a session has not taken", sent.Reply));
        return new TakeBackAnswer(false, refusal.Error ?? sent.Reply.Payload, refusal.Refusal);
    }

    /// <summary>
    /// Move one of the person's words held on a part's record to the head of the line <paramref name="to"/> is on (LINESTOGETHER1b's move door):
    /// taken off and kept there in one step, a retry answered moved again. A host without the door says so with no word.
    /// </summary>
    public async Task<MoveAnswer> MoveSaidAsync(string id, string word, string? to = null, CancellationToken ct = default)
    {
        var sent = await _service.SendAsync(new MoveSaid(id, word, to is { Length: > 0 } ? to : null), ct).ConfigureAwait(false);
        if (sent.Answer is { } moved)
        {
            return new MoveAnswer(moved.Moved, moved.Message ?? "", moved.Why)
            {
                To = moved.To,
                Again = moved.Again ?? false,
                Word = moved.Said is { } said ? SessionReads.ReadWord(said) : null,
            };
        }

        if (sent.Reply.Ok || Refused(sent.Reply) is not { } refusal) return new MoveAnswer(false, NoDoor("moving words to a session's head", sent.Reply));
        return new MoveAnswer(false, refusal.Error ?? sent.Reply.Payload, refusal.Refusal) { To = refusal.Head };
    }

    /// <summary>
    /// Mark these waiting words attempted on this machine's record, before the wire (COWORK1c's attempt door, the message-urgency design §6): the
    /// first moment kept, so a shell that comes up again never sends one twice. <see cref="AttemptAnswer.Marked"/> is the words asked less those
    /// the service answers as <c>unmarked</c>, and is empty where the mark was not acknowledged at all (FOUNDATION1 row p, the design §3.4 rule 5).
    /// </summary>
    public async Task<AttemptAnswer> AttemptAsync(string id, IReadOnlyList<string> said, CancellationToken ct = default)
    {
        var sent = await _service.SendAsync(new AttemptSaid(id, [.. said]), ct).ConfigureAwait(false);
        if (sent.Answer is { } attempted)
        {
            // The words the answer names as no longer waiting; every other word asked was marked.
            var unmarked = (attempted.Unmarked ?? []).OfType<string>().ToList();
            return new AttemptAnswer(true, attempted.Message ?? "", [.. said.Except(unmarked, StringComparer.Ordinal)], unmarked);
        }

        if (sent.Reply.Ok || Refused(sent.Reply) is not { } refusal) return new AttemptAnswer(false, NoDoor("a word's attempt", sent.Reply), [], said);
        return new AttemptAnswer(false, refusal.Error ?? sent.Reply.Payload, [], said);
    }

    /// <summary>
    /// Take the person's words off a record once a session took them (MSG1a's door, MSG1b): by their ids, so a word said after the driver read
    /// the record stays for the next run; <paramref name="by"/> names the new session a fallback handed them to. A refusal, or a host without
    /// the door, is false with a sentence saying which.
    /// </summary>
    public async Task<(bool Ok, string Message)> TakenAsync(string id, IReadOnlyList<string> said, string? by = null, CancellationToken ct = default)
    {
        var sent = await _service.SendAsync(new TakeSaid(id, [.. said], by), ct).ConfigureAwait(false);
        if (sent.Answer is { } taken) return (true, taken.Message ?? "");
        if (sent.Reply.Ok || Refused(sent.Reply) is not { } refusal) return (false, NoDoor("the words a session took", sent.Reply));
        return (false, refusal.Error ?? sent.Reply.Payload);
    }
}
