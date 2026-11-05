using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>
/// One press's reads of the service's quests (FABRIC-CLOSE1h3, D173's note): each quest read once while the door names its landing,
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
        return QuestResults.Judged(pressed) ? new PressedResult(session, pressed, token => read(quest.TrimStart('#'), token), late, read) : null;
    }
}

/// <summary>
/// The quest result a person's press was made on (FABRIC-CLOSE1h3, D173's note; the fabric design §E5), and the landing's two judgements
/// of it (<see cref="TreeLands.LandAsync"/>'s <c>judged</c> and <c>beforePush</c>): once it holds its tree, and once more just before its
/// plugin is told. The automatic landing's judgements are <see cref="AutoLander"/>'s, of a world read afresh; these read the one quest.
/// </summary>
/// <remarks>
/// <para><b>Only the result moving stops a press</b> (the parent's decisions 1 and 2): a reopen or a done turned down (its quest's result
/// is no longer in), a later submission, or another <c>ready</c> at the same commit (a submission is known by its machine and sequence,
/// <see cref="QuestResults.Same"/>). A quest's hold is never read here: Accept is the person's own word, and today's press never read it;
/// the automatic landing's predicate is not copied, which would make a press an automatic acceptance.</para>
/// <para><b>Read only where time could have moved it.</b> Under the tree's hold, where that hold waited behind another landing, or the
/// press was <c>late</c>; just before the push, a submission every time (the branch's making lies between), a done only where the push
/// waited. An uncontended press reads one quest more at most, and only on a submission, so the owner's install, which binds nothing,
/// reads nothing more than it did.</para>
/// </remarks>
public sealed class PressedResult
{
    private readonly Func<CancellationToken, Task<QuestView?>> _again;
    private readonly bool _late;
    private readonly Func<string, CancellationToken, Task<QuestView?>>? _read;

    /// <param name="read">The service's quest by id, which judges the other sessions' work its push carries (FABRIC-CLOSE1h11).</param>
    internal PressedResult(
        string session, QuestView pressed, Func<CancellationToken, Task<QuestView?>> again, bool late,
        Func<string, CancellationToken, Task<QuestView?>>? read = null)
    {
        Session = session;
        Pressed = pressed;
        _again = again;
        _late = late;
        _read = read;
    }

    /// <summary>The session pressed, whose hand-off the push's sentence names.</summary>
    public string Session { get; }

    /// <summary>The quest as the press read it.</summary>
    public QuestView Pressed { get; }

    /// <summary>
    /// The landing's judgement once it holds its tree, told whether it waited: a refusal where the result moved, before anything is made
    /// (no branch, no advance, no push); null lets it go on.
    /// </summary>
    public Func<bool, CancellationToken, Task<TreeLanding?>> Judged => JudgedAsync;

    /// <summary>
    /// The landing's judgement just before its plugin is told, told the branch's record as it reads under the branch's hold and whether
    /// the push waited: the sentence that holds the push back where the result moved, the branch made and recorded and no plugin spoken
    /// to (<see cref="TreeLanding.Withheld"/>); null lets it begin. FABRIC-CLOSE1h11 (D173's CLOSELAND1 note): the push carries every
    /// session's work no push carried yet, so each other one is judged too, by the person's rule (<see cref="Carried"/>).
    /// </summary>
    public Func<LandedBranch?, bool, CancellationToken, Task<string?>> BeforePush => BeforePushAsync;

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

    private async Task<string?> BeforePushAsync(LandedBranch? record, bool waited, CancellationToken ct) =>
        await OwnBeforePushAsync(waited, ct).ConfigureAwait(false) ?? await CarriedBeforePushAsync(record, ct).ConfigureAwait(false);

    /// <summary>The press's own result judged just before the push (FABRIC-CLOSE1h3), as the person's hand-off judges its last session's.</summary>
    internal async Task<string?> OwnBeforePushAsync(bool waited, CancellationToken ct)
    {
        if (!waited && !QuestResults.Submitted(Pressed)) return null;
        var now = await _again(ct).ConfigureAwait(false);
        return Moved(Pressed, now) is { } why ? HeldBack(why, Session) : null;
    }

    /// <summary>
    /// FABRIC-CLOSE1h11: each other session whose work the push carries unpushed, judged on the quest the record keeps for it, read now
    /// whether or not the push waited: the door never read it. Work the record keeps no quest for (a conversation's, a landing from
    /// before) holds it to nothing, as a session on no quest does at the person's door; where nothing else is carried, nothing is read.
    /// </summary>
    private async Task<string?> CarriedBeforePushAsync(LandedBranch? record, CancellationToken ct)
    {
        if (record is null || _read is null) return null;
        foreach (var work in record.UnpushedBesides(Session))
        {
            if (work.Quest is not { } quest) continue;
            var id = quest.TrimStart('#');
            if (Carried(work, await _read(id, ct).ConfigureAwait(false)) is { } why)
            {
                return HeldBack($"session {work.Session}'s quest #{id}, whose work this push carries, {why}", Session);
            }
        }

        return null;
    }

    /// <summary>
    /// Why another session's work a push carries holds it back by the person's rule (FABRIC-CLOSE1h11): its quest's result no longer in
    /// (<c>was taken back</c>), or, where the record keeps the submission its work was accepted on, another made since
    /// (<c>was submitted again</c>, FABRIC-CLOSE1h13); null where it stands, held or not.
    /// </summary>
    public static string? Carried(LandedWork work, QuestView? now) =>
        !QuestResults.Judged(now) ? "was taken back"
        : SubmittedSince(work, now) ? "was submitted again"
        : null;

    /// <summary>
    /// Whether <paramref name="now"/> stands on another submission than the one the record keeps for <paramref name="work"/>
    /// (FABRIC-CLOSE1h13), by machine and sequence. A record that keeps none, or a quest that answers none, says nothing moved.
    /// </summary>
    internal static bool SubmittedSince(LandedWork work, QuestView? now) =>
        work.Submission is { } was && now?.Submission is { } current
        && (!string.Equals(current.Machine, was.Machine, StringComparison.Ordinal) || current.Sequence != was.Sequence);

    /// <summary>
    /// The sentence that holds a person's push back just before its plugin is told, for <paramref name="why"/>: it names the hand-off of
    /// <paramref name="session"/>, the session whose result moved (FABRIC-CLOSE1h3; FABRIC-CLOSE1h10 for a later session's advance).
    /// </summary>
    internal static string HeldBack(string why, string session) =>
        $"{Capital(why)} before its plugin was told, so its push was not tried: the branch stands, and once you have looked at the quest "
        + $"as it stands, `daoris tree hand {session}` hands it on";

    private static string Capital(string words) => char.ToUpperInvariant(words[0]) + words[1..];
}
