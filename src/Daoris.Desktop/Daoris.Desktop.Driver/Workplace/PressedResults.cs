using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>
/// One press's reads of the service's quests: each quest read once while the door names its landing,
/// so the quest the press is made on is the one its door read, with no read of its own, and read afresh only where the landing judges
/// it again (<see cref="PressedResult"/>). The page's press, the terminal's and an entry's Accept make one each.
/// </summary>
/// <param name="read">The service's quest by id, as it holds it now (<see cref="ServiceClient.FindQuestAsync"/>).</param>
public sealed class PressQuests(Func<string, CancellationToken, Task<QuestView?>> read)
{
    private readonly ConcurrentDictionary<string, Task<QuestView?>> _read = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// A quest as the service held it when this press first asked for it: the door's find for the landing's name (the chain's walk), read
    /// once however often it is asked.
    /// </summary>
    public Task<QuestView?> FindAsync(string id, CancellationToken ct = default) => _read.GetOrAdd(id.TrimStart('#'), each => read(each, ct));

    /// <summary>
    /// The result <paramref name="session"/>'s press is made on: its quest as this press read it, where its result is in, a done or a
    /// closure graph's submission, held or not (<see cref="QuestResults.Judged"/>). Null holds the landing to nothing, so it lands as
    /// before: a conversation (no quest), and a quest whose result is not in (open, or taken with no submission), the parent's decision 3.
    /// </summary>
    /// <param name="late">
    /// The press was made before its landing's own moment: an entry's part, behind the press's fetch and its earlier parts. Its result is
    /// then read again under its tree's hold whether or not that hold waited.
    /// </param>
    public async Task<PressedResult?> PressedAsync(string session, string? quest, bool late = false, CancellationToken ct = default)
    {
        if (quest is null) return null;
        var pressed = await FindAsync(quest, ct).ConfigureAwait(false);
        return QuestResults.Judged(pressed) ? new PressedResult(session, pressed, token => read(quest.TrimStart('#'), token), late) : null;
    }
}

/// <summary>
/// The quest result a person's press is made on, and the landing's judgement
/// of it (<see cref="TreeLands.LandAsync"/>'s <c>judged</c>), once it holds its tree. This reads the one quest.
/// </summary>
/// <remarks>
/// <para><b>Only the result moving stops a press</b>: a reopen or a done turned down (its quest's result
/// is no longer in), a later submission, or another <c>ready</c> at the same commit (a submission is known by its machine and sequence,
/// <see cref="QuestResults.Same"/>). A quest's hold is never read here: Accept is the person's own word, and a press never reads it;
/// the automatic landing's predicate is not copied, which would make a press an automatic acceptance.</para>
/// <para><b>Read only where time could have moved it.</b> Under the tree's hold, where that hold waited behind another landing, or the
/// press was <c>late</c>; just before the push, a submission every time (the branch's making lies between), a done only where the push
/// waited. An uncontended press reads one quest more at most, and only on a submission, so the owner's install, which binds nothing,
/// reads nothing more.</para>
/// </remarks>
public sealed class PressedResult
{
    private readonly Func<CancellationToken, Task<QuestView?>> _again;
    private readonly bool _late;

    internal PressedResult(string session, QuestView pressed, Func<CancellationToken, Task<QuestView?>> again, bool late)
    {
        Session = session;
        Pressed = pressed;
        _again = again;
        _late = late;
    }

    /// <summary>The session pressed.</summary>
    public string Session { get; }

    /// <summary>The quest as the press read it.</summary>
    public QuestView Pressed { get; }

    /// <summary>
    /// The landing's judgement once it holds its tree, told whether it waited: a refusal where the result moved, before anything is made
    /// (no branch, no advance, no push); null lets it go on.
    /// </summary>
    public Func<bool, CancellationToken, Task<TreeLanding?>> Judged => JudgedAsync;

    /// <summary>
    /// Why the result <paramref name="pressed"/> stood on moved, read as <paramref name="now"/>: its quest taken back (its result no longer
    /// in, or gone), or its submission made again; null where it stands, held or closed done on that submission alike.
    /// </summary>
    public static string? Moved(QuestView pressed, QuestView? now) =>
        !QuestResults.Judged(now) ? "its quest was taken back"
        : !QuestResults.Same(pressed, now) ? "its quest's submission was made again"
        : null;

    private async Task<TreeLanding?> JudgedAsync(bool waited, CancellationToken ct)
    {
        if (!waited && !_late) return null;
        var now = await _again(ct).ConfigureAwait(false);
        if (Moved(Pressed, now) is not { } why) return null;
        // A reopen or a done turned down is the code a quest no longer done keeps; a later submission is a refusal in its own sentence.
        return new TreeLanding(false, $"Nothing was landed: {why} while this press waited to land, so what it was made on stands no more. "
            + "Look at the quest as it stands, then accept it again.")
        {
            Refusal = QuestResults.Judged(now) ? LandingCode.Refused : LandingCode.Undone,
        };
    }

}
