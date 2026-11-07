namespace Daoris.Driver;

/// <summary>
/// Claude Code's words for an account's limit: five recorded sentences, on its ACP
/// adapter's door and on the maker's own CLI outside Daoris, which inform the grammar, and the weekly reset
/// the maker's errors page documents with a weekday.
/// </summary>
public static class ClaudeLimits
{
    public static LimitWords Words { get; } = new(
        Markers: [@"\byou've hit your (?<hit>.+?) limit$"],
        Resets: [@"^(?:your (?<window>.+?) limit )?resets (?<when>.+?)(?: \((?<zone>[^()]*)\))?$"],
        Recorded:
        [
            new(
                "Internal error: You've hit your individual spend limit · run /usage-credits to ask your admin for a higher limit · your session limit resets 7am (<zone>)",
                "2026-09-27",
                "the protocol door: the JSON-RPC error answering session/prompt, Claude Code over its ACP adapter, 370k tokens into a driven turn; the adapter then exited 0"),
            new(
                "the ACP agent refused the call: Internal error: You've hit your individual spend limit · … · your session limit resets 7:50am (<zone>)",
                "2026-09-29",
                "the protocol door, in two driven sessions, as the driver's note; the record given to the design kept the note's start and the reset, and TOOL4's row names it the spend-limit refusal, written here in observation 1's words"),
            new(
                "You've hit your weekly limit · resets Oct 6, 10pm (<zone>)",
                "2026-10-01",
                "the maker's CLI outside Daoris, an HTTP 429 rate_limit: no door of Daoris's"),
            new(
                "You've hit your individual spend limit · … · your weekly limit resets Oct 3, 4pm (<zone>)",
                "2026-10-01",
                "the protocol door, a driven carry-on on the tool's own sign-in, refused mid-turn and then twice at once; the record given to the design elided the middle clause"),
            new(
                "You've hit your individual spend limit · run /usage-credits to ask your admin for a higher limit · your weekly limit resets Oct 3, 4pm (<zone>)",
                "2026-10-02",
                "the maker's CLI outside Daoris, an HTTP 429 rate_limit mid-run: no door of Daoris's"),
            new(
                "You've hit your weekly limit · resets Mon 12:00am",
                "2026-10-02",
                "documented, not seen: the maker's errors page, read by TOOL4b (limit-signals evidence §2), which prints it with no zone; the weekday it names is TOOL4k's grammar"),
            new(
                "You've hit your session limit · resets 3am (<zone>)",
                "2026-11-07",
                "the protocol door: the agent's whole last message of a driven turn its adapter then ended end_turn, on Sonnet, 155k tokens into the session, and again as each of four later turns' only words; no failure was carried"),
        ]);
}
