using System.Diagnostics;
using System.Globalization;
using LibGit2Sharp;

namespace GitLibraryBench;

/// <summary>What the bench prints: one line a measure, milliseconds rounded, so a run reads as the area's table does.</summary>
internal static class Report
{
    public static void Machine()
    {
        var processes = Process.GetProcesses();
        int Named(string name) => processes.Count(each => each.ProcessName.Equals(name, StringComparison.OrdinalIgnoreCase));
        Console.WriteLine(
            $"machine: {Environment.ProcessorCount} cores, {processes.Length} processes ({Named("git")} git, {Named("dotnet")} dotnet, "
            + $"{Named("node")} node, {Named("claude")} claude); libgit2 {GlobalSettings.Version.LibGit2CommitSha}, features {GlobalSettings.Version.Features}");
    }

    public static void Starts(string name, double totalMs, IReadOnlyList<StartSample> samples)
    {
        var walls = samples.Select(each => each.WallMs).ToArray();
        var creates = samples.Select(each => each.CreateMs).ToArray();
        var owns = samples.Where(each => each.GitMs is not null).Select(each => each.GitMs!.Value).ToArray();
        var outsides = samples.Where(each => each.OutsideMs is not null).Select(each => each.OutsideMs!.Value).ToArray();
        string Of(Func<StartSample, double?> part) => samples.Select(part).OfType<double>().ToArray() is { Length: > 0 } values ? Ms(Median(values)) : "-";
        Console.WriteLine(
            $"{name}: {samples.Count} starts in {Ms(totalMs)}; a start min/median/p90/max {Ms(walls.Min())}/{Ms(Median(walls))}/{Ms(Percentile(walls, 0.9))}/{Ms(walls.Max())}; "
            + $"Process.Start median {Ms(Median(creates))} (max {Ms(creates.Max())}); "
            + (owns.Length > 0
                ? $"git's own median {Ms(Median(owns))} (max {Ms(owns.Max())}: to its start {Of(each => each.SetupMs)}, the trace's process walk {Of(each => each.WalkMs)}, "
                    + $"the command {Of(each => each.WorkMs)}); outside git median {Ms(Median(outsides))} (max {Ms(outsides.Max())})"
                : "untraced"));
    }

    public static void Row(string name, IReadOnlyList<double> passes, string? note = null)
    {
        var first = passes[0];
        var rest = passes.Skip(1).ToArray();
        Console.WriteLine($"  {name}: first {Ms(first)}, then median {(rest.Length > 0 ? Ms(Median(rest)) : "-")}{(note is null ? "" : $" ({note})")}");
    }

    public static string Ms(double value) => value.ToString(value < 10 ? "0.0" : "0", CultureInfo.InvariantCulture) + " ms";

    public static double Median(IReadOnlyList<double> values) => Percentile(values, 0.5);

    public static double Percentile(IReadOnlyList<double> values, double share)
    {
        var sorted = values.Order().ToArray();
        if (sorted.Length == 0) return double.NaN;
        var at = (sorted.Length - 1) * share;
        var low = (int)Math.Floor(at);
        var high = (int)Math.Ceiling(at);
        return sorted[low] + (sorted[high] - sorted[low]) * (at - low);
    }
}
