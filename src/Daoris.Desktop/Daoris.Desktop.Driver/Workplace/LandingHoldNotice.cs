namespace Daoris.Driver;

/// <summary>
/// Where a door says it is waiting behind another landing (LANDHOLD2, D145 point 2, D173's LANDRACE1 note): the press's page and the
/// terminal's verb each hand a callback for the call they make, and <see cref="LandingHolds"/> calls it with true before it waits for a
/// tree's or a branch's hold, and with false once the wait is over, whether the hold was taken or not. A door that waits for nothing
/// calls it never, so an uncontended landing says nothing new.
/// </summary>
/// <remarks>
/// Found by the flow of the call, as the shell's press steps are, so no landing or hand-off takes a parameter for it. A waiting door
/// names what it waits behind where the holder said who it is (LANDHOLD3, <see cref="LandingHolder"/>): with the first word where it
/// is known then, and with true again once it is known or names another, so a callback may be told true more than once in one wait.
/// </remarks>
public static class LandingHoldNotice
{
    private static readonly AsyncLocal<Action<bool, LandingHolder?>?> Current = new();

    /// <summary>The landings and hand-offs this call makes say to <paramref name="told"/> that they wait (true) and stop waiting (false), until disposed.</summary>
    public static IDisposable Told(Action<bool> told) => Told((waiting, _) => told(waiting));

    /// <summary>As <see cref="Told(Action{bool})"/>, told too what each wait is behind, where the holder said (LANDHOLD3); null where it did not.</summary>
    public static IDisposable Told(Action<bool, LandingHolder?> told)
    {
        var before = Current.Value;
        Current.Value = told;
        return new Restore(before);
    }

    /// <summary>A door says it waits (true) or has stopped (false) to the callback of the call it runs in; to nobody where none was handed.</summary>
    public static void Waiting(bool waiting, LandingHolder? behind = null) => Current.Value?.Invoke(waiting, behind);

    private sealed class Restore(Action<bool, LandingHolder?>? before) : IDisposable
    {
        public void Dispose() => Current.Value = before;
    }
}

/// <summary>
/// Who holds a tree's or a branch's hold (LANDHOLD3): what the holder writes to the hold when it takes it, so a door waiting behind it
/// names it. Neither is a machine path.
/// </summary>
/// <param name="Session">The session whose landing or push holds it.</param>
/// <param name="Branch">The branch it holds: a branch hold's own, a tree hold's the branch the tree stands on; null where none is read.</param>
public sealed record LandingHolder(string Session, string? Branch);
