using Lyntai.Agents;

namespace Daoris.Driver;

/// <summary>
/// What a layer run's session runs beside itself, kept where someone watches its console: the door's streams beside
/// (<see cref="LayerRegistration.Beside"/>), each child's and task's opened and ended by its reading, and each event a child said (one whose
/// <c>ParentCallId</c> names the call that spawned it) shown on that child's stream through a translation of its own, never on the session's
/// console or in its record, as the native reader takes a child's frames.
/// </summary>
/// <remarks>One per process, as the native capture's streams are one per capture.</remarks>
/// <param name="beside">The door's streams beside, over the run's.</param>
/// <param name="translation">A child's own translation of its events, its lines what its stream shows.</param>
internal sealed class LayerStreams(IStreamsReader beside, Func<LayerReadings> translation)
{
    private readonly Dictionary<string, LayerReadings> _children = new(StringComparer.Ordinal);

    /// <summary>A child's or a task's start or end, as its reading: its stream opened or ended.</summary>
    public void Read(WireReading reading) => beside.Read(reading);

    /// <summary>True when the event is a child's, said on its stream, so the session's translation never reads it.</summary>
    public bool Take(AgentStreamEvent evt)
    {
        if (evt is AgentTaskOutputFile { TaskId: { Length: > 0 } task, Path: { Length: > 0 } path }) beside.Followed(task, path);
        if (Spawner(evt) is not { Length: > 0 } call) return false;
        if (!_children.TryGetValue(call, out var words)) _children[call] = words = translation();
        foreach (var reading in words.Read(evt))
        {
            if (reading is WireReading.Shown shown) beside.Said(call, shown.Text);
        }

        return true;
    }

    /// <summary>Every stream still open ended, because the session's output has: each says so.</summary>
    public Task EndAllAsync() => beside.EndAllAsync();

    private static string? Spawner(AgentStreamEvent evt) => evt switch
    {
        TextDelta text => text.ParentCallId,
        Thinking thought => thought.ParentCallId,
        ToolCall call => call.ParentCallId,
        ToolResult result => result.ParentCallId,
        _ => null,
    };
}
