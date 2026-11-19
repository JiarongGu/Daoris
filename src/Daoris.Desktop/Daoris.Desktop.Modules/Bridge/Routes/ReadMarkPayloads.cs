namespace Daoris.Desktop;

/// <summary>A conversation's read mark on this machine, by the conversation's id (a record's line, else its own id).</summary>
public sealed record ReadMarkRead(string Conversation);

/// <summary>
/// A conversation's read mark moved to the newest event of a part the person had on screen: its number, its time as the record keeps
/// it, and how many of its run's steps had passed, where its session runs any.
/// </summary>
public sealed record ReadMarkMove(string Conversation, string Part, long Seq, string At, long? Steps);
