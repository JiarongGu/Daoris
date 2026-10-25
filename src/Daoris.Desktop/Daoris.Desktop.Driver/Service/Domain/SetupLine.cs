namespace Daoris.Driver;

/// <summary>
/// A workspace plan's line in the machine log (WSSETUP6, D94): its event and its fields, names, words and counts only,
/// never a sentence or a path. The catalogue is <see cref="SetupLines"/>, so every writer writes the same lines.
/// </summary>
public sealed record SetupLine(string Event, IReadOnlyList<(string Key, object? Value)> Data);
