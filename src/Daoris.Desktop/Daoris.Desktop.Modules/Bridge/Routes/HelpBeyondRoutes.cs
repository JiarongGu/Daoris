using Daoris.Driver;
using static Daoris.Desktop.DriverArguments;

namespace Daoris.Desktop;

/// <summary>The conversation whose receipt is read.</summary>
public sealed record HelpBeyondRead(string Session);

/// <summary>
/// What an Ask Daoris conversation looked at and did beyond its focus, the page's `bridge/helpBeyond.ts`
/// (`docs/2026-11-11-ask-scope-ux-design.md` §4): the places its connector noted (<see cref="HelpBeyond"/>) and the presses it made in
/// full access this run whose place was beyond its focus at the press (<see cref="HelpPressRecord"/>). The page draws one receipt under
/// each turn from them, by time. Only what passes Daoris's own doors is listed; a shell command is never parsed.
/// </summary>
public sealed class HelpBeyondRoutes(DriverContext context)
{
    private readonly DriverLoop _loop = context.Loop;

    // `{ session }` answers `{ session, entries: [{ at, act, place, what, outcome }] }`, first to last: a press's `what` is its
    // module and route, `MODULE.ROUTE`. A read: it declares nothing. Desktop-only: the notes are this machine's.
    [DriverRoute("HELP_BEYOND", PipeClass.Open)]
    private async Task<object?> HelpBeyondAsync(HelpBeyondRead press, CancellationToken cancellationToken)
    {
        var service = _loop.Service ?? throw NotReady();
        var record = SessionRecords.Parse(await service.SessionRecordsJsonAsync(cancellationToken).ConfigureAwait(false))
            .FirstOrDefault(each => string.Equals(each.Id, press.Session, StringComparison.Ordinal));
        var registry = await service.RegistryAsync(cancellationToken).ConfigureAwait(false);
        var home = _loop.Home;
        var noted = await Task.Run(() => HelpBeyond.Of(home, press.Session, record?.Workspace, registry), cancellationToken).ConfigureAwait(false);
        var presses = _loop.Pipe?.StandingConfirmation.HelpPresses.Now() ?? [];
        return new { press.Session, Entries = Entries(noted, presses, press.Session).Select(entry => entry.Wire()).ToList() };
    }

    /// <summary>The noted places and <paramref name="session"/>'s presses placed beyond its focus, first to last.</summary>
    public static IReadOnlyList<HelpBeyondEntry> Entries(
        IReadOnlyList<HelpBeyondEntry> noted, IReadOnlyList<HelpPressNow> presses, string session) =>
        [.. noted
            .Concat(presses
                .Where(each => each.Beyond && string.Equals(each.Session, session, StringComparison.Ordinal))
                .Select(each => Place(each) is { } place
                    ? new HelpBeyondEntry(each.At, "press", place, $"{each.Module}.{each.Route}", each.Outcome)
                    : null)
                .OfType<HelpBeyondEntry>())
            .OrderBy(entry => entry.At)];

    private static HelpFocus? Place(HelpPressNow press) => press.Place is { } wire
        ? HelpFocus.Read(System.Text.Json.JsonSerializer.SerializeToElement(wire))
        : null;
}
