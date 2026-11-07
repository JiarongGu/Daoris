namespace Daoris.Driver;

/// <summary>
/// Codex's words for an account's limit: the agent's, which its protocol door,
/// <c>codex-acp</c>, is read through. Read in Codex's source and never seen
/// on a door: they reach Daoris as the JSON-RPC error's <c>data.message</c>, which the door says after the
/// message <c>Internal error</c>.
/// </summary>
/// <remarks>
/// <para><b>One clause of sentences.</b> Codex joins no <c>·</c>: the reset is the rest of the marker's own
/// clause, <i>Try again at {time}.</i>, and <i>Try again later.</i> names none.</para>
///
/// <para><b>Its time has no zone</b>: the machine's local time, <i>4:05 PM</i> the same day, else
/// <i>Oct 3rd, 2026 4:05 PM</i>, read in the machine's zone and said as assumed.</para>
///
/// <para><b>The marker starts a clause or follows what a door put before it</b> (<c>: </c>), since a clause that
/// does not end on it, as Claude Code's must, cannot tell a refusal from a quotation any other way.</para>
/// </remarks>
public static class CodexLimits
{
    private const string Source =
        "read, not measured: codex 0.159.3's source at rust-v0.159.3 (codex-rs/protocol/src/error.rs, UsageLimitReachedError), which codex-acp 2.1.1 sends as the JSON-RPC error's data.message beside the message `Internal error` (limit-signals evidence §4)";

    public static LimitWords Words { get; } = new(
        Markers:
        [
            @"(?:^|: )you've hit your (?<hit>usage) limit(?: for .+?)?\.(?= |$)",
            // The workspace's credits are spent: the owner refills, so the sentence names no reset.
            @"(?:^|: )your workspace is out of (?<hit>credits)\.(?= |$)",
        ],
        Resets: [@"\btry again at (?<when>.+?)\.?$"],
        Recorded:
        [
            new(
                "Your workspace is out of credits. Ask your workspace owner to refill in order to continue.",
                "2026-10-26",
                "the protocol door, codex-acp 2.1.1: the JSON-RPC error `Internal error` answering session/prompt in three driven sessions, the sentence in data.message (CODEXCREDITS1)",
                "2.1.1"),
            new("You’ve hit your usage limit. … Try again at 4:05 PM.", "2026-10-02", $"{Source}; the plan's own next step elided", "0.159.3"),
            new("You’ve hit your usage limit. … or try again at Oct 3rd, 2026 4:05 PM.", "2026-10-02", $"{Source}; the plan's own next step elided", "0.159.3"),
            new("You’ve hit your usage limit for …. Switch to another model now, or try again at 4:05 PM.", "2026-10-02", $"{Source}; a named model's limit, the model elided", "0.159.3"),
            new("You’ve hit your usage limit. … Try again later.", "2026-10-02", $"{Source}; no reset known, the plan's own next step elided", "0.159.3"),
        ]);
}
