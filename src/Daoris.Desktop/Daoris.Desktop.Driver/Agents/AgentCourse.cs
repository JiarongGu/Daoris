using System.Text.Json;
using System.Text.Json.Serialization;

namespace Daoris.Driver;

/// <summary>The original word as a protocol door reads it; higher conversation state stays with its owner.</summary>
[JsonConverter(typeof(AgentCourseJson<IAgentWord>))]
public interface IAgentWord
{
    string Text { get; }
    string Prompt { get; }
    string? Id { get; }
    IReadOnlyList<KeptFile> Files { get; }
    bool IsDaoris { get; }
    Func<Task<IReadOnlyList<string>>>? Attempting { get; }
    Action? Went { get; }
    Task<bool> GoesAsync(CancellationToken ct);
    SessionEvent Dropped();
}

/// <summary>The original queue supplied by the inbox's owner, retaining its serialized shape.</summary>
[JsonConverter(typeof(AgentCourseJson<IAgentQueue>))]
public interface IAgentQueue
{
    bool Taking { get; }
    IReadOnlyList<IAgentWord> Queued { get; }
    DateTimeOffset? LastTurnEnded { get; }
    bool Opening { get; }
    bool Pressing { get; }
}

/// <summary>The existing inbox's operations used by the process registry and protocol mechanism.</summary>
public interface IAgentInbox : IEpisodeWords
{
    bool Holds { get; }
    void Attach(Func<Task>? interrupt, Func<IAgentWord, Task<string>>? deliver, Func<IAgentWord, Task<string>>? steer);
    void Flow();
    void LastAnswered();
    bool HandBack(IAgentWord word);
    AgentNext? NextOrClose();
    IReadOnlyList<IAgentWord> Close();
    void OnClosing(Action closing, Func<bool> personStopped, Action<IReadOnlyList<IAgentWord>> left);
}

/// <summary>The inbox's next action, retaining the original word and answer/wait tasks.</summary>
public sealed record AgentNext(IAgentWord? Held, Task<string>? Answer)
{
    public Task? Waits { get; init; }
}

/// <summary>Writing a supplied view keeps its owner's complete runtime shape, including higher properties.</summary>
public sealed class AgentCourseJson<T> : JsonConverter<T> where T : class
{
    public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        throw new NotSupportedException("a supplied Course view is read by its concrete owner");

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
}

internal interface IAgentCourse
{
    IAgentInbox OpenInbox(Action<IAgentQueue> changed);
    Func<TransientEpisode, string, CancellationToken, Task<IAgentWord?>> RetryGoOn(ServiceClient service, string sessionId);
    Task GoOnOffAsync(ServiceClient service, string sessionId, CancellationToken ct);
    string? HandOverText(string? note);
}

internal static class AgentCourse
{
    private static IAgentCourse? _current;
    internal static IAgentCourse Current
    {
        get => _current ?? throw new InvalidOperationException("no agent course operations are installed: Course installs them when the driver loads");
        set => _current = value;
    }
}
