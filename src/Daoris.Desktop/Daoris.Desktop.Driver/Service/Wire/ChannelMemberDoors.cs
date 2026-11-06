using Daoris.Contracts.Asks;

namespace Daoris.Driver;

/// <summary>
/// A member outside Daoris as a door answers it: its id, its channel's origin, the agent and repository it claimed, and when it left; a
/// listed one adds when it joined, when it last read or posted, and whether its channel's claims have expired.
/// </summary>
public sealed record ChannelMemberView(
    string Id, string Origin, string Agent, string? Repository, DateTimeOffset? Left, string? Why,
    DateTimeOffset? Joined = null, DateTimeOffset? Seen = null, bool Spent = false)
{
    /// <summary>The reason the look's end of a membership gives, which the service keeps.</summary>
    public const string Expired = "expired";
}

/// <summary>What a member's door answered: the member, or the service's code where it refused.</summary>
public sealed record ChannelMemberAnswer(ChannelMemberView? Member, string? Code);

/// <summary>A member's read: its items in time order, how many were past its mark, or the service's code where it refused.</summary>
public sealed record ChannelMemberRead(IReadOnlyList<ChannelItemView> Items, int Unread, string? Code);

/// <summary>
/// The doors of an agent outside Daoris on a channel (CHANNELJOIN1; the event-channel design §5.3, §5.4): the person's admission, which
/// the shell presses on their Confirm, and the member's read, post and leave, which carry no key and are judged as the member's. A
/// refusal is an answer carrying the service's code; a host from before the members answers none, which is said.
/// </summary>
public static class ChannelMemberDoors
{
    /// <summary>The person's admission of the agent to the channel <paramref name="join"/> names.</summary>
    /// <exception cref="DriverException">The host answered neither a member nor a code.</exception>
    public static async Task<ChannelMemberAnswer> JoinChannelAsync(this ServiceClient service, JoinChannel join, CancellationToken ct = default) =>
        Member(await service.SendAsync(join, ct).ConfigureAwait(false), "admit the agent to the channel");

    /// <summary>The member's own read of its channel, after its mark or after <paramref name="since"/>.</summary>
    /// <exception cref="DriverException">The host answered neither the channel nor a code.</exception>
    public static async Task<ChannelMemberRead> MemberReadAsync(this ServiceClient service, string member, DateTimeOffset? since, CancellationToken ct = default)
    {
        var sent = await service.SendAsync(new ReadMemberChannel(member, since?.ToString("O")), ct).ConfigureAwait(false);
        if (sent.Answer is { } read) return new([.. read.Items.Select(ChannelView.ItemOf).OfType<ChannelItemView>()], read.Unread, null);
        return AskDoorViews.CodeOf(sent.Reply.Payload) is { Length: > 0 } code
            ? new([], 0, code)
            : throw new DriverException($"the service would not read member `{member}`'s channel: {sent.Reply.Payload}");
    }

    /// <summary>The member's post on its channel, under its own id.</summary>
    /// <exception cref="DriverException">The host answered neither the post nor a code.</exception>
    public static async Task<ChannelPosted> MemberPostAsync(
        this ServiceClient service, string member, string text, string? to, string? re, bool question, bool urgent, CancellationToken ct = default)
    {
        var sent = await service.SendAsync(new PostMemberChannelMessage(member, text, to, re, question, urgent), ct).ConfigureAwait(false);
        if (sent.Answer is { } posted && ChannelView.ItemOf(posted.Message) is { } item) return new ChannelPosted(true, item, null);
        return AskDoorViews.CodeOf(sent.Reply.Payload) is { Length: > 0 } code
            ? new ChannelPosted(false, null, code)
            : throw new DriverException($"the service would not take member `{member}`'s post: {sent.Reply.Payload}");
    }

    /// <summary>The member's own leave of its channel.</summary>
    /// <exception cref="DriverException">The host answered neither a member nor a code.</exception>
    public static async Task<ChannelMemberAnswer> MemberLeaveAsync(this ServiceClient service, string member, CancellationToken ct = default) =>
        Member(await service.SendAsync(new LeaveChannel(member), ct).ConfigureAwait(false), $"let member `{member}` leave");

    /// <summary>The members still on the channel of an ask or of the line a session is a part of, or on every channel here with neither (the person's read).</summary>
    /// <exception cref="DriverException">The host answered neither the members nor a code.</exception>
    public static async Task<IReadOnlyList<ChannelMemberView>> ChannelMembersAsync(
        this ServiceClient service, string? ask, string? line, CancellationToken ct = default)
    {
        var sent = await service.SendAsync(new ListChannelMembers(ask, line), ct).ConfigureAwait(false);
        return sent.Answer is { } listed
            ? [.. listed.Members.Select(View)]
            : throw new DriverException($"the service would not list the channel's members: {sent.Reply.Payload}");
    }

    /// <summary>
    /// A membership ended by the person's key, never the member's: <c>removed</c> for the person's <i>Remove</i>, <c>expired</c> for the
    /// look's end of one spent or quiet.
    /// </summary>
    /// <exception cref="DriverException">The host answered neither a member nor a code.</exception>
    public static async Task<ChannelMemberAnswer> EndMemberAsync(this ServiceClient service, string member, bool expired, CancellationToken ct = default) =>
        Member(await service.SendAsync(new LeaveChannel(member, expired ? ChannelMemberView.Expired : null), ct).ConfigureAwait(false), $"end member `{member}`");

    private static ChannelMemberView View(ChannelMemberResponse member) =>
        new(member.Id, member.Origin, member.Agent, member.Repository, member.Left, member.Why, member.Joined, member.Seen, member.Spent == true);

    private static ChannelMemberAnswer Member(Sent<ChannelMemberResponse> sent, string what) =>
        sent.Answer is { } member
            ? new(View(member), null)
            : AskDoorViews.CodeOf(sent.Reply.Payload) is { Length: > 0 } code
                ? new(null, code)
                : throw new DriverException($"the service would not {what}: {sent.Reply.Payload}");
}
