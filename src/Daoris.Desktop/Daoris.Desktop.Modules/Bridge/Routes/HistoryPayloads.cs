namespace Daoris.Desktop;

// A history route names one workspace, one quest or one ask; <c>Failed</c> only beside a quest. A terminal's verbs send <c>Said</c>,
// <c>Clear</c> and <c>Json</c>, read as the page's flags are: true only where they say true.

/// <summary>The first press: what a scope holds and would clear.</summary>
public sealed record HistoryPlanRead(string? Workspace, string? Quest, string? Ask, bool Failed = false, bool Said = false, bool Clear = false, bool Json = false);

/// <summary>One unit the first press listed.</summary>
public sealed record HistoryUnit(string? Kind, string? Id);

/// <summary>The second press: exactly the <c>Units</c> the first press listed as may go.</summary>
public sealed record HistoryClearPress(string? Workspace, string? Quest, string? Ask, IReadOnlyList<HistoryUnit>? Units, bool Failed = false, bool Said = false);
