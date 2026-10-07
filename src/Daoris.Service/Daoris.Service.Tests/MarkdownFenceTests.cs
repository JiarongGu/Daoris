using System.Text.Json;
using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// Which lines a fence holds, as the service reads one (ORIENT2h3, <see cref="MarkdownFence"/>), held to the table every
/// fence reader here is held to.
/// </summary>
/// <remarks>
/// The service's half of a TWIN (ORIENT2h6b, <c>.claude/knowledge/twins.md</c>'s fence row) with the CLI's
/// <c>markdownFence</c>, the tools' <c>fenced</c> and the driver's <c>SelfDescription.Fence</c>: each reads a fence with
/// code of its own, and all four read the CLI's <c>test/fixtures/fence-cases.json</c>, row for row, each case's lines and
/// the lines a fence holds, from 1, its opening and closing lines included. Before ORIENT2h6b the service was held to the
/// others only through the digest's note table, which asks a fence's question by way of a decision's notes.
/// </remarks>
public sealed class MarkdownFenceTests
{
    [Fact]
    public void Each_line_is_fenced_as_the_shared_table_reads_it()
    {
        using var table = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(Root(), "src", "Daoris.Cli", "test", "fixtures", "fence-cases.json")));
        var cases = table.RootElement.GetProperty("cases").EnumerateArray().ToList();

        foreach (var row in cases)
        {
            var why = row.GetProperty("why").GetString();
            var fence = new MarkdownFence();
            var held = row.GetProperty("lines").EnumerateArray()
                .Select((line, at) => (Held: fence.Holds(line.GetString()!), Line: at + 1))
                .Where(line => line.Held).Select(line => line.Line).ToList();
            var expected = row.GetProperty("fenced").EnumerateArray().Select(line => line.GetInt32()).ToList();
            Assert.True(expected.SequenceEqual(held), $"{why}: {string.Join(", ", held)}");
        }

        // A table that lost its cases, or every case of one kind, would hold nothing.
        Assert.Contains(cases, row => row.GetProperty("fenced").GetArrayLength() == 0);
        Assert.Contains(cases, row => row.GetProperty("fenced").GetArrayLength() > 0);
    }

    /// <summary>The workspace root, found by walking up from the test binaries to <c>daoris.json</c>.</summary>
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "daoris.json"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("no workspace root above the test binaries");
    }
}
