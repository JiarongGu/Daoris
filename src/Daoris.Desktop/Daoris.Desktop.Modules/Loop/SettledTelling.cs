using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// A press the terminal's pipe settled with no terminal waiting on its line, told at <c>work/event</c> as <c>confirmation.ended</c>
/// (EV-F; the event channel design §3.3). The pipe calls it after the ending's record is written; the hook set is read at the moment
/// of telling, since the run sets it once it is up and the stop takes it back.
/// </summary>
public static class SettledTelling
{
    /// <summary>The pipe's <see cref="PipeConfirmations.Settled"/> sink over the loop's hook set.</summary>
    public static Action<PipeConfirmation, PipeEnding> Of(Func<HookSet?> hooks, Func<DateTimeOffset> clock) =>
        (shown, ending) => hooks()?.Tell(WorkEventPlugins.Settled(shown.Id, ending.End.ToString(), clock()));
}
