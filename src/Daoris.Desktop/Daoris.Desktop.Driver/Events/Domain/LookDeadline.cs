namespace Daoris.Driver;

/// <summary>
/// The <see cref="ChangeTopics.Look"/> topic's deadline (FOUNDATION1 mod-vii, the design §14.1): the rule the look held in Loop, and the
/// events' own, since the topic is theirs. Pure: it is handed the time since the last whole look ended, never a clock.
/// </summary>
public static class LookDeadline
{
    /// <summary>How much sooner than its pace a wait may end and still be the pace's: a timer's own granularity.</summary>
    public static readonly TimeSpan Early = TimeSpan.FromMilliseconds(50);

    /// <summary>Whether the topic's deadline has come: <c>pollSeconds</c> since the last whole look ended, a timer's granularity allowed.</summary>
    public static bool Due(TimeSpan sinceWhole, DriverConfig config) => sinceWhole >= TimeSpan.FromSeconds(config.PollSeconds) - Early;
}
