namespace Daoris.Desktop;

/// <summary>A session's conversation: the newest page, an earlier one (<c>Before</c>), or only what is newer (<c>After</c>).</summary>
public sealed record SessionHistoryRead(string Id, long? After, long? Before, long? Limit);

/// <summary>A running turn stopped; <c>Keep</c> keeps what waits for the next turn, <c>Said</c> asks for the verb's own words.</summary>
public sealed record CancelTurnPress(string Id, bool Said = false, bool Keep = false);

/// <summary>A word that waits behind a running turn moved to another session.</summary>
public sealed record SessionQueueMovePress(string Session, string Id, string To);

/// <summary>
/// A waiting word taken back (no <c>Text</c>), reworded (a text) or raised (a <c>Level</c> alone). The text is the value as sent, since
/// words reworded to blank are refused rather than read as none.
/// </summary>
public sealed record SessionQueueEditPress(string Session, string Id, System.Text.Json.JsonElement? Text, string? Level, bool Said = false);

/// <summary>A session by its id: the press that ends its chat, stops its wait, reads its options or its queue, or starts from it.</summary>
public sealed record SessionIdPress(string Id, bool Said = false);

/// <summary>
/// A session handed over to another agent, or, with no <c>Agent</c>, gone on in a new session on the walk's pick; <c>Said</c> asks for the
/// verb's own words.
/// </summary>
public sealed record SessionHandOverPress(
    string Id, string? Agent, string? Account, string? Why, string? AskedBy, string? Model, string? Effort, bool Said = false);

/// <summary>
/// The person's answer to a go-ahead an agent asked for, named by its <c>Number</c> on its <c>Ask</c>; the session goes on unless
/// <c>GoesOn</c> says false.
/// </summary>
public sealed record SessionGoAheadPress(string Id, string Ask, bool Approved, long? Number, string? Words, bool? GoesOn);

/// <summary>
/// A conversation started in a repository, on the <c>Door</c> named or Chat's order, under a <c>Profile</c>, in a tree of its own or not
/// (<c>OwnTree</c>, none leaving it to the repository's standing choice). <c>Text</c> are the opening words and <c>Files</c> what is attached
/// to them, each <c>{ name, content }</c> with the bytes as base64; <c>RequestId</c> keeps a press that arrives twice to one start.
/// </summary>
public sealed record StartChatPress(
    string Repository, string? Door, string? Profile, string? Text, System.Text.Json.JsonElement? Files, string? RequestId, bool? OwnTree);

/// <summary>
/// The person's words to a session. <c>Level</c> is when they should reach a working session; <c>Question</c> names the park's question
/// they answer; <c>Draft</c> is the workflow draft open in the editor; <c>Preface</c> is where the person is; <c>Key</c> keeps a press that
/// arrives twice to one word; <c>Said</c> asks for the verb's own words, and then <c>Paths</c> name the files on this machine.
/// </summary>
public sealed record SessionInputPress(
    string Id, string Text, string? Level, string? Question, System.Text.Json.JsonElement? Draft, System.Text.Json.JsonElement? Files,
    string? Preface, string? Key, IReadOnlyList<string>? Paths, bool Said = false);

/// <summary>One option of a session's chat set to a value.</summary>
public sealed record SetSessionOptionPress(string Id, string Option, string Value);
