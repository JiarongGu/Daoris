using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Within one act a read of git's objects by their full ids is asked once; a read of HEAD, a ref, the working tree or the index is git's
/// every time, and an answer git did not give is asked again. The read is a function handed in, so what is counted is how often git
/// would have been asked; the repository is a real one, whose promisor and replacements decide what is kept.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class AddressedReadsTests : IDisposable
{
    private const string Commit = "1111111111111111111111111111111111111111";

    private static readonly string[] Listing = ["ls-tree", "-r", "-z", Commit];

    private readonly string _root = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "addressed-reads", Guid.NewGuid().ToString("N")[..8]);

    private int _asked;
    private int _code;

    public AddressedReadsTests()
    {
        Directory.CreateDirectory(_root);
        GitSeed.GitAsync(_root, "init", "--quiet", "-b", "main").GetAwaiter().GetResult();
    }

    public void Dispose() => GitSeed.Discard(_root);

    private Task<(int Code, string Stdout, string Stderr)> Run(CancellationToken _)
    {
        Interlocked.Increment(ref _asked);
        return Task.FromResult((_code, "answer", ""));
    }

    [Fact]
    public async Task A_listing_of_a_commit_by_its_id_is_asked_once_in_an_act_and_again_outside_it()
    {
        using (AddressedReads.Within())
        {
            var first = await AddressedReads.Asked(_root, Listing, Run, default)!;
            var second = await AddressedReads.Asked(_root, Listing, Run, default)!;

            Assert.Equal(first, second);
            Assert.Equal(1, _asked);
        }

        Assert.Null(AddressedReads.Asked(_root, Listing, Run, default));
    }

    [Theory]
    [InlineData("ls-tree", "-r", "-z", "HEAD")]
    [InlineData("ls-tree", "-r", "-z", "main")]
    [InlineData("ls-tree", "-r", "-z", Commit, "--", "src/*")]
    [InlineData("ls-tree", "-r", Commit, "--", "src")]
    [InlineData("status", "--porcelain", "-z")]
    [InlineData("rev-parse", "HEAD")]
    [InlineData("grep", "-e", "obj", "HEAD")]
    [InlineData("grep", "-e", "obj", "--cached")]
    [InlineData("ls-files", "-v", "-z")]
    public void A_read_that_names_head_a_ref_or_the_files_is_never_kept(params string[] arguments)
    {
        Assert.False(AddressedReads.Addressed(arguments));
    }

    [Theory]
    [InlineData("ls-tree", "-r", "-z", Commit)]
    [InlineData("--no-optional-locks", "ls-tree", "-r", "-z", "--full-tree", Commit)]
    [InlineData("ls-tree", "-r", "-z", Commit, "--", "src")]
    [InlineData("--no-optional-locks", "grep", "-I", "-F", "-z", "--no-color", "-e", "obj", Commit, "--", "*.json", "*.ts")]
    public void A_read_that_names_one_commit_by_its_id_alone_is_kept(params string[] arguments)
    {
        Assert.True(AddressedReads.Addressed(arguments));
    }

    [Fact]
    public async Task A_read_that_git_did_not_answer_is_asked_again()
    {
        _code = 128;
        using var act = AddressedReads.Within();

        await AddressedReads.Asked(_root, Listing, Run, default)!;
        await AddressedReads.Asked(_root, Listing, Run, default)!;

        Assert.Equal(2, _asked);
    }

    [Fact]
    public async Task A_search_that_found_nothing_is_an_answer_and_a_listing_that_failed_is_not()
    {
        _code = 1;
        string[] search = ["--no-optional-locks", "grep", "-I", "-e", "obj", Commit, "--", "*.json"];
        using var act = AddressedReads.Within();

        await AddressedReads.Asked(_root, search, Run, default)!;
        await AddressedReads.Asked(_root, search, Run, default)!;
        await AddressedReads.Asked(_root, Listing, Run, default)!;
        await AddressedReads.Asked(_root, Listing, Run, default)!;

        Assert.Equal(3, _asked);
    }

    [Fact]
    public async Task A_file_read_at_a_commit_is_asked_once_and_a_file_at_head_is_not_kept()
    {
        var asked = 0;
        Task<(byte[] Bytes, int Count)?> Read(CancellationToken _)
        {
            asked++;
            return Task.FromResult(Reply($"{Commit} blob 3\nabc\n"));
        }

        string[] batch = ["cat-file", "--batch"];
        using var act = AddressedReads.Within();

        await AddressedReads.AskedBytes(_root, batch, 100, $"{Commit}:daoris.json\n", Read, default)!;
        await AddressedReads.AskedBytes(_root, batch, 100, $"{Commit}:daoris.json\n", Read, default)!;
        await AddressedReads.AskedBytes(_root, batch, 50, $"{Commit}:daoris.json\n", Read, default)!;

        Assert.Equal(2, asked);
        Assert.Null(AddressedReads.AskedBytes(_root, batch, 100, "HEAD:daoris.json\n", Read, default));
        Assert.Null(AddressedReads.AskedBytes(_root, batch, 100, $"{Commit}:daoris.json\nHEAD\n", Read, default));
    }

    private static (byte[] Bytes, int Count)? Reply(string text)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(text);
        return (bytes, bytes.Length);
    }

    [Fact]
    public async Task In_a_repository_with_a_promisor_a_batch_that_said_missing_is_asked_again_so_a_fetching_retry_reaches_git()
    {
        File.WriteAllText(Path.Combine(_root, ".git", "config"), "[core]\n\trepositoryformatversion = 1\n[remote \"origin\"]\n\turl = file:///nowhere\n\tpromisor = true\n[extensions]\n\tpartialclone = origin\n");
        var asked = 0;
        Task<(byte[] Bytes, int Count)?> Read(CancellationToken _) =>
            Task.FromResult(++asked == 1 ? Reply($"{Commit} missing\n") : Reply($"{Commit} blob 3\nabc\n"));
        string[] batch = ["cat-file", "--batch"];
        using var act = AddressedReads.Within();

        var first = await AddressedReads.AskedBytes(_root, batch, 100, $"{Commit}\n", Read, default)!;
        var again = await AddressedReads.AskedBytes(_root, batch, 100, $"{Commit}\n", Read, default)!;
        var kept = await AddressedReads.AskedBytes(_root, batch, 100, $"{Commit}\n", Read, default)!;

        Assert.Equal(2, asked);
        Assert.Equal($"{Commit} missing\n".Length, first!.Value.Count);
        Assert.Equal(again!.Value.Count, kept!.Value.Count);
        Assert.NotEqual(first.Value.Count, again.Value.Count);
    }

    [Fact]
    public async Task In_a_repository_without_one_a_missing_object_stays_missing_and_is_asked_once()
    {
        var asked = 0;
        Task<(byte[] Bytes, int Count)?> Read(CancellationToken _)
        {
            asked++;
            return Task.FromResult(Reply($"{Commit} missing\n"));
        }

        using var act = AddressedReads.Within();

        await AddressedReads.AskedBytes(_root, ["cat-file", "--batch"], 100, $"{Commit}\n", Read, default)!;
        await AddressedReads.AskedBytes(_root, ["cat-file", "--batch"], 100, $"{Commit}\n", Read, default)!;

        Assert.Equal(1, asked);
    }

    [Fact]
    public async Task Nothing_is_kept_once_a_replacement_can_be_in_force_and_what_was_kept_before_it_stands_once_it_goes()
    {
        using var act = AddressedReads.Within();
        await AddressedReads.Asked(_root, Listing, Run, default)!;
        await AddressedReads.Asked(_root, Listing, Run, default)!;
        Assert.Equal(1, _asked);

        var replace = Path.Combine(_root, ".git", "refs", "replace");
        Directory.CreateDirectory(replace);
        File.WriteAllText(Path.Combine(replace, Commit), Commit + "\n");

        Assert.Null(AddressedReads.Asked(_root, Listing, Run, default));
        Assert.Null(AddressedReads.AskedBytes(_root, ["cat-file", "--batch"], 100, $"{Commit}\n", _ => Task.FromResult(Reply("x")), default));

        File.Delete(Path.Combine(replace, Commit));
        await AddressedReads.Asked(_root, Listing, Run, default)!;
        Assert.Equal(1, _asked);
    }

    [Fact]
    public void A_folder_that_is_no_repository_keeps_nothing()
    {
        using var act = AddressedReads.Within();

        Assert.Null(AddressedReads.Asked(Path.Combine(_root, "elsewhere"), Listing, Run, default));
    }
}
