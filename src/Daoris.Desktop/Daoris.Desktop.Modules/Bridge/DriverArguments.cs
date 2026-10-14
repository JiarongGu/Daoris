using System.Text.Json;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// What the page may leave out of a request to <see cref="DriverModule"/>, read one way (REV3 CLEAN1), and what a route says
/// before the loop's service answers. Every domain's handler class imports it with <c>using static</c>, so a handler reads
/// <c>Optional(request, "workspace")</c> as it did when the domains were the module's partials (PART10d, D166).
/// </summary>
/// <remarks>
/// The routes read optional values by hand, and differently, before REV3: one took an empty <c>adapter</c> as a name where
/// this takes it as absent.
/// </remarks>
public static class DriverArguments
{
    /// <summary>A string the page may send, or null — blank is absent.</summary>
    internal static string? Optional(IpcRequest request, string name) =>
        request.Payload is { } payload
        && payload.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
        && value.GetString() is { Length: > 0 } text
            ? text
            : null;

    /// <summary>A flag the page may send: true only when it says true.</summary>
    internal static bool Flag(IpcRequest request, string name) =>
        request.Payload is { } payload
        && payload.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.True;

    /// <summary>
    /// A conversation's tree choice (D51, NEW1a): absent is the driver's own, true a tree of its own, false the checkout.
    /// <see cref="Flag"/> cannot tell absent from false.
    /// </summary>
    public static bool? TreeChoice(IpcRequest request) =>
        request.Payload is { } payload && payload.TryGetProperty("ownTree", out var value)
            ? value.ValueKind switch { JsonValueKind.True => true, JsonValueKind.False => false, _ => null }
            : null;

    /// <summary>A whole number the page may send, or null.</summary>
    internal static long? Number(IpcRequest request, string name) =>
        request.Payload is { } payload
        && payload.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetInt64(out var number)
            ? number
            : null;

    /// <summary>
    /// The profile a profile verb is about: Agents' account actions and Accounts' <i>Try now</i>. Absent is a refusal, never
    /// a guess.
    /// </summary>
    internal static string Named(IpcRequest request) =>
        Optional(request, "profile") is { Length: > 0 } profile
            ? profile
            : throw Refusals.Because(
                Refusals.HarnessProfileNeeded,
                "that action needs an account name.",
                ("action", "profile"));

    /// <summary>What every route that needs the loop's service says before it answers (REV3 CLEAN1: five wrote it).</summary>
    internal static Exception NotReady() => Refusals.Because(
        Refusals.DriverNotReady, "the driver is still coming up — its service is not answering yet. A moment.");
}
