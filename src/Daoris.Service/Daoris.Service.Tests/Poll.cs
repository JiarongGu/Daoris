namespace Daoris.Service.Tests;

/// <summary>Waiting on a signal: the service's copy of the driver tests' <c>Poll.Patience</c> (TESTCLOCK1).</summary>
internal static class Poll
{
    /// <summary>
    /// How long a test waits on a signal that only fails to come when the code is wrong (TESTPATIENCE1, TESTCLOCK1).
    /// Two minutes, because the bound elapses only on failure, and a starved run reached any shorter one on time alone.
    /// Never for an elapsed-time bound that states the claim itself, nor for a window in which nothing should happen.
    /// </summary>
    public static readonly TimeSpan Patience = TimeSpan.FromMinutes(2);
}
