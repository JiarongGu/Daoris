using Daoris.Knowledge;

namespace Daoris.Service.Tests;

public sealed class QuestScanTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "daoris-requests-" + Guid.NewGuid().ToString("N")[..8]);

    private void Write(string relative, string content)
    {
        var file = Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, content);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private IReadOnlyList<KnowledgeEntry> Quests() =>
        new RepositoryScanner().Scan(_root).Where(e => e.Kind == EntryKind.Quest).ToList();

    private const string Heading = "## Quests from other repositories";

    /// <summary>
    /// The only entry that describes what a repository OWES rather than what it knows. Buried in one
    /// backlog it is visible to one repository; the question worth asking is family-wide.
    /// </summary>
    [Fact]
    public void An_open_quest_is_indexed_with_its_title_and_body()
    {
        Write("TASKS.md",
            $"# TASKS\n\n{Heading}\n\n- [ ] **Adopt the canon**\n  Four rules collide.\n\n  _Requested by `Asker` on 2026-08-05._\n");

        var quest = Assert.Single(Quests());

        Assert.Equal("Adopt the canon", quest.Title);
        Assert.Contains("Four rules collide", quest.Body);
        Assert.Contains("Asker", quest.Body);
    }

    /// <summary>What is worth surfacing is what is still outstanding; a done request is the repo's history.</summary>
    [Fact]
    public void A_completed_quest_is_not_indexed()
    {
        Write("TASKS.md", $"# TASKS\n\n{Heading}\n\n- [x] **Already done**\n  finished\n");

        Assert.Empty(Quests());
    }

    [Fact]
    public void Several_quests_are_separate_entries()
    {
        Write("TASKS.md",
            $"# TASKS\n\n{Heading}\n\n- [ ] **First**\n  one\n\n- [ ] **Second**\n  two\n");

        Assert.Equal(["First", "Second"], Quests().Select(r => r.Title));
    }

    /// <summary>The section ends where the next heading begins; a repo's own work is not an obligation.</summary>
    [Fact]
    public void Tasks_under_a_later_heading_are_not_quests()
    {
        Write("TASKS.md",
            $"# TASKS\n\n{Heading}\n\n- [ ] **A quest**\n  theirs\n\n## Our own work\n\n- [ ] **Not a quest**\n  ours\n");

        var quest = Assert.Single(Quests());
        Assert.Equal("A quest", quest.Title);
    }

    [Fact]
    public void A_backlog_with_no_quests_section_yields_none()
    {
        Write("TASKS.md", "# TASKS\n\n## Backlog\n\n- [ ] **Ours**\n  ours\n");

        Assert.Empty(Quests());
    }

    /// <summary>
    /// A quest is this repository's own obligation, never doctrine — so it can never be mistaken for
    /// canonical content however the lock happens to read.
    /// </summary>
    [Fact]
    public void Quests_are_always_local_provenance()
    {
        Write("TASKS.md", $"# TASKS\n\n{Heading}\n\n- [ ] **Something**\n  body\n");

        Assert.Equal(Provenance.Local, Assert.Single(Quests()).Provenance);
    }
}
