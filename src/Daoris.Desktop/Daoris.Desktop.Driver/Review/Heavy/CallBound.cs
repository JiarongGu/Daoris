namespace Daoris.Driver;

/// <summary>
/// The bound a light session's call may run to before the watcher tells it: the
/// pipeline's <c>callMinutes</c>, 20 by default. Under Current no rule sets it, and a steps version's <c>work</c> takes no such field, so
/// their light sessions' bound is the default; a graph version's <c>claimed</c> option is read from the version its run bound, with its
/// <c>checks</c> (<see cref="MinutesOf"/>), and kept beside them by the start (<see cref="SessionChecks.HeldMinutes"/>), so every run of
/// the session reads the same. The look itself is the session watcher's (<see cref="LongCallLook"/>).
/// </summary>
public static class CallBound
{
    /// <summary><c>callMinutes</c>' default: a judgement the install measures.</summary>
    public const int Default = 20;

    /// <summary>The option's name, on a graph version's <c>claimed</c> node and beside a session's kept checks.</summary>
    public const string Option = "callMinutes";

    /// <summary>
    /// The <c>callMinutes</c> a run's bound graph version sets on its <c>claimed</c> node, as written: a whole number of minutes from one;
    /// null where it sets none or none that reads, under Current, and under a steps version, whose bound is the default.
    /// </summary>
    public static int? MinutesOf(ICheckedProcess? process)
    {
        if (process is not { Named: true, Claimed: { } claimed } || !claimed.TryGetValue(Option, out var text)) return null;
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(text);
            return Minutes(document.RootElement);
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    /// <summary>A bound as written: a JSON number that is a whole number of minutes from one, else null, the default.</summary>
    internal static int? Minutes(System.Text.Json.JsonElement value) =>
        value.ValueKind == System.Text.Json.JsonValueKind.Number && value.TryGetInt32(out var minutes) && minutes >= 1 ? minutes : null;

    /// <summary>
    /// The bound a session's run takes: its <c>callMinutes</c> under <c>light</c>, <paramref name="minutes"/> where its version set them and
    /// the default where it did not, and none under any other checks.
    /// </summary>
    public static TimeSpan? Of(string? checks, int? minutes = null) =>
        checks == SessionChecks.Light ? TimeSpan.FromMinutes(minutes ?? Default) : null;
}
