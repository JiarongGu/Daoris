using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>A working session's press the risk policy classes: its verdict at the press, its session and subject, and the judge asked again at its release.</summary>
public sealed record PressRisk(RiskVerdict Verdict, string Session, string Subject, Func<RiskVerdict> Again)
{
    public string Actor => AgentVersionAct.PipeSessionActor(Session);
}

/// <summary>
/// The risk policy's reading of a working session's proved press, where it classes the press's act: the Agents place's <c>pin</c> and
/// <c>update</c> are <c>agent.version</c> at the machine (<see cref="AgentVersionAct"/>). Reading it journals nothing: the pipe journals a
/// press once it is admitted under its request id (<see cref="Opened"/>), and a held one again where it ends (<see cref="Released"/>), so a
/// resent request is one hold.
/// </summary>
public static class DelegateRisk
{
    /// <summary>The press's verdict at <paramref name="home"/>'s <c>driver.json</c> now; null for a press the policy does not class.</summary>
    public static PressRisk? Of(string home, string session, Shenora.Core.Ipc.IpcRequest request)
    {
        if (!string.Equals(request.Module, DriverModule.Name, StringComparison.OrdinalIgnoreCase) || request.Type != AgentRoutes.Harness.Route) return null;
        if (request.Payload is not { ValueKind: System.Text.Json.JsonValueKind.Object } payload) return null;
        string? Text(string name) => payload.EnumerateObject().FirstOrDefault(field => string.Equals(field.Name, name, StringComparison.OrdinalIgnoreCase)) is
            { Value.ValueKind: System.Text.Json.JsonValueKind.String } found ? found.Value.GetString() : null;
        if (!AgentVersionAct.Moves(Text("action"))) return null;
        var actor = AgentVersionAct.PipeSessionActor(session);
        var subject = $"{Text("action")} {Text("door")}{(Text("version") is { } version ? $" {version}" : "")}";
        return new PressRisk(AgentVersionAct.Verdict(home, null, actor, subject), session, subject, () => AgentVersionAct.Verdict(home, null, actor, subject));
    }

    /// <summary>The admitted press's line: a pass is its move's end, a gate the hold's open.</summary>
    public static void Opened(string home, PressRisk risk, string request, DateTimeOffset at)
    {
        var passed = risk.Verdict.Outcome == RiskVerdict.Pass;
        AgentVersionAct.Journal(home, null, risk.Verdict,
            new RiskLine(Key(risk, request), passed, risk.Actor, risk.Subject, passed ? AgentVersionAct.Passed : AgentVersionAct.Waits, !passed, at));
    }

    /// <summary>A held press's release, the policy read again then, with what ended it and who answered.</summary>
    public static void Released(string home, PressRisk risk, string request, string today, DateTimeOffset at) =>
        AgentVersionAct.Journal(home, null, risk.Again(), new RiskLine(Key(risk, request), true, risk.Actor, risk.Subject, today, true, at));

    private static string Key(PressRisk risk, string request) => $"{AgentVersionAct.Domain}:{risk.Actor}/{request}";
}
