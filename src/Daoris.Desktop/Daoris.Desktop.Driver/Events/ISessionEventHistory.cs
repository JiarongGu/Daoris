namespace Daoris.Driver;

/// <summary>A supplied record's complete event read, retaining the record instance used for writing and subscriptions.</summary>
public interface ISessionEventHistory : ISessionEvents
{
    IReadOnlyList<SessionEvent> ReadAll(string sessionId);
}
