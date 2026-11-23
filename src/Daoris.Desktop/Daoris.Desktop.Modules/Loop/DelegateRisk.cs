using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>The risk policy's reading of a working session's proved press, where it classes the press's act.</summary>
public static class DelegateRisk
{
    /// <summary>
    /// A working session's press the risk policy classes, judged at the home's <c>driver.json</c> now and journaled: the Agents place's
    /// <c>pin</c> and <c>update</c> are <c>agent.version</c> at the machine (<see cref="AgentVersionAct"/>); null for any other press.
    /// </summary>
    public static RiskVerdict? Of(string home, string session, Shenora.Core.Ipc.IpcRequest request, DateTimeOffset now)
    {
        if (!string.Equals(request.Module, DriverModule.Name, StringComparison.OrdinalIgnoreCase) || request.Type != AgentRoutes.Harness.Route) return null;
        if (request.Payload is not { ValueKind: System.Text.Json.JsonValueKind.Object } payload) return null;
        string? Text(string name) => payload.EnumerateObject().FirstOrDefault(field => string.Equals(field.Name, name, StringComparison.OrdinalIgnoreCase)) is
            { Value.ValueKind: System.Text.Json.JsonValueKind.String } found ? found.Value.GetString() : null;
        if (!AgentVersionAct.Moves(Text("action"))) return null;
        var actor = AgentVersionAct.PipeSessionActor(session);
        var subject = $"{Text("action")} {Text("door")}{(Text("version") is { } version ? $" {version}" : "")}";
        return AgentVersionAct.Judged(home, null, actor, subject, now);
    }
}
