namespace Daoris.Driver;

/// <summary>
/// The machine log's evidence line: <c>evidence.checked</c>, one per read, with counts and codes and no
/// words: the quest, the session whose end is read (null for the terminal's), how the commit is chosen, how many items are
/// read and how many each code took, and what the door said.
/// </summary>
public sealed record EvidenceLine(string Event, IReadOnlyList<(string Key, object? Value)> Data);
