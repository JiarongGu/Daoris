namespace Daoris.Driver;

/// <summary>
/// Where a door says it is waiting behind another landing (LANDHOLD2, D145 point 2, D173's LANDRACE1 note): the press's page and the
/// terminal's verb each hand a callback for the call they make, and <see cref="LandingHolds"/> calls it with true before it waits for a
/// tree's or a branch's hold, and with false once the wait is over, whether the hold was taken or not. A door that waits for nothing
/// calls it never, so an uncontended landing says nothing new./// </summary>
/// <remarks>
/// Found by the flow of the call, as the shell's press steps are, so no landing or hand-off takes a parameter for it. The hold's file
/// is held by the one before and written when it has landed, so what it waits behind is not named: nothing is read from it while it
/// waits.
/// </remarks>
public static class LandingHoldNotice
{
    private static readonly AsyncLocal<Action<bool>?> Current = new();

    /// <summary>The landings and hand-offs this call makes say to <paramref name="told"/> that they wait (true) and stop waiting (false), until disposed.</summary>
    public static IDisposable Told(Action<bool> told)
    {
        var before = Current.Value;
        Current.Value = told;
        return new Restore(before);
    }

    /// <summary>A door says it waits (true) or has stopped (false) to the callback of the call it runs in; to nobody where none was handed.</summary>
    public static void Waiting(bool waiting) => Current.Value?.Invoke(waiting);

    private sealed class Restore(Action<bool>? before) : IDisposable
    {
        public void Dispose() => Current.Value = before;
    }
}
