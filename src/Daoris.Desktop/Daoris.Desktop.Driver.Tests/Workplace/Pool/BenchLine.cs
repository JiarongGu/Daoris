namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What one step wrote, as <see cref="TreeWrites"/> estimates it: the files new or changed at their sizes (never the bytes the disk
/// wrote), the files gone, and the seconds it took on the machine as loaded as it was.
/// </summary>
internal readonly record struct BenchStep(int Files, long Bytes, int Gone, double Seconds)
{
    public static BenchStep operator +(BenchStep one, BenchStep other) =>
        new(one.Files + other.Files, one.Bytes + other.Bytes, one.Gone + other.Gone, one.Seconds + other.Seconds);

    internal static BenchStep Of((int Written, long Bytes, int Deleted) wrote, double seconds) => new(wrote.Written, wrote.Bytes, wrote.Deleted, seconds);

    public override string ToString() => $"{Files} files {Mb(Bytes)} MB, {Gone} gone, {Seconds:F0} s";

    internal static double Mb(long bytes) => Math.Round(bytes / 1048576.0, 1);
}

/// <summary>
/// One line of a bench, its steps apart: a slot's take or a fresh tree's open, the files stage, the line's build and a fresh tree's
/// removal; and the mean of a run's lines, step by step and in all, which the design's saving is read from.
/// </summary>
internal sealed record BenchLine(string Name, BenchStep First, BenchStep Stage, BenchStep Build, BenchStep Removal = default)
{
    internal BenchStep Total => First + Stage + Build + Removal;

    public override string ToString() => $"{Name}: first {First}; stage {Stage}; build {Build}; removal {Removal}; in all {Total}";

    internal static string Means(IReadOnlyList<BenchLine> lines)
    {
        if (lines.Count == 0) return "no lines";
        string Mean(Func<BenchLine, BenchStep> step) =>
            $"{lines.Average(each => step(each).Files):F0} files {BenchStep.Mb((long)lines.Average(each => step(each).Bytes))} MB, "
            + $"{lines.Average(each => step(each).Gone):F0} gone, {lines.Average(each => step(each).Seconds):F0} s";
        return $"mean of {lines.Count}: first {Mean(each => each.First)}; stage {Mean(each => each.Stage)}; build {Mean(each => each.Build)}; "
            + $"removal {Mean(each => each.Removal)}; in all {Mean(each => each.Total)}";
    }
}
