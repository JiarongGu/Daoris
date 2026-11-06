using Lyntai.Agents;

namespace Daoris.Driver;

/// <summary>
/// A turn's own report on the layer path (the agent-lyntai design §5.4, LQ17): the calls the harness denied, the context the turn held
/// against the window the harness names, and the tokens split by the models that answered, each read into the readings today's native
/// capture hands its writer for the same thing (<c>StructuredOutput</c>'s result). One report a turn, kept by <see cref="LayerReadings"/>.
/// </summary>
internal sealed class LayerTurnReport
{
    private readonly HashSet<string> _refused = new(StringComparer.Ordinal);

    private readonly List<WireReading> _lines = [];

    private readonly List<WireReading> _events = [];

    private long? _context;

    private long? _window;

    private IReadOnlyDictionary<string, TurnTokens>? _split;

    private IReadOnlyList<string> _reported = [];

    /// <summary>The context at its high-water mark against the window the harness named; null where it reported none.</summary>
    public AcpUsage? Usage { get; private set; }

    /// <summary>
    /// What the last assistant message held in context (its input and cache reads). The layer's tick carries no count of what was written to
    /// the cache, so this is short by that count where an agent wrote some; the oracle finds the difference.
    /// </summary>
    public void Held(UsageLive live)
    {
        var total = live.InputTokens + live.CacheReadTokens;
        if (total > 0) _context = total;
    }

    /// <summary>The turn's tokens by model as the report states them, and the window it names (the first model's that names one).</summary>
    public void Split(AgentModelUsage report)
    {
        _reported = [.. report.Models.Select(each => each.Model)];
        _window = report.Models.Select(each => each.ContextWindow).FirstOrDefault(window => window is not null);
        var read = new Dictionary<string, TurnTokens>(StringComparer.Ordinal);
        foreach (var each in report.Models)
        {
            var counts = new TurnTokens(each.InputTokens, each.OutputTokens, each.CacheReadTokens, each.CacheCreateTokens);
            if (counts is { Input: null, Output: null, CacheRead: null, CacheWrite: null } || !read.TryAdd(each.Model, counts))
            {
                _split = null;
                return;
            }
        }

        _split = read;
    }

    /// <summary>
    /// One denied call: its console line and its refusal in the record, each the first time the call is denied; a call the session's record
    /// does not hold is said on the console and is no card, and a denial naming no call is an absence.
    /// </summary>
    /// <param name="titles">The titles of the calls seen, by call id.</param>
    /// <remarks>Kept for the terminal, where the capture says them: the lines with the turn's own, then the records.</remarks>
    public void Denied(AgentToolDenied denial, IReadOnlyDictionary<string, string> titles)
    {
        if (denial.CallId is not { } id || !_refused.Add(id)) return;
        var known = titles.TryGetValue(id, out var title);
        var by = denial.Reason is { Length: > 0 } reason ? $" ({reason})" : "";
        _lines.Add(new WireReading.Shown($"  permission refused: {(known ? title : AcpRender.OneLine(denial.Tool))}{by}"));
        if (!known) return;
        _events.Add(new WireReading.Said(new SessionEvent
        {
            Kind = SessionEventKind.Tool,
            Id = id,
            Status = "refused",
            ToolName = denial.Tool,
            RefusedBy = denial.Reason is { Length: > 0 } ? denial.Reason : null,
        }));
    }

    /// <summary>The lines the terminal says before its own: the denials'.</summary>
    public IReadOnlyList<WireReading> Lines => _lines;

    /// <summary>
    /// The records the terminal says before the turn's own: the denials', then the context the turn held against the window it names, nothing
    /// of it where either is unknown.
    /// </summary>
    public IReadOnlyList<WireReading> Events()
    {
        if (_context is not { } used || _window is not { } size) return _events;
        if (Usage is null || used > Usage.Used) Usage = new AcpUsage(used, size);
        return [.. _events, new WireReading.Said(new SessionEvent { Kind = SessionEventKind.Usage, Used = used, Size = size })];
    }

    /// <summary>
    /// The protocol door's context figures (<c>usage_update</c>), each said as the agent sent it: nothing where it sent neither, and one
    /// it left out stays unknown.
    /// </summary>
    public static WireReading Figures(ContextReading figures) =>
        new WireReading.Said(new SessionEvent { Kind = SessionEventKind.Usage, Used = figures.UsedTokens, Size = figures.WindowTokens });

    /// <summary>
    /// Which models the turn's tokens were spent on: the one the layer named for the session, and the split where it agrees with the total.
    /// </summary>
    public TurnModels Models(TurnTokens? total, string? session) =>
        TurnModels.Of(total, [session], TurnModels.FromMessages, _split, null, TurnModels.FromModelUsage, _reported);
}
