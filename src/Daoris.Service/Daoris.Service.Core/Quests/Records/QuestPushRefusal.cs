namespace Daoris.Knowledge;

/// <summary>A quest a remote would not take, and why, in its own words.</summary>
public sealed record QuestPushRefusal(string Quest, string Reason);
