using Daoris.Driver;
using Shenora.Core.Events;

namespace Daoris.Desktop;

/// <summary>
/// A session's console lines, as the page's one event for them. The shape is the page's contract, so one writer builds it: the
/// loop's relay's batches and a harness action's lines alike.
/// </summary>
internal static class OutputTold
{
    /// <param name="live">
    /// Whether the console still runs as the batch goes out: a session's last words are written after it closed, and a batch the
    /// page read as live re-marked a finished session so.
    /// </param>
    public static Task TellAsync(IEventBus bus, string session, IEnumerable<ConsoleLine> lines, bool live = true) =>
        bus.EmitAsync("DAORIS", "SESSION_OUTPUT", new
        {
            Session = session,
            Lines = lines.Select(line => new { line.Sequence, line.Text }).ToArray(),
            Live = live,
        });
}
