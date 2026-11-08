using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// The words held for a record to end, in the order said, and the pump that tries them each time the record moves or a slow look
/// passes until the record keeps them all. They are written to the held file as each is held or leaves, so a restart in that moment
/// does not lose them (<see cref="HeldWordsDisk"/>); the next shell reads them back as it starts.
/// </summary>
internal sealed class HeldWordsQueue(SessionWords owner, DriverLoop loop, WordsShown shown, CancellationToken stopping)
{
    private readonly object _gate = new();
    private readonly Dictionary<string, Waiting> _waiting = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The words held for one session's record to end, in the order said, and what tells them it moved.</summary>
    public sealed class Waiting
    {
        public SemaphoreSlim Turn { get; } = new(1, 1);

        public Queue<HeldSay> Words { get; } = new();

        public TaskCompletionSource Moved { get; set; } = Signal();

        public bool Pumping { get; set; }
    }

    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Waiting WaitingFor(string id)
    {
        lock (_gate)
        {
            if (!_waiting.TryGetValue(id, out var waiting)) _waiting[id] = waiting = new Waiting();
            return waiting;
        }
    }

    /// <summary>What tells the held words for this record that it moved, read before a say so a move while it is on its way is not missed.</summary>
    public TaskCompletionSource MovedOf(Waiting waiting)
    {
        lock (_gate) return waiting.Moved;
    }

    /// <summary>The words held for a record are tried now, and what a word said to it now would do is told again.</summary>
    public void Wake(string session)
    {
        TaskCompletionSource? told = null;
        Waiting? idle = null;
        lock (_gate)
        {
            if (_waiting.TryGetValue(session, out var waiting))
            {
                told = waiting.Moved;
                waiting.Moved = Signal();
                // Words held with no pump running (one a failure nothing read ended) are tried again at this move.
                if (!waiting.Pumping && waiting.Words.Count > 0) idle = waiting;
            }
        }

        told?.TrySetResult();
        if (idle is not null)
        {
            var now = Signal();
            now.SetResult();
            StartPump(session, idle, now);
        }

        owner.Tell(session);
    }

    public void StartPump(string id, Waiting waiting, TaskCompletionSource moved)
    {
        lock (_gate)
        {
            if (waiting.Pumping) return;
            waiting.Pumping = true;
        }

        _ = Task.Run(() => PumpAsync(id, waiting, moved));
    }

    /// <summary>
    /// Held words, tried each time the record moves here or a slow look passes, until the record keeps them all. A word the
    /// service refuses for good is said in the conversation; one it could not answer for is tried again.
    /// </summary>
    private async Task PumpAsync(string id, Waiting waiting, TaskCompletionSource moved)
    {
        var ct = stopping;
        try
        {
            while (true)
            {
                await Task.WhenAny(moved.Task, Task.Delay(owner.Poll, ct)).ConfigureAwait(false);
                ct.ThrowIfCancellationRequested();

                await waiting.Turn.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    moved = MovedOf(waiting);
                    // While its conversation is being handed over or moved the words wait; once it went on in another,
                    // they go there, never onto the record that ended.
                    var (waits, next, moving) = await WhereAsync(id, ct).ConfigureAwait(false);
                    // A host with the marks keeps each held word on its record, an older shell's from its file among them.
                    var keeps = !waits && await owner.KeepsAsync(ct).ConfigureAwait(false);
                    while (!waits && waiting.Words.TryPeek(out var word))
                    {
                        if (next is not null)
                        {
                            if (!await PassAsync(id, next, word, ct).ConfigureAwait(false)) break;
                            Taken(waiting);
                            continue;
                        }

                        if (loop.Service is not { } service) break;
                        SayAnswer said;
                        try
                        {
                            said = await service.SayAsync(id, word.Text, word.Files, ct, key: word.Key, ending: keeps, question: word.Question).ConfigureAwait(false);
                        }
                        catch (Exception error) when (WordsRead.Unanswered(error) && !ct.IsCancellationRequested)
                        {
                            break;
                        }

                        // The record is no longer its line's head, which the answer names. The
                        // word is said again there once, under its key, as the forward to a conversation it went on in is said; off the held
                        // file once the head took it, and tried again where nothing could answer. A second `continued` there follows the next
                        // head, and a walk that comes back to a record it asked ends, the word said lost.
                        if (said.Head is { } head)
                        {
                            var walk = new HeadWalk(id);
                            if (!walk.On(head)) shown.Lost(id, WordsNever.Continued, walk.Ended(head));
                            else if (!await PassAsync(id, head, word, ct, walk).ConfigureAwait(false)) break;
                            Taken(waiting);
                            continue;
                        }

                        if (said.Refusal == "running") break;
                        // Off the file before it is shown: the record has it now, and a restart must not say it twice.
                        Taken(waiting);
                        // Settled, a retry of a word whose first say has been kept and taken, its answer lost: off the
                        // file, and nothing shown, since no word came back and the conversation shows it already.
                        if (said.Settled is not null) continue;
                        if (said.Kept) owner.Kept(id, said, word, keeps, moving);
                        else shown.Lost(id, said);
                    }

                    if (waiting.Words.Count == 0)
                    {
                        EndPump(waiting);
                        return;
                    }
                }
                catch
                {
                    // Whatever ends the pump with words held lets the record's next move, or the next word held, start it again.
                    EndPump(waiting);
                    throw;
                }
                finally
                {
                    waiting.Turn.Release();
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // The loop is closing: what is held stays in its file for the next shell to try.
        }
    }

    private void EndPump(Waiting waiting)
    {
        lock (_gate) waiting.Pumping = false;
    }

    /// <summary>
    /// Where held words go now: nowhere yet while the record's conversation is handed over or moved (its box stands), or
    /// where the service did not answer; to the conversation it went on in where one did; else to the record itself, as before.
    /// A host with the marks keeps them on the moving record, which its open moves (<c>Moving</c>).
    /// </summary>
    private async Task<(bool Waits, string? Next, bool Moving)> WhereAsync(string id, CancellationToken ct)
    {
        try
        {
            if (await WordsRead.OfAsync(loop, id, ct).ConfigureAwait(false) is not { Record: { } record } read) return (false, null, false);
            if (owner.TakeWaits(record)) return await owner.KeepsAsync(ct).ConfigureAwait(false) ? (false, null, true) : (true, null, false);
            // Where the host has the `head` mark, its say door names the head, and the driver's own walk stands aside.
            return (false, (await read.JudgedAsync(loop, ct).ConfigureAwait(false)).Next, false);
        }
        catch (Exception error) when (WordsRead.Unanswered(error) && !ct.IsCancellationRequested)
        {
            // Nothing known is nothing claimed: the words are tried again at the record's next move, or the next look.
            return (true, null, false);
        }
    }

    /// <summary>
    /// A held word said to the conversation its record went on in, at the door it is first said at, with its files read
    /// back from where they were kept: that conversation's turns take it where it runs here, else its record keeps it and it is shown
    /// there, as any word said to it would be. True where it is off this record's queue: taken, or refused and said so here. False
    /// where nothing could answer now, so it is tried again.
    /// </summary>
    /// <param name="walk">The records this word is said to on its way to its line's head; null where it starts at <paramref name="next"/>.</param>
    private async Task<bool> PassAsync(string id, string next, HeldSay word, CancellationToken ct, HeadWalk? walk = null)
    {
        WordsAnswer? said;
        try
        {
            // Said there as the first say did, with its key, level and display choice; a word with no key
            // says at the default level.
            said = await owner.SayAsync(next, word.Text, Uploads(id, word.Files), preface: null, word.Door, ct, word.Level, word.Key, word.Choice, word.Question, walk ?? new HeadWalk(next))
                .ConfigureAwait(false);
        }
        catch (DriverException)
        {
            // The service did not answer for the conversation it went on in (its files were taken once already): tried again.
            return false;
        }

        if (said is null) return false;
        if (said.Running) owner.KeepOnAsk(said.At ?? next, word.Text);
        if (!said.Sent) shown.Lost(id, said.Why, said.Message);
        return true;
    }

    /// <summary>A held word's files, read back from where its record kept them; one gone since is left out, as a resumed run's is.</summary>
    private IReadOnlyList<ChatUpload> Uploads(string id, IReadOnlyList<string> names)
    {
        var uploads = new List<ChatUpload>();
        foreach (var name in names)
        {
            try
            {
                if (ChatFiles.Find(loop.Home, id, name) is { } kept) uploads.Add(new ChatUpload(name, File.ReadAllBytes(kept.Path)));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // Held open or gone since being kept: the words still go.
            }
        }

        return uploads;
    }

    /// <summary>A word held behind the others for this record, and the file written with it.</summary>
    public void Hold(Waiting waiting, HeldSay word)
    {
        lock (_gate)
        {
            waiting.Words.Enqueue(word);
            Save();
        }
    }

    /// <summary>The first word held for this record, kept or refused for good, off the queue and the file.</summary>
    private void Taken(Waiting waiting)
    {
        lock (_gate)
        {
            waiting.Words.Dequeue();
            Save();
        }
    }

    /// <summary>Called under the gate, where every queue is changed, so the file is never written from a queue mid-change.</summary>
    private void Save() =>
        HeldWordsDisk.Save(
            SessionWords.HeldPath(loop.Home),
            [.. _waiting.SelectMany(pair => pair.Value.Words.Select(word => (Session: pair.Key, Word: word)))]);

    /// <summary>
    /// The words an earlier shell held when it closed, read back as this one starts: each queued for its record
    /// in the order said, and tried at once, then as any held words are, since the record may well have ended while no shell
    /// ran. A file that does not read holds nothing, and the next word held replaces it.
    /// </summary>
    public void Restore()
    {
        List<(string Session, HeldSay Word)> held;
        try
        {
            held = HeldWordsDisk.Read(SessionWords.HeldPath(loop.Home));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            return;
        }

        foreach (var words in held.GroupBy(each => each.Session, StringComparer.OrdinalIgnoreCase))
        {
            var waiting = WaitingFor(words.Key);
            lock (_gate)
            {
                foreach (var (_, word) in words) waiting.Words.Enqueue(word);
            }

            var now = Signal();
            now.SetResult();
            StartPump(words.Key, waiting, now);
        }
    }
}
