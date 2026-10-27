namespace Daoris.Driver;

/// <summary>
/// What a start's walk over Build's order answered (D205 §4.1, MODELROLE1c2): the selection, the entry it took (null where it waits), and
/// each entry it passed before, in order, with its code: what the wait's sentence and the opening note's actor line say.
/// </summary>
public sealed record BuildWalk(HarnessSelection Selection, ActorEffective? Chosen, IReadOnlyList<BuildPass> Passes);

/// <summary>An actor's name as the screen says it in English (D205 §2.2), for the lines the driver writes; the page words it by its id.</summary>
public static class ActorWords
{
    public static string Name(string actor) => actor switch
    {
        "build" => "Build",
        "intake" => "Intake",
        "opinion" => "Second opinion",
        "helper" => "Helper",
        "partner" => "Partner",
        "brainstorm" => "Brainstorm",
        "ask-daoris" => "Ask Daoris",
        "chat" => "Chat",
        _ => actor,
    };
}

/// <summary>
/// One entry of Build's order a start passed (D205 §4.1): <c>unavailable</c>, <c>no-posture</c>, <c>no-connector</c>, <c>not-offered</c>,
/// <c>cooling</c>, <c>signed-out</c> or <c>held</c>, and the walk's own reason in English.
/// </summary>
public sealed record BuildPass(ActorEffective Entry, string Code, string? Why)
{
    /// <summary>The entry as the person's order says it: its agent, and its model or <i>account default</i>.</summary>
    public string Named => $"{Entry.Agent} · {Entry.Model ?? "account default"}";

    /// <summary>The pass in the wait's words: <c>choice n, agent · model: why</c>.</summary>
    public string Said => $"choice {Entry.From}, {Named}: {Why?.TrimEnd('.')}";

    /// <summary>The pass as the machine log keeps it (LOGCOLLECTION1): <c>choice:agent:code</c>, names and a code, never the walk's sentence.</summary>
    public string Word => $"{Entry.From}:{Entry.Agent}:{Code}";
}

/// <summary>
/// What each walk last passed, per quest, ask or conversation, so the machine log says a pass when it is new and not at every look
/// while a choice stays passed for the same reason (LOGCOLLECTION1, D94): the first look of a walk that passes, and every look whose
/// passes differ from the look before. The install wrote 10,488 <c>actor.passed</c> lines in a day for one held quest.
/// </summary>
/// <remarks>
/// A walk is compared with the walk before it, so a pass that stopped and began again is said again, and a walk that passes nothing
/// (it took its first choice) leaves nothing to compare with. A re-ask at an open (<c>counts</c> false) says nothing and remembers
/// nothing. Held in memory only: a restart says each held pass once more, and past <see cref="Most"/> subjects it starts over.
/// </remarks>
public sealed class PassMemory
{
    /// <summary>How many subjects are remembered at once: a quest that was started stays remembered until this fills.</summary>
    private const int Most = 1000;

    private readonly object _gate = new();
    private readonly Dictionary<string, HashSet<string>> _last = new(StringComparer.Ordinal);

    /// <summary>A walk over <paramref name="subject"/>'s order, ended by disposing it.</summary>
    public PassWalk Walk(string subject, bool counts) => new(this, subject, counts);

    private HashSet<string> Last(string subject)
    {
        lock (_gate) return _last.TryGetValue(subject, out var last) ? last : [];
    }

    private void Keep(string subject, HashSet<string> passed)
    {
        lock (_gate)
        {
            if (passed.Count == 0)
            {
                _last.Remove(subject);
                return;
            }

            if (_last.Count >= Most && !_last.ContainsKey(subject)) _last.Clear();
            _last[subject] = passed;
        }
    }

    /// <summary>One walk's passes against the walk before's.</summary>
    public sealed class PassWalk : IDisposable
    {
        private readonly PassMemory _memory;
        private readonly string _subject;
        private readonly bool _counts;
        private readonly HashSet<string> _before;
        private readonly HashSet<string> _now = new(StringComparer.Ordinal);

        internal PassWalk(PassMemory memory, string subject, bool counts)
        {
            (_memory, _subject, _counts) = (memory, subject, counts);
            _before = counts ? memory.Last(subject) : [];
        }

        /// <summary>Whether this pass is to be written: it is not one the walk before passed for the same choice, agent and code.</summary>
        public bool Fresh(BuildPass pass)
        {
            if (!_counts) return false;
            var key = pass.Word;
            _now.Add(key);
            return !_before.Contains(key);
        }

        public void Dispose()
        {
            if (_counts) _memory.Keep(_subject, _now);
        }
    }
}
