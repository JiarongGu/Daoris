namespace Daoris.Knowledge;

/// <summary>A number a remote gave one operation, named by the machine and sequence that made it.</summary>
public sealed record QuestAcceptance(string Machine, long Sequence, long Number);
