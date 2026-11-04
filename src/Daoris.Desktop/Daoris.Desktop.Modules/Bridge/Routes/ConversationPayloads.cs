namespace Daoris.Desktop;

/// <summary>A session's conversation: the newest page, an earlier one (<c>Before</c>), or only what is newer (<c>After</c>).</summary>
public sealed record SessionHistoryRead(string Id, long? After, long? Before, long? Limit);

/// <summary>A session by its id: the press that ends its chat, stops its wait, reads its options or its queue, or starts from it.</summary>
public sealed record SessionIdPress(string Id, bool Said = false);

/// <summary>A session handed over to another agent; <c>Said</c> asks for the verb's own words.</summary>
public sealed record SessionHandOverPress(
    string Id, string Agent, string? Account, string? Why, string? AskedBy, string? Model, string? Effort, bool Said = false);

/// <summary>
/// The person's answer to a go-ahead an agent asked for, named by its <c>Number</c> on its <c>Ask</c>; the session goes on unless
/// <c>GoesOn</c> says false.
/// </summary>
public sealed record SessionGoAheadPress(string Id, string Ask, bool Approved, long? Number, string? Words, bool? GoesOn);

/// <summary>One option of a session's chat set to a value.</summary>
public sealed record SetSessionOptionPress(string Id, string Option, string Value);
