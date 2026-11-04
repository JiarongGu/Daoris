namespace Daoris.Knowledge;

/// <summary>
/// The person's reopen of a quest's own submission (FABRIC-CLOSE1c4, §E5), as the quest keeps it until a later submission answers
/// it: the <see cref="QuestOperationKind.Reopened"/> operation by its machine and sequence, the submission it reopened
/// (<paramref name="Of"/>), the person's words, and when.
/// </summary>
/// <param name="Session">
/// The session that said the reopened submission's <c>ready</c> (FABRIC-CLOSE1c8), which the reopen clears with the submission: the
/// one its taker's machine tells. Null where none did (the respond door) and for a reopen a build before it kept.
/// </param>
public sealed record QuestReopen(string Machine, long Sequence, QuestOperationRef Of, string? Note, DateTimeOffset At, string? Session = null);
