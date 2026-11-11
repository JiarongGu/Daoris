using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The look's tidy of a folder department's copies: one whose run ended with nothing returning it is moved aside, its sidecar gone, and
/// said in the tidy's words; one whose run goes on, whose return waits on the person, that a session works in, or that names no run, stays.
/// </summary>
public sealed class CopyTidyTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 11, 9, 0, 0, TimeSpan.Zero);
    private readonly string _scratch = ConformanceScratch.New("copy-tidy");
    private string Home => Path.Combine(_scratch, "home");
    private string Place => Path.Combine(_scratch, "reports");
    private NodeRecordFiles Records => new(Home, () => true);

    public void Dispose() => ConformanceScratch.Remove(_scratch);

    private FolderCopy Copy(string? run = "q1")
    {
        Directory.CreateDirectory(Place);
        File.WriteAllText(Path.Combine(Place, "notes.md"), "first\n");
        var copy = FolderCopies.Make(Home, "default", "reports", Place, ManifestBounds.Default).Copy!;
        FolderCopies.Owned(Home, copy.Folder, "rec-1", run);
        return copy;
    }

    private void Ended(string run, string? waits = null) => Records.Write(new NodeRecord(RunUnits.IdOf(run))
    {
        Ended = new RunEnd(run, Now),
        Statuses = waits is null
            ? new Dictionary<string, NodeStatus>()
            : new Dictionary<string, NodeStatus> { ["back"] = new(RunUnits.IdOf(run), "back", NodeStates.Active) { Code = waits, Holds = "notes.md" } },
    });

    [Fact]
    public void A_copy_whose_run_ended_with_nothing_returning_it_is_moved_aside_and_said()
    {
        var copy = Copy();
        Ended("q1");

        var tidied = Assert.Single(CopyTidy.Tidy(Home, new HashSet<string>(), Now));

        Assert.False(Directory.Exists(copy.Folder));
        Assert.False(File.Exists(copy.Folder + ".json"));
        Assert.True(File.Exists(Path.Combine(tidied.MovedTo!, "notes.md")));
        Assert.Contains("its run ended and nothing returns it", CopyTidy.Said(tidied, 14), StringComparison.Ordinal);
        Assert.StartsWith("tidy  reports: ", CopyTidy.Said(tidied, 14), StringComparison.Ordinal);
    }

    [Fact]
    public void A_copy_whose_run_goes_on_is_kept()
    {
        var copy = Copy();
        Records.Write(new NodeRecord(RunUnits.IdOf("q1")));

        Assert.Empty(CopyTidy.Tidy(Home, new HashSet<string>(), Now));
        Assert.True(Directory.Exists(copy.Folder));
    }

    [Fact]
    public void A_copy_whose_return_waits_on_the_person_is_kept()
    {
        var copy = Copy();
        Ended("q1", ReturnNode.BothChanged);

        Assert.Empty(CopyTidy.Tidy(Home, new HashSet<string>(), Now));
        Assert.True(Directory.Exists(copy.Folder));
    }

    [Fact]
    public void A_copy_a_session_works_in_or_that_names_no_run_is_kept()
    {
        var copy = Copy();
        Ended("q1");
        Assert.Empty(CopyTidy.Tidy(Home, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { copy.Folder }, Now));
        Assert.True(Directory.Exists(copy.Folder));

        var unnamed = Copy(run: null);
        Assert.DoesNotContain(CopyTidy.Tidy(Home, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { copy.Folder }, Now), each => each.Copy.Id == unnamed.Id);
        Assert.True(Directory.Exists(unnamed.Folder));
    }
}
