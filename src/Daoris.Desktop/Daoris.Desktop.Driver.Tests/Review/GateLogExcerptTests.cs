using System.Text;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A failed gate hands the work the failing step's own lines, not only its exit: each red block of its log (an <c>== RED</c> line and its
/// lines up to the next <c>== </c>) and the log's last lines, within about 40 lines and 4 KB, with the log's path.
/// </summary>
public sealed class GateLogExcerptTests
{
    private const string Path = "data/workflows/lines/q/attempts/i-1/gate-01-cli.log";

    [Fact]
    public void A_red_block_and_the_last_lines_are_told_with_the_logs_path()
    {
        var log = string.Join("\n",
        [
            "verify:checks: 3 of 3 steps, every step: no changed paths were handed",
            "== ok  node tools/a.mjs (0.1s)",
            "a is fine",
            "== RED node tools/contracts.mjs --check (0.4s)",
            "src/Daoris.Web/src/data/generated/service.ts is stale: the contracts changed since it was written",
            "== ok  node tools/b.mjs (0.1s)",
            "verify:checks: 3 steps in 1.0s, 1 red",
            "  RED node tools/contracts.mjs --check (exit 1)",
            "",
        ]);

        var told = GateLogExcerpt.Told(log, Path)!;

        Assert.Contains(Path, told);
        Assert.Contains("== RED node tools/contracts.mjs --check (0.4s)\nsrc/Daoris.Web/src/data/generated/service.ts is stale", told);
        Assert.Contains("  RED node tools/contracts.mjs --check (exit 1)", told);
    }

    [Fact]
    public void A_long_log_is_bounded_keeping_the_red_blocks_head_and_the_logs_end()
    {
        var lines = new List<string>();
        for (var at = 0; at < 300; at++) lines.Add($"== ok  step {at} (0.1s)");
        lines.Add("== RED node tools/broken.mjs (2.0s)");
        lines.Add("broken: the first line says why");
        for (var at = 0; at < 200; at++) lines.Add($"    at frame {at} {new string('x', 60)}");
        lines.Add("== ok  node tools/after.mjs (0.1s)");
        for (var at = 0; at < 100; at++) lines.Add($"after {at}");
        lines.Add("the last line");

        var told = GateLogExcerpt.Told(string.Join("\n", lines), Path)!;

        Assert.True(Encoding.UTF8.GetByteCount(told) <= GateLogExcerpt.Bytes + 512, $"{Encoding.UTF8.GetByteCount(told)} bytes");
        Assert.True(told.Split('\n').Length <= GateLogExcerpt.Lines + 8, $"{told.Split('\n').Length} lines");
        Assert.Contains("== RED node tools/broken.mjs (2.0s)\nbroken: the first line says why", told);
        Assert.EndsWith("the last line", told);
        Assert.DoesNotContain("== ok  step 5 ", told);
        Assert.DoesNotContain("at frame 199", told);
    }

    [Fact]
    public void A_log_without_a_red_block_tells_its_last_lines_and_an_empty_one_tells_nothing()
    {
        var log = string.Join("\n", Enumerable.Range(0, 100).Select(at => $"line {at}"));

        var told = GateLogExcerpt.Told(log, Path)!;

        Assert.Contains("line 99", told);
        Assert.DoesNotContain("line 10\n", told);
        Assert.Null(GateLogExcerpt.Told("\r\n\n", Path));
    }
}
