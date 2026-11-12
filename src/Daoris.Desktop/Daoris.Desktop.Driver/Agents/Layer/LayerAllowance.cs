using System.Text.Json;
using System.Text.Json.Nodes;
using Lyntai.Agents;

namespace Daoris.Driver;

/// <summary>
/// An allowance report the layer states mid-turn, put in the shape the native door's rate-limit frame has so the one reader of an agent's
/// windows (<see cref="AccountReadings"/>) reads it: the report's window under the agent's own key, its use as a fraction, its reset in Unix
/// seconds, its standing and its overage as the frame's fields. A report naming no window with a reset says nothing, as a frame that names none.
/// </summary>
internal static class LayerAllowance
{
    public static JsonElement? Info(AgentAllowanceReport report)
    {
        if (report.Window is not { } window || KeyOf(window) is not { } key) return null;

        var named = new JsonObject();
        if (window.UsedPercent is { } used) named["utilization"] = used / 100;
        if (window.ResetsAt is { } reset) named["resetsAt"] = reset.ToUnixTimeMilliseconds() / 1000.0;

        var info = new JsonObject { ["rateLimitType"] = key, ["unifiedWindows"] = new JsonObject { [key] = named } };
        if (report.Status is { } status) info["status"] = status;
        if (report.IsUsingOverage is { } overage) info["isUsingOverage"] = overage;
        return JsonSerializer.SerializeToElement(info);
    }

    /// <summary>The agent's key for a window: the five-hour and the weekly by Claude Code's words, a model's week by its scope, any other by its own word.</summary>
    private static string? KeyOf(AgentAllowanceWindow window) => window.Name switch
    {
        AgentAllowanceWindow.FiveHour => "five_hour",
        AgentAllowanceWindow.Weekly when window.Scope is { Length: > 0 } scope => "seven_day_" + scope,
        AgentAllowanceWindow.Weekly => "seven_day",
        { Length: > 0 } other => other,
        _ => null,
    };
}
