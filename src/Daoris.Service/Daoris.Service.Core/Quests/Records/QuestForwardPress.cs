namespace Daoris.Knowledge;

/// <summary>
/// A bring-forward this machine made (SYNCMERGE1b0): the conflict it was to settle and the take it made, kept by this machine alone and
/// never pushed. The log forgets a bring-forward whose take lost (D178 point 5), so this is what still says the second loss was one.
/// </summary>
public sealed record QuestForwardPress(QuestOperationRef Conflict, QuestOperationRef Take, DateTimeOffset At);
