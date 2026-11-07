using Daoris.Contracts.Asks;
using Daoris.Driver;
using Shenora.Core.Ipc;
using static Daoris.Desktop.DriverArguments;

namespace Daoris.Desktop;

/// <summary>An agent outside Daoris asking to join a channel: an ask's or a line's (a session of it), the agent and repository it claims.</summary>
public sealed record ChannelJoinPress(string? Ask, string? Line, string? Agent, string? Repository, bool Said = false);

/// <summary>
/// The join of an agent outside Daoris to a channel: the agent's own press,
/// <c>daoris ask channel join</c>, which the person confirms on a card naming the channel, the asking process, the agent and the
/// repository it claims. A process in an agents' job is refused before any card (<see cref="PipeOutsideOnly"/>). On the Confirm the
/// service's <c>POST /api/channel/members</c> admits it, the person's door, and the terminal hears the member's id.
/// </summary>
public sealed class ChannelJoinRoutes(DriverLoop loop)
{
    private readonly DriverLoop _loop = loop;

    /// <summary>
    /// The member admitted: answers <c>joined</c> with the member, or the service's code where it refused (<c>not-found</c>,
    /// <c>no-session</c>, <c>helper</c>, <c>too-many</c>). Its grounds are the ask or the line's session and the repository it claims,
    /// read at the ask and again at the Confirm; the bounds are the service's, judged as it admits.
    /// </summary>
    [DriverRoute("CHANNEL_JOIN", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("CHANNEL_JOIN", Ask = "ask")]
    [GroundAsk("CHANNEL_JOIN", "ask")]
    [GroundSession("CHANNEL_JOIN", "line")]
    [GroundRepository("CHANNEL_JOIN", "repository")]
    [Says("CHANNEL_JOIN", "ask:ask", "line:session", "agent:agent", "repository:repository")]
    [Publishes("CHANNEL_JOIN", StateScopes.Asks)]
    private async Task<object?> ChannelJoinAsync(ChannelJoinPress press, CancellationToken cancellationToken)
    {
        var service = _loop.Service ?? throw NotReady();
        var asked = new ChannelMemberAsk(
            ChannelMemberAsk.Join, Ask: press.Ask?.Trim().TrimStart('#'), Line: press.Line?.Trim(), Agent: press.Agent, Repository: press.Repository);
        if (press.Said)
        {
            return await TerminalSaid.RunAsync(output => ChannelMemberVerb.JoinAsync(asked, service, output, cancellationToken)).ConfigureAwait(false);
        }

        var joined = await service.JoinChannelAsync(new JoinChannel(asked.Ask, asked.Line, asked.Agent, asked.Repository), cancellationToken).ConfigureAwait(false);
        return new ChannelJoinAnswer(joined.Member is not null, joined.Member, joined.Code);
    }
}

/// <summary>What a join answers the window: whether the member was admitted, the member, or the service's code.</summary>
public sealed record ChannelJoinAnswer(bool Joined, ChannelMemberView? Member, string? Code);
