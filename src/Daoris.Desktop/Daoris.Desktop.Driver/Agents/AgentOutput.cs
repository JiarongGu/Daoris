namespace Daoris.Driver;

/// <summary>A supplied session output: Context retains keys, buffers, retention and notification handling.</summary>
public interface ISessionOutput
{
    event Action<string, ConsoleLine>? Lined;
    void Append(string sessionId, string text);
    ISessionStreams ForSession(string sessionId);
}

/// <summary>The existing per-session stream facade, supplied without exposing Context's engine.</summary>
public interface ISessionStreams
{
    void Open(SessionStream stream);
    void Line(string streamId, string text);
    void End(string streamId, string? state);
}
