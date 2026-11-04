namespace Daoris.Knowledge;

/// <summary>
/// One message on an ask's channel (SESSONE2a). Kept as the record shape that can travel (SESSONE2d): an id the poster's machine made
/// with <see cref="Origin"/> naming the machine it came from where it was not made here, and words that carry no machine path.
/// </summary>
/// <param name="Id">Short and stable; <c>origin/id</c> across machines.</param>
/// <param name="Ask">The ask it is on.</param>
/// <param name="Workspace">The ask's circle, which decides where the record travels.</param>
/// <param name="At">When it was posted.</param>
/// <param name="Session">The session that posted it; null for the person.</param>
/// <param name="Repository">The poster's repository, for a session.</param>
/// <param name="Agent">The poster's harness adapter, a name and never a model (D24), for a session.</param>
/// <param name="Entry">The poster's entry (D167 point 2), for a session: its read mark's key.</param>
/// <param name="To">The one repository it is addressed to; null for everyone.</param>
/// <param name="Re">The message it answers, where it answers one.</param>
/// <param name="Question">Whether its poster marked it a question.</param>
/// <param name="Text">The words as written, at most <see cref="ChannelBounds.MaxText"/> characters.</param>
/// <param name="Origin">The machine that posted it, where not this one; null for this machine's own.</param>
public sealed record ChannelMessage(
    string Id, string Ask, string Workspace, DateTimeOffset At, string? Session, string? Repository, string? Agent,
    string? Entry, string? To, string? Re, bool Question, string Text, string? Origin = null) : IChannelItem
{
    /// <summary>Whether the person wrote it: no session did. A session's message is that agent's claim, never the person's.</summary>
    public bool ByPerson => Session is null;

    /// <summary>A question no message has answered yet; false for every other message. Read from the channel, never stored.</summary>
    public bool Open { get; init; }

    /// <summary>
    /// When its sender asked it to reach a working session (COWORK1d, D188 point 2; the message-urgency design §9): true, urgent, at
    /// the session's next step where its door takes words then; false, ordinary, at a natural point. Said on every message kept from
    /// COWORK1d on, decided in the insert's own statement (<see cref="ChannelBounds.UrgentPerPart"/>). Null only on a message from
    /// before, or from an older teammate's machine, which keeps its old reach; null never means ordinary.
    /// </summary>
    public bool? Urgent { get; init; }

    /// <summary>The origin of a message kept in <c>channel_addressed</c> (<see cref="ChannelAddressedStore"/>); null for an ask's.</summary>
    public string? Channel { get; init; }

    /// <summary>The one part it is to, a session whose line's head takes it; null for every part.</summary>
    public string? Part { get; init; }
}
