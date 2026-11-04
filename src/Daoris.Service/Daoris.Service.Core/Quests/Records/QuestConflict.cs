namespace Daoris.Knowledge;

/// <summary>A move that lost to another machine's (D68 §5) — kept on the quest, for a person.</summary>
/// <param name="Machine">The machine whose move it was.</param>
/// <param name="Attempted">What it tried to move the quest to.</param>
/// <param name="Note">Its note or reason, as it was given.</param>
/// <param name="At">When it was made.</param>
/// <param name="Sequence">
/// The losing move's sequence on <paramref name="Machine"/> — with it, the conflict's name everywhere,
/// which is what a dismissal names (SYNC6c). Zero only in a cache written before dismissals, which the
/// store refills from the log as it opens.
/// </param>
public sealed record QuestConflict(string Machine, QuestStatus Attempted, string? Note, DateTimeOffset At, long Sequence = 0);
