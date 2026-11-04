using System.Text.Json;

namespace Daoris.Knowledge;

/// <summary>
/// One kind of quest operation, registered with <see cref="QuestOperationAttribute"/> under the kind its log rows are written with and
/// built once per process from a public constructor taking nothing: what a row of the kind carries, and what the store's rebase and a
/// remote's push ask of the kind. Its verbs take the <see cref="QuestTx"/> the store's door opened, and the door reaches them by the
/// module's type, so a verb's callers are unchanged by where the kind lives.
/// </summary>
internal interface IQuestOperation
{
    /// <summary>The kind it is, which its mark names as the log writes it.</summary>
    QuestOperationKind Kind { get; }

    /// <summary>
    /// The payload a row of the kind holds, written whole as one object. A field is written only when it is set, so a row from before
    /// it reads as it did, and an older build reads it as absent.
    /// </summary>
    void WritePayload(Utf8JsonWriter writer, QuestOperation operation);

    /// <summary>The kind's own fields read from a row's payload onto <paramref name="read"/>, which holds what every kind reads.</summary>
    QuestOperation ReadPayload(QuestOperation read, JsonElement payload);

    /// <summary>Whether a remote keeps one pushed on a quest it deleted, which the table applies it to no longer (D95).</summary>
    bool KeptOnADeletedQuest => false;

    /// <summary>
    /// What else goes in a rebase's write once a pending operation of the kind lost (<see cref="QuestLog.Lost"/>), past its own row:
    /// the conflicts that made, which the rebase reports beside its own.
    /// </summary>
    /// <param name="standing">The quest as the rebase has replayed it so far.</param>
    /// <param name="takeLost">Whether it lost with this machine's take on the quest.</param>
    Task<IReadOnlyList<QuestOperation>> LostAsync(QuestTx tx, QuestOperation operation, Quest? standing, bool takeLost) =>
        Task.FromResult<IReadOnlyList<QuestOperation>>([]);
}
