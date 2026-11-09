using System.Security.Cryptography;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The store's kernel without git: its keys and versions, a checkout's file asked by its stat, the three answers on disk, a kind's parse
/// kept by its content alone, the bound per kind and its weights, and a tip read from the refs' files.
/// </summary>
public sealed class DeclarationKernelTests : IDisposable
{
    private const string One = "1111111111111111111111111111111111111111";
    private const string Two = "2222222222222222222222222222222222222222";
    private const string Three = "3333333333333333333333333333333333333333";

    private readonly string _scratch = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "declarations", Guid.NewGuid().ToString("N")[..8]);

    public DeclarationKernelTests() => Directory.CreateDirectory(_scratch);

    public void Dispose()
    {
        VolumeClock.Seam.Value = null;
        try
        {
            Directory.Delete(_scratch, recursive: true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // A link a test made may be held a moment; the run's scratch is cleared with the fixtures.
        }
    }

    [Fact]
    public void Unread_is_the_same_as_no_version_and_has_no_text()
    {
        var key = DeclarationVersion.Of(ContentKey.Of("a"u8));
        var unread = DeclarationVersion.Unread(DeclarationUnread.Io);

        Assert.False(DeclarationVersion.Same(unread, unread));
        Assert.False(DeclarationVersion.Same(unread, DeclarationVersion.Unread(DeclarationUnread.Io)));
        Assert.False(DeclarationVersion.Same(unread, DeclarationVersion.Absent));
        Assert.False(DeclarationVersion.Same(DeclarationVersion.Absent, unread));
        Assert.False(DeclarationVersion.Same(unread, key));
        Assert.False(DeclarationVersion.Same(null, key));
        Assert.True(DeclarationVersion.Same(DeclarationVersion.Absent, DeclarationVersion.Absent));
        Assert.True(DeclarationVersion.Same(key, DeclarationVersion.Of(ContentKey.Of("a"u8))));
        Assert.False(DeclarationVersion.Same(key, DeclarationVersion.Of(ContentKey.Of("b"u8))));
        Assert.IsAssignableFrom<IOException>(Assert.Throws<DeclarationUnreadException>(() => unread.Text));
        Assert.Equal(ContentKey.Of("a"u8).Hex, key.Text);
    }

    [Fact]
    public void A_blob_id_is_proven_by_the_bytes_that_hash_to_it_alone()
    {
        Assert.True(new BlobId("ce013625030ba8dba906f756967f9e9ca394464a").Proves("hello\n"u8));
        Assert.True(new BlobId("e69de29bb2d1d6434b8b29ae775ad8c2e48c5391").Proves([]));
        Assert.True(new BlobId("473a0f4c3be8a93681a267e3b1e9a7dcda1185436fe141f7749120a303721813").Proves([]));
        Assert.False(new BlobId("ce013625030ba8dba906f756967f9e9ca394464a").Proves("hellO\n"u8));
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData("hello\n"u8)), ContentKey.Of("hello\n"u8).Hex);
    }

    [Fact]
    public void A_checkout_ask_is_kept_by_its_stat_and_read_again_where_the_stat_moved()
    {
        Ahead(TimeSpan.FromMinutes(1));
        var path = Write(DeclarationNames.Manifest, "{ \"one\": 1 }");

        var first = Assert.IsType<DeclarationAnswer.Present>(RepositoryDeclarations.OnDisk(path));
        Assert.Null(FileStamp.Of(path).Bytes);
        File.WriteAllText(path, "{ \"two\": 22 }");
        var second = Assert.IsType<DeclarationAnswer.Present>(RepositoryDeclarations.OnDisk(path));

        Assert.NotEqual(first.Content!.Key, second.Content!.Key);
        Assert.Equal("{ \"two\": 22 }", second.Content.Text());
        Assert.Null(second.Content.Id);
    }

    [Fact]
    public void A_racy_stat_is_hashed_again_at_every_ask_and_never_promoted_by_time()
    {
        var path = Write(DeclarationNames.Gates, "{}");
        VolumeClock.Seam.Value = _ => File.GetLastWriteTimeUtc(path) + TimeSpan.FromSeconds(1);

        Assert.IsType<DeclarationAnswer.Present>(RepositoryDeclarations.OnDisk(path));
        Assert.NotNull(FileStamp.Of(path).Bytes);
        VolumeClock.Seam.Value = _ => File.GetLastWriteTimeUtc(path) + TimeSpan.FromMinutes(1);

        Assert.NotNull(FileStamp.Of(path).Bytes);
        Assert.Null(FileStamp.Of(path).Bytes);
    }

    [Fact]
    public void A_stat_moved_during_the_read_answers_its_bytes_and_keeps_nothing()
    {
        var path = Write(DeclarationNames.Lanes, "{ \"a\": 1 }");
        var asked = 0;
        VolumeClock.Seam.Value = _ =>
        {
            if (asked++ == 0) File.WriteAllText(path, "{ \"b\": 22 }");
            return DateTime.UtcNow + TimeSpan.FromMinutes(1);
        };

        var during = Assert.IsType<DeclarationAnswer.Present>(RepositoryDeclarations.OnDisk(path));

        Assert.Equal("{ \"b\": 22 }", during.Content!.Text());
        Assert.NotNull(FileStamp.Of(path).Bytes);
    }

    [Fact]
    public void A_link_is_read_at_every_ask()
    {
        var target = Write("target.json", "{}");
        var link = Path.Combine(_scratch, DeclarationNames.Rules);
        try
        {
            File.CreateSymbolicLink(link, target);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // This machine gives no right to make a link, so no link can stand here to be read.
            return;
        }

        Ahead(TimeSpan.FromMinutes(1));
        Assert.IsType<DeclarationAnswer.Present>(RepositoryDeclarations.OnDisk(link));
        Assert.NotNull(FileStamp.Of(link).Bytes);
        Assert.NotNull(FileStamp.Of(link).Bytes);
    }

    [Fact]
    public void Each_cause_of_unread_on_disk_is_kept_for_none()
    {
        Ahead(TimeSpan.FromMinutes(1));
        var gone = Path.Combine(_scratch, "gone", DeclarationNames.Manifest);
        Assert.Equal(DeclarationUnread.FolderGone, Unread(RepositoryDeclarations.OnDisk(gone)));
        Directory.CreateDirectory(Path.GetDirectoryName(gone)!);
        Assert.IsType<DeclarationAnswer.Absent>(RepositoryDeclarations.OnDisk(gone));
        File.WriteAllText(gone, "{}");
        Assert.IsType<DeclarationAnswer.Present>(RepositoryDeclarations.OnDisk(gone));

        Directory.CreateDirectory(Path.Combine(_scratch, DeclarationNames.Gates));
        Assert.Equal(DeclarationUnread.NotAFile, Unread(RepositoryDeclarations.OnDisk(Path.Combine(_scratch, DeclarationNames.Gates))));

        var large = Write(DeclarationNames.PersonRules, new string('x', 64));
        Assert.Equal(DeclarationUnread.TooLarge, Unread(RepositoryDeclarations.OnDisk(large, limit: 16)));
        Assert.IsType<DeclarationAnswer.Present>(RepositoryDeclarations.OnDisk(large, limit: 64));

        if (!OperatingSystem.IsWindows()) return;
        var held = Write(DeclarationNames.FoundRules, "{}");
        using (new FileStream(held, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.Equal(DeclarationUnread.Io, Unread(RepositoryDeclarations.OnDisk(held)));
        }

        Assert.IsType<DeclarationAnswer.Present>(RepositoryDeclarations.OnDisk(held));
    }

    [Fact]
    public void A_clean_parse_is_kept_by_its_content_and_one_naming_a_problem_is_made_again()
    {
        Ahead(TimeSpan.FromMinutes(1));
        var kind = new Counted();
        var rules = File.ReadAllText(Path.Combine(WorkspaceRoot.Folder, DeclarationNames.Rules));
        var clean = RepositoryDeclarations.OnDisk(Write(DeclarationNames.Rules, rules + new string(' ', 3)));
        var broken = RepositoryDeclarations.OnDisk(Write(DeclarationNames.PersonRules, "{ not json " + Guid.NewGuid().ToString("N")));

        Assert.Empty(RepositoryDeclarations.Parse(clean, kind)!.Problems);
        Assert.Same(RepositoryDeclarations.Parse(clean, kind), RepositoryDeclarations.Parse(clean, kind));
        Assert.Equal(1, kind.Parses);
        Assert.NotEmpty(RepositoryDeclarations.Parse(broken, kind)!.Problems);
        _ = RepositoryDeclarations.Parse(broken, kind);
        Assert.Equal(3, kind.Parses);
        Assert.Null(RepositoryDeclarations.Parse(RepositoryDeclarations.OnDisk(Path.Combine(_scratch, "none.json")), kind));
    }

    [Fact]
    public void A_view_is_made_once_a_key_from_the_contents_its_key_holds()
    {
        Ahead(TimeSpan.FromMinutes(1));
        var rules = File.ReadAllText(Path.Combine(WorkspaceRoot.Folder, DeclarationNames.Rules));
        var answer = Assert.IsType<DeclarationAnswer.Present>(RepositoryDeclarations.OnDisk(Write(DeclarationNames.Rules, rules + new string(' ', 5))));
        var view = new RulesOfKind();
        var key = new RulesOfKind.Key(answer.Content!.Key, FileKind.Dependency);

        var first = RepositoryDeclarations.View(key, view, answer.Content);

        Assert.Same(first, RepositoryDeclarations.View(key, view, answer.Content));
        Assert.Equal(1, view.Made);
        Assert.Throws<InvalidOperationException>(() => new DeclarationContents([]).Parse(answer.Content.Key, RuleFileKind.One));
    }

    [Fact]
    public void Each_kind_is_bounded_alone_dropping_its_least_recently_used()
    {
        var bound = new DeclarationBound("test", budget: 300, largest: 150);

        Assert.True(bound.Keep("a", "A", 100));
        Assert.True(bound.Keep("b", "B", 100));
        Assert.True(bound.TryGet<string>("a", out _));
        Assert.True(bound.Keep("c", "C", 100));
        Assert.True(bound.Keep("d", "D", 100));
        Assert.False(bound.TryGet<string>("b", out _));
        Assert.True(bound.TryGet<string>("a", out _));
        Assert.Equal(300, bound.Weight);
        Assert.False(bound.Keep("e", "E", 151));
        Assert.False(bound.TryGet<string>("e", out _));

        var other = new DeclarationBound("other", budget: 100, largest: 100);
        Assert.True(other.Keep("x", "X", 100));
        Assert.True(bound.TryGet<string>("a", out _));
        Assert.Equal(16L << 20, DeclarationBound.Contents.Budget);
        Assert.Equal((24L << 20, 6L << 20), (DeclarationBound.Lists.Budget, DeclarationBound.Lists.Largest));
        Assert.Equal(4L << 20, DeclarationBound.Indexes.Budget);
    }

    [Fact]
    public void Each_weight_is_measured_against_what_making_its_value_allocated()
    {
        var bytes = File.ReadAllBytes(Path.Combine(WorkspaceRoot.Folder, DeclarationNames.Rules));
        _ = Allocated(() => DeclarationBytes.Of([.. bytes], null));
        var (content, made) = Allocated(() => DeclarationBytes.Of([.. bytes], null));
        Assert.InRange(content.Weight, made, made * 3 / 2);

        _ = Allocated(() => RuleFileKind.One.Parse(content));
        var (parsed, parsing) = Allocated(() => RuleFileKind.One.Parse(content));
        // A parse holds no less than the text it keeps and no more than its making allocated, the reader's garbage among it.
        Assert.InRange(RuleFileKind.One.Weigh(parsed), content.Size, parsing);

        var tree = Tree(("README.md", "100644", One), (DeclarationNames.Manifest, "100644", Two), ("src", "40000", Three));
        _ = Allocated(() => DeclarationListing.Entries(tree, 20));
        var (entries, listing) = Allocated(() => DeclarationListing.Entries(tree, 20));
        Assert.InRange(DeclarationListing.Weigh(entries!), listing / 2, listing * 2);
    }

    [Fact]
    public void A_tip_is_read_from_the_loose_the_packed_and_the_symbolic_refs()
    {
        Ahead(TimeSpan.FromMinutes(1));
        var root = Path.Combine(_scratch, "repo");
        var git = Path.Combine(root, ".git");
        var heads = Path.Combine(git, "refs", "heads");
        Directory.CreateDirectory(Path.Combine(heads, "daoris"));
        File.WriteAllText(Path.Combine(git, "HEAD"), "ref: refs/heads/main\n");
        var packed = Path.Combine(git, "packed-refs");
        File.WriteAllText(packed, $"# pack-refs with: peeled fully-peeled sorted\n{One} refs/heads/main\n{Two} refs/heads/daoris/s-1\n");

        Assert.Equal((true, One), TipOf.FromFiles(root, "refs/heads/main"));
        Assert.Equal((true, One), TipOf.FromFiles(root, "HEAD"));
        Assert.Equal((true, Two), TipOf.FromFiles(root, "refs/heads/daoris/s-1"));

        File.WriteAllText(Path.Combine(heads, "main"), Three + "\n");
        Assert.Equal((true, Three), TipOf.FromFiles(root, "refs/heads/main"));
        Assert.Equal((true, Three), TipOf.FromFiles(root, "HEAD"));

        File.WriteAllText(Path.Combine(heads, "daoris", "s-1"), One + "\n");
        Assert.Equal((true, One), TipOf.FromFiles(root, "refs/heads/daoris/s-1"));

        File.WriteAllText(packed, $"{Three} refs/heads/other\n{Two} refs/heads/daoris/s-2\n");
        Assert.Equal((true, Three), TipOf.FromFiles(root, "refs/heads/other"));
        Assert.Equal((true, Two), TipOf.FromFiles(root, "refs/heads/daoris/s-2"));
        Assert.Equal((true, null), TipOf.FromFiles(root, "refs/heads/none"));

        for (var at = 0; at < 6; at++) File.WriteAllText(Path.Combine(heads, $"s{at}"), $"ref: refs/heads/s{at + 1}\n");
        Assert.False(TipOf.FromFiles(root, "refs/heads/s0").Known);
        Assert.Throws<ArgumentException>(() => TipOf.FromFiles(root, "main"));

        Directory.CreateDirectory(Path.Combine(git, "reftable"));
        Assert.False(TipOf.FromFiles(root, "refs/heads/main").Known);
    }

    private static void Ahead(TimeSpan ahead) => VolumeClock.Seam.Value = _ => DateTime.UtcNow + ahead;

    private static DeclarationUnread Unread(DeclarationAnswer answer) => Assert.IsType<DeclarationAnswer.Unread>(answer).Cause;

    private static (T Value, long Bytes) Allocated<T>(Func<T> make)
    {
        var before = GC.GetAllocatedBytesForCurrentThread();
        var value = make();
        return (value, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    private static byte[] Tree(params (string Name, string Mode, string Id)[] entries)
    {
        using var tree = new MemoryStream();
        foreach (var (name, mode, id) in entries)
        {
            tree.Write(System.Text.Encoding.UTF8.GetBytes($"{mode} {name}\0"));
            tree.Write(Convert.FromHexString(id));
        }

        return tree.ToArray();
    }

    private string Write(string name, string text)
    {
        var path = Path.Combine(_scratch, name);
        File.WriteAllText(path, text);
        return path;
    }

    private sealed class Counted : IDeclarationKind<RepositoryFilesRead>
    {
        public int Parses { get; private set; }

        public RepositoryFilesRead Parse(DeclarationBytes content)
        {
            Parses++;
            return RuleFileKind.One.Parse(content);
        }

        public bool Problem(RepositoryFilesRead parsed) => RuleFileKind.One.Problem(parsed);

        public long Weigh(RepositoryFilesRead parsed) => RuleFileKind.One.Weigh(parsed);
    }

    private sealed class RulesOfKind : IDeclarationView<RulesOfKind.Key, string>
    {
        internal sealed record Key(ContentKey Rules, FileKind Kind);

        public int Made { get; private set; }

        public string View(Key key, DeclarationContents contents)
        {
            Made++;
            return string.Join(",", contents.Parse(key.Rules, RuleFileKind.One).Rules.Where(rule => rule.Kind == key.Kind).Select(rule => rule.Paths[0]));
        }

        public long Weigh(string value) => DeclarationBound.Text(value);
    }
}
