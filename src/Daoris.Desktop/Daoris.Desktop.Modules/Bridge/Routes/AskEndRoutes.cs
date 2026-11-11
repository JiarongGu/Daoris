using Daoris.Driver;
using Shenora.Core.Ipc;
using static Daoris.Desktop.DriverArguments;

namespace Daoris.Desktop;

/// <summary>An ask named by its id, and whether the terminal asks for the verb's own words.</summary>
public sealed record AskEndPress(string Ask, bool Said = false);

/// <summary>
/// An ask's delete as the person's press of the pipe: the service's own door for it (<c>DELETE /api/asks/{id}</c>), which the ask's
/// record and <c>daoris ask --delete</c> reach by HTTP, sent as the person's record. The ask goes with every quest it became, or the
/// service refuses it whole.
/// </summary>
/// <remarks>A refusal of the service is an answer, <c>{ done: false, refused, message }</c>, in the service's own words.</remarks>
public sealed class AskEndRoutes(DriverContext context)
{
    private readonly DriverLoop _loop = context.Loop;

    /// <summary>An ask made by mistake deleted, refused in the service's words for one a quest it became has been started on.</summary>
    [DriverRoute("ASK_DELETE", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("ASK_DELETE", Ask = "ask")]
    [GroundAsk("ASK_DELETE", "ask")]
    [Says("ASK_DELETE", "ask:ask")]
    [Publishes("ASK_DELETE", StateScopes.Quests | StateScopes.Sessions | StateScopes.Asks)]
    private async Task<object?> AskDeleteAsync(AskEndPress press, CancellationToken cancellationToken)
    {
        var service = _loop.Service ?? throw NotReady();
        var ask = press.Ask.Trim().TrimStart('#');
        if (press.Said)
        {
            var said = await TerminalSaid.RunAsync(output => AskDeleteCommand.RunAsync(ask, service, output, cancellationToken)).ConfigureAwait(false);
            _loop.Nudge();
            return said;
        }

        var (ok, message) = await service.DeleteAskAsync(ask, cancellationToken).ConfigureAwait(false);
        _loop.Nudge();
        return new { Done = ok, Refused = ok ? null : StewardWhy.Refused, Message = message };
    }
}
