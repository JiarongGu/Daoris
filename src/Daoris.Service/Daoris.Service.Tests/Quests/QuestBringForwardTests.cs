using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// SYNCMERGE1a (D178 points 3 and 5): bringing a lost take forward is an ordinary take of the step and a dismissal of its conflict,
/// marked with the take it made. The mark makes the dismissal the taker's own: it applies only while its machine's claim holds, only
/// on that machine's own take conflict still standing on the quest, and a plain dismissal stays as it was. These are the replay's
/// rules alone; the two machines and the remote are <see cref="QuestSyncTests"/>' and <see cref="QuestReopenSyncTests"/>'.
/// </summary>
public sealed class QuestBringForwardTests
{
    private const string Id = "abcdefabcdef";
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-19T09:00:00Z");

    private static readonly Quest Asked =
        new(Id, "ask #a1b2c3", "reports", "Write the report", "Through the bridge.", QuestStatus.Open, null, Now, Now);

    private static QuestOperation Op(QuestOperationKind kind, string machine, long sequence) =>
        new(Id, kind, machine, sequence, Now.AddMinutes(sequence));

    private static readonly QuestOperation Published = Op(QuestOperationKind.Published, "a", 1) with { Published = Asked, Number = 1 };

    /// <summary>B's take that lost, kept on the quest as a conflict.</summary>
    private static readonly QuestOperation Lost = Op(QuestOperationKind.Conflict, "b", 2) with { Attempted = QuestStatus.Taken, Number = 2 };

    private static QuestOperation Marked(long sequence = 4, long take = 3, string machine = "b", long conflict = 2) =>
        Op(QuestOperationKind.Dismissed, machine, sequence) with
        {
            Dismisses = new QuestOperationRef("b", conflict), Forward = new QuestOperationRef(machine, take),
        };

    private static QuestOperation Plain(string machine, long sequence) =>
        Op(QuestOperationKind.Dismissed, machine, sequence) with { Dismisses = new QuestOperationRef("b", 2) };

    [Fact]
    public void A_marked_dismissal_is_the_takers_own_and_a_plain_one_is_not()
    {
        Assert.True(QuestLog.TakersOwn(Marked()));
        Assert.False(QuestLog.TakersOwn(Plain("b", 4)));
        // The kind alone still names a dismissal no taker's: the mark is the operation's.
        Assert.False(QuestLog.TakersOwn(QuestOperationKind.Dismissed));
    }

    /// <summary>
    /// A marked dismissal applies only to its own machine's take conflict, standing on the quest when it lands: a conflict already settled
    /// authorizes no take (D178 point 5). A plain one applies to any quest there is, settled or not, as it always did.
    /// </summary>
    [Fact]
    public void A_marked_dismissal_applies_only_on_its_own_take_conflict_still_standing()
    {
        var fresh = Op(QuestOperationKind.Taken, "b", 3);
        var taken = QuestLog.Replay([Published, Lost, fresh])!;
        Assert.Equal(QuestStatus.Taken, taken.Status);

        Assert.True(QuestLog.Applies(taken, Marked()));
        Assert.Empty(QuestLog.Step(taken, Marked())!.Conflicts);

        // Settled first by a plain dismissal: the mark applies no more, and a second plain one is harmless.
        var settled = QuestLog.Step(taken, Plain("a", 5))!;
        Assert.False(QuestLog.Applies(settled, Marked()));
        Assert.True(QuestLog.Applies(settled, Plain("b", 6)));

        // Another machine's conflict, a conflict that attempted a done, or a take another machine made: not this machine's to bring forward.
        Assert.False(QuestLog.Applies(taken, Marked(machine: "c")));
        var aDone = QuestLog.Replay([Published, Lost with { Attempted = QuestStatus.Done }, fresh])!;
        Assert.False(QuestLog.Applies(aDone, Marked()));
        Assert.False(QuestLog.Applies(taken, Marked() with { Forward = new QuestOperationRef("a", 3) }));
        Assert.False(QuestLog.Applies(null, Marked()));
    }

    /// <summary>
    /// The mark is made on its take (D69, WAITCLAIM2): where its machine's claim reads lost on a taken quest, the marked dismissal is
    /// judged lost with it, as a done or a wait is; a plain dismissal is no move on the take and never is.
    /// </summary>
    [Fact]
    public void On_a_take_that_lost_a_marked_dismissal_is_judged_lost_and_a_plain_one_is_not()
    {
        IReadOnlyList<QuestOperation> history = [Published, Op(QuestOperationKind.Taken, "a", 2) with { Number = 2 }, Lost with { Sequence = 3, Number = 3 }];
        var standing = QuestLog.Replay(history)!;

        Assert.True(QuestLog.OnALostTake(standing, history, Marked(conflict: 3)));
        Assert.False(QuestLog.OnALostTake(standing, history, Plain("b", 4) with { Dismisses = new QuestOperationRef("b", 3) }));

        // Made beside the fresh take that stands, it is no move on a lost one.
        IReadOnlyList<QuestOperation> afresh = [Published, Lost, Op(QuestOperationKind.Taken, "b", 3)];
        Assert.False(QuestLog.OnALostTake(QuestLog.Replay(afresh), afresh, Marked()));
    }

    /// <summary>A rebase forgets a marked dismissal it loses, as it forgets a plain one: only a move becomes a conflict.</summary>
    [Fact]
    public void A_lost_marked_dismissal_is_forgotten() =>
        Assert.Equal(QuestLoss.Forgotten, QuestLog.Lost(QuestOperationKind.Dismissed));
}
