using System.Text.Json;

namespace Daoris.Desktop;

/// <summary>
/// What the page may leave out of a payload, read one way by <see cref="RoutePayload"/> and by the readers of a field
/// sent as free JSON, and what a route says before the loop's service answers. Every domain's handler class imports it with
/// <c>using static</c>.
/// </summary>
public static class DriverArguments
{
    /// <summary>A string the page may send, or null — blank is absent.</summary>
    internal static string? Optional(JsonElement? payload, string name) =>
        Sent(payload, name) is { ValueKind: JsonValueKind.String } value && value.GetString() is { Length: > 0 } text ? text : null;

    /// <summary>A flag the page may send: true only when it says true.</summary>
    internal static bool Flag(JsonElement? payload, string name) => Sent(payload, name) is { ValueKind: JsonValueKind.True };

    /// <summary>A yes or no the page may leave out: true or false as said, null otherwise; <see cref="Flag(JsonElement?, string)"/> cannot tell absent from false.</summary>
    internal static bool? Choice(JsonElement? payload, string name) =>
        Sent(payload, name)?.ValueKind switch { JsonValueKind.True => true, JsonValueKind.False => false, _ => null };

    /// <summary>A list of strings the page may send, or null where it sends none; an item that is not a string is left out.</summary>
    internal static IReadOnlyList<string>? Listed(JsonElement? payload, string name) =>
        Sent(payload, name) is { ValueKind: JsonValueKind.Array } value
            ? [.. value.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString()!)]
            : null;

    /// <summary>A whole number the page may send, or null.</summary>
    internal static long? Number(JsonElement? payload, string name) =>
        Sent(payload, name) is { ValueKind: JsonValueKind.Number } value && value.TryGetInt64(out var number) ? number : null;

    /// <summary>The field as sent, a JSON null included; null only where the payload is no object or does not name it.</summary>
    internal static JsonElement? Sent(JsonElement? payload, string name) =>
        payload is { ValueKind: JsonValueKind.Object } body && body.TryGetProperty(name, out var value) ? value : null;

    /// <summary>
    /// The profile a profile verb is about: Agents' account actions and Accounts' <i>Try now</i>. Absent is a refusal, never
    /// a guess.
    /// </summary>
    internal static string Named(string? named) =>
        named is { Length: > 0 } profile
            ? profile
            : throw Refusals.Because(
                Refusals.HarnessProfileNeeded,
                "that action needs an account name.",
                ("action", "profile"));

    /// <summary>What every route that needs the loop's service says before it answers.</summary>
    internal static Exception NotReady() => Refusals.Because(
        Refusals.DriverNotReady, "the driver is still coming up — its service is not answering yet. A moment.");
}
