namespace Daoris.Driver;

/// <summary>
/// The machine log's evidence line (EVID1b, D144 §5, D94): <c>evidence.checked</c>, one per read, with counts and codes and no
/// words: the quest, the session whose end was read (null for the terminal's), how the commit was chosen, how many items were
/// read and how many each code took, and what the door said.
/// </summary>
public sealed record EvidenceLine(string Event, IReadOnlyList<(string Key, object? Value)> Data);
