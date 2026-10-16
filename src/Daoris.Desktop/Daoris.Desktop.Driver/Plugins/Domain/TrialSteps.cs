using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>What a trial is asked to do beyond its defaults.</summary>
/// <param name="Point">One point to try; absent, every point the manifest declares.</param>
/// <param name="Frame">The frame to send at <paramref name="Point"/> instead of the sample — the person's, root and all.</param>
/// <param name="Patience">
/// How long each point's call waits; absent, as long as the driver waits at the points tried. The handshake always waits
/// that long, as the driver's does (PLUGKIT2).
/// </param>
public sealed record TrialOptions(string? Point = null, JsonObject? Frame = null, TimeSpan? Patience = null);

/// <summary>
/// One thing a trial checked: a declared tool or one of its checks (PLUGTOOL1a), the handshake, a point, the shutdown, stdout —
/// or the manifest, when that is as far as it got.
/// </summary>
public sealed record TrialStep(string Name, bool Ok, string Sentence);

/// <summary>What `plugins try` found, step by step, and what the plugin said on stderr meanwhile.</summary>
public sealed record PluginTrial(
    string Plugin, string Folder, IReadOnlyList<string> Command, IReadOnlyList<TrialStep> Steps, IReadOnlyList<string> Said)
{
    public bool Passed => Steps.Count > 0 && Steps.All(step => step.Ok);

    /// <summary>The family contract: 0 when every answer is one the driver reads, 1 when the plugin failed a check.</summary>
    public int ExitCode => Passed ? 0 : 1;

    public string Summary => Passed
        ? $"`{Plugin}` answered as the driver reads it."
        : $"`{Plugin}` failed {Steps.Count(step => !step.Ok)} of {Steps.Count} check{(Steps.Count == 1 ? "" : "s")}.";
}
