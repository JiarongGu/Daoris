using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// The person's words waiting on a parked or ended record, taken back, reworded or moved to the line's head through the service's
/// doors (SAIDBACK1b, LINESTOGETHER1b): the service keeps them, and this tells the conversation and both queues what became of them.
/// </summary>
internal sealed class WordsQueueEdits(DriverLoop loop, Action<string> tell)
{
    /// <summary>The move's answer where the service's never came (LINESTOGETHER1b): not moved, nor refused, but not known.</summary>
    public const string NotKnown = "not-known";

    /// <summary>
    /// Take back one of the person's words waiting on a record, or reword it (SAIDBACK1b, D76). <see cref="QueueEdit.Taken"/> where
    /// nothing holds the word for this machine: no service yet, a word no longer on the record, a record that runs again or is gone, or
    /// a host without the door.
    /// </summary>
    /// <exception cref="DriverException">The service's refusal, in its sentence (what never goes on); or a service that did not answer.</exception>
    public async Task<QueueEdit> TakeBackAsync(string id, string word, string? text, CancellationToken ct)
    {
        if (await TakeBackToServiceAsync(id, word, text, ct).ConfigureAwait(false) is not { } answer) return QueueEdit.Taken;
        if (!answer.Done)
        {
            return answer.Why is null or "taken" or WordsNever.NotFound ? QueueEdit.Taken : throw new DriverException(answer.Message);
        }

        return QueueEdit.Done;
    }

    /// <summary>
    /// Move one of the person's words held on a part's record to the head (LINESTOGETHER1b, the review-flow design §2). Moved, the part
    /// settles the old word with the withdrawn note and the head shows the person's words under their new id; a retry the service
    /// answers moved already shows nothing again. Both queues are told again whatever it answers. A request whose answer never came is
    /// <see cref="NotKnown"/>: the move may have happened, and asked again under its key it says.
    /// </summary>
    /// <exception cref="DriverException">No service yet, or a host without the door, in its sentence.</exception>
    public async Task<MoveAnswer> MoveAsync(string id, string word, string to, CancellationToken ct)
    {
        var service = loop.Service ?? throw new DriverException(SessionWords.NotUp);
        MoveAnswer answer;
        try
        {
            answer = await service.MoveSaidAsync(id, word, to, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (WordsRead.Unanswered(error))
        {
            tell(id);
            tell(to);
            return new MoveAnswer(false, $"the service did not answer for session `{id}`, so whether word `{word}` moved is not known: {error.Message}", NotKnown);
        }

        tell(id);
        if (answer.To is { } at) tell(at);
        if (!answer.Moved) return answer.Why is null ? throw new DriverException(answer.Message) : answer;
        if (answer.Again || answer.To is not { } head) return answer;

        loop.Output.Append(id, $"— the person moved word {word} to session {head} before this session took it.");
        loop.Events.Keep(id, SessionNotes.Withdrawn(word), null);
        if (answer.Word is { } kept)
        {
            loop.Output.Append(head, $"— the person said, which the same session goes on with: {kept.Text}");
            loop.Events.Keep(head, LoopWords.Shown(kept, "screen"), line => loop.Output.Append(head, line));
        }

        loop.Said(head);
        return answer;
    }

    /// <summary>
    /// A terminal's <c>sessions take-back</c> (SAIDBACK1c, D137 §2.4): the shell's half, so the withdrawn note is written in the record
    /// of the conversation this shell holds, which the page may have open. Answered in the say's shape: sent is done, a refusal's code
    /// is <c>why</c>.
    /// </summary>
    public async Task<WordsHeld> HoldTakeBackAsync(SessionRequest request, CancellationToken ct)
    {
        if (request.Word is not { Length: > 0 } word) return WordsHeld.Failed("a take-back names the word to take back.");
        var back = await TakeBackToServiceAsync(request.Session, word, request.Text, ct).ConfigureAwait(false);
        if (back is null) return WordsHeld.Failed(SessionWords.NotUp);
        if (back.Done) return new WordsHeld(true, null, null) { Word = back.Word?.Id, Message = back.Message };
        return back.Why is { } why ? WordsHeld.Refused(why, back.Message) : WordsHeld.Failed(back.Message);
    }

    /// <summary>
    /// The service's take-back door asked, the queue told again whatever it answers, and, where it is done, the conversation
    /// settled and a reword's new words shown. Null where there is no service yet.
    /// </summary>
    private async Task<TakeBackAnswer?> TakeBackToServiceAsync(string id, string word, string? text, CancellationToken ct)
    {
        if (loop.Service is not { } service) return null;

        TakeBackAnswer answer;
        try
        {
            answer = await service.TakeBackAsync(id, word, text, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (WordsRead.Unanswered(error))
        {
            throw new DriverException($"the service did not answer for session `{id}`, so word `{word}` was not taken back: {error.Message}");
        }

        tell(id);
        if (!answer.Done) return answer;

        // QUEUEEDIT1a's line, by its code, which the page words; the one note shape every door keeps (WITHDRAWNLINE1).
        loop.Output.Append(id, $"— the person took back word {word} before the session took it.");
        loop.Events.Keep(id, SessionNotes.Withdrawn(word), null);
        if (answer.Word is { } kept)
        {
            loop.Output.Append(id, $"— the person said, which the same session goes on with: {kept.Text}");
            loop.Events.Keep(id, LoopWords.Shown(kept, "screen"), line => loop.Output.Append(id, line));
        }

        return answer;
    }
}
