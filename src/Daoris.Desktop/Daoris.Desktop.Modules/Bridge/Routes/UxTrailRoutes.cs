namespace Daoris.Desktop;

/// <summary>
/// The window's UX trail (<see cref="UxTrail"/>): whether it is on, turned on or off, and its entries kept. A window's alone. The switch is
/// read once as the module is built and held after, and every write is made off the window's thread, one after another, so the read
/// answers from memory (NOBLOCK1).
/// </summary>
public sealed class UxTrailRoutes(DriverContext context)
{
    private readonly UxTrail _trail = new(context.Loop.Home);
    private readonly Lock _gate = new();
    private bool _on = new UxTrail(context.Loop.Home).On;
    private Task _writes = Task.CompletedTask;

    // `{}` reads whether it is on; `{ on }` turns it on or off (Settings' row); `{ entries }` keeps what the window recorded, nothing while
    // off. Answered `{ on, kept }`, kept the entries of a kind the trail names that go to its writer. Only a window records the person's
    // pages and presses: a terminal is refused.
    [DriverRoute("UX_TRAIL", PipeClass.Window)]
    [Owner("UX_TRAIL", None = OwnerNone.NamesNothing)]
    private object? Trail(UxTrailPress press)
    {
        lock (_gate)
        {
            if (press.On is { } on)
            {
                _on = on;
                Write(() => _trail.Set(on));
            }

            var taken = _on && press.Entries is { } entries ? entries.Where(entry => UxTrail.Kinds.Contains(entry.Kind)).ToList() : [];
            if (taken.Count > 0)
            {
                var at = DateTimeOffset.UtcNow;
                Write(() => _trail.Append(taken, at));
            }

            return new { On = _on, Kept = taken.Count };
        }
    }

    // A write that fails is lost, as the trail's day cap loses one: the trail is the person's own record of their pages, never the work's.
    private void Write(Action write) => _writes = _writes.ContinueWith(_ => write(), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
}

/// <summary>The trail turned on or off with <c>On</c>, or the window's recorded <c>Entries</c> to keep; neither reads whether it is on.</summary>
public sealed record UxTrailPress(bool? On = null, IReadOnlyList<UxTrailEntry>? Entries = null);
