namespace Daoris.Knowledge;

/// <summary>
/// What a graph's close names its Done by: the invocation's key, which the
/// driver persists before it sends and sends again after a timeout, and the close token it computed from the unit, the
/// revision, the graph's digest and the prerequisite acceptances. A second close carrying the same key answers the first's
/// outcome, so a close is never applied twice.
/// </summary>
public sealed record QuestClose(string Key, string Token);
