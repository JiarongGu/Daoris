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
        return QuestResults.Judged(pressed) ? new PressedResult(session, pressed, token => read(quest.TrimStart('#'), token), late) : null;
    }

    /// <summary>
    /// The result a person's hand-off of <paramref name="entry"/> is made on (FABRIC-CLOSE1h8, D173's note): the quest of the session
    /// that last moved the branch (<see cref="LandedBranch.LastSession"/>), not its landing's first, read where the door names the
    /// branch. The hand-off judges it once more under the branch's hold, just before its plugin is told (<see cref="HandedResult.BeforePush"/>).
    /// </summary>
    /// <param name="sessionQuest">The quest a session serves, null for none (<see cref="ServiceClient.SessionQuestAsync"/>).</param>
    /// <param name="read">The service's quest by id, as it holds it now (<see cref="ServiceClient.FindQuestAsync"/>).</param>
    public static async Task<HandedResult> HandedAsync(
        LandedBranch entry, Func<string, CancellationToken, Task<string?>> sessionQuest,
        Func<string, CancellationToken, Task<QuestView?>> read, CancellationToken ct = default)
    {
        // The record keeps no quest per advance, so the session's own is asked: one read, and one more only where it names a quest.
        var session = entry.LastSession();
        var quest = await sessionQuest(session, ct).ConfigureAwait(false);
        var pressed = await new PressQuests(read).PressedAsync(session, quest, ct: ct).ConfigureAwait(false);
        return new HandedResult(session, pressed, sessionQuest, read);
    }

    /// <inheritdoc cref="HandedAsync(LandedBranch, Func{string, CancellationToken, Task{string?}}, Func{string, CancellationToken, Task{QuestView?}}, CancellationToken)"/>
    public static Task<HandedResult> HandedAsync(LandedBranch entry, ServiceClient service, CancellationToken ct = default) =>
        HandedAsync(entry, service.SessionQuestAsync, service.FindQuestAsync, ct);
}

/// <summary>
/// What a person's hand-off is made on (FABRIC-CLOSE1h8 and FABRIC-CLOSE1h10, D173's notes), and its judgement under the branch's hold
/// just before its plugin is told (<see cref="TreeHandOffs.HandAsync"/>'s <c>beforePush</c>): the session that last moved the branch as
/// the door read it, and that session's result where one is in.
/// </summary>
public sealed class HandedResult
{
    private readonly Func<string, CancellationToken, Task<string?>> _sessionQuest;
    private readonly Func<string, CancellationToken, Task<QuestView?>> _read;

    internal HandedResult(
        string session, PressedResult? pressed, Func<string, CancellationToken, Task<string?>> sessionQuest,
        Func<string, CancellationToken, Task<QuestView?>> read)
    {
        Session = session;
        Pressed = pressed;
        _sessionQuest = sessionQuest;
        _read = read;
    }

    /// <summary>The session that last moved the branch as the door read it (<see cref="LandedBranch.LastSession"/>).</summary>
    public string Session { get; }

    /// <summary>
    /// Its result as the door read it; null where it holds the hand-off to nothing, so it hands on as before: a session on no quest, or a
    /// quest whose result is not in (FABRIC-CLOSE1h8's decision 5).
    /// </summary>
    public PressedResult? Pressed { get; }

    /// <summary>
    /// The hand-off's judgement under the branch's hold, just before its plugin is told: told the session that last moved the branch as the
    /// record reads there, and whether the hold waited. The sentence that holds the push back (<see cref="TreeHand.Withheld"/>), or null.
    /// </summary>
    public Func<string, bool, CancellationToken, Task<string?>> BeforePush => BeforePushAsync;

    private async Task<string?> BeforePushAsync(string last, bool waited, CancellationToken ct)
    {
        // The parent's decision 4: no later session moved the branch, so it is judged as FABRIC-CLOSE1h8 judges it, with no read more.
        if (string.Equals(last, Session, StringComparison.OrdinalIgnoreCase))
        {
            return Pressed is null ? null : await Pressed.BeforePush(waited, ct).ConfigureAwait(false);
        }

        // FABRIC-CLOSE1h10 (D173's note): another session's done moved the branch on before this hand-off held it, so its work rides this
        // push. The door never read that session's result, so it is read now, whether or not the hold waited, by the person's rule: a
        // result no longer in holds the push back, and a hold does not. A session on no quest holds it to nothing, as at the door.
        if (await _sessionQuest(last, ct).ConfigureAwait(false) is not { } quest) return null;
        var now = await _read(quest.TrimStart('#'), ct).ConfigureAwait(false);
        return QuestResults.Judged(now) ? null : PressedResult.HeldBack("its quest was taken back", last);
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

    internal PressedResult(string session, QuestView pressed, Func<CancellationToken, Task<QuestView?>> again, bool late)
    {
        Session = session;
        Pressed = pressed;
        _again = again;
        _late = late;
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
    /// The landing's judgement just before its plugin is told, told whether the push waited: the sentence that holds the push back where
    /// the result moved, the branch made and recorded and no plugin spoken to (<see cref="TreeLanding.Withheld"/>); null lets it begin.
    /// </summary>
    public Func<bool, CancellationToken, Task<string?>> BeforePush => BeforePushAsync;

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
            Refusal = QuestResults.Judged(now) ? AutoLandingCode.Refused : AutoLandingCode.Undone,
        };
    }

    private async Task<string?> BeforePushAsync(bool waited, CancellationToken ct)
    {
        if (!waited && !QuestResults.Submitted(Pressed)) return null;
        var now = await _again(ct).ConfigureAwait(false);
        return Moved(Pressed, now) is { } why ? HeldBack(why, Session) : null;
    }

    /// <summary>
    /// The sentence that holds a person's push back just before its plugin is told, for <paramref name="why"/>: it names the hand-off of
    /// <paramref name="session"/>, the session whose result moved (FABRIC-CLOSE1h3; FABRIC-CLOSE1h10 for a later session's advance).
    /// </summary>
    internal static string HeldBack(string why, string session) =>
        $"{Capital(why)} before its plugin was told, so its push was not tried: the branch stands, and once you have looked at the quest "
        + $"as it stands, `daoris-driver trees hand {session}` hands it on";

    private static string Capital(string words) => char.ToUpperInvariant(words[0]) + words[1..];
}
