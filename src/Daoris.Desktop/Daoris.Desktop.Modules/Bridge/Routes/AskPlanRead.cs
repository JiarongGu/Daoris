namespace Daoris.Desktop;

/// <summary>An ask's plan as the page reads it: its parts, each with the quest it became.</summary>
public sealed record AskPlanRead(string? Ask);

/// <summary>
/// The person's answer to one open question of an ask's delivery plan (<c>Key</c>, <c>Words</c>), or their waiver of one release order
/// (<c>Deployable</c> after <c>On</c>, each <c>repository/id</c>, with <c>Reason</c>).
/// </summary>
public sealed record AskPlanSettlePress(string Ask, string? Key = null, string? Words = null, string? Deployable = null, string? On = null, string? Reason = null);
