namespace Daoris.Knowledge;

/// <param name="Refusal"><see cref="QuestCloseRefusal.None"/> when every named quest is Done.</param>
/// <param name="Message">The full answer, phrased once here: what closed, or which check refused, on which quest.</param>
/// <param name="Quests">Every named quest as it now stands, when the close wrote them or answered its first outcome again.</param>
public sealed record QuestCloseOutcome(QuestCloseRefusal Refusal, string Message, IReadOnlyList<Quest> Quests);
