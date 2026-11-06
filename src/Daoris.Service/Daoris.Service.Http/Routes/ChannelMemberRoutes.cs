using System.Globalization;
using Daoris.Contracts.Asks;
using Daoris.Knowledge.Hosting;

namespace Daoris.Knowledge.Http.Routes;

/// <summary>
/// An agent outside Daoris on a channel (CHANNELJOIN1; the event-channel design §5.3, §5.4), a local host's alone: the person's admission,
/// pressed by their Confirm of the join, and the member's three doors, answered without the key and judged as the member's, its id the
/// one name it has. The person's key at the leave is their <i>Remove</i>.
/// </summary>
public sealed class ChannelMemberRoutes(HostWiring host) : IRouteModule
{
    /// <summary>The four doors, as <see cref="PersonDoors.Table"/> classes them.</summary>
    public static IReadOnlyList<Door> Doors { get; } =
    [
        new("POST", "/api/channel/members", DoorClass.Person, "an outside agent's admission to a channel")
            { Marks = OwnerMark.None("an outside agent's admission, which names no quest") },
        // The ids a listing holds are the members' only names, so it is the driver's read with the key, never an open one.
        new("GET", "/api/channel/members", DoorClass.Driver, "the members of a channel"),
        new("GET", "/api/channel/members/{id}/messages", DoorClass.Agent, "an outside agent's read of its channel"),
        new("POST", "/api/channel/members/{id}/messages", DoorClass.Agent, "an outside agent's message on its channel"),
        new("POST", "/api/channel/members/{id}/leave", DoorClass.Agent, "an outside agent's leave of its channel"),
    ];

    private ComposedService Service => host.Service;

    public void Map(RouteMap routes)
    {
        if (host.Mode != ServiceMode.Local) return;

        routes.Map<JoinChannel, ChannelMemberResponse>(JoinAsync);
        routes.Map<ListChannelMembers, ChannelMembersResponse>(ListAsync);
        routes.Map<ReadMemberChannel, MemberChannelResponse>(ReadAsync);
        routes.Map<PostMemberChannelMessage, ChannelPostResponse>(PostAsync);
        routes.Map<LeaveChannel, ChannelMemberResponse>(LeaveAsync);
    }

    private async Task<Answer<ChannelMemberResponse>> JoinAsync(JoinChannel request, HttpContext http, CancellationToken ct)
    {
        var joined = await Service.Channel.Members.JoinAsync(
            new ChannelJoin(request.Ask, request.Line, request.Agent, request.Repository, request.Confirmation), DateTimeOffset.UtcNow, ct);
        return joined.Member is { } member ? ToMember(member) : Refused(joined.Refusal);
    }

    private async Task<Answer<ChannelMembersResponse>> ListAsync(ListChannelMembers request, HttpContext http, CancellationToken ct)
    {
        var standing = await Service.Channel.Roll.ListAsync(request.Ask, request.Line, ct);
        return new ChannelMembersResponse([.. standing.Select(each => ToMember(each.Member) with { Spent = each.Spent })]);
    }

    private async Task<Answer<MemberChannelResponse>> ReadAsync(ReadMemberChannel request, HttpContext http, CancellationToken ct)
    {
        DateTimeOffset? since = null;
        if (!string.IsNullOrEmpty(request.Since))
        {
            if (!DateTimeOffset.TryParse(request.Since, CultureInfo.InvariantCulture, out var after)) return Refusal.BadRequest("`since` is a moment, ISO 8601.");
            since = after;
        }

        var read = await Service.Channel.Members.ReadAsync(request.Id, since, DateTimeOffset.UtcNow, ct);
        if (read.Read is not { } channel) return Refused(read.Refusal);
        return new MemberChannelResponse(
            request.Id.Trim(), read.Origin!, read.Ask?.Id ?? ChannelDesk.AskOfOrigin(read.Origin!),
            [.. channel.Items.Select(item => ChannelRoutes.ToChannelItem(item))], channel.Unread, channel.Mark);
    }

    private async Task<Answer<ChannelPostResponse>> PostAsync(PostMemberChannelMessage request, HttpContext http, CancellationToken ct)
    {
        var posted = await Service.Channel.Members.PostAsync(
            request.Id, request.Text, request.To, request.Re, request.Question ?? false, request.Urgent ?? false, DateTimeOffset.UtcNow, ct);
        return posted.Message is { } message ? new ChannelPostResponse(ChannelRoutes.ToChannelItem(message)) : Refused(posted.Refusal);
    }

    private async Task<Answer<ChannelMemberResponse>> LeaveAsync(LeaveChannel request, HttpContext http, CancellationToken ct)
    {
        // Without the key it is the member's own leave; with it, the person's Remove, or the look's end of one spent or quiet.
        var why = host.Gate.IsAgent(http) ? ChannelMember.ByItself : request.Why == ChannelMember.Expired ? ChannelMember.Expired : ChannelMember.ByPerson;
        var left = await Service.Channel.Members.LeaveAsync(request.Id, why, DateTimeOffset.UtcNow, ct);
        return left.Member is { } member ? ToMember(member) : Refused(left.Refusal);
    }

    private static ChannelMemberResponse ToMember(ChannelMember member) =>
        new(member.Id, member.Origin, member.Entry, member.Agent, member.Repository, member.Joined, member.Seen, member.Read, member.Left, member.Why);

    private static Refusal Refused(ChannelRefusal refusal)
    {
        var code = ChannelDesk.Spell(refusal);
        return refusal is ChannelRefusal.NotMember or ChannelRefusal.NotFound or ChannelRefusal.NoSession
            ? Refusal.NotFound($"No such channel or member here: {code}.", code)
            : Refusal.BadRequest(code, code);
    }
}
