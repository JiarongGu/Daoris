namespace Daoris.Driver;

/// <summary>
/// The second stand-in's words (AGENTPROVIDER1 row c; the agent-provider design §10): a limit, sign-in and window grammar no real agent
/// uses, each pattern standing on a sentence or frame its own program prints (<c>Program/agent.mjs</c>), so a reader that matched
/// another agent's words by habit fails against it. Its recorded sentences carry the program's, with the zone a placeholder as every
/// table's do.
/// </summary>
internal static class AltStubWords
{
    private const string Channel = "the stand-in's program, as its scenario prints it";

    public static LimitWords Limits { get; } = new(
        Markers: [@"^alt-stub: quota spent \((?<hit>.+?)\)$"],
        Resets: [@"^refills (?<when>.+?)(?: \((?<zone>[^()]*)\))?$"],
        Recorded:
        [
            new("alt-stub: quota spent (session) · refills 7am (<zone>)", "2026-11-06", Channel),
        ]);

    public static SignInWords SignIn { get; } = new(
        Markers: [@"(?:^|: )login needed(?=$|[.:])"],
        Recorded:
        [
            new("the ACP agent refused the call: login needed", "2026-11-06", Channel),
        ]);

    public static WindowWords Windows { get; } = new(
        Windows: new Dictionary<string, string>(StringComparer.Ordinal) { ["span_5h"] = "session", ["span_7d"] = AccountWindows.Weekly },
        Clear: ["open"],
        Warned: ["dwindling"],
        Refused: ["spent"],
        Scale: 1,
        Credits: "onCredit",
        Recorded:
        [
            new(
                """{"status":"open","resetsAt":<+2.0 h>,"rateLimitType":"span_5h","onCredit":false,"unifiedWindows":{"span_5h":{"utilization":0.4,"resetsAt":<+2.0 h>},"span_7d":{"utilization":0.1,"resetsAt":<+105.3 h>}}}""",
                "2026-11-06", "the stand-in's declaration of the frame its windows are said in"),
            new("""{"status":"dwindling"}""", "2026-11-06", "the stand-in's declaration"),
            new("""{"status":"spent"}""", "2026-11-06", "the stand-in's declaration"),
        ]);
}
