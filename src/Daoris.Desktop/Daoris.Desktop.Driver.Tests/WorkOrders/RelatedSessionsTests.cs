using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// LINESTOGETHER1c (the review-flow design §3): the sessions of one repository with a tree here, chats included, compared on a git
/// fixture of three branches, an uncommitted edit counted, and a failed read after a good one said *not read*.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class RelatedSessionsTests : IDisposable
{
    private readonly string _root = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "related", Guid.NewGuid().ToString("N")[..8]);
    private string _base = "";

    public void Dispose() => GitSeed.Discard(_root);

    private async Task<string> TreeAsync(string name, string file, string text, bool commit = true)
    {
        var main = Path.Combine(_root, "main");
        if (!Directory.Exists(main))
        {
            Directory.CreateDirectory(main);
            await GitFixture.GitAsync(main, "init", "-q", "-b", "main");
            await GitFixture.GitAsync(main, "config", "user.email", "t@example.test");
            await GitFixture.GitAsync(main, "config", "user.name", "t");
            File.WriteAllText(Path.Combine(main, "shared.txt"), "0\n");
            File.WriteAllText(Path.Combine(main, "other.txt"), "0\n");
            await GitFixture.GitAsync(main, "add", "-A");
            await GitFixture.GitAsync(main, "commit", "-q", "-m", "base");
            _base = (await GitFixture.GitAsync(main, "rev-parse", "HEAD")).Trim();
        }

        var tree = Path.Combine(_root, name);
        await GitFixture.GitAsync(main, "worktree", "add", "-q", "-b", name, tree, _base);
        await GitFixture.GitAsync(tree, "config", "user.email", "t@example.test");
        await GitFixture.GitAsync(tree, "config", "user.name", "t");
        File.WriteAllText(Path.Combine(tree, file), text);
        if (commit)
        {
            await GitFixture.GitAsync(tree, "add", "-A");
            await GitFixture.GitAsync(tree, "commit", "-q", "-m", name);
        }

        return tree;
    }

    private RelatedCandidate Quest(string id, string tree, bool running = false, params string[] declared) =>
        new(id, "driven", tree, _base, running, declared, Quest: "q-" + id);

    private RelatedCandidate Chat(string id, string tree, bool running = false) => new(id, "chat", tree, _base, running, []);

    [Fact]
    public async Task Two_sessions_changing_one_file_meet_a_chat_among_them_and_a_third_does_not()
    {
        var quest = await TreeAsync("one", "shared.txt", "1\n");
        var chat = await TreeAsync("two", "shared.txt", "2\n");
        var apart = await TreeAsync("three", "other.txt", "3\n");
        var related = new RelatedSessions(RelatedSessions.GitReads, () => DateTimeOffset.UtcNow);

        var found = await related.LookAsync(Quest("one", quest), [Chat("two", chat), Quest("three", apart)]);

        var only = Assert.Single(found);
        Assert.Equal(("two", "chat", "read"), (only.Session, only.Kind, only.Read));
        Assert.Equal(["shared.txt"], only.ChangedOverlap);
        Assert.Empty(only.DeclaredOverlap);
    }

    [Fact]
    public async Task A_declared_path_that_meets_another_sessions_work_is_told_apart_from_a_changed_one()
    {
        var quest = await TreeAsync("one", "shared.txt", "1\n");
        var apart = await TreeAsync("three", "other.txt", "3\n");
        var related = new RelatedSessions(RelatedSessions.GitReads, () => DateTimeOffset.UtcNow);

        var found = await related.LookAsync(Quest("one", quest, declared: "other.txt"), [Quest("three", apart)]);

        var only = Assert.Single(found);
        Assert.Equal(["other.txt"], only.DeclaredOverlap);
        Assert.Empty(only.ChangedOverlap);
    }

    [Fact]
    public async Task An_uncommitted_edit_counts_and_a_clean_ended_tree_is_not_read_again()
    {
        var quest = await TreeAsync("one", "shared.txt", "1\n");
        var ended = await TreeAsync("three", "other.txt", "3\n");
        var diffs = 0;
        var reads = RelatedSessions.GitReads with
        {
            Diff = (tree, before, ct) =>
            {
                if (tree == ended) diffs++;
                return RelatedSessions.GitReads.Diff(tree, before, ct);
            },
        };
        var related = new RelatedSessions(reads, () => DateTimeOffset.UtcNow);

        Assert.Empty(await related.LookAsync(Quest("one", quest, running: true), [Quest("three", ended)]));
        Assert.Empty(await related.LookAsync(Quest("one", quest, running: true), [Quest("three", ended)]));
        Assert.Equal(1, diffs);

        File.WriteAllText(Path.Combine(ended, "shared.txt"), "edited\n");
        var found = await related.LookAsync(Quest("one", quest, running: true), [Quest("three", ended)]);

        Assert.Equal(["shared.txt"], Assert.Single(found).ChangedOverlap);
    }

    [Fact]
    public async Task A_failed_read_after_a_good_one_says_not_read_and_not_the_old_answer()
    {
        var quest = await TreeAsync("one", "shared.txt", "1\n");
        var chat = await TreeAsync("two", "shared.txt", "2\n");
        var fail = false;
        var reads = RelatedSessions.GitReads with
        {
            Diff = (tree, before, ct) => fail && tree == chat ? Task.FromResult<IReadOnlyList<string>?>(null) : RelatedSessions.GitReads.Diff(tree, before, ct),
        };
        var at = DateTimeOffset.Parse("2026-10-31T10:00:00Z");
        var related = new RelatedSessions(reads, () => at);
        Assert.Equal("read", Assert.Single(await related.LookAsync(Quest("one", quest), [Chat("two", chat, running: true)])).Read);

        fail = true;
        var found = await related.LookAsync(Quest("one", quest), [Chat("two", chat, running: true)]);

        var only = Assert.Single(found);
        Assert.Equal("not-read", only.Read);
        Assert.Equal(at, only.ReadAt);
        Assert.Empty(only.ChangedOverlap);
    }
}
