using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// How a change came to be, the page's `bridge/trace.ts` (TRACE1b, D143, D50): the screen's door to `daoris-driver trace`,
/// one read from a session or a quest back to its ask through the stores that keep each link.
/// </summary>
public sealed partial class DriverModule
{
    // The driver's own read, over the stores the terminal reads (the service, the home, `driver.json`): the chain as data,
    // each link with its store and a missing one with why. It writes nothing. What a failed read said, and a tree's path,
    // are the terminal's alone (D47 §4): the chain's records keep them off the wire.
    [DriverRoute("TRACE")]
    private async Task<object?> TraceAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        var kind = PayloadHelper.GetRequiredValue<string>(request.Payload, "kind");
        var id = PayloadHelper.GetRequiredValue<string>(request.Payload, "id").Trim();
        if (!TraceEntry.All.Contains(kind)) throw new DriverException($"a trace starts from a commit, a session or a quest, not `{kind}`.");
        if (id.Length == 0) throw new DriverException($"a trace names the {kind} it starts from.");
        if (kind == TraceEntry.Commit && !Trace.IsCommit(id.TrimStart('#')))
        {
            throw new DriverException($"a commit is named by at least {Trace.CommitDigits} of its hexadecimal digits, not `{id}`.");
        }

        var service = _loop.Service ?? throw NotReady();
        var read = await Trace.ReadChainAsync(
            new TraceAsk(id, kind), new TraceSources(service, _loop.Home, DriverConfig.Load(_loop.ConfigPath)), cancellationToken)
            .ConfigureAwait(false);
        return new { read.Chain, read.Unread };
    }
}
