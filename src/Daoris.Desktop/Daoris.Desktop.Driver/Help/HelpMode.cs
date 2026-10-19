namespace Daoris.Driver;

/// <summary>
/// Ask Daoris's access mode (HELPMODE1a1, D183 point 1), the person's choice, machine-wide for now: <see cref="Restricted"/>, what
/// D89 made it, reading and proposing with the person applying; or <see cref="Full"/>, its harness's own mode that asks nothing.
/// </summary>
/// <remarks>
/// <para><b>Kept in the driver's file</b> as <c>helpMode</c> (<see cref="DriverConfig.HelpMode"/>), written only while full; the
/// CLI's <c>driverconfig.ts</c> reads it by the same table. Set at a terminal with <c>daoris-driver help mode</c>, and never by Ask
/// Daoris itself: no proposal names it (D183 point 2, full access is never chosen for the person).</para>
/// </remarks>
public static class HelpModes
{
    /// <summary>It reads, advises and proposes, and the person applies (D89): the default.</summary>
    public const string Restricted = "restricted";

    /// <summary>It acts on this machine itself, in its harness's mode that asks nothing (D183 point 1).</summary>
    public const string Full = "full";

    /// <summary>The modes, in the order a person is offered them.</summary>
    public static IReadOnlyList<string> All { get; } = [Restricted, Full];

    /// <summary>The mode <paramref name="value"/> names, trimmed and in any case, or null for none.</summary>
    public static string? Named(string? value) =>
        All.FirstOrDefault(mode => string.Equals(mode, value?.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// A record's mode as read (HELPMODE1a2, D183 point 3): full where it says full, in any case; restricted for none, a record from
    /// before the modes, and for one this build does not know, as the driver's file reads it.
    /// </summary>
    public static string Recorded(string? kept) => Named(kept) == Full ? Full : Restricted;

    /// <summary>How a conversation runs in <paramref name="mode"/>, in a sentence: restricted, or with full access.</summary>
    private static string Runs(string mode) => Recorded(mode) == Full ? "with full access" : "restricted";

    /// <summary>The mode's name in a sentence: restricted, or full access.</summary>
    private static string Spelled(string mode) => Recorded(mode) == Full ? "full access" : "restricted";

    /// <summary>
    /// What a conversation going on says first where it goes on under another mode than it last ran under (HELPMODE1a2, D183 point
    /// 3), before what it runs under now; null where the mode is the same.
    /// </summary>
    /// <param name="ran">The mode its record says it last ran under.</param>
    /// <param name="now">The mode it goes on under.</param>
    public static string? ChangedSince(string? ran, string now) => Recorded(ran) == Recorded(now)
        ? null
        : $"Ask Daoris's mode changed since this conversation last ran: it ran {Runs(Recorded(ran))}, and goes on {Runs(now)}.";

    /// <summary>
    /// What a running conversation's console is told where the person's mode changes under it (HELPMODE1a2, D183 point 2): the switch
    /// takes its next start, never the conversation as it runs, since what its start handed for its mode (its rules, its servers and
    /// its room) is not changed while it runs; and how to take it now. Null where it already runs in the mode chosen.
    /// </summary>
    /// <param name="running">The mode it runs under, its start's.</param>
    /// <param name="chosen">The mode the person chose now.</param>
    public static string? TakesNextStart(string running, string chosen) => Recorded(running) == Recorded(chosen)
        ? null
        : $"— Ask Daoris's mode is now {Spelled(chosen)}, from this conversation's next start: it runs {Runs(running)} until then, "
          + "since the rules, servers and room its start handed are not changed while it runs. End it, and your next words resume it "
          + $"{Runs(chosen)}.";

    /// <summary>
    /// The mark before a protocol door's posture that must be set (<c>AcpSession.SetPostureAsync</c>): an agent that does not offer
    /// it fails the start, so a full start never runs in a narrower mode under the full label (D183 point 3), nor a driven start in
    /// another mode than its repository's asking or unrestricted permission prompts name (AUTONOMY1b, <see cref="DrivenAccess"/>).
    /// </summary>
    public const char Required = '!';
}

/// <summary>
/// What a start of Ask Daoris gets on its harness under the person's mode (HELPMODE1a1, D183 points 1, 3): the posture its protocol
/// door asks, or the permission mode its native door names; or why it cannot start in that mode. Read at each start, a
/// conversation going on among them, from the mode chosen then.
/// </summary>
/// <param name="Mode">The mode it runs in, one of <see cref="HelpModes"/>.</param>
/// <param name="Said">
/// What its conversation is told first (D183 point 3): the mode, and the harness's own word for it. The mode chosen is the file's;
/// this is the one the start got, kept apart from it, and its record keeps <paramref name="Mode"/> (HELPMODE1a2).
/// </param>
public sealed record HelpAccess(string Mode, string Said)
{
    /// <summary>The protocol door's posture: the agent's asking mode restricted, its mode that asks nothing marked required in full.</summary>
    public string? Posture { get; init; }

    /// <summary>The native door's <c>--permission-mode</c> in full access; null keeps the adapter's own.</summary>
    public string? PermissionMode { get; init; }

    /// <summary>Why it does not start in this mode on this harness, said plainly; null where it does.</summary>
    public string? Refusal { get; init; }

    /// <summary>Whether it runs with full access.</summary>
    public bool Full => Mode == HelpModes.Full;

    /// <summary>
    /// The access a start of Ask Daoris gets on <paramref name="adapter"/> in <paramref name="mode"/>: restricted as it always ran;
    /// full in the harness's own mode that asks nothing (<see cref="ISessionAdapter.FullPosture"/>), or refused where it names none.
    /// </summary>
    public static HelpAccess For(string mode, ISessionAdapter adapter)
    {
        if (mode != HelpModes.Full)
        {
            return new(
                HelpModes.Restricted,
                adapter.Wire == SessionWire.Pipe
                    ? $"Ask Daoris runs restricted on `{adapter.Name}`: the `acceptEdits` permission mode with its allow-list, so it "
                      + "reads and proposes, and the person applies."
                    : $"Ask Daoris runs restricted on `{adapter.Name}`: it asks for the agent's `{HelpRoom.Posture}` mode where the agent "
                      + "offers it, so it reads and proposes, and the person applies.")
            {
                Posture = HelpRoom.Posture,
            };
        }

        if (adapter.FullPosture is not { Length: > 0 } full)
        {
            var refusal = $"Ask Daoris's full access has no mode on `{adapter.Name}`: Daoris knows none of this agent's that asks nothing, so "
                + "it does not start there in full access. Choose restricted access for it (`daoris-driver help mode restricted`), or "
                + "another agent for Ask Daoris.";
            return new(HelpModes.Full, refusal) { Refusal = refusal };
        }

        var denies = adapter.TakesSettings
            ? "and the person's own denies still hold"
            : "and this agent takes no rules, so the person's own denies do not reach it";
        return adapter.Wire == SessionWire.Pipe
            ? new(HelpModes.Full, $"Ask Daoris runs with full access on `{adapter.Name}`, as the person chose: the `{full}` permission "
                + $"mode, which asks nothing, with every plugin server here; {denies}.")
            {
                PermissionMode = full,
            }
            : new(HelpModes.Full, $"Ask Daoris runs with full access on `{adapter.Name}`, as the person chose: the agent's `{full}` mode, "
                + $"which asks nothing, with every plugin server here; {denies}.")
            {
                Posture = $"{HelpModes.Required}{full}",
            };
    }
}
