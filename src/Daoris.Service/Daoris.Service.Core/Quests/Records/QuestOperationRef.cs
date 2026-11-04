namespace Daoris.Knowledge;

/// <summary>
/// One operation, named where it was made: the machine and its sequence. What a dismissal names the
/// conflict by — the losing move keeps its machine and sequence when it becomes a conflict, so the name
/// is the same on every machine that holds it.
/// </summary>
public sealed record QuestOperationRef(string Machine, long Sequence);
