using System.Text;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A failed gate hands the work the failing step's own lines, not only its exit: each red block of its log (an <c>== RED</c> line and its
/// lines up to the next <c>== </c>) and the log's last lines, within about 40 lines and 4 KB, with the log's path.
/// </summary>
public sealed class GateLogExcerptTests
{
    private static readonly string Home = System.IO.Path.Combine(TestHomes.Root, "gate-log-excerpt");

    private static readonly string Log = System.IO.Path.Combine(VerifyFiles.AttemptFolder(Home, "q", "i-1"), "gate-01-cli.log");

    private static readonly string Path = System.IO.Path.GetRelativePath(Home, Log).Replace('\\', '/');

    // Assembled at run time: the sensitive gate scans this file, and a literal token shape in it would be refused as one.
    private static string Token() => string.Concat("ghp", "_", new string('a', 30), "0123");

    [Fact]
    public void A_line_holding_a_token_pattern_is_withheld_and_the_lines_around_it_are_told()
    {
        var log = string.Join("\n",
        [
            "== RED node tools/push.mjs (0.4s)",
            "remote: token " + Token() + " was refused",
            "the line after it",
            "> Authorization: Basic dXNlcjpzM2NyZXQ=",
            string.Concat("-----BEGIN ", "OPENSSH PRIVATE KEY-----"),
            "b3BlbnNzaC1rZXktdjEAAAAABG5vbmU",
            string.Concat("-----END ", "OPENSSH PRIVATE KEY-----"),
            "the last line",
        ]);

        var told = GateLogExcerpt.Told(log, Log, Home)!;

        Assert.DoesNotContain(Token(), told);
        Assert.DoesNotContain("was refused", told);
        Assert.DoesNotContain("dXNlcjpzM2NyZXQ", told);
        Assert.DoesNotContain("b3BlbnNzaC1rZXktdjEAAAAABG5vbmU", told);
        Assert.DoesNotContain(GitWords.Cut, told);
        Assert.Contains(GateLogExcerpt.Withheld, told);
        Assert.Contains("== RED node tools/push.mjs (0.4s)\n" + GateLogExcerpt.Withheld + "\nthe line after it", told);
        Assert.EndsWith("the last line", told);
    }

    [Fact]
    public void The_logs_path_is_said_relative_to_the_home_and_a_path_outside_it_by_its_file_name()
    {
        var inside = GateLogExcerpt.Told("a line", Log, Home)!;
        var outside = GateLogExcerpt.Told("a line", System.IO.Path.Combine(TestHomes.Root, "elsewhere", "gate-01-cli.log"), Home)!;

        Assert.StartsWith($"its log, {Path}, says:", inside);
        Assert.DoesNotContain(Home, inside);
        Assert.StartsWith("its log, gate-01-cli.log, says:", outside);
        Assert.StartsWith("its output says:", GateLogExcerpt.Told("a line", null, Home));
    }

    [Fact]
    public void A_line_that_only_begins_with_the_red_words_is_no_red_block()
    {
        var lines = new List<string> { "== REDUCED to a header that is no step's" };
        for (var at = 0; at < 100; at++) lines.Add($"filler {at}");
        lines.Insert(1, "its own first line");

        var told = GateLogExcerpt.Told(string.Join("\n", lines), Log, Home)!;

        Assert.DoesNotContain("REDUCED", told);
        Assert.Contains("filler 99", told);
    }

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

        var told = GateLogExcerpt.Told(log, Log, Home)!;

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

        var told = GateLogExcerpt.Told(string.Join("\n", lines), Log, Home)!;

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

        var told = GateLogExcerpt.Told(log, Log, Home)!;

        Assert.Contains("line 99", told);
        Assert.DoesNotContain("line 10\n", told);
        Assert.Null(GateLogExcerpt.Told("\r\n\n", Log, Home));
    }
}
