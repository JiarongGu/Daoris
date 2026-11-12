namespace Daoris.Driver;

/// <summary>
/// Claude Code's words for its windows: the <c>rate_limit_event</c>'s
/// <c>rate_limit_info</c>, which its protocol door forwards unchanged as <c>usage_update._meta["_claude/rateLimit"]</c>.
/// </summary>
public static class ClaudeWindows
{
    private const string Declared =
        "declared, not seen: the Agent SDK's SDKRateLimitInfo.status (sdk.d.ts:5584, 0.3.284 and 0.3.287), read by TOOL4b (limit-signals evidence §1.2); the CLI derives its warning itself (§1.3)";

    public static WindowWords Words { get; } = new(
        Windows: new Dictionary<string, string>(StringComparer.Ordinal) { ["five_hour"] = "session", ["seven_day"] = AccountWindows.Weekly },
        Clear: ["allowed"],
        Warned: ["allowed_warning"],
        Refused: ["rejected"],
        Scale: 1,
        Credits: "isUsingOverage",
        Recorded:
        [
            new(
                """{"status":"allowed","resetsAt":<+2.0 h>,"rateLimitType":"five_hour","overageStatus":"rejected","overageDisabledReason":"member_zero_credit_limit","isUsingOverage":false,"unifiedWindows":{"five_hour":{"utilization":0.88,"resetsAt":<+2.0 h>},"seven_day":{"utilization":0.14,"resetsAt":<+105.3 h>}}}""",
                "2026-10-02",
                "the native door's binary and flags, one headless turn outside Daoris's driver, as rate_limit_event's rate_limit_info (limit-signals evidence §1.1); KNOW3's 116 frames that day share its keys",
                "2.1.287"),
            new("""{"status":"allowed_warning"}""", "2026-10-02", Declared),
            new("""{"status":"rejected"}""", "2026-10-02", Declared),
        ],
        Shape: Shape);

    /// <summary>
    /// A window's length and scope as Claude Code's key names it, for the usage history: <c>five_hour</c> five
    /// hours and <c>seven_day</c> a week, the whole plan's; <c>seven_day_&lt;scope&gt;</c> a week of what the rest names (a model
    /// family: <c>seven_day_opus</c>); any other key
    /// neither, never guessed.
    /// </summary>
    public static (int? Minutes, string? Scope) Shape(string key) => key switch
    {
        "five_hour" => (300, null),
        "seven_day" => (10080, null),
        _ when key.StartsWith("seven_day_", StringComparison.Ordinal) && key.Length > "seven_day_".Length => (10080, key["seven_day_".Length..]),
        _ => (null, null),
    };
}
