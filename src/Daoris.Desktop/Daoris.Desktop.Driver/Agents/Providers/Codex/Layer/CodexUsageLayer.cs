using System.Globalization;
using System.Runtime.CompilerServices;
using Lyntai;
using Lyntai.Agents;

namespace Daoris.Driver;

/// <summary>
/// Codex's windows read through the layer: its CLI provider registered under the holder's
/// id for its usage source alone, never opened; its server in the driver's own folder, and told the client
/// name the read uses; the reading named as <see cref="CodexUsage"/> names it, by length and its limit's scope, its plan
/// never read. Bound when the driver loads, where the driver carries the layer.
/// </summary>
internal static class CodexUsageLayer
{
    public static LayerUsageVendor Vendor { get; } = new(
        CodexAccounts.Agent,
        CodexUsage.Patience,
        (builder, command) => builder.AddCodexCliAgentSession(command: command, id: CodexAccounts.Agent, agent: CodexAccounts.Agent),
        options =>
        {
            options.WorkingDirectory = _ => Directory.GetCurrentDirectory();
            options.ClientName = "daoris-driver";
            options.ClientVersion = "0";
        },
        Answer);

    [ModuleInitializer]
    internal static void Install() => UsageReads.Bind(CodexUsage.Question, new LayerUsageRead(CodexUsage.Question, Vendor));

    private static LayerUsageAnswer Answer(AgentAllowanceReading reading, DateTimeOffset now)
    {
        var walk = new List<WindowReading>();
        var seen = new List<WindowSeen>();
        foreach (var window in reading.Windows)
        {
            if (window.Length is not { } length) continue;
            var minutes = (int)length.TotalMinutes;
            var own = window.Scope is null;
            var used = window.UsedPercent / 100;
            if (used is not null || window.ResetsAt is not null)
            {
                var key = $"{minutes.ToString(CultureInfo.InvariantCulture)}-minute";
                var scope = own ? reading.LimitId : window.Scope;
                seen.RemoveAll(each => each.Window == key && each.Scope == scope);
                seen.Add(new WindowSeen(
                    key, own ? CodexUsage.WindowOf(minutes) : null, scope, minutes, used, LayerUsageRead.Precision(window.UsedStep),
                    window.ResetsAt, window.ResetsAt is null ? null : UsageHistory.ResetSaid));
            }

            if (!own || used is not { } fraction || window.ResetsAt is not { } reset) continue;
            var name = CodexUsage.WindowOf(minutes);
            walk.RemoveAll(each => each.Window == name);
            walk.Add(new WindowReading(name, fraction, reset));
        }

        return new(walk, seen, Plan: null);
    }
}
