namespace Daoris.Desktop;

/// <summary>The window's UX trail (<see cref="UxTrail"/>): whether it is on, turned on or off, and its entries kept. A window's alone.</summary>
public sealed class UxTrailRoutes(DriverContext context)
{
    private readonly DriverContext _context = context;

    // `{}` reads whether it is on; `{ on }` turns it on or off (Settings' row); `{ entries }` keeps what the window recorded, nothing while
    // off. Answered `{ on, kept }`. Only a window records the person's pages and presses: a terminal is refused.
    [DriverRoute("UX_TRAIL", PipeClass.Window)]
    [Owner("UX_TRAIL", None = OwnerNone.NamesNothing)]
    private object? Trail(UxTrailPress press)
    {
        var trail = new UxTrail(_context.Loop.Home);
        if (press.On is { } on) trail.Set(on);
        var kept = press.Entries is { } entries ? trail.Append(entries, DateTimeOffset.UtcNow) : 0;
        return new { trail.On, Kept = kept };
    }
}

/// <summary>The trail turned on or off with <c>On</c>, or the window's recorded <c>Entries</c> to keep; neither reads whether it is on.</summary>
public sealed record UxTrailPress(bool? On = null, IReadOnlyList<UxTrailEntry>? Entries = null);
