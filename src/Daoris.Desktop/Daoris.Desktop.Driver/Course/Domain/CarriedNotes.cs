namespace Daoris.Driver;

/// <summary>The lines a limit's move says where it carries the conversation to the new account, or why it hands the brief instead.</summary>
internal static class CarriedNotes
{
    /// <summary>On the part it left: where its conversation went on.</summary>
    public static Noted On(string session, string agent, string? account, string why, long? tokens) =>
        Noted.Of(
            StartNoteCodes.MovedCarriedOn,
            $"Went on as a new part of this session, {session}, with {agent}{As(account)}, because {HandOverWhy.Noted(why)}: "
            + $"its own conversation{At(tokens)} was carried to that account and goes on there.",
            ("session", session), ("agent", agent), ("account", account), ("why", why), ("tokens", tokens));

    /// <summary>On the new part: the conversation it goes on with, whole or compacted first.</summary>
    public static Noted From(string session, string agent, string? account, string why, CarryPlan.Carried carried) =>
        carried is CarryPlan.Compacted compacted
            ? Noted.Of(
                StartNoteCodes.MovedCompactedFrom,
                $"Continues the part before it, {session}, which ran on {agent}{As(account)}, because {HandOverWhy.Noted(why)}: "
                + $"its own conversation{At(compacted.Tokens)} was carried here, past the {compacted.Bound} tokens its agent carries whole, "
                + "so its agent compacts it before it goes on, with no brief.",
                ("session", session), ("agent", agent), ("account", account), ("why", why), ("tokens", compacted.Tokens), ("bound", compacted.Bound))
            : Noted.Of(
                StartNoteCodes.MovedCarriedFrom,
                $"Continues the part before it, {session}, which ran on {agent}{As(account)}, because {HandOverWhy.Noted(why)}: "
                + $"its own conversation{At(carried.Tokens)} was carried here whole and goes on, with no brief.",
                ("session", session), ("agent", agent), ("account", account), ("why", why), ("tokens", carried.Tokens));

    /// <summary>On a new part handed the brief where the conversation could not be carried, by the carry's code.</summary>
    public static Noted NotCarried(string reason) =>
        Noted.Of(StartNoteCodes.MovedNotCarried, $"Its own conversation was not carried to this account ({reason}), so it was handed Daoris's brief.", ("reason", reason));

    private static string As(string? account) => account is { Length: > 0 } named ? $" as `{named}`" : "";

    private static string At(long? tokens) => tokens is { } counted ? $", at {counted} tokens of context," : "";
}
