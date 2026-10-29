namespace Daoris.Driver;

/// <summary>
/// The records one word has been said to on its way to its line's head (FOUNDATION1d-ii, the foundation design §3.4 rule 2): a host with the
/// <c>head</c> mark refuses a word said to a record that is not its line's head as <c>continued</c>, naming the head, and the caller says the same
/// word there. A second <c>continued</c> follows the next head, which only a line that moved between the two says names; a walk that comes back
/// to a record it asked, a planted cycle, ends, as does one past <see cref="Most"/>, so no answer of a host's makes a caller say a word without end.
/// </summary>
public sealed class HeadWalk
{
    /// <summary>The most records one word is said to: far past any line a move makes between two says, and short of a walk without end.</summary>
    public const int Most = 8;

    private readonly HashSet<string> _asked;

    /// <summary>A walk from the record the word was first said to.</summary>
    public HeadWalk(string first)
    {
        First = first;
        At = first;
        _asked = new HashSet<string>(StringComparer.Ordinal) { first };
    }

    /// <summary>The record the word was first said to.</summary>
    public string First { get; }

    /// <summary>The record it is said to now.</summary>
    public string At { get; private set; }

    /// <summary>
    /// Whether the word goes on to <paramref name="head"/>: a record this walk has not asked, within <see cref="Most"/>. True moves the walk
    /// there; false ends it, and <see cref="Ended"/> says why.
    /// </summary>
    public bool On(string head)
    {
        if (_asked.Count >= Most || !_asked.Add(head)) return false;
        At = head;
        return true;
    }

    /// <summary>The driver's sentence for a walk that ended at <paramref name="head"/>, named again or past the bound: nothing kept the words.</summary>
    public string Ended(string head) =>
        $"the words said to `{First}` were not kept: its line came back to `{head}`, which they were said to already, "
        + $"or named more than {Most} heads in turn, so nothing goes on with them.";
}

/// <summary>
/// The say door's callers' walk to the line's head (FOUNDATION1d-ii, the foundation design §3.4 rules 1 and 2): the word said where it was meant,
/// and wherever a host with the <c>head</c> mark refuses it <c>continued</c>, the same say again at the head the answer names, whole: the say is
/// the caller's own, so the word goes with its producer's key, its level, its author and its files' names exactly as it went first. A caller whose
/// word carries files carries them to each head's folder before it is said there, so the head's go-on finds them. A host without the mark never
/// answers so, and the say is today's one call.
/// </summary>
/// <remarks>
/// The callers that reach a dispatcher of this machine's for the head (the shell's <c>SessionWords</c>, the headless <see cref="LoopWords"/>) walk
/// with <see cref="HeadWalk"/> through their own judgement instead; <c>ContinuedReadersCensusTests</c> names every caller and which it does.
/// </remarks>
public static class WordsHead
{
    /// <summary>
    /// Say a word at <paramref name="id"/>, and at its line's head where the answer is <c>continued</c>: the last answer, and the record it was
    /// said to. A walk that ends answers a refusal of its own, <c>continued</c> with no head and the driver's sentence, never kept.
    /// </summary>
    /// <param name="say">The caller's say, of the word as it is, to the record it is handed.</param>
    /// <param name="carry">Before the say at a head: the word's files carried from the record before it to the head; null for a word with none.</param>
    public static async Task<(SayAnswer Said, string At)> FollowAsync(string id, Func<string, Task<SayAnswer>> say, Action<string, string>? carry = null)
    {
        var walk = new HeadWalk(id);
        var said = await say(id).ConfigureAwait(false);
        while (said.Head is { } head)
        {
            var from = walk.At;
            if (!walk.On(head)) return (Ended(walk, head), from);
            carry?.Invoke(from, head);
            said = await say(head).ConfigureAwait(false);
        }

        return (said, walk.At);
    }

    /// <summary>A walk's end as a say answers it: refused <c>continued</c>, naming no head, with the driver's sentence.</summary>
    public static SayAnswer Ended(HeadWalk walk, string head) => new(false, walk.Ended(head)) { Refusal = SayAnswer.Continued };
}
