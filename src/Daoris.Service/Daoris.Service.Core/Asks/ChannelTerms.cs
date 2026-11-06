namespace Daoris.Knowledge;

/// <summary>
/// The channel's bounds (SESSONE2a; the co-working design §2.6). First values, read again at the canary: a message's text, how many
/// messages one part posts, and how many of the newest a next part is handed.
/// </summary>
public static class ChannelBounds
{
    /// <summary>The most characters one message holds. A link travels as text, a file not at all in this version.</summary>
    public const int MaxText = 4000;

    /// <summary>The most messages one part (one session record) posts.</summary>
    public const int MaxPerPart = 10;

    /// <summary>The most messages a next part is handed on starting: the newest, within the instruction's share (SESSONE2b).</summary>
    public const int HandedNewest = 20;

    /// <summary>
    /// The most of one part's posts kept urgent (COWORK1d, D188 point 2; the message-urgency design §2.2, trap 10): a first value, a
    /// judgement, within the part's <see cref="MaxPerPart"/>. A further urgent post is kept ordinary, and its poster told so.
    /// </summary>
    public const int UrgentPerPart = 3;

    /// <summary>The most members outside Daoris one channel holds at once (CHANNELJOIN1, the event-channel design §5.6).</summary>
    public const int MembersPerChannel = 3;

    /// <summary>The most members outside Daoris this machine holds at once, across its channels.</summary>
    public const int MembersHere = 8;
}

/// <summary>Why the channel did not do what was asked of it, by a code: a tool says it in a sentence, a door answers the code.</summary>
public enum ChannelRefusal
{
    None,

    /// <summary>No words, or only whitespace.</summary>
    Empty,

    /// <summary>Words past <see cref="ChannelBounds.MaxText"/> characters.</summary>
    TooLong,

    /// <summary>No ask under that id on this machine.</summary>
    NotFound,

    /// <summary>The connector names no session this machine holds.</summary>
    NoSession,

    /// <summary>The session's ask is not held on this machine, so its channel is not here.</summary>
    NoAsk,

    /// <summary>A helper or a reviewer: each answers one question, and neither posts nor reads (D155, D163).</summary>
    Helper,

    /// <summary>A <c>to</c> that is not one of the ask's repositories.</summary>
    BadTo,

    /// <summary>A <c>re</c> the ask's channel does not hold.</summary>
    BadRe,

    /// <summary>The part has posted <see cref="ChannelBounds.MaxPerPart"/> messages already.</summary>
    TooMany,

    /// <summary>A <c>to</c> naming a session that is no other part of the poster's own line of work.</summary>
    BadPart,

    /// <summary>A <c>to</c> naming a part on another machine: a message to one part never travels.</summary>
    PartElsewhere,

    /// <summary>No member outside Daoris under that id holds a place on a channel: never admitted, or it left.</summary>
    NotMember,
}

/// <summary>
/// What an entry acknowledged of a channel message (COWORK1d, D188 point 6; the message-urgency design §6), one row per ask, entry and
/// message, ranked so none moves back: <see cref="Partly"/>, <see cref="Taking"/>, <see cref="Sent"/>, <see cref="Read"/>,
/// <see cref="Taken"/>.
/// </summary>
public static class ChannelAcks
{
    /// <summary>A part of the entry claimed it and holds it for its door: one part takes a message.</summary>
    public const string Taking = "taking";

    /// <summary>Written to the part's harness: the write returned.</summary>
    public const string Sent = "sent";

    /// <summary>The door confirmed the part took it, or a start's packet carried it whole.</summary>
    public const string Taken = "taken";

    /// <summary>The agent's own <c>channel_read</c> returned it whole.</summary>
    public const string Read = "read";

    /// <summary>A start's packet carried it cut, or counted it among the older ones left out: a new part is handed it again.</summary>
    public const string Partly = "partly";

    /// <summary>A part let its claim go unsent (its run ended with nothing else to say): never kept, the claim's row leaves.</summary>
    public const string Released = "released";

    /// <summary>Whether a door names a state this build keeps or lets go.</summary>
    public static bool Known(string? state) => state is Taking or Sent or Taken or Read or Partly or Released;

    /// <summary>Whether the entry has the message: sent, taken or read. A claim and a partial hand are not yet.</summary>
    public static bool Settles(string? state) => state is Sent or Taken or Read;
}

/// <summary>One entry's acknowledgment of one message: its state, the part's session where one wrote it, and when.</summary>
public sealed record ChannelAck(string State, string? Session, DateTimeOffset At);

/// <summary>
/// What a message reached in one repository's line of work, as the person reads it (COWORK1d, the design §7, §8.4): never more than
/// was observed. Answered is a reply from that repository, never obeyed.
/// </summary>
public static class ChannelReceipts
{
    public const string Waiting = "waiting";

    /// <summary>A running part claimed it and holds it for its door.</summary>
    public const string Held = "held";

    public const string Sent = "sent";

    public const string Taken = "taken";

    public const string Read = "read";

    public const string Partly = "partly";

    public const string Answered = "answered";

    /// <summary>Its ask has no quest open and no part of the entry runs here: no part will come (the design §4.2).</summary>
    public const string NotReached = "not-reached";
}

/// <summary>A message's receipt in one repository it addresses.</summary>
public sealed record ChannelReceipt(string Repository, string State);
