namespace Daoris.Driver;

/// <summary>
/// The prompts a session has on the wire and not yet answered, oldest first, and the run's door every prompt and steer passes on its
/// way there: the one state that the run's thread, the reader's thread and the person's sends all change, each under one gate.
/// </summary>
/// <remarks>
/// <para><b>What the gate guards.</b> Every field here, and nothing else, is read and written under <c>_gate</c>: the open prompts and
/// each one's <see cref="OpenPrompt.Taken"/>, <see cref="OpenPrompt.Heard"/> and <see cref="OpenPrompt.Written"/>; the passages entered
/// and not yet admitted, and each one's <see cref="PromptPassage.TurnedAway"/> and <see cref="PromptPassage.Words"/>; whether the door
/// has shut; the words turned away and those handed back; the words an error left unacknowledged and those refused after they were
/// taken; whether an error answered the last prompt; the count of prompts opened; <see cref="Unreached"/>; and, as an answer leaves, the
/// retry's <see cref="TransientRetry.OpeningUnacknowledged"/>. Nothing is called out while the gate is held: the listener, the record,
/// the inbox and the wake are told by the caller once a member returns.</para>
///
/// <para><b>The order of the locks.</b> The wire's write lock is taken before the gate, never inside it: <see cref="Admit"/> and
/// <see cref="Shut"/> run under it. <see cref="Admit"/>'s condition reads the wake under the wake's own gate before this one is taken,
/// the order every look takes, so the two are never held together. The marks' chain has its own lock, never held with this one.</para>
///
/// <para><b>What a race would break.</b> A prompt opens as it is admitted, under the write lock, so the prompts open are those on the
/// wire, in the order written: opened before its write, the answer before it could hand the turn to words the agent never got; opened
/// outside the lock, two sends could stand in another order than on the wire, and the hand-off would take the wrong words. The door
/// shuts under the write lock, between two writes, so a passage is admitted (on the wire: answered, or counted untaken at the end) or
/// turned away (handed back), never both, which would send its words twice, and never neither, which would lose them. A passage is
/// entered before its words' mark is awaited, so a run that ends during the mark turns it away rather than lose it, and its admission
/// asks again. A word is taken once, by the answer before it, by a late take after an error, or by its own answer: read and set under
/// one hold, or the record tells it twice or never. A word whose prompt an error refused after it is taken is marked on the reader's
/// thread before the request faults, so its send reads the mark as it catches the refusal.</para>
/// </remarks>
internal sealed class OpenPrompts
{
    private readonly object _gate = new();
    private readonly LinkedList<OpenPrompt> _prompts = [];
    private readonly List<PromptPassage> _passing = [];
    private bool _shut;
    private readonly List<IAgentWord> _turnedAway = [];
    private IReadOnlyList<IAgentWord> _handedBack = [];
    private readonly List<IAgentWord> _unacknowledged = [];
    private readonly HashSet<IAgentWord> _refusedTaken = new(ReferenceEqualityComparer.Instance);

    /// <summary>An error answered the prompt before a word, which waits for the agent's next update to be taken.</summary>
    private bool _afterError;

    private int _promptCount;

    /// <summary>
    /// The words the run handed back unprompted, then every word it is asked to send and never wrote, turned away at its door, in the
    /// order they came to it.
    /// </summary>
    public IReadOnlyList<IAgentWord> HandedBack
    {
        get
        {
            lock (_gate) return [.. _handedBack, .. _turnedAway];
        }
    }

    /// <summary>The words held for the turn's end that the run hands back unprompted, before those turned away.</summary>
    public void HandBack(IReadOnlyList<IAgentWord> words)
    {
        lock (_gate) _handedBack = words;
    }

    /// <summary>The person's words the run sent and the session never took, counted at the run's end; empty until then.</summary>
    public IReadOnlyList<IAgentWord> Unreached { get; private set; } = [];

    /// <summary>
    /// The door shuts: every passage not yet admitted is turned away and its words go back. Called under the write lock, between two
    /// writes; their prompts were never admitted, so none of them is open.
    /// </summary>
    public void Shut()
    {
        lock (_gate)
        {
            _shut = true;
            foreach (var passage in _passing)
            {
                passage.TurnedAway = true;
                _turnedAway.AddRange(passage.Words);
            }

            _passing.Clear();
        }
    }

    /// <summary>Whether the run's door has shut.</summary>
    public bool DoorShut
    {
        get
        {
            lock (_gate) return _shut;
        }
    }

    /// <summary>A prompt asked to go, entered at the door before its words are marked, so a run that ends meanwhile hands them back.</summary>
    /// <exception cref="AcpTurnedAway">The door has shut: nothing is marked or written, and the words went back.</exception>
    public PromptPassage Enter(IEnumerable<IAgentWord?> words)
    {
        var passage = new PromptPassage([.. words.OfType<IAgentWord>()]);
        lock (_gate)
        {
            if (!_shut)
            {
                _passing.Add(passage);
                return passage;
            }

            _turnedAway.AddRange(passage.Words);
        }

        throw new AcpTurnedAway();
    }

    /// <summary>A passage whose words' mark came back: refused where the door shut meanwhile, or let go where the record did not mark them.</summary>
    /// <exception cref="AcpTurnedAway">The door shut while the mark is awaited: the words went back with the run's.</exception>
    /// <exception cref="AcpWordsUnmarked">The record did not name them marked: they wait on their record.</exception>
    public void Marked(PromptPassage passage, bool marked)
    {
        lock (_gate)
        {
            if (passage.TurnedAway) throw new AcpTurnedAway();
            if (!marked)
            {
                _passing.Remove(passage);
                throw new AcpWordsUnmarked(AcpWordsUnmarked.Sentence);
            }
        }
    }

    /// <summary>
    /// A prompt's passage marked (<see cref="Marked"/>), and the prompt it opens once admitted, counted under the same hold: the first
    /// opened is the run's opening.
    /// </summary>
    public OpenPrompt Opened(PromptPassage passage, bool marked, IAgentWord? words)
    {
        lock (_gate)
        {
            Marked(passage, marked);
            return new OpenPrompt(words) { Opening = Interlocked.Increment(ref _promptCount) == 1 };
        }
    }

    /// <summary>A try's passage keeps only the words the record marked: one it would not mark waits on its record, never handed back.</summary>
    /// <exception cref="AcpTurnedAway">The door shut while the marks were awaited.</exception>
    public void Going(PromptPassage passage, IReadOnlyList<IAgentWord> words)
    {
        lock (_gate)
        {
            if (passage.TurnedAway) throw new AcpTurnedAway();
            passage.Words = words;
        }
    }

    /// <summary>
    /// A passage admitted to the wire as its write begins, under the write lock, and its prompt open from then; false where the door shut
    /// first, or where <paramref name="when"/>, what the prompt is decided on, no longer holds, its words then going back with the run's.
    /// </summary>
    /// <remarks><paramref name="when"/> reads the wake under its own gate before this gate is taken: the order every look takes.</remarks>
    public bool Admit(PromptPassage passage, OpenPrompt? open = null, Func<bool>? when = null)
    {
        var holds = when?.Invoke() ?? true;
        lock (_gate)
        {
            if (passage.TurnedAway) return false;
            _passing.Remove(passage);
            if (!holds)
            {
                _turnedAway.AddRange(passage.Words);
                return false;
            }

            if (open is not null)
            {
                // 🔴 Words prompted to an idle session are taken at the first thing the agent does for them, or by their own answer,
                // never before the wire, as words after an error are.
                if (open.Words is not null && _prompts.Count == 0) _afterError = true;
                _prompts.AddLast(open);
            }

            return true;
        }
    }

    /// <summary>A prompt's write returned: it is on the wire.</summary>
    public void Written(OpenPrompt open)
    {
        lock (_gate) open.Written = true;
    }

    /// <summary>Whether the session has taken a prompt's words.</summary>
    public bool Took(OpenPrompt open)
    {
        lock (_gate) return open.Taken;
    }

    /// <summary>
    /// A prompt leaves as its answer is read, on the reader's thread: the words it carried judged first, then the words behind it taken
    /// where it is answered with a result, the moment the turn is handed off to them. The words taken now, if any; and whether it is the
    /// last open (<paramref name="last"/>).
    /// </summary>
    public OpenPrompt? Leaves(OpenPrompt open, bool answered, TransientRetry? retry, out bool last)
    {
        OpenPrompt? taken = null;
        lock (_gate)
        {
            // Answered with an error before any update of its turn, a word may or may not be in the agent's conversation: unacknowledged.
            if (retry is not null && !answered && !open.Heard)
            {
                _unacknowledged.AddRange(open.All.Where(word => !word.IsDaoris));
                // The opening prompt's blocks (a resume's words) carry no message here: the conclusion takes their disposition from this.
                if (open.Opening) retry.OpeningUnacknowledged = true;
            }

            // A word's prompt refused after the word is taken, by its mark as the error came, never by a later update.
            if (retry is not null && !answered && open is { Taken: true, Words: { } refusedTaken }) _refusedTaken.Add(refusedTaken);
            _prompts.Remove(open);
            last = _prompts.Count == 0;
            // 🔴 An error hands the turn to nothing: a word is taken once the agent does something for it (TakeLate), or is handed back as
            // never reached, never at the instant its door failed.
            if (answered && _prompts.First?.Value is { Taken: false, Words: not null } head)
            {
                head.Taken = true;
                taken = head;
            }

            _afterError = !answered;
        }

        return taken;
    }

    /// <summary>
    /// After an error answered the prompt before it, the next word is taken by the first thing the agent does on the wire for the session,
    /// or by its own answer. The words taken now, where they are <paramref name="only"/>'s (or, null, the first waiting); else null.
    /// </summary>
    public OpenPrompt? TakeLate(OpenPrompt? only = null)
    {
        lock (_gate)
        {
            if (!_afterError || _prompts.First?.Value is not { Taken: false, Words: not null } head)
            {
                _afterError = false;
                return null;
            }

            if (only is not null && head != only) return null;
            _afterError = false;
            head.Taken = true;
            return head;
        }
    }

    /// <summary>An update of the oldest open prompt's turn: its words are in the agent's conversation, whatever answers it.</summary>
    public void Heard()
    {
        lock (_gate)
        {
            if (_prompts.First?.Value is { } turn) turn.Heard = true;
        }
    }

    /// <summary>Whether a prompt is on the wire and not yet answered.</summary>
    public bool Prompted
    {
        get
        {
            lock (_gate) return _prompts.Count > 0;
        }
    }

    /// <summary>
    /// The prompts left open at the run's end, forgotten: those never taken kept as <see cref="Unreached"/>, except, once an episode of
    /// <paramref name="retry"/> opened, those written, whose words are returned as uncertain.
    /// </summary>
    public List<IAgentWord> Untaken(TransientRetry? retry)
    {
        lock (_gate)
        {
            var left = _prompts.Where(open => open.Words is not null && !open.Taken).ToList();
            var written = retry?.Opened == true ? left.Where(open => open.Written).ToList() : [];
            Unreached = [.. left.Except(written).SelectMany(open => open.All)];
            List<IAgentWord> uncertain = [.. written.SelectMany(open => open.All).Where(word => !word.IsDaoris)];
            _prompts.Clear();
            return uncertain;
        }
    }

    /// <summary>The person's words a failed prompt left unacknowledged, taken off the list.</summary>
    public List<IAgentWord> TakeUnacknowledged()
    {
        lock (_gate)
        {
            var words = _unacknowledged.ToList();
            _unacknowledged.Clear();
            return words;
        }
    }

    /// <summary>The unacknowledged words forgotten.</summary>
    public void ForgetUnacknowledged()
    {
        lock (_gate) _unacknowledged.Clear();
    }

    /// <summary>Whether this word's prompt is refused after it is taken, read once, by reference.</summary>
    public bool RefusedTaken(IAgentWord words)
    {
        lock (_gate) return _refusedTaken.Remove(words);
    }
}
