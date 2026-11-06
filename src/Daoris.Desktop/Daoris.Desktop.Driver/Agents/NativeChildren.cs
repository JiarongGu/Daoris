namespace Daoris.Driver;

/// <summary>
/// The children a native door's stream says (AGENTPROVIDER1l, COWORKVIEW1a): Claude Code's <c>stream-json</c> announces a subagent with a
/// <c>system</c> <c>task_started</c> of <c>local_agent</c>, its parent, depth, type and task's description beside it, and ends it with its
/// <c>task_notification</c> in the wire's word. Each is read from the line's one <see cref="NativeFrame"/> into one
/// <see cref="WireReading.ChildSeen"/> or <see cref="WireReading.ChildEnded"/>, once per child however the wire repeats it; a background
/// command's task is no child, and every other line says none.
/// </summary>
/// <remarks>One per capture: an end is read only for a start its own stream said.</remarks>
internal sealed class NativeChildren
{
    private readonly HashSet<string> _seen = new(StringComparer.Ordinal);
    private readonly HashSet<string> _ended = new(StringComparer.Ordinal);

    /// <summary>The reading the frame says of a child, or null.</summary>
    public WireReading? Read(NativeFrame frame)
    {
        if (frame.Type != "system") return null;
        switch (frame.Subtype)
        {
            case "task_started" when frame.Text("task_type") == "local_agent" && frame.Text("task_id") is { Length: > 0 } child:
                return _seen.Add(child)
                    ? new WireReading.ChildSeen(child, ChildSignal.Stream)
                    {
                        Parent = frame.Text("parent_task_id"),
                        Depth = Json.Int(frame.Root, "spawn_depth") is int depth and > 0 ? depth : null,
                        Type = frame.Text("subagent_type"),
                        Task = frame.Text("description"),
                    }
                    : null;

            case "task_notification" when frame.Text("task_id") is { } child && _seen.Contains(child) && _ended.Add(child):
                return new WireReading.ChildEnded(child, ChildSignal.Stream, frame.Text("status"));

            default:
                return null;
        }
    }
}
