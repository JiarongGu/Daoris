using Lyntai.Agents;
using LayerEnded = Lyntai.Agents.SessionEnded;
using LayerPlanEntry = Lyntai.Agents.PlanEntry;
using LayerStarted = Lyntai.Agents.SessionStarted;
using LayerToolCall = Lyntai.Agents.ToolCall;
using LayerToolResult = Lyntai.Agents.ToolResult;

namespace Daoris.Driver;

/// <summary>
/// The events' translation (the agent-lyntai design §5.4): each event a layer door yields read once into the readings today's native capture
/// hands its writer for the same thing, in its order: a failure first, through the ingress (<see cref="LayerFailures"/>), then the
/// conversation named, the lines the console shows and the events the record keeps. One translation a turn, which keeps what a later event
/// reads: a call's name, the turn's counts, the conversation first named and the turn's one terminal. No frame is read here: Events reads
/// nothing of the layer.
/// </summary>
/// <remarks>
/// What the layer's released members do not carry is no reading yet: live usage and the usage readings (LQ17's context and its tokens by
/// model), background tasks (AG18 with LQ7), and the native door's opening evidence, so the conversation is named by the first id the door
/// yields (LQ17 widened tells the agent's own initialization apart, before a native door moves).
/// </remarks>
/// <param name="door">The bound door's transport word, the failure's door.</param>
/// <param name="clock">The clock the failure is seen on.</param>
/// <param name="coolOff">D125's default cool-off, for a limit whose recovery is not stated.</param>
internal sealed class LayerReadings(string door, TimeProvider clock, TimeSpan coolOff)
{
    private readonly Dictionary<string, string> _titles = new(StringComparer.Ordinal);

    private TurnTokens? _tokens;

    private bool _stopping;

    /// <summary>The conversation the door named first; null until one is.</summary>
    public string? Conversation { get; private set; }

    /// <summary>The turn's one terminal; null until it ended.</summary>
    public LayerEnded? Ended { get; private set; }

    /// <summary>The turn's failure as Daoris's outcome; null where it did not fail or was stopped.</summary>
    public NormalizedFailure? Failure { get; private set; }

    /// <summary>Daoris asked a stop before the terminal: the terminal that follows is the stop, never the fatal exit after the kill.</summary>
    public void Stop() => _stopping = Ended is null;

    /// <summary>The readings <paramref name="evt"/> is, in the order today's capture hands them.</summary>
    public IReadOnlyList<WireReading> Read(AgentStreamEvent evt) => evt switch
    {
        LayerStarted started => Named(started.SessionId),
        TextDelta text =>
        [
            .. text.Text.Split('\n').Select(Shown),
            Said(new SessionEvent { Kind = SessionEventKind.Message, Text = text.Text }),
        ],
        Thinking thought =>
        [
            Shown($"· {thought.Text.Split('\n')[0]}"),
            Said(new SessionEvent { Kind = SessionEventKind.Thought, Text = thought.Text }),
        ],
        LayerToolCall call => Called(call),
        LayerToolResult result => Returned(result),
        PlanUpdated plan => [Said(new SessionEvent { Kind = SessionEventKind.Plan, Entries = [.. plan.Entries.Select(Entry)] })],
        UsageFinal usage => Counted(usage),
        LayerEnded ended => Ending(ended),
        _ => [],
    };

    private IReadOnlyList<WireReading> Named(string conversation)
    {
        if (Conversation is not null) return [];
        Conversation = conversation;
        return [new WireReading.ConversationNamed(conversation)];
    }

    private IReadOnlyList<WireReading> Called(LayerToolCall call)
    {
        var title = AcpRender.OneLine(call.Name);
        if (call.CallId is { } id) _titles[id] = title;
        return
        [
            Shown($"→ {title}"),
            Said(new SessionEvent
            {
                Kind = SessionEventKind.Tool,
                Id = call.CallId,
                Title = title,
                Status = "in_progress",
                Input = SessionEventText.Cut(call.ArgumentsJson, SessionEventText.RawLimit),
            }),
        ];
    }

    private IReadOnlyList<WireReading> Returned(LayerToolResult result)
    {
        // Said by the call's name, never its id: an id says nothing to a person reading along.
        var name = result.CallId is { } id && _titles.TryGetValue(id, out var known) ? known : "a tool";
        return
        [
            Shown(result.IsError ? $"  ✗ {name} failed" : $"  ✓ {name}"),
            Said(new SessionEvent
            {
                Kind = SessionEventKind.Tool,
                Id = result.CallId,
                Status = result.IsError ? "failed" : "completed",
                Content = result.Content is { Length: > 0 } text ? [new ToolContent("text", Text: text)] : null,
            }),
        ];
    }

    private IReadOnlyList<WireReading> Counted(UsageFinal usage)
    {
        _tokens = new TurnTokens(usage.InputTokens, usage.OutputTokens, usage.CacheReadTokens, usage.CacheCreateTokens);
        return [];
    }

    private IReadOnlyList<WireReading> Ending(LayerEnded ended)
    {
        if (Ended is not null) return [];
        Ended = ended;
        if (_stopping)
        {
            return [Shown("— the turn was stopped"), Said(new SessionEvent { Kind = SessionEventKind.Turn, StopReason = "cancelled", Tokens = _tokens })];
        }

        var subtype = ended.Subtype ?? (ended.IsError ? "error" : "success");
        var readings = new List<WireReading>();
        if (ended.IsError)
        {
            Failure = LayerFailures.Normalize(
                ended.Failure ?? new AgentFailure(AgentFailureKind.Fatal, "no-failure-fields"), door, DoorWhere.Result, ended.Diagnostic,
                clock.GetUtcNow(), coolOff);
            readings.Add(new WireReading.Failed(Failure.Failure));
        }

        readings.Add(Shown(ended.IsError && ended.Diagnostic is { Length: > 0 } why
            ? $"— the turn failed ({subtype}): {why}"
            : $"— the turn ended: {subtype}"));
        readings.Add(Said(new SessionEvent
        {
            Kind = SessionEventKind.Turn,
            StopReason = !ended.IsError && subtype == "success" ? "end_turn" : subtype,
            Tokens = _tokens,
        }));
        return readings;
    }

    private static PlanEntry Entry(LayerPlanEntry entry) => new(entry.Content, entry.Status switch
    {
        PlanEntryStatus.Pending => "pending",
        PlanEntryStatus.InProgress => "in_progress",
        PlanEntryStatus.Completed => "completed",
        _ => null,
    });

    private static WireReading Shown(string text) => new WireReading.Shown(text);

    private static WireReading Said(SessionEvent e) => new WireReading.Said(e);
}
