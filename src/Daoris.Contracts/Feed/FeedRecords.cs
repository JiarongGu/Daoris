namespace Daoris.Contracts.Feed;

/// <summary>
/// One entry of a knowledge feed. <c>FirstLine</c> and <c>LastLine</c> are the lines of its file an entry is, taken only
/// where they can be its body's; a feed that names none is read without them.
/// </summary>
public sealed record FeedEntryRecord(
    string? Kind, string? Title, string? Body, string? RelativePath, string? Anchor, int? FirstLine = null, int? LastLine = null);

/// <summary>Which commit a repository's knowledge, code map and declaration stand on at this deployment; null where nothing has fed.</summary>
public sealed record FeedHeldResponse(string Repository, string? Knowledge, string? CodeMap, string? Registration);

public sealed record FeedResponse(int Accepted, string Message);

/// <summary>
/// A refusal the client should report rather than fix: a stale or branch feed is the system working, and the machine that is
/// behind is simply behind. <c>Information</c> lets a sync loop tell news from breakage without matching on the sentence.
/// </summary>
public sealed record FeedRefusalResponse(string Error, bool Information);
