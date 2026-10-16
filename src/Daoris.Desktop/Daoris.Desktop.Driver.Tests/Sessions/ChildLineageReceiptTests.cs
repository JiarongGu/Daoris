using System.Text.RegularExpressions;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-C1a's proof, on what real harnesses wrote (2026-10-16, the probe kit with Daoris's own connector and this driver's
/// lineage hook: <c>tools/fabric-probe/launch.mjs --connector … --lineage-hook …</c>). Each fixture's lineage file is the one the
/// connector and the hook wrote, as they wrote it; each stream file is Claude Code's <c>task_*</c> lines off its
/// <c>stream-json</c>, cut to their ids (its other fields carry the subagents' prompts and an output path), which this driver
/// reads as <see cref="ChildLineage.Watch"/> does at run time. Claude Code 2.1.299 on <c>claude-sonnet-5-5</c>; Codex 0.160.0
/// on <c>gpt-6.1-sol</c>, effort low; D173's note has the receipts.
/// </summary>
/// <remarks>Files only: the suite's fast half.</remarks>
public sealed class ChildLineageReceiptTests
{
    private static readonly string Fixtures =
        Path.Combine(WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "fixtures", "lineage");

    private static IEnumerable<string> Lines(string name) => File.ReadAllLines(Path.Combine(Fixtures, name)).Where(line => line.Length > 0);

    /// <summary>The run's lineage file, with the stream's lines as the driver writes them beside it, and the part's end where given.</summary>
    private static ChildBindings Run(string name, bool? clean = null)
    {
        var agents = new HashSet<string>(StringComparer.Ordinal);
        var stream = File.Exists(Path.Combine(Fixtures, $"{name}.stream.jsonl"))
            ? Lines($"{name}.stream.jsonl").Select(line => ChildBindings.StreamLine(line, DateTimeOffset.UnixEpoch, agents)).OfType<string>()
            : [];
        var end = clean is { } ended ? [ChildBindings.EndLine(ended, DateTimeOffset.UnixEpoch)] : Array.Empty<string>();
        return ChildBindings.Fold([.. Lines($"{name}.lineage.jsonl"), .. stream, .. end]);
    }

    [Fact]
    public void Claude_code_s_real_child_grandchild_and_two_siblings_are_told_apart_and_bound_to_the_part()
    {
        var bound = Run("claude-family", clean: true);

        Assert.Equal(("claude-code", "1855c64c-14bc-48d0-9a56-bf2093b378e7"), (bound.Harness, bound.Root));
        Assert.Equal((1, 0, 0), (bound.RootCalls, bound.Unjoined, bound.UnknownCalls));
        Assert.Equal(
            [
                ("a9b1456decf181940", "1855c64c-14bc-48d0-9a56-bf2093b378e7", 1),
                ("af7e45b4d6f2967a9", "a9b1456decf181940", 2),
                ("a24ebe27d77bec19f", "1855c64c-14bc-48d0-9a56-bf2093b378e7", 1),
            ],
            bound.Children.Select(child => (child.Native, child.Parent!, child.Depth!.Value)));
        Assert.All(bound.Children, child =>
        {
            Assert.Equal(["hook", "call", "stream"], child.Provenance);
            Assert.Equal((ChildBindings.Connected, 1, "general-purpose"), (child.Connectivity, child.Calls, child.Type));
            // Each said its own stop, on the hook and on the stream.
            Assert.Equal(ChildBindings.Finished, child.State);
        });
    }

    [Fact]
    public void Codex_s_real_child_grandchild_and_two_siblings_are_told_apart_by_their_threads_and_end_with_the_part()
    {
        var running = Run("codex-family");
        var ended = Run("codex-family", clean: true);

        Assert.Equal(("codex", "01a142ba-a51f-79c1-9abe-a433884f6011", 1), (running.Harness, running.Root, running.RootCalls));
        Assert.Equal(
            [
                ("01a142ba-f33d-7ac3-b71a-8903bbd7707c", "01a142ba-a51f-79c1-9abe-a433884f6011", 1),
                ("01a142bb-1c2f-7163-b7de-ba3bdfd6c6cf", "01a142ba-f33d-7ac3-b71a-8903bbd7707c", 2),
                ("01a142bb-6a20-7093-a370-d13c1b017c2c", "01a142ba-a51f-79c1-9abe-a433884f6011", 1),
            ],
            running.Children.Select(child => (child.Native, child.Parent!, child.Depth!.Value)));
        Assert.All(running.Children, child =>
        {
            Assert.Equal(["call"], child.Provenance);
            Assert.Equal((ChildBindings.Connected, ChildBindings.Active), (child.Connectivity, child.State));
            Assert.Null(child.Type);
        });
        // Codex says nothing of a thread's end: each ends with the part, never "finished" (canary 9).
        Assert.All(ended.Children, child => Assert.Equal(ChildBindings.Ended, child.State));
    }

    [Fact]
    public void A_real_killed_root_leaves_its_running_child_lost()
    {
        var running = Run("claude-kill");
        var killed = Run("claude-kill", clean: false);

        var child = Assert.Single(running.Children);
        Assert.Equal(("a47dc441076104543", ChildBindings.Active, ChildBindings.Connected), (child.Native, child.State, child.Connectivity));
        Assert.True(killed.PartEnded);
        Assert.Equal(ChildBindings.Lost, Assert.Single(killed.Children).State);
    }

    /// <summary>What the connector and the hook wrote names no place on a disk: every value is an id, a moment or a flag.</summary>
    [Fact]
    public void What_the_harnesses_wrote_holds_ids_and_no_path()
    {
        var value = new Regex("\"[A-Za-z]+\":(?:\"(?<text>[^\"]*)\"|true|false|\\d+)", RegexOptions.CultureInvariant);
        foreach (var file in Directory.GetFiles(Fixtures, "*.lineage.jsonl"))
        {
            foreach (var line in Lines(Path.GetFileName(file)))
            {
                Assert.DoesNotContain("\\", line, StringComparison.Ordinal);
                foreach (Match pair in value.Matches(line))
                {
                    if (pair.Groups["text"].Success) Assert.Matches("^[A-Za-z0-9][A-Za-z0-9_.:-]*$", pair.Groups["text"].Value);
                }
            }
        }
    }
}
