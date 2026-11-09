using System.Diagnostics;
using System.Text;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The store's facts about a commit, on real git: a listing kept only where its bytes hash to their ids, a blob asked by its id answered
/// only by bytes that hash to it, every id-keyed part passed through while a replacement can be in force, a tracked list kept under its
/// fence, and the door's search over the declarations equal to git's.
/// </summary>
/// <remarks>The fixture's files are seconds old, so the clock is read a minute ahead: a fence refused here is refused by its stats, not
/// by its age. Each test commits a content of its own, since the store keeps facts by id across repositories.</remarks>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class RepositoryDeclarationsProcessTests : LandedFixture
{
    private static readonly CancellationToken None = CancellationToken.None;

    [Fact]
    public async Task A_commit_is_listed_once_its_blob_proven_and_then_answered_with_no_start()
    {
        using var _ = Settled();
        var (root, commit, blob) = await GatesAsync("listed");

        using (var cold = GitStartTally.Open(root))
        {
            var first = Assert.IsType<DeclarationAnswer.Present>(await RepositoryDeclarations.AtAsync(root, commit, DeclarationNames.Gates, None));
            Assert.Equal(new BlobId(blob), first.Content!.Id);
            Assert.Equal("100644", first.Entry!.Value.Mode);
            Assert.Equal(2, cold.Starts.Count);
            Assert.All(cold.Scopes, scope => Assert.Equal(DeclarationAsker.Store, scope));
        }

        using var warm = GitStartTally.Open(root);
        var watch = Stopwatch.StartNew();
        Assert.IsType<DeclarationAnswer.Present>(await RepositoryDeclarations.AtAsync(root, commit, DeclarationNames.Gates, None));
        Assert.IsType<DeclarationAnswer.Absent>(await RepositoryDeclarations.AtAsync(root, commit, DeclarationNames.Lanes, None));
        Assert.Equal(commit, await TipOf.OfAsync(root, "refs/heads/main", None));
        watch.Stop();
        Assert.Empty(warm.Starts);
        Assert.True(watch.Elapsed < TimeSpan.FromMilliseconds(50), $"three warm asks took {watch.Elapsed.TotalMilliseconds} ms");
    }

    [Fact]
    public async Task A_kept_blob_replaced_is_unread_by_its_id_and_answered_with_gits_bytes_and_no_id_by_its_path()
    {
        using var _ = Settled();
        var (root, commit, blob) = await GatesAsync("replaced");
        Assert.IsType<DeclarationAnswer.Present>(await RepositoryDeclarations.AtAsync(root, commit, DeclarationNames.Gates, None));
        var other = await BlobAsync(root, "{ \"other\": 1 }\n");
        await GitAsync(root, "replace", blob, other);

        var byPath = Assert.IsType<DeclarationAnswer.Present>(await RepositoryDeclarations.AtAsync(root, commit, DeclarationNames.Gates, None));
        Assert.Equal("{ \"other\": 1 }\n", byPath.Content!.Text());
        Assert.Null(byPath.Content.Id);
        var byId = Assert.IsType<DeclarationAnswer.Unread>(await RepositoryDeclarations.ByIdAsync(root, new BlobId(blob), None));
        Assert.Equal(DeclarationUnread.Replaced, byId.Cause);

        await GitAsync(root, "replace", "-d", blob);
        var restored = Assert.IsType<DeclarationAnswer.Present>(await RepositoryDeclarations.ByIdAsync(root, new BlobId(blob), None));
        Assert.Equal(new BlobId(blob), restored.Content!.Id);
    }

    [Fact]
    public async Task A_commit_its_tree_and_a_folder_each_replaced_after_the_gate_admit_nothing()
    {
        using var _ = Settled();
        var root = await RepositoryAsync("turns");
        Directory.CreateDirectory(Path.Combine(root, "sub"));
        var first = await HeadAsync(root);
        await CommitAsync(root, "sub/x.json", $"\"alt {Guid.NewGuid():N}\"\n", "alt");
        var alt = await HeadAsync(root);

        foreach (var replaced in new[] { "{0}", "{0}^{{tree}}", "{0}:sub" })
        {
            await GitAsync(root, "reset", "-q", "--hard", first);
            Directory.CreateDirectory(Path.Combine(root, "sub"));
            await CommitAsync(root, "sub/x.json", $"\"main {Guid.NewGuid():N}\"\n", "main");
            var main = await HeadAsync(root);
            var said = await GitAsync(root, "show", "HEAD:sub/x.json");
            var from = await RevAsync(root, string.Format(System.Globalization.CultureInfo.InvariantCulture, replaced, main));
            var to = await RevAsync(root, string.Format(System.Globalization.CultureInfo.InvariantCulture, replaced, alt));

            RepositoryDeclarations.BeforeRead.Value = () => GitAsync(root, "replace", from, to);
            var during = Assert.IsType<DeclarationAnswer.Present>(await RepositoryDeclarations.AtAsync(root, main, "sub/x.json", None));
            RepositoryDeclarations.BeforeRead.Value = null;
            await GitAsync(root, "replace", "-d", from);
            Assert.StartsWith("\"alt ", during.Content!.Text(), StringComparison.Ordinal);
            Assert.Null(during.Content.Id);

            using var tally = GitStartTally.Open(root);
            var after = Assert.IsType<DeclarationAnswer.Present>(await RepositoryDeclarations.AtAsync(root, main, "sub/x.json", None));
            Assert.Equal(said, after.Content!.Text());
            Assert.Contains(tally.Starts, start => start.StartsWith("cat-file --batch", StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task A_blob_replaced_after_the_gate_and_restored_after_its_read_is_not_admitted()
    {
        using var _ = Settled();
        var (root, commit, blob) = await GatesAsync("after-gate");
        var other = await BlobAsync(root, "{ \"between\": 1 }\n");
        RepositoryDeclarations.BeforeRead.Value = () => GitAsync(root, "replace", blob, other);

        var during = Assert.IsType<DeclarationAnswer.Present>(await RepositoryDeclarations.AtAsync(root, commit, DeclarationNames.Gates, None));
        RepositoryDeclarations.BeforeRead.Value = null;
        await GitAsync(root, "replace", "-d", blob);

        Assert.Equal("{ \"between\": 1 }\n", during.Content!.Text());
        Assert.Null(during.Content.Id);
        using var tally = GitStartTally.Open(root);
        var after = Assert.IsType<DeclarationAnswer.Present>(await RepositoryDeclarations.AtAsync(root, commit, DeclarationNames.Gates, None));
        Assert.Equal(new BlobId(blob), after.Content!.Id);
        Assert.Single(tally.Starts);
    }

    [Fact]
    public async Task A_replacement_beside_replace_refs_off_is_passed_through_and_nothing_admitted()
    {
        using var _ = Settled();
        var (root, commit, blob) = await GatesAsync("refs-off");
        var other = await BlobAsync(root, "{ \"off\": 1 }\n");
        await GitAsync(root, "replace", blob, other);
        await GitAsync(root, "config", "core.useReplaceRefs", "false");

        var passed = Assert.IsType<DeclarationAnswer.Present>(await RepositoryDeclarations.AtAsync(root, commit, DeclarationNames.Gates, None));
        Assert.Null(passed.Content!.Id);
        Assert.DoesNotContain("\"off\"", passed.Content.Text(), StringComparison.Ordinal);

        using var tally = GitStartTally.Open(root);
        Assert.IsType<DeclarationAnswer.Present>(await RepositoryDeclarations.AtAsync(root, commit, DeclarationNames.Gates, None));
        Assert.NotEmpty(tally.Starts);
    }

    [Fact]
    public async Task A_tracked_list_read_across_an_add_and_a_remove_of_a_replacement_is_refused_by_its_fence()
    {
        using var _ = Settled();
        var root = await RepositoryAsync("tracked");
        var first = await HeadAsync(root);
        await CommitAsync(root, $"{Guid.NewGuid():N}.txt", "more\n", "more");
        var tip = await HeadAsync(root);
        RepositoryDeclarations.BeforeRead.Value = () => GitAsync(root, "replace", tip, first);

        var during = await RepositoryDeclarations.TrackedAsync(root, tip, None);
        RepositoryDeclarations.BeforeRead.Value = null;
        await GitAsync(root, "replace", "-d", tip);

        Assert.Equal(2, during.Count);
        var after = await RepositoryDeclarations.TrackedAsync(root, tip, None);
        Assert.Equal(3, after.Count);
        using var tally = GitStartTally.Open(root);
        Assert.Equal(after, await RepositoryDeclarations.TrackedAsync(root, tip, None));
        Assert.Empty(tally.Starts);
    }

    [Fact]
    public async Task Two_trees_of_one_repository_share_its_facts_and_two_files_of_one_content_share_a_parse()
    {
        using var _ = Settled();
        var (root, commit, _) = await GatesAsync("contexts");
        var linked = Path.Combine(Scratch, "contexts-linked");
        await GitAsync(root, "worktree", "add", "-q", "--detach", linked, commit);
        await GitAsync(root, "config", "extensions.worktreeConfig", "true");
        await GitAsync(linked, "config", "--worktree", "core.useReplaceRefs", "false");
        await File.WriteAllBytesAsync(Path.Combine(linked, DeclarationNames.Gates), await File.ReadAllBytesAsync(Path.Combine(root, DeclarationNames.Gates)));
        Assert.IsType<DeclarationAnswer.Present>(await RepositoryDeclarations.AtAsync(root, commit, DeclarationNames.Gates, None));

        using (var tally = GitStartTally.Open(root))
        {
            Assert.IsType<DeclarationAnswer.Present>(await RepositoryDeclarations.AtAsync(linked, commit, DeclarationNames.Gates, None));
            Assert.Empty(tally.Starts);
        }

        var kind = new Counted();
        var mine = RepositoryDeclarations.OnDisk(Path.Combine(root, DeclarationNames.Gates));
        var theirs = RepositoryDeclarations.OnDisk(Path.Combine(linked, DeclarationNames.Gates));
        Assert.True(DeclarationVersion.Same(mine.Version, theirs.Version));
        Assert.Same(RepositoryDeclarations.Parse(mine, kind), RepositoryDeclarations.Parse(theirs, kind));
        Assert.Equal(1, kind.Parses);
    }

    [Fact]
    public async Task Each_cause_of_unread_at_a_commit_is_kept_for_none()
    {
        using var _ = Settled();
        var (root, commit, _) = await GatesAsync("unread");
        var missing = new string('7', commit.Length);

        Assert.Equal(DeclarationUnread.Missing, Unread(await RepositoryDeclarations.AtAsync(root, missing, DeclarationNames.Gates, None)));
        Assert.Equal(DeclarationUnread.Failed, Unread(await RepositoryDeclarations.AtAsync(Scratch, commit, DeclarationNames.Gates, None)));
        Assert.Equal(DeclarationUnread.TooLarge, Unread(await RepositoryDeclarations.AtAsync(root, commit, DeclarationNames.Gates, None, limit: 4)));
        Assert.IsType<DeclarationAnswer.Present>(await RepositoryDeclarations.AtAsync(root, commit, DeclarationNames.Gates, None));

        using var tally = GitStartTally.Open(root);
        Assert.Equal(DeclarationUnread.Missing, Unread(await RepositoryDeclarations.AtAsync(root, missing, DeclarationNames.Gates, None)));
        Assert.NotEmpty(tally.Starts);
    }

    [Fact]
    public async Task The_doors_search_finds_the_lines_gits_search_finds()
    {
        var root = await RepositoryAsync("search");
        var name = $"tool{Guid.NewGuid():N}"[..12];
        await File.WriteAllBytesAsync(Path.Combine(root, DeclarationNames.Manifest), Encoding.UTF8.GetBytes($"a\r{name} after a lone CR\nplain {name}\r\nx\r\n{name} after a CR line\n"));
        await File.WriteAllBytesAsync(Path.Combine(root, DeclarationNames.Gates), [.. Encoding.UTF8.GetBytes($"{name}\n"), 0, .. "binary\n"u8]);
        await File.WriteAllBytesAsync(Path.Combine(root, DeclarationNames.Lanes), Encoding.UTF8.GetBytes($"﻿{name} with a mark\fand a feed\nnone here\n"));
        await File.WriteAllBytesAsync(Path.Combine(root, "toolbox.json"), Encoding.UTF8.GetBytes($"{name} in a tool\rcut\n"));
        await GitAsync(root, "-c", "core.autocrlf=false", "add", "-A");
        await GitAsync(root, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "commit", "-q", "-m", "the search");
        var commit = await HeadAsync(root);
        string[] names = [name, "absent-name"];
        string[] pathspecs = ["*.json", "Makefile"];

        var git = await FileGit.LinesAsync(root, commit, names, pathspecs, None);
        var door = await GitTreeReads.LinesAsync(root, commit, names, pathspecs, None);

        Assert.Equal(git, door);
        Assert.Contains((DeclarationNames.Manifest, "a"), door);
    }

    private static IDisposable Settled()
    {
        VolumeClock.Seam.Value = _ => DateTime.UtcNow + TimeSpan.FromMinutes(1);
        return new Ended(() => VolumeClock.Seam.Value = null);
    }

    private static DeclarationUnread Unread(DeclarationAnswer answer) => Assert.IsType<DeclarationAnswer.Unread>(answer).Cause;

    private async Task<(string Root, string Commit, string Blob)> GatesAsync(string name)
    {
        var root = await RepositoryAsync(name);
        await CommitAsync(root, DeclarationNames.Gates, $"{{ \"gates\": [], \"note\": \"{Guid.NewGuid():N}\" }}\n", "the gates");
        return (root, await HeadAsync(root), await RevAsync(root, $"HEAD:{DeclarationNames.Gates}"));
    }

    private static async Task<string> BlobAsync(string root, string text)
    {
        var file = Path.Combine(root, $"{Guid.NewGuid():N}.blob");
        await File.WriteAllTextAsync(file, text);
        var id = (await GitAsync(root, "hash-object", "-w", file)).Trim();
        File.Delete(file);
        return id;
    }

    private static Task<string> HeadAsync(string root) => RevAsync(root, "HEAD");

    private static async Task<string> RevAsync(string root, string revision) => (await GitAsync(root, "rev-parse", revision)).Trim();

    private sealed class Ended(Action end) : IDisposable
    {
        public void Dispose() => end();
    }

    private sealed class Counted : IDeclarationKind<string>
    {
        public int Parses { get; private set; }

        public string Parse(DeclarationBytes content)
        {
            Parses++;
            return content.Text();
        }

        public bool Problem(string parsed) => false;

        public long Weigh(string parsed) => DeclarationBound.Text(parsed);
    }
}
