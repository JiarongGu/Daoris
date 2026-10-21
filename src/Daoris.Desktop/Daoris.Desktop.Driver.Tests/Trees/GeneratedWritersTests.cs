using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// GENREGEN1 (D87's note): <c>daoris.gates.json</c>'s <c>generated</c> read as a landing's replay reads it, and what the landing says of
/// the generated files it took, written again or not. The replay itself is <c>LandRebaseLandingTests</c>' (Process).
/// </summary>
public sealed class GeneratedWritersTests
{
    [Fact]
    public void Each_entry_with_a_folder_and_a_command_is_a_writer_and_anything_else_is_passed_over()
    {
        var writers = GeneratedWriters.Parse("""
            { "gates": [], "generated": [
              { "paths": "docs/index/", "run": "node tools/orient-index.mjs" },
              { "paths": "out" },
              { "run": "node write.mjs" },
              "docs/other"
            ] }
            """);

        var writer = Assert.Single(writers);
        Assert.Equal(new GeneratedWriters.Writer("docs/index", "node tools/orient-index.mjs"), writer);
        Assert.True(writer.Holds("docs/index/routes.md"));
        Assert.False(writer.Holds("docs/index.md"));
        Assert.Empty(GeneratedWriters.Parse("{ \"gates\": [] }"));
        Assert.Empty(GeneratedWriters.Parse("{ not json"));
    }

    [Fact]
    public void The_landing_says_the_files_were_written_again_by_their_command_or_why_not()
    {
        IReadOnlyList<string> taken = ["docs/index/routes.md"];

        Assert.Equal(
            " 1 generated file(s) the line changed too took the line's copy (docs/index/routes.md), to be written again where the work is merged.",
            LandingSyncWords.Generated(taken));
        Assert.Equal(
            " 1 generated file(s) the line changed too took the line's copy (docs/index/routes.md), then were written again from the replayed "
            + "tree by `node tools/orient-index.mjs`.",
            LandingSyncWords.Generated(taken, ["node tools/orient-index.mjs"]));
        Assert.Equal(
            " 1 generated file(s) the line changed too took the line's copy (docs/index/routes.md), to be written again where the work is "
            + "merged: writing them here failed (`node tools/orient-index.mjs` ended 1: boom).",
            LandingSyncWords.Generated(taken, ["node tools/orient-index.mjs"], "`node tools/orient-index.mjs` ended 1: boom"));
        Assert.Equal("", LandingSyncWords.Generated([], ["node tools/orient-index.mjs"]));
    }
}
