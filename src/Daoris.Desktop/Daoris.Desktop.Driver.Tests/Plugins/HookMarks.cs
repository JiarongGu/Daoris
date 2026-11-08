using System.Diagnostics;
using System.Text.Json;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The mark a hook leaves when it runs, for the tests that hold an install starts nothing it declares. A mark read right after a
/// synchronous call proves nothing, since a process starts later and the script itself may never run: the script is run once here to prove
/// it writes its mark, and the mark's absence is then read after the time a start would have taken.
/// </summary>
internal static class HookMarks
{
    /// <summary>A hook's script that writes <paramref name="mark"/> when it runs, as an ES module (a <c>.mjs</c> has no <c>require</c>).</summary>
    public static string Script(string mark) =>
        $"import {{ writeFileSync }} from 'node:fs';\nwriteFileSync({JsonSerializer.Serialize(mark)}, 'ran');\n";

    /// <summary>Runs <paramref name="script"/> once and holds that it wrote <paramref name="mark"/>, then clears it: the absence read later can then mean something.</summary>
    public static void Prove(string script, string mark)
    {
        using var node = Process.Start(new ProcessStartInfo("node", ["--no-warnings", script]) { CreateNoWindow = true, UseShellExecute = false })
            ?? throw new InvalidOperationException("node did not start");
        Assert.True(node.WaitForExit(30_000), "the control run of the hook's script did not end");
        Assert.True(File.Exists(mark), "the hook's script, run, writes no mark, so its absence would prove nothing");
        File.Delete(mark);
    }

    /// <summary>Holds that nothing wrote <paramref name="mark"/> for a second, which is long enough for a started node to have.</summary>
    public static void AssertNone(string mark)
    {
        for (var wait = 0; wait < 20 && !File.Exists(mark); wait++) Thread.Sleep(50);
        Assert.False(File.Exists(mark), "a hook the install declared ran");
    }
}
