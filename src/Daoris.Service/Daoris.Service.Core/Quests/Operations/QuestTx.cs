using Microsoft.Data.Sqlite;

namespace Daoris.Knowledge;

/// <summary>
/// One write on the quest store as an operation's verb reaches it: the store's doors bound to the transaction the store's own door
/// opened, so a verb replays, judges and writes under one lock and never opens a write of its own.
/// </summary>
internal sealed class QuestTx(QuestStore store, SqliteTransaction transaction, CancellationToken ct)
{
    /// <summary>The store's machine, which every operation it writes is stamped with.</summary>
    internal string Machine => store.Machine;

    /// <summary>The token the write runs on: once begun, nobody cancels it.</summary>
    internal CancellationToken Ct => ct;

    internal Task<Quest?> FindAsync(string id) => store.FindAsync(id, transaction, ct);

    internal Task<IReadOnlyList<QuestOperation>> HistoryAsync(string id) => store.HistoryAsync(id, transaction, ct);

    /// <summary>A quest's history from its row, for a quest the log never saw.</summary>
    internal Task<IReadOnlyList<QuestOperation>> GiveHistoryAsync(Quest quest) => store.GiveHistoryAsync(quest, transaction, ct);

    /// <summary>
    /// Appends <paramref name="draft"/> under this machine and its next sequence, each field kept only on the kinds that carry it; its
    /// machine and sequence are the store's. Answers it as it reads back.
    /// </summary>
    internal Task<QuestOperation> AppendAsync(QuestOperation draft) => store.AppendAsync(
        draft.Quest, draft.Kind, draft.At, draft.Note, draft.Published, transaction, ct, draft.Dismisses, draft.Answers, draft.WhileOpen,
        draft.Evidence, draft.SetUp, draft.Verdict, draft.NoteParts, draft.Close, draft.Submission, draft.Session, draft.Forward,
        draft.Choice);

    internal Task WriteCacheAsync(Quest quest) => store.WriteCacheAsync(quest, transaction, ct);

    /// <summary>The chain's next step <paramref name="quest"/>'s close or submission published, where it stands; else null.</summary>
    internal Task<Quest?> SuccessorAsync(Quest quest) => Of<QuestPublishOperation>().SuccessorAsync(this, quest);

    /// <summary>The step that seals <paramref name="quest"/>'s current submission, or null where none does.</summary>
    internal Task<Quest?> SealingAsync(Quest quest) => Of<QuestSubmitOperation>().SealingAsync(this, quest);

    /// <summary>The chain's next step a close, a yes or a verdict on <paramref name="closed"/> lets go, published; null where none is.</summary>
    internal Task<Quest?> PublishNextAsync(Quest closed, DateTimeOffset now) => Of<QuestPublishOperation>().PublishNextAsync(this, closed, now);

    /// <summary>A rebind of <paramref name="id"/> onto <paramref name="from"/>, judged by the replay's rule; null where it refused it.</summary>
    internal Task<Quest?> ReboundInAsync(string id, QuestOperationRef from, bool whileOpen, DateTimeOffset now) =>
        Of<QuestReboundOperation>().ReboundInAsync(this, id, from, whileOpen, now);

    /// <summary>
    /// The follow-ups only a lost <paramref name="published"/> on <paramref name="parent"/> published, forgotten with it; where the
    /// parent's take lost to the person's reopen, <paramref name="reopened"/> says so on theirs.
    /// </summary>
    internal Task<IReadOnlyList<QuestOperation>> ForgetFollowUpsAsync(string parent, bool takeLost, string published, bool reopened = false) =>
        store.ForgetFollowUpsAsync(parent, takeLost, published, reopened, transaction, ct);

    /// <summary>The module of type <typeparamref name="T"/>, whose verbs another kind's verb calls.</summary>
    internal T Of<T>()
        where T : IQuestOperation => QuestOperations.Registered.Of<T>();

    /// <summary>The store's clock, which a take measures its wait for the write by.</summary>
    internal TimeProvider Time => store.Time;

    /// <summary>The newer version a store is stamped by, which refuses a write this build would make; null otherwise.</summary>
    internal int? NewerVersion => store.NewerVersion;

    /// <summary>A command in this write's transaction, for a table an operation keeps beside the log.</summary>
    internal SqliteCommand Command()
    {
        var command = store.Command();
        command.Transaction = transaction;
        return command;
    }
}
