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
/// The turn's own report (denials, the context against the window, tokens by model) is <see cref="LayerTurnReport"/>'s, said at the
/// terminal as the native capture says it, and the conversation is the first the agent announced. What the layer's members do not carry is
/// no reading yet: background tasks (AG18 with LQ7).
/// </remarks>
/// <param name="door">The bound door's transport word, the failure's door.</param>
/// <param name="clock">The clock the failure is seen on.</param>
/// <param name="coolOff">D125's default cool-off, for a limit whose recovery is not stated.</param>
/// <param name="steps">The door's own reading of a tool call (<see cref="LayerRegistration.Steps"/>); null shows a call by its name alone.</param>
internal sealed class LayerReadings(
    string door, TimeProvider clock, TimeSpan coolOff, LayerRefusals? refusals = null, Func<LayerToolCall, SessionEvent>? steps = null)
{
    private readonly Dictionary<string, string> _titles = new(StringComparer.Ordinal);

    private readonly LayerRefusals _refusals = refusals ?? LayerRefusals.None;

    private readonly LayerTurnReport _report = new();

    private TurnTokens? _tokens;

    private string? _model;

    private bool _stopping;

    /// <summary>Whether the turn showed words, thinking or a call of the session's own: what today's guard on a refused model reads.</summary>
    private bool _ran;

    /// <summary>The failed turn was the door's refusal of the model it was handed: the ending's flag, set on the native door's failed result alone.</summary>
    public bool ModelRefused { get; private set; }

    /// <summary>The conversation the door named first; null until one is.</summary>
    public string? Conversation { get; private set; }

    /// <summary>The context at its high-water mark against the window the harness named; null where it reported none.</summary>
    public AcpUsage? Usage => _report.Usage;

    /// <summary>The turn's one terminal; null until it ended.</summary>
    public LayerEnded? Ended { get; private set; }

    /// <summary>The turn's failure as Daoris's outcome; null where it did not fail or was stopped.</summary>
    public NormalizedFailure? Failure { get; private set; }

    /// <summary>Daoris asked a stop before the terminal: the terminal that follows is the stop, never the fatal exit after the kill.</summary>
    public void Stop() => _stopping = Ended is null;

    /// <summary>The readings <paramref name="evt"/> is, in the order today's capture hands them.</summary>
    public IReadOnlyList<WireReading> Read(AgentStreamEvent evt)
    {
        if (evt is TextDelta { ParentCallId: null } or Thinking { ParentCallId: null } or LayerToolCall { ParentCallId: null }) _ran = true;
        return Translate(evt);
    }

    private IReadOnlyList<WireReading> Translate(AgentStreamEvent evt) => evt switch
    {
        LayerStarted started => Named(started),
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
        UsageLive live => Report(() => _report.Held(live)),
        AgentToolDenied denial => Report(() => _report.Denied(denial, _titles)),
        AgentModelUsage byModel => Report(() => _report.Split(byModel)),
        ContextReading figures when figures.UsedTokens is not null || figures.WindowTokens is not null => [LayerTurnReport.Figures(figures)],
        LayerEnded ended => Ending(ended),
        _ => [],
    };

    /// <summary>What the turn's own report keeps for its terminal: no reading yet.</summary>
    private static IReadOnlyList<WireReading> Report(Action keep)
    {
        keep();
        return [];
    }

    /// <summary>
    /// The conversation is the one the agent announced, the first kept: a later frame that only carries an id names nothing, as the native
    /// door reads its initialization alone.
    /// </summary>
    private IReadOnlyList<WireReading> Named(LayerStarted started)
    {
        if (!started.Announced || Conversation is not null) return [];
        var conversation = started.SessionId;
        Conversation = conversation;
        return [new WireReading.ConversationNamed(conversation)];
    }

    private IReadOnlyList<WireReading> Called(LayerToolCall call)
    {
        var step = steps?.Invoke(call) ?? new SessionEvent
        {
            Kind = SessionEventKind.Tool,
            Id = call.CallId,
            Title = AcpRender.OneLine(call.Name),
            Status = "in_progress",
            Input = SessionEventText.Cut(call.ArgumentsJson, SessionEventText.RawLimit),
        };
        var title = AcpRender.OneLine(step.Title ?? call.Name);
        if (call.CallId is { } id) _titles[id] = title;
        return [Shown($"→ {title}"), Said(step)];
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
        _model = usage.Model;
        return [];
    }

    private IReadOnlyList<WireReading> Ending(LayerEnded ended)
    {
        if (Ended is not null) return [];
        Ended = ended;
        if (_stopping) return Closing([], "— the turn was stopped", "cancelled");

        var subtype = ended.Subtype ?? (ended.IsError ? "error" : "success");
        var failed = new List<WireReading>();
        if (ended.IsError)
        {
            Failure = LayerFailures.Normalize(
                ended.Failure ?? new AgentFailure(AgentFailureKind.Fatal, "no-failure-fields"), door, DoorWhere.Result, ended.Diagnostic,
                clock.GetUtcNow(), coolOff, ended.Turn);
            ModelRefused = _refusals.Refused(Failure, door, DoorWhere.Result, ranNothing: !_ran, stopped: false);
            failed.Add(new WireReading.Failed(Failure.Failure));
        }

        return Closing(
            failed,
            ended.IsError && ended.Diagnostic is { Length: > 0 } why ? $"— the turn failed ({subtype}): {why}" : $"— the turn ended: {subtype}",
            !ended.IsError && subtype == "success" ? "end_turn" : subtype);
    }

    /// <summary>The terminal as the capture says it: the failure, the lines (the denials', then its own), then the records (the denials', the context, the turn).</summary>
    private IReadOnlyList<WireReading> Closing(IEnumerable<WireReading> failed, string line, string stopReason) =>
    [
        .. failed,
        .. _report.Lines,
        Shown(line),
        .. _report.Events(),
        Said(new SessionEvent { Kind = SessionEventKind.Turn, StopReason = stopReason, Tokens = _tokens, Models = _report.Models(_tokens, _model) }),
    ];

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
