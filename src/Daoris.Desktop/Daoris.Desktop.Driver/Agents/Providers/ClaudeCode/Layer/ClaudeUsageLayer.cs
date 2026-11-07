using System.Runtime.CompilerServices;
using Lyntai;
using Lyntai.Agents;

namespace Daoris.Driver;

/// <summary>
/// Claude Code's windows read through the layer: its CLI provider registered under
/// <c>claude-code</c> for its usage source alone, never opened, its server in the account home's own folder,
/// and the reading named as <see cref="ClaudeUsage"/> names it, a window not yet opened turning over a window's length after the read.
/// Bound when the driver loads, where the driver carries the layer: Lyntai's reader keeps a scoped row
/// whose model the installed server names with a null <c>id</c> and skips a row it cannot read, so the
/// oracle finds the two reads alike but for the spawn's <c>-p</c>, which changes no answer.
/// </summary>
internal static class ClaudeUsageLayer
{
    private const string Door = "claude-code";

    private const string ScopedWeek = "seven_day_";

    public static LayerUsageVendor Vendor { get; } = new(
        Door,
        ClaudeUsage.Patience,
        (builder, command) => builder.AddClaudeCliAgentSession(command: command, id: Door, agent: Door),
        options => options.WorkingDirectory = account => Path.Combine(account.Home!, "usage-read"),
        Answer);

    [ModuleInitializer]
    internal static void Install() => UsageReads.Bind(ClaudeUsage.Question, new LayerUsageRead(ClaudeUsage.Question, Vendor));

    /// <summary>The reading under the usage reader's names: <c>five_hour</c>, <c>seven_day</c> and <c>seven_day_&lt;scope&gt;</c>, and the server's rows as <c>limits:&lt;kind&gt;</c>.</summary>
    private static LayerUsageAnswer Answer(AgentAllowanceReading reading, DateTimeOffset now)
    {
        var walk = new List<WindowReading>();
        var seen = new List<WindowSeen>();
        foreach (var window in reading.Windows)
        {
            var key = window.Scope is { } scope ? ScopedWeek + scope
                : window.Name == AgentAllowanceWindow.FiveHour ? "five_hour"
                : window.Name == AgentAllowanceWindow.Weekly ? "seven_day"
                : null;
            if (key is null) continue;
            var (minutes, scoped) = ClaudeWindows.Shape(key);
            var walked = ClaudeWindows.Words.Windows.GetValueOrDefault(key);
            var used = window.UsedPercent / 100;
            var reset = window.ResetsAt;
            var from = reset is null ? null : UsageHistory.ResetSaid;
            if (reset is null && walked is not null && used == 0)
            {
                reset = now + (minutes == ClaudeUsage.SessionLength.TotalMinutes ? ClaudeUsage.SessionLength : ClaudeUsage.WeekLength);
                from = UsageHistory.ResetMadeUp;
            }

            Kept(seen, new WindowSeen(key, walked, scoped, minutes, used, LayerUsageRead.Precision(window.UsedStep), reset, from));
            if (window.Scope is null && walked is not null && used is { } fraction && reset is { } at) walk.Add(new WindowReading(walked, fraction, at));
        }

        foreach (var row in reading.Limits)
        {
            var minutes = row.Kind == "session" ? (int)ClaudeUsage.SessionLength.TotalMinutes
                : row.Kind.StartsWith("weekly", StringComparison.Ordinal) ? (int)ClaudeUsage.WeekLength.TotalMinutes : (int?)null;
            Kept(seen, new WindowSeen(
                $"limits:{row.Kind}", null, ScopeOf(row), minutes, row.UsedPercent / 100,
                LayerUsageRead.Precision(row.UsedStep), row.ResetsAt, row.ResetsAt is null ? null : UsageHistory.ResetSaid));
        }

        return new(walk, seen, reading.Plan is { Length: > 0 } plan ? plan : null);
    }

    /// <summary>
    /// A row's scope under the usage reader's name: the word a string scope carries. A scope the server writes as an object, naming a model (with its
    /// <see cref="AgentAllowanceLimit.ScopeName"/>) or a <see cref="AgentAllowanceLimit.Surface"/>, is a row the usage reader keeps with no
    /// scope, so the model and the surface are not kept here either. A model named with no display name reads as a word would: the
    /// reading cannot tell the two apart, and no recorded answer has one.
    /// </summary>
    private static string? ScopeOf(AgentAllowanceLimit row) =>
        row.ScopeName is null && row.Surface is null && row.Scope is { Length: > 0 } scope ? scope : null;

    /// <summary>As the usage reader keeps a window: one that says something, a window said twice its last.</summary>
    private static void Kept(List<WindowSeen> seen, WindowSeen window)
    {
        if (window.Used is null && window.Reset is null) return;
        seen.RemoveAll(each => each.Window == window.Window && each.Scope == window.Scope);
        seen.Add(window);
    }
}
