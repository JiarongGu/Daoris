using Daoris.Contracts.Parts;

namespace Daoris.Knowledge;

/// <summary>
/// Registers an <see cref="IQuestOperation"/> with the quest store under the kind its log rows are written with
/// (<see cref="QuestRecordCodec.KindText"/>). The store builds every marked class once, as it opens, and refuses there a kind declared twice
/// and a mark naming another kind than its operation's.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
internal sealed class QuestOperationAttribute(string name, QuestOperationPhase phase, params string[] after) : PartAttribute(name, phase, after);
