using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// The machine's own file watchers over a real folder: a document, a ref, a room's instructions and a folder made later are
/// heard and marked, and what git ignores is not. The real watcher is what these prove, and it delivers on its own time, so
/// they wait for it and are Process.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class CheckoutWatchTests : FreshnessCheckout
{
    [Fact]
    public async Task The_machines_watchers_hear_a_document_and_a_ref_and_the_next_search_reads_them()
    {
        var checkout = Checkout();
        using var freshness = new CheckoutFreshness(checkout, documents: "docs", index: "docs/index");
        Assert.True(freshness.Watching);
        var service = Serve(checkout, freshness, TimeProvider.System);
        Assert.False(await Finds(service));

        Write("served/docs/later.md", "# Zeppelins\n\nWritten while the session ran.\n");
        await Heard(freshness);
        Assert.True(await Finds(service));

        // A ref moved by a commit or a switch: no document changed, and the reading is still marked.
        // (The write's own later events are let in and taken, so the next hearing is the ref's alone.)
        await Task.Delay(500);
        freshness.Take();
        Write("served/.git/refs/heads/main", "1111111111111111111111111111111111111111\n");
        await Heard(freshness);
    }

    [Fact]
    public async Task A_checkout_that_is_a_worktree_is_watched_at_the_refs_its_git_file_names()
    {
        var checkout = Checkout();
        Directory.Delete(Path.Combine(checkout, ".git"), recursive: true);
        Write("common/HEAD", "ref: refs/heads/main\n");
        Write("common/refs/heads/main", "0000000000000000000000000000000000000000\n");
        Write("common/worktrees/served/HEAD", "ref: refs/heads/side\n");
        Write("common/worktrees/served/commondir", "../..\n");
        Write("served/.git", "gitdir: " + Path.Combine(_root, "common", "worktrees", "served") + "\n");
        using var freshness = new CheckoutFreshness(checkout, documents: "docs", index: null);

        Write("common/refs/heads/main", "2222222222222222222222222222222222222222\n");
        await Heard(freshness);
    }

    [Fact]
    public async Task A_write_in_a_folder_git_ignores_inside_a_watched_one_is_not_heard()
    {
        // `.claude/` is watched for the knowledge in it, and `.claude/worktrees/` beneath it holds every agent's
        // checkout, written by each build and each install; none of it is this checkout's knowledge.
        var checkout = Path.Combine(_root, "served");
        Write("served/.gitignore", ".claude/worktrees/\n");
        Write("served/.claude/knowledge/twins.md", "# Twins\n");
        Write("served/.claude/worktrees/side/AGENTS.md", "# A worktree's brief\n");
        Write("served/docs/design.md", "# The probe lock\n");
        GitCheckouts.Init(checkout);
        using var freshness = new CheckoutFreshness(checkout, documents: "docs", index: null);
        Assert.True(freshness.Watching);
        await Settled(freshness);

        Write("served/.claude/worktrees/side/AGENTS.md", "# A worktree's brief, changed on its branch\n");
        Write("served/.claude/worktrees/side/src/bin/Daoris.dll", "built");
        Write("served/.claude/worktrees/other/AGENTS.md", "# Another agent's checkout\n");
        await Task.Delay(1500);
        Assert.False(freshness.IsStale, "a write under .claude/worktrees/, which git ignores, marked the reading stale");

        Write("served/.claude/knowledge/twins.md", "# Twins, changed\n");
        await Heard(freshness);
    }

    [Fact]
    public async Task A_checkouts_ignored_folders_at_its_top_are_neither_read_nor_watched()
    {
        // The deployment rehearsal runs under `local/gate-tree/`, and its swap renames the install's `app/` there,
        // refused while any handle is open inside it. What reads and watches a checkout opens nothing under `local/`,
        // `node_modules/` or `_fixtures/`, and hears nothing written there.
        var checkout = Path.Combine(_root, "served");
        const string app = "served/local/gate-tree/_fixtures/deployment-rehearsal/install/app";
        Write("served/.gitignore", "local/\nnode_modules/\n_fixtures/\n");
        Write("served/docs/design.md", "# The probe lock\n\n## Why a lock\n\nOne probe at a time.\n");
        Write("served/docs/index/README.md", "# Where things are\n\n| Where | What |\n|---|---|\n| `src/Probe.cs` | the probe lock |\n");
        var held = new[]
        {
            Write($"{app}/cli/canon/README.md", "# Zeppelins\n\n## Zeppelins\n\nAn install's canon.\n"),
            Write("served/node_modules/pkg/README.md", "# Zeppelins\n\n## Zeppelins\n\nA package.\n"),
            Write("served/_fixtures/probe/notes.md", "# Zeppelins\n\n## Zeppelins\n\nScratch.\n"),
        };
        GitCheckouts.Init(checkout);
        using var freshness = new CheckoutFreshness(checkout, documents: "docs", index: "docs/index");
        Assert.True(freshness.Watching);

        // Held with no sharing, as a swap holds what it moves: a read that opened one would fail on it.
        var holds = held.Select(file => new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None)).ToList();
        try
        {
            var entries = new RepositoryScanner(documents: "docs", index: "docs/index").Scan(checkout);
            Assert.Contains(entries, entry => entry.RelativePath == "docs/design.md");
            Assert.DoesNotContain(entries, entry => entry.Title == "Zeppelins");
        }
        finally
        {
            holds.ForEach(hold => hold.Dispose());
        }

        await Task.Delay(500);
        freshness.Take();

        // The swap's moves: the install's app/ set aside and put back. A handle anywhere in it would refuse the move.
        var appFolder = Path.Combine(_root, app.Replace('/', Path.DirectorySeparatorChar));
        Directory.Move(appFolder, appFolder + ".previous");
        Directory.Move(appFolder + ".previous", appFolder);
        Write("served/node_modules/other/index.js", "module.exports = 1;\n");
        Write("served/_fixtures/probe/notes.md", "# Rewritten\n");
        Write("served/local/notes.md", "# A person's own\n");
        await Task.Delay(1500);
        Assert.False(freshness.IsStale, "a write under a folder git ignores at the checkout's top marked the reading stale");

        Write("served/docs/design.md", "# The probe lock, changed\n");
        await Heard(freshness);
    }

    [Fact]
    public async Task A_knowledge_folder_made_after_the_watch_opened_is_read_at_the_next_search_with_no_deadline_passing()
    {
        // A `docs/` made after start is not left to the deadline. Its making is heard at the checkout's top, and
        // its own watch opens then, so a write in it later is heard as well.
        Write("served/daoris.json", """{ "source": "s" }""");
        Write("served/.git/HEAD", "ref: refs/heads/main\n");
        var checkout = Path.Combine(_root, "served");
        var time = new SteppedTime(DateTimeOffset.Parse("2026-10-21T10:00:00Z"));
        using var freshness = new CheckoutFreshness(checkout, documents: "docs", index: "docs/index");
        Assert.True(freshness.Watching);
        var service = Serve(checkout, freshness, time);
        Assert.False(await Finds(service));

        Write("served/docs/later.md", "# Zeppelins\n\nWritten after the server started.\n");
        await Heard(freshness);
        Assert.True(await Finds(service));

        await Task.Delay(500);
        freshness.Take();
        Write("served/docs/second.md", "# Airships\n\nWritten in the folder made later.\n");
        await Heard(freshness);
    }

    [Fact]
    public async Task A_declared_rooms_instructions_are_heard_and_a_room_made_later_is_watched_when_it_is_made()
    {
        Write("served/daoris.json", """{ "source": "s", "rooms": ["src/probe", "tools/later"] }""");
        Write("served/src/probe/AGENTS.md", "# The probe's room\n");
        Write("served/src/probe/Probe.cs", "class Probe {}\n");
        var checkout = Path.Combine(_root, "served");
        using var freshness = new CheckoutFreshness(checkout, documents: "docs", index: null);
        await Task.Delay(500);
        freshness.Take();

        // The room is watched for its instructions alone, not for the code beside them.
        Write("served/src/probe/Probe.cs", "class Probe { int changed; }\n");
        await Task.Delay(1500);
        Assert.False(freshness.IsStale, "a write beside a room's AGENTS.md marked the reading stale");
        Write("served/src/probe/AGENTS.md", "# The probe's room, changed\n");
        await Heard(freshness);

        // `tools/` is not there: the room is awaited at the top, then in `tools/`, and opened once it is made.
        await Task.Delay(500);
        freshness.Take();
        Write("served/tools/later/AGENTS.md", "# A room made later\n");
        await Heard(freshness);
        await Task.Delay(1000);
        freshness.Take();
        Write("served/tools/later/AGENTS.md", "# A room made later, changed\n");
        await Heard(freshness);
    }

    [Fact]
    public async Task A_folder_made_inside_one_watched_in_parts_is_watched_from_when_it_is_made()
    {
        var checkout = Path.Combine(_root, "served");
        Write("served/.gitignore", ".claude/worktrees/\n");
        Write("served/.claude/knowledge/twins.md", "# Twins\n");
        Write("served/.claude/worktrees/side/AGENTS.md", "# A worktree's brief\n");
        GitCheckouts.Init(checkout);
        using var freshness = new CheckoutFreshness(checkout, documents: "docs", index: null);
        await Task.Delay(500);
        freshness.Take();

        Write("served/.claude/skills/probe/SKILL.md", "# A skill made later\n");
        await Heard(freshness);
        await Task.Delay(1000);
        freshness.Take();
        Write("served/.claude/skills/probe/SKILL.md", "# A skill made later, changed\n");
        await Heard(freshness);
    }

    /// <summary>
    /// The setup's own writes heard and taken: a loaded machine delivers them after any fixed window, so the reading
    /// is taken again until half a second passes with nothing heard, within <see cref="Poll.Patience"/>.
    /// </summary>
    private static async Task Settled(CheckoutFreshness freshness)
    {
        for (var tries = 0; tries < Poll.Patience.TotalMilliseconds / 500; tries++)
        {
            freshness.Take();
            await Task.Delay(500);
            if (!freshness.IsStale) return;
        }

        Assert.Fail($"the setup's own writes were still heard after {Poll.Patience}");
    }

    private static async Task Heard(CheckoutFreshness freshness)
    {
        for (var waited = 0; waited < Poll.Patience.TotalMilliseconds / 100 && !freshness.IsStale; waited++) await Task.Delay(100);
        Assert.True(freshness.IsStale, $"a file moved under a watched folder and nothing was marked within {Poll.Patience}");
    }
}
