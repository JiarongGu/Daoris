using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The doctrine tool's <c>status --json</c>, read by the desktop for an adopted repository, its
/// verdicts taken field by field (the update's direction and lists, the keeps it reopened, a newer lock) and never judged again here.
/// The answers are the shapes the tool prints over a fixture adopter.
/// </summary>
public sealed class DoctrineStatusTests
{
    private static string Answer(string update = "null", string kept = "[]", string newerLock = "null", bool synced = true) => $$"""
        {
          "harness": "Claude Code", "packs": ["core"], "synced": {{(synced ? "true" : "false")}}, "canonVersion": "0.0.1",
          "drifted": [], "kept": {{kept}}, "keepsDropped": [], "update": {{update}}, "newerLock": {{newerLock}}
        }
        """;

    private const string Differs = """
        {
          "available": "0.0.1", "locked": "0.0.1", "direction": "unknown",
          "digest": { "carried": "66fa", "locked": "0000" },
          "changed": ["rules/a.md", "rules/b.md"], "added": ["knowledge/autonomous-development.md"], "retired": [], "versionOnly": false, "notes": []
        }
        """;

    private const string Behind = """
        { "available": "0.0.2", "locked": "0.0.1", "direction": "newer", "changed": [], "added": [], "retired": ["rules/old.md"], "versionOnly": false, "notes": [] }
        """;

    private const string Reopened = """
        [
          { "target": "knowledge/claims-need-checks.md", "reason": "ours", "canon": "1111", "now": "122f", "reopened": true, "absentCore": false },
          { "target": "rules/kept.md", "reason": "ours too", "canon": "2222", "now": "2222", "reopened": false, "absentCore": false }
        ]
        """;

    [Fact]
    public void An_equal_version_over_another_canon_is_a_difference_with_its_counts_and_is_offered()
    {
        var status = DoctrineStatus.Parse(Answer(update: Differs))!;

        Assert.Equal(DoctrineUpdates.Differs, status.Update);
        Assert.Equal(("0.0.1", "0.0.1"), (status.Carried, status.Locked));
        Assert.Equal((2, 1, 0), (status.Changed, status.Added, status.Retired));
        Assert.True(status.Offers);
    }

    [Fact]
    public void An_older_lock_is_behind_and_is_offered()
    {
        var status = DoctrineStatus.Parse(Answer(update: Behind))!;

        Assert.Equal(DoctrineUpdates.Behind, status.Update);
        Assert.Equal(("0.0.2", "0.0.1"), (status.Carried, status.Locked));
        Assert.Equal((0, 0, 1), (status.Changed, status.Added, status.Retired));
        Assert.True(status.Offers);
    }

    [Fact]
    public void A_keep_the_canon_moved_past_is_offered_with_no_update()
    {
        var status = DoctrineStatus.Parse(Answer(kept: Reopened))!;

        Assert.Null(status.Update);
        Assert.Equal(1, status.Reopened);
        Assert.True(status.Offers);
    }

    [Fact]
    public void Nothing_moved_offers_nothing()
    {
        var status = DoctrineStatus.Parse(Answer())!;

        Assert.True(status.Synced);
        Assert.Null(status.Update);
        Assert.Equal(0, status.Reopened);
        Assert.False(status.Offers);
    }

    /// <summary>A lock a newer canon wrote is never rolled back, so nothing is offered, and its version is said.</summary>
    [Fact]
    public void A_lock_a_newer_canon_wrote_offers_nothing_and_names_its_version()
    {
        var status = DoctrineStatus.Parse(Answer(kept: Reopened, newerLock: """{ "locked": "9.9.9", "carried": "0.0.1", "run": "npx daoris@9.9.9" }"""))!;

        Assert.Equal("9.9.9", status.NewerLock);
        Assert.Equal("0.0.1", status.Carried);
        Assert.False(status.Offers);
    }

    [Fact]
    public void A_checkout_never_synced_offers_nothing()
    {
        var status = DoctrineStatus.Parse(Answer(synced: false))!;

        Assert.False(status.Synced);
        Assert.False(status.Offers);
    }

    [Theory]
    [InlineData("")]
    [InlineData("daoris: no daoris.json here")]
    [InlineData("[1, 2]")]
    [InlineData("{ \"update\": \"newer\" }")]
    public void An_answer_that_is_no_status_reads_as_none(string said) => Assert.Null(DoctrineStatus.Parse(said));
}
