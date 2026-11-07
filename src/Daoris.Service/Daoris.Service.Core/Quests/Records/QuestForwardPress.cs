namespace Daoris.Knowledge;

/// <summary>
/// A bring-forward this machine made: the conflict it settles and the take it made, kept by this machine alone and
/// never pushed. The log forgets a bring-forward whose take lost, so this is what still says the second loss is one.
/// </summary>
public sealed record QuestForwardPress(QuestOperationRef Conflict, QuestOperationRef Take, DateTimeOffset At);
