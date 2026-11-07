using Lyntai;
using Lyntai.Agents;

namespace Daoris.Driver;

/// <summary>
/// What an agent's provider folder hands the layer's usage read (the agent-lyntai design §4.1, §4.2): its door's id, which the layer's
/// provider is registered under; today's patience, which bounds the read; the vendor's registration over the resolved command; the read's
/// options that are the host's to choose (where the server runs, what it is told of its client); and the reading mapped into today's
/// walk and history, Daoris's naming and inferred resets kept.
/// </summary>
internal sealed record LayerUsageVendor(
    string Door,
    TimeSpan Patience,
    Action<LyntaiBuilder, string> Register,
    Action<AgentUsageReadingOptions> Options,
    Func<AgentAllowanceReading, DateTimeOffset, LayerUsageAnswer> Answer);

/// <summary>A reading as today's reader parses it: the walk's windows, the usage history's rows, and the plan's word where it is read.</summary>
internal sealed record LayerUsageAnswer(IReadOnlyList<WindowReading> Walk, IReadOnlyList<WindowSeen> Seen, string? Plan);
