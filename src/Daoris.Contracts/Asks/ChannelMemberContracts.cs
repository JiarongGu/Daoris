namespace Daoris.Contracts.Asks;

// The doors of an agent outside Daoris on a channel, a local host's alone (CHANNELJOIN1): the person's admission once they confirmed its
// join, and the member's own read, post and leave, answered without the key and judged as the member's. A member's id is no session's.

/// <summary>
/// One member outside Daoris on a channel: its id, the channel's <c>Origin</c>, its own <c>Entry</c>, the <c>Agent</c> and
/// <c>Repository</c> it claimed, when it joined, last read or posted (<c>Seen</c>) and last read, and when and why it left
/// (<c>left</c> by itself, <c>removed</c> by the person, <c>expired</c> by the look). <c>Spent</c> is set on a listed member whose
/// channel's claims have expired: its ask has no quest open and no part runs.
/// </summary>
public sealed record ChannelMemberResponse(
    string Id, string Origin, string Entry, string Agent, string? Repository, DateTimeOffset Joined, DateTimeOffset? Seen = null,
    DateTimeOffset? Read = null, DateTimeOffset? Left = null, string? Why = null, bool? Spent = null);

/// <summary>The members still on a channel.</summary>
public sealed record ChannelMembersResponse(IReadOnlyList<ChannelMemberResponse> Members);

/// <summary>
/// The members still on a channel, the person's read: the channel of an <c>Ask</c> or of the line a session (<c>Line</c>) is a part of;
/// with neither, every member on this machine.
/// </summary>
public sealed record ListChannelMembers(string? Ask = null, string? Line = null) : IRoute<ChannelMembersResponse>
{
    public static string Method => "GET";
    public static string Path => "/api/channel/members";
}

/// <summary>
/// The person's admission of an agent outside Daoris to a channel, which their Confirm of its join presses: the channel by an <c>Ask</c>'s
/// id or a <c>Line</c>'s session, the <c>Agent</c> and <c>Repository</c> it claimed, and the <c>Confirmation</c> that admitted it. Answers
/// the member; refused with the channel's code (<c>not-found</c>, <c>no-session</c>, <c>helper</c>, <c>too-many</c>).
/// </summary>
public sealed record JoinChannel(string? Ask = null, string? Line = null, string? Agent = null, string? Repository = null, string? Confirmation = null)
    : IRoute<ChannelMemberResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/channel/members";
}

/// <summary>A member's read of its channel: its items after its own mark, or after <c>Since</c>, and how many were past its mark.</summary>
public sealed record MemberChannelResponse(
    string Member, string Origin, string? Ask, IReadOnlyList<ChannelItemResponse> Items, int Unread, DateTimeOffset? Mark);

/// <summary>A member's read of its channel, moving its own mark; refused not found, <c>not-member</c>, for no member or one that left.</summary>
/// <param name="Since">A moment, ISO 8601: what came after it, whatever the mark says.</param>
public sealed record ReadMemberChannel(string Id, string? Since = null) : IRoute<MemberChannelResponse>
{
    public static string Method => "GET";
    public static string Path => "/api/channel/members/{id}/messages";
}

/// <summary>
/// A member's post on its channel, under its own id: to everyone or one of the channel's repositories (<c>To</c>), answering a message
/// (<c>Re</c>) or not, a question or not, ordinary unless <c>Urgent</c>. A part's bounds hold; a session named in <c>To</c> is refused
/// <c>bad-part</c>, and a member that is none <c>not-member</c>.
/// </summary>
public sealed record PostMemberChannelMessage(
    string Id, string? Text = null, string? To = null, string? Re = null, bool? Question = null, bool? Urgent = null) : IRoute<ChannelPostResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/channel/members/{id}/messages";
}

/// <summary>
/// A membership ended: the member's own leave, or with the person's key their <i>Remove</i>, or, with the key and <c>Why</c> of
/// <c>expired</c>, the look's end of one whose claims expired or that was quiet a day. Refused <c>not-member</c> once it has.
/// </summary>
public sealed record LeaveChannel(string Id, string? Why = null) : IRoute<ChannelMemberResponse>
{
    public static string Method => "POST";
    public static string Path => "/api/channel/members/{id}/leave";
}
