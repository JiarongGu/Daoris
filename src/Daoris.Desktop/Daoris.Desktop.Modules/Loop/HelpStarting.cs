using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// Ask Daoris's conversations between their record and their agent: the person's words said to one while it starts wait here
/// by its id, in the order said, as its queue, opening; they go to its door the moment it opens, and where it could not start
/// they are handed back for its record to keep. Held in this process only, as a turn's waiting words are.
/// </summary>
public sealed class HelpStarting
{
    private readonly object _gate = new();

    // Each conversation starting, by its id, with the words said to it meanwhile.
    private readonly Dictionary<string, List<HeldWord>> _starting = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>A conversation is placed: words said to it wait from now until its start is <see cref="Over"/>.</summary>
    public void Placed(string session)
    {
        lock (_gate) _starting[session] = [];
    }

    /// <summary>Hold words said to a conversation starting; false where it is not one starting, which leaves them to the usual judge.</summary>
    public bool Hold(string session, string text, IReadOnlyList<ChatUpload> files, string? preface)
    {
        lock (_gate)
        {
            if (!_starting.TryGetValue(session, out var words)) return false;
            words.Add(new HeldWord(text, files, preface));
            return true;
        }
    }

    /// <summary>What waits for a conversation starting, as its queue (<c>opening</c>); null where it is not one starting.</summary>
    public ChatQueue? Queue(string session)
    {
        lock (_gate)
        {
            if (!_starting.TryGetValue(session, out var words)) return null;
            return new ChatQueue(
                false, [.. words.Select(word => new ChatMessage(word.Text, [.. word.Files.Select(file => new KeptFile(file.Name, ""))]))],
                Opening: true);
        }
    }

    /// <summary>
    /// The start is over: <paramref name="say"/> hands each word to its door in the order said, while no other word can be said
    /// to it, and what it did not take is answered, for the record to keep. Null <paramref name="say"/> is a start that failed,
    /// which takes none. Nothing waits here for it after; another conversation's start is left as it is.
    /// </summary>
    public IReadOnlyList<HeldWord> Over(string session, Func<HeldWord, bool>? say)
    {
        lock (_gate)
        {
            if (!_starting.Remove(session, out var words)) return [];
            return say is null ? [.. words] : words.Where(word => !say(word)).ToList();
        }
    }
}

/// <summary>Words said to Ask Daoris's conversation while it starts: the text, the files as sent, where the person is.</summary>
public sealed record HeldWord(string Text, IReadOnlyList<ChatUpload> Files, string? Preface);
