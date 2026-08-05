using Daoris.Devkit;

namespace Daoris.Devkit.Tests;

public sealed class HistoryScanTests : IDisposable
{
    private readonly Fixture _fx = new("history");

    public void Dispose() => _fx.Dispose();

    private sealed class FakeGit : IGit
    {
        public IReadOnlyList<string> StagedFiles() => [];

        public IReadOnlyList<string> TrackedFiles() => [];
    }

    private sealed class FakeObjects(params HistoryItem[] items) : IGitObjects
    {
        public IEnumerable<HistoryItem> Everything() => items;
    }

    private GateContext Context() => new(_fx.Path, new GateDeclaration());

    private GateResult Scan(params HistoryItem[] items)
    {
        _fx.Write("local/sensitive-patterns.txt", "AcmeSecretProject");
        return new SensitiveGate(ScanScope.History, new FakeGit(), objects: new FakeObjects(items)).Run(Context());
    }

    /// <summary>
    /// The whole reason the mode exists. A leak deleted from the working tree is still in the objects,
    /// and after a push there are copies you no longer control — so a clean tree proves nothing about
    /// what is about to become public.
    /// </summary>
    [Fact]
    public void A_leak_that_survives_only_in_an_old_object_is_found()
    {
        var result = Scan(new HistoryItem("blob abc1234", "notes on the AcmeSecretProject migration"));

        Assert.False(result.Passed);
        Assert.Contains("abc1234", result.Detail);
    }

    /// <summary>Deleting a file does not delete the name it had, and a name can be the leak by itself.</summary>
    [Fact]
    public void A_path_that_only_ever_existed_in_history_is_found()
    {
        var result = Scan(new HistoryItem("docs/AcmeSecretProject.md (path, historical)", "docs/AcmeSecretProject.md"));

        Assert.False(result.Passed);
        Assert.Contains("path, historical", result.Detail);
    }

    [Fact]
    public void A_commit_message_in_history_is_found()
    {
        var result = Scan(new HistoryItem("commit def5678 (message)", "fix: port the AcmeSecretProject adapter"));

        Assert.False(result.Passed);
        Assert.Contains("def5678", result.Detail);
    }

    [Fact]
    public void Clean_history_passes_and_says_how_much_it_looked_at()
    {
        var result = Scan(
            new HistoryItem("blob aaa", "ordinary content"),
            new HistoryItem("README.md (path, historical)", "README.md"));

        Assert.True(result.Passed, result.Detail);
        Assert.Contains("2 objects and paths", result.Detail);
    }

    /// <summary>
    /// A leak that survived a hundred commits produces a hundred findings that are all the same leak,
    /// and printing every one buries the second distinct problem. Truncating SILENTLY would be worse —
    /// a report that stops without saying so reads as "that was all of it".
    /// </summary>
    [Fact]
    public void Many_findings_are_capped_and_the_cap_is_stated()
    {
        var items = Enumerable.Range(0, 60)
            .Select(i => new HistoryItem($"blob {i:0000000}", "the AcmeSecretProject again"))
            .ToArray();

        var result = Scan(items);

        Assert.False(result.Passed);
        Assert.Contains("more finding(s), not shown", result.Detail);
        Assert.Equal(41, result.Detail.Split('\n').Length);
    }

    /// <summary>
    /// A reviewed object is one someone read and judged benign — most often a test fixture that
    /// deliberately contains the shape the scanner hunts for, which is exactly what happened to this
    /// repository on the devkit's own first history audit.
    /// </summary>
    [Fact]
    public void A_reviewed_object_is_not_reported_again()
    {
        _fx.Write("local/sensitive-patterns.txt", "AcmeSecretProject");
        var declaration = new GateDeclaration
        {
            Sensitive = new SensitiveOptions(ReviewedObjects: ["04801cb"]),
        };

        var gate = new SensitiveGate(ScanScope.History, new FakeGit(), objects: new FakeObjects(
            new HistoryItem("blob 04801cb", "the AcmeSecretProject fixture", "04801cb5bfb95ad3d83e72649c47bcad")));

        Assert.True(gate.Run(new GateContext(_fx.Path, declaration)).Passed);
    }

    /// <summary>
    /// The safety property. Acknowledging one object must not acknowledge the next leak — and it cannot,
    /// because a different object has a different hash. This is what makes the mechanism safe where a
    /// path-based ignore-list would not be.
    /// </summary>
    [Fact]
    public void Acknowledging_one_object_does_not_cover_a_different_one()
    {
        _fx.Write("local/sensitive-patterns.txt", "AcmeSecretProject");
        var declaration = new GateDeclaration
        {
            Sensitive = new SensitiveOptions(ReviewedObjects: ["04801cb"]),
        };

        var gate = new SensitiveGate(ScanScope.History, new FakeGit(), objects: new FakeObjects(
            new HistoryItem("blob 04801cb", "reviewed AcmeSecretProject fixture", "04801cb5bfb95ad3d83e72649c47bcad"),
            new HistoryItem("blob bbbbbbb", "a NEW AcmeSecretProject leak", "bbbbbbb2222222222222222222222222")));

        var result = gate.Run(new GateContext(_fx.Path, declaration));

        Assert.False(result.Passed);
        Assert.Contains("bbbbbbb", result.Detail);
        Assert.DoesNotContain("04801cb", result.Detail);
    }

    /// <summary>
    /// An acknowledgement is scoped to the audit. If it silenced the working tree too, it would be an
    /// ignore-list — and the next secret written into that file would be silent as well.
    /// </summary>
    [Fact]
    public void A_reviewed_object_does_not_silence_the_working_tree()
    {
        _fx.Write("local/sensitive-patterns.txt", "AcmeSecretProject");
        _fx.Write("notes.md", "the AcmeSecretProject again");
        var declaration = new GateDeclaration
        {
            Sensitive = new SensitiveOptions(ReviewedObjects: ["04801cb"]),
        };

        var gate = new SensitiveGate(ScanScope.Tree, new TrackingGit("notes.md"));

        Assert.False(gate.Run(new GateContext(_fx.Path, declaration)).Passed);
    }

    private sealed class TrackingGit(params string[] files) : IGit
    {
        public IReadOnlyList<string> StagedFiles() => files;

        public IReadOnlyList<string> TrackedFiles() => files;
    }

    /// <summary>
    /// `--history` without a source is a wiring mistake, and it must not look like a clean audit. This
    /// is the failure mode that matters most: the mode whose entire job is to be trusted before a push
    /// reporting "nothing found" because it scanned nothing.
    /// </summary>
    [Fact]
    public void History_without_an_object_source_throws_rather_than_reporting_clean()
    {
        _fx.Write("local/sensitive-patterns.txt", "AcmeSecretProject");

        var gate = new SensitiveGate(ScanScope.History, new FakeGit());

        Assert.Throws<DevkitException>(() => gate.Run(Context()));
    }
}
