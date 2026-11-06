namespace Daoris.Driver;

/// <summary>
/// How an account's windows are read between turns (the agent-lyntai design §4.1): the question's declaration (<see cref="IUsageQuestion"/>)
/// and the read itself, which answers Daoris's own <see cref="UsageAnswer"/>, the walk's windows and the usage history's rows, or nothing.
/// Today's implementation is <see cref="UsageQuestion"/> over each agent's own reader; where the driver carries Lyntai's agent layer, the
/// layer's (<see cref="UsageReads"/>). The host's half (a key not read, the probe lock, no read while a session runs on the account) and
/// the floor stay the roster's around either.
/// </summary>
public interface IUsageRead : IUsageQuestion
{
    /// <summary>Starts the server under the account's home, asks it and stops it; null where no reading could be had.</summary>
    Task<IReadOnlyList<WindowReading>?> AskAsync(UsageAsk ask, CancellationToken ct);
}
