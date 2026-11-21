using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Within one act a read of git's objects by their full ids is asked once; a read of HEAD, a ref, the working tree or the index is git's
/// every time, and an answer git did not give is asked again. The read is a function handed in, so what is counted is how often git
/// would have been asked.
/// </summary>
public sealed class AddressedReadsTests
{
    private const string Commit = "1111111111111111111111111111111111111111";

    private static readonly string[] Listing = ["ls-tree", "-r", "-z", Commit];

    private int _asked;
    private int _code;

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
            var first = await AddressedReads.Asked("root", Listing, Run, default)!;
            var second = await AddressedReads.Asked("root", Listing, Run, default)!;

            Assert.Equal(first, second);
            Assert.Equal(1, _asked);
        }

        Assert.Null(AddressedReads.Asked("root", Listing, Run, default));
    }

    [Theory]
    [InlineData("ls-tree", "-r", "-z", "HEAD")]
    [InlineData("ls-tree", "-r", "-z", "main")]
    [InlineData("ls-tree", "-r", "-z", Commit, "--", "src")]
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

        await AddressedReads.Asked("root", Listing, Run, default)!;
        await AddressedReads.Asked("root", Listing, Run, default)!;

        Assert.Equal(2, _asked);
    }

    [Fact]
    public async Task A_search_that_found_nothing_is_an_answer_and_a_listing_that_failed_is_not()
    {
        _code = 1;
        string[] search = ["--no-optional-locks", "grep", "-I", "-e", "obj", Commit, "--", "*.json"];
        using var act = AddressedReads.Within();

        await AddressedReads.Asked("root", search, Run, default)!;
        await AddressedReads.Asked("root", search, Run, default)!;
        await AddressedReads.Asked("root", Listing, Run, default)!;
        await AddressedReads.Asked("root", Listing, Run, default)!;

        Assert.Equal(3, _asked);
    }

    [Fact]
    public async Task A_file_read_at_a_commit_is_asked_once_and_a_file_at_head_is_not_kept()
    {
        var asked = 0;
        Task<(byte[] Bytes, int Count)?> Read(CancellationToken _)
        {
            asked++;
            return Task.FromResult<(byte[] Bytes, int Count)?>(([1, 2, 3], 3));
        }

        string[] batch = ["cat-file", "--batch"];
        using var act = AddressedReads.Within();

        await AddressedReads.AskedBytes("root", batch, 100, $"{Commit}:daoris.json\n", Read, default)!;
        await AddressedReads.AskedBytes("root", batch, 100, $"{Commit}:daoris.json\n", Read, default)!;
        await AddressedReads.AskedBytes("root", batch, 50, $"{Commit}:daoris.json\n", Read, default)!;

        Assert.Equal(2, asked);
        Assert.Null(AddressedReads.AskedBytes("root", batch, 100, "HEAD:daoris.json\n", Read, default));
        Assert.Null(AddressedReads.AskedBytes("root", batch, 100, $"{Commit}:daoris.json\nHEAD\n", Read, default));
    }
}
