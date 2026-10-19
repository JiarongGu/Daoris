using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// Ask Daoris's conversation between its record and its agent (HELPSTART1, D89's note): <c>START_HELP</c> answers once the record
/// is open, and the room is read and the agent spawned after, off the request. The person's words said to it meanwhile wait here,
/// in the order said, as its queue, opening; they go to its door the moment it opens, and where it could not start they are
/// handed back for its record to keep as theirs.
/// </summary>
/// <remarks>
/// <b>One at a time in each workspace</b> (ASKSCOPE1a, D158's ASKSCOPE1 note point 3), as one conversation runs in each:
/// <see cref="PlacingIn"/> lets one press place a workspace's while a second press in it waits, then hands that one the conversation
/// placed rather than placing another; a press in another workspace places its own beside it. A workspace unnamed is
/// <c>default</c>'s, and a name is one workspace whatever its letters' case. Held in this process only, as a conversation's own
/// queue is: words held here when the shell closes go with it, as words waiting on a turn do.
/// </remarks>
public sealed class HelpStarting
{
    private readonly object _gate = new();

    // Each workspace's press gate, made at its first press and kept: a handful, one per workspace pressed in.
    private readonly Dictionary<string, SemaphoreSlim> _placing = new(StringComparer.OrdinalIgnoreCase);

    // Each workspace's conversation starting, with the words said to it meanwhile.
    private readonly Dictionary<string, (string Session, List<HeldWord> Words)> _starting = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>One press places a workspace's conversation at a time; the next press in it is handed the one placed.</summary>
    public SemaphoreSlim PlacingIn(string? workspace)
    {
        lock (_gate)
        {
            var circle = RemoteTarget.Workspace(workspace);
            if (!_placing.TryGetValue(circle, out var placing)) _placing[circle] = placing = new SemaphoreSlim(1, 1);
            return placing;
        }
    }

    /// <summary>The workspace's conversation placed whose agent is not running yet, or null where none is.</summary>
    public string? SessionIn(string? workspace)
    {
        lock (_gate) return _starting.TryGetValue(RemoteTarget.Workspace(workspace), out var starting) ? starting.Session : null;
    }

    /// <summary>A workspace's conversation was placed: words said to it wait from now until its start is <see cref="Over"/>.</summary>
    public void Placed(string? workspace, string session)
    {
        lock (_gate) _starting[RemoteTarget.Workspace(workspace)] = (session, []);
    }

    /// <summary>Hold words said to a conversation starting; false where it is not one starting, which leaves them to the usual judge.</summary>
    public bool Hold(string session, string text, IReadOnlyList<ChatUpload> files, string? preface)
    {
        lock (_gate)
        {
            if (WordsOf(session) is not { } words) return false;
            words.Add(new HeldWord(text, files, preface));
            return true;
        }
    }

    /// <summary>
    /// What waits for a conversation starting, as its queue says it (HELP4's <c>opening</c>): nothing taking yet, its words in
    /// order with their files' names. Null where it is not one starting.
    /// </summary>
    public ChatQueue? Queue(string session)
    {
        lock (_gate)
        {
            if (WordsOf(session) is not { } words) return null;
            return new ChatQueue(
                false, [.. words.Select(word => new ChatMessage(word.Text, [.. word.Files.Select(file => new KeptFile(file.Name, ""))]))],
                Opening: true);
        }
    }

    /// <summary>
    /// The start is over: <paramref name="say"/> hands each word to its door in the order said, while no other word can be said
    /// to it, and what it did not take is answered, for the record to keep. Null <paramref name="say"/> is a start that failed,
    /// which takes none. Nothing waits here for it after; another workspace's start is left as it is.
    /// </summary>
    public IReadOnlyList<HeldWord> Over(string session, Func<HeldWord, bool>? say)
    {
        lock (_gate)
        {
            if (_starting.FirstOrDefault(each => Same(each.Value.Session, session)) is not { Key: { } workspace, Value.Words: var words })
            {
                return [];
            }

            var left = say is null ? [.. words] : words.Where(word => !say(word)).ToList();
            _starting.Remove(workspace);
            return left;
        }
    }

    // The words held for this conversation, where it is one starting; read under the gate.
    private List<HeldWord>? WordsOf(string session) =>
        _starting.Values.FirstOrDefault(each => Same(each.Session, session)).Words;

    private static bool Same(string? left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}

/// <summary>Words said to Ask Daoris's conversation while it starts (HELPSTART1): the text, the files as sent, where the person is.</summary>
public sealed record HeldWord(string Text, IReadOnlyList<ChatUpload> Files, string? Preface);
