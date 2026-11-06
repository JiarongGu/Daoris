using Daoris.Driver;
using Shenora.Core.Ipc;
using static Daoris.Desktop.DriverArguments;

namespace Daoris.Desktop;

/// <summary>The channel whose outside agents the page asks for: an ask's, or the line a session is a part of.</summary>
public sealed record ChannelMembersAsk(string? Ask, string? Line);

/// <summary>The person's <i>Remove</i> on one outside agent.</summary>
public sealed record ChannelMemberRemove(string Member);

/// <summary>The agents outside Daoris on a channel, as the page lists them.</summary>
public sealed record ChannelMembersAnswer(IReadOnlyList<ChannelMemberView> Members);

/// <summary>What <i>Remove</i> did: whether the membership ended, or the service's code where it did not.</summary>
public sealed record ChannelMemberRemoved(bool Removed, string? Code);

/// <summary>
/// The window's doors on the agents outside Daoris on a channel (CHANNELJOIN1c; the event-channel design §5.8): who is on a line's
/// channel, for <i>At work</i> (a read, as the channel's own is), and the person's <i>Remove</i>, which is the window's alone: no terminal
/// ends a membership over the pipe. The member's own doors are the service's.
/// </summary>
public sealed class ChannelOutsideRoutes(DriverLoop loop)
{
    private readonly DriverLoop _loop = loop;

    /// <summary>The members still on the channel of an ask or of a session's line, each with when it joined and last read or posted.</summary>
    [DriverRoute("CHANNEL_MEMBERS", PipeClass.Open)]
    [ByEvent("CHANNEL_MEMBERS", Untold = "the service's members on a channel, which a terminal's join or leave and the service's own expiry move and no scope or slice tells")]
    private async Task<object?> ChannelMembersAsync(ChannelMembersAsk press, CancellationToken cancellationToken)
    {
        var service = _loop.Service ?? throw NotReady();
        return new ChannelMembersAnswer(await service.ChannelMembersAsync(press.Ask?.Trim().TrimStart('#'), press.Line?.Trim(), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>The person's <i>Remove</i>: the membership ends as <c>removed</c> by their key, and the member reads and posts nothing after.</summary>
    [DriverRoute("CHANNEL_MEMBER_REMOVE", PipeClass.Window)]
    [Owner("CHANNEL_MEMBER_REMOVE", None = OwnerNone.NamesNothing)]
    [Publishes("CHANNEL_MEMBER_REMOVE", StateScopes.Asks)]
    private async Task<object?> ChannelMemberRemoveAsync(ChannelMemberRemove press, CancellationToken cancellationToken)
    {
        var service = _loop.Service ?? throw NotReady();
        var ended = await service.EndMemberAsync(press.Member.Trim(), expired: false, cancellationToken).ConfigureAwait(false);
        return new ChannelMemberRemoved(ended.Member is not null, ended.Code);
    }
}
