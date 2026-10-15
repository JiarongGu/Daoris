using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A count of the git the fixtures start themselves (PROC1a), off unless <see cref="Switch"/> names a file.
/// </summary>
/// <remarks>
/// The Process half's time is mostly git starts, and a test's starts are the fixture's and the product's together. Set to a
/// path, each start through <see cref="GitFixture"/> or <see cref="LandedFixture"/> appends one tab-separated line there:
/// when it began (UTC), how long <c>Process.Start</c> took, how long it ran to its exit, its runner and its arguments. The
/// product's own starts are then a git trace's starts less these (<c>GIT_TRACE2_EVENT</c> on the same run). Unset, nothing
/// is written. What it measured is in FLAKE1's FIX-LOG entry (PROC1a).
/// </remarks>
internal static class FixtureSpawns
{
    /// <summary>The variable naming the file the lines go to.</summary>
    internal const string Switch = "DAORIS_TEST_GIT_SPAWNS";

    private static readonly string? Target = Environment.GetEnvironmentVariable(Switch) is { Length: > 0 } path ? path : null;
    private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    private static readonly Lock Gate = new();

    /// <summary>Whether starts are being counted.</summary>
    internal static bool On => Target is not null;

    /// <summary>One start's line: <paramref name="began"/> is a <see cref="Stopwatch"/> timestamp taken before the start.</summary>
    internal static void Note(string runner, DateTime beganUtc, long began, long started, IEnumerable<string> arguments)
    {
        if (Target is null) return;
        var ended = Stopwatch.GetTimestamp();
        var line = string.Join('\t',
            beganUtc.ToString("o", CultureInfo.InvariantCulture),
            Stopwatch.GetElapsedTime(began, started).TotalMilliseconds.ToString("F1", CultureInfo.InvariantCulture),
            Stopwatch.GetElapsedTime(began, ended).TotalMilliseconds.ToString("F1", CultureInfo.InvariantCulture),
            runner,
            string.Join(' ', arguments)) + "\n";
        lock (Gate) File.AppendAllText(Target, line, Utf8);
    }
}
