namespace Daoris.Knowledge;

/// <summary>
/// A line's own channel as the person reads it: its origin, its messages oldest first, and by message id what each one to one
/// part reached in that part's line.
/// </summary>
public sealed record LineChannelRead(string Origin, IReadOnlyList<ChannelMessage> Messages, IReadOnlyDictionary<string, string> Receipts);
