using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// Ask Daoris's conversation between its record and its agent (HELPSTART1, D89's note): <c>START_HELP</c> answers once the record
/// is open, and the room is read and the agent spawned after, off the request. The person's words said to it meanwhile wait here,
/// in the order said, as its queue, opening; they go to its door the moment it opens, and where it could not start they are
/// handed back for its record to keep as theirs.
/// </summary>
/// <remarks>
/// <b>One at a time</b>, as the room holds one conversation: <see cref="Placing"/> lets one press place it while a second waits,
/// then hands that one the conversation placed rather than placing another. Held in this process only: a shell that closes
/// with words here ends the record it placed, and the page that sent them still shows them.
/// </remarks>
public sealed class HelpStarting
{
    private readonly object _gate = new();
    private readonly List<HeldWord> _words = [];
    private string? _session;

    /// <summary>One press places a conversation at a time; the next is handed the one placed.</summary>
    public SemaphoreSlim Placing { get; } = new(1, 1);

    /// <summary>The conversation placed whose agent is not running yet, or null where none is.</summary>
    public string? Session
    {
        get
        {
            lock (_gate) return _session;
        }
    }

    /// <summary>A conversation was placed: words said to it wait from now until its start is <see cref="Over"/>.</summary>
    public void Placed(string session)
    {
        lock (_gate)
        {
            _session = session;
            _words.Clear();
        }
    }

    /// <summary>Hold words said to the conversation starting; false where it is not the one starting, which leaves them to the usual judge.</summary>
    public bool Hold(string session, string text, IReadOnlyList<ChatUpload> files, string? preface)
    {
        lock (_gate)
        {
            if (!Starting(session)) return false;
            _words.Add(new HeldWord(text, files, preface));
            return true;
        }
    }

    /// <summary>
    /// What waits for the conversation starting, as its queue says it (HELP4's <c>opening</c>): nothing taking yet, its words in
    /// order with their files' names. Null where it is not the one starting.
    /// </summary>
    public ChatQueue? Queue(string session)
    {
        lock (_gate)
        {
            if (!Starting(session)) return null;
            return new ChatQueue(
                false, [.. _words.Select(word => new ChatMessage(word.Text, [.. word.Files.Select(file => new KeptFile(file.Name, ""))]))],
                Opening: true);
        }
    }

    /// <summary>
    /// The start is over: <paramref name="say"/> hands each word to its door in the order said, while no other word can be said
    /// to it, and what it did not take is answered, for the record to keep. Null <paramref name="say"/> is a start that failed,
    /// which takes none. Nothing waits here after.
    /// </summary>
    public IReadOnlyList<HeldWord> Over(string session, Func<HeldWord, bool>? say)
    {
        lock (_gate)
        {
            if (!Starting(session)) return [];
            var left = say is null ? [.. _words] : _words.Where(word => !say(word)).ToList();
            _session = null;
            _words.Clear();
            return left;
        }
    }

    private bool Starting(string session) => string.Equals(_session, session, StringComparison.OrdinalIgnoreCase);
}

/// <summary>Words said to Ask Daoris's conversation while it starts (HELPSTART1): the text, the files as sent, where the person is.</summary>
public sealed record HeldWord(string Text, IReadOnlyList<ChatUpload> Files, string? Preface);
