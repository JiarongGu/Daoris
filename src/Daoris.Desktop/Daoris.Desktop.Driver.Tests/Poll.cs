namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Waiting for a real process to reach a state. Every test here that spawns one polls, and seven
/// classes had written the loop for themselves (REV3 CLEAN1).
/// </summary>
internal static class Poll
{
    /// <summary>Wait until <paramref name="condition"/> holds, or fail saying what was seen.</summary>
    /// <param name="seen">What the test saw, for the failure: the state the condition was waiting on.</param>
    /// <param name="within">
    /// How long, 30 seconds unless the caller says. Counted in polls rather than read off the clock, as
    /// every copy this replaced counted: a clock deadline is stricter under a loaded run, where each
    /// look at the condition is slower, and a loaded run is where these tests already struggle.
    /// </param>
    public static async Task Until(Func<bool> condition, Func<string>? seen = null, TimeSpan? within = null)
    {
        var limit = (within ?? TimeSpan.FromSeconds(30)).TotalMilliseconds;
        for (var waited = 0; !condition(); waited += 50)
        {
            if (waited > limit)
            {
                throw new TimeoutException($"the condition never held — {seen?.Invoke() ?? "nothing more to say"}");
            }

            await Task.Delay(50);
        }
    }
}
