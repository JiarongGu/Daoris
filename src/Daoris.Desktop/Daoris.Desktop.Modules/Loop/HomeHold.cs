using Daoris.Driver;
using Shenora.Core.Events;

namespace Daoris.Desktop;

/// <summary>
/// This home's lease, waited for by the desktop's loop for its life (<see cref="DriverLoop.HoldHomeAsync"/>): one live driver per
/// home. While another live driver holds it (a terminal's <c>daoris drive</c>) the wait is said at once, on the channel a person acts
/// on; while an act holds it (a terminal's verb acting alone, the first-start move, the launcher's swap) it is said once that hold has
/// lasted the loop's tell-after, on that channel and in the machine log. The lease is taken the moment the holder lets go.
/// </summary>
/// <remarks>
/// Waited for rather than refused, as a terminal's loop is: the desktop is where a person drives from, and nothing else it carries
/// races anyone for a quest.
/// </remarks>
internal static class HomeHold
{
    /// <summary>The lease, or null when the shell closes first.</summary>
    public static async Task<HomeLease?> WaitAsync(
        string home, TimeSpan retry, TimeSpan tellAfter, IEventBus eventBus, MachineLog? log, CancellationToken ct)
    {
        var told = false;
        DateTimeOffset? actSince = null;
        while (true)
        {
            HomeLease? held;
            string? said;
            LeaseHolder? holder = null;
            try
            {
                held = HomeLease.TryAcquire(home, LeaseKind.Desktop, out holder);
                said = holder is null
                    ? null
                    : holder.IsInstance
                        ? $"The driver loop is waiting: this home is already driven by {holder.Named}. It starts "
                          + "here the moment that one stops."
                        : $"The driver loop is waiting: this home is held by {holder.Named}, which has not let it go in "
                          + $"{tellAfter.TotalSeconds:0} seconds. It starts here the moment that one ends.";
            }
            catch (HomeLeaseException error)
            {
                held = null;
                said = error.Message;
            }

            if (held is not null) return held;

            // An act holds the home for a moment, as a verb's press does: said only once it has lasted the wait.
            if (holder is { IsInstance: false })
            {
                actSince ??= DateTimeOffset.UtcNow;
                if (DateTimeOffset.UtcNow - actSince < tellAfter) said = null;
            }

            if (!told && said is not null)
            {
                await eventBus.EmitAsync("DAORIS", "DRIVER_ERROR", new { Message = said }).ConfigureAwait(false);
                if (holder is not null)
                {
                    log?.Warn("home.waiting", ("holder", holder.Kind.ToString().ToLowerInvariant()), ("pid", holder.Pid), ("what", holder.What));
                }

                told = true;
            }

            try
            {
                await Task.Delay(retry, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }
    }
}
