using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>No tier, or one that is neither ask nor act, is the driver's refusal.</summary>
public sealed record SetTerminal(string? Tier);

/// <summary>
/// The tier of a person's press from a terminal no Daoris session owns, the page's Settings row: <c>ask</c> or <c>act</c>, over the same
/// <c>terminal</c> of <c>driver.json</c> that <c>daoris settings terminal</c> sets, read by the pipe at each press.
/// </summary>
public sealed class TerminalTierRoutes(DriverContext context)
{
    // Its own press: from a terminal under ask, setting act waits on the card, which is the person's grant.
    [DriverRoute("SET_TERMINAL", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("SET_TERMINAL", None = OwnerNone.MachineSetting)]
    [GroundRead("SET_TERMINAL", nameof(PipeReads.Terminal))]
    [Publishes("SET_TERMINAL", StateScopes.Driver)]
    private object? SetTerminal(SetTerminal press)
    {
        string? was = null;
        string? now = null;
        context.Change(config =>
        {
            was = config.Terminal;
            var changed = config.WithTerminal(press.Tier);
            now = changed.Terminal;
            return changed;
        });
        if (now is { } tier && was != tier)
        {
            var (by, session) = PipeDoor.By;
            context.Loop.TerminalActs.Changed(tier, by, session);
            context.Loop.Log?.Info(TierChangedEvent, ("tier", tier), ("by", by), ("session", session ?? ""));
        }

        return DriverStateAnswer.State(context.Loop);
    }

    /// <summary>The machine log's event for a change of the tier, naming who made it.</summary>
    public const string TierChangedEvent = "terminal.tier";
}
