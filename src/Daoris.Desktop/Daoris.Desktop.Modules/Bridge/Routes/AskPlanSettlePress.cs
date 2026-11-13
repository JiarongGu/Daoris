namespace Daoris.Desktop;

/// <summary>The person's answer to an open question of an ask's delivery plan (<c>Key</c>, <c>Words</c>) or waiver of a release order (<c>Deployable</c> after <c>On</c>, with <c>Reason</c>).</summary>
public sealed record AskPlanSettlePress(string Ask, string? Key = null, string? Words = null, string? Deployable = null, string? On = null, string? Reason = null);
