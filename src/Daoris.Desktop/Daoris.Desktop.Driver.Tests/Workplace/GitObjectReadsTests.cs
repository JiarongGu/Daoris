using System.Diagnostics;
using System.Text;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Every object read as one <c>cat-file --batch</c> question, over real git, each answer held to the form it replaced: a blob's bytes
/// to <c>cat-file blob</c>'s (empty, binary, a byte-order mark, past its asker's bound), its text to that form's text as git's text
/// output is read, presence and type to <c>cat-file -e</c> and <c>-t</c>; a path holding a line break read by its id and never written as
/// a question; a filtered blob of a partial clone asked again once as a fetch, and said missing where its promisor is gone.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class GitObjectReadsTests : TreeSyncFixture
{
    private static readonly (string Name, byte[] Bytes)[] Files =
    [
        ("empty.txt", []),
        ("binary.bin", [.. Enumerable.Range(0, 256).Select(at => (byte)at), (byte)'\r', (byte)'\n', 0, 0]),
        ("marked.txt", [0xEF, 0xBB, 0xBF, .. "marked\r\nline\n"u8.ToArray()]),
        ("large.txt", [.. Enumerable.Range(0, 300 * 1024).Select(at => (byte)('a' + at % 26))]),
    ];

    private async Task<string> FilesAsync()
    {
        var root = await RepositoryAsync("objects");
        foreach (var (name, bytes) in Files) await File.WriteAllBytesAsync(Path.Combine(root, name), bytes);
        await File.WriteAllTextAsync(Path.Combine(root, ".gitattributes"), "* -text\n");
        await GitAsync(root, "add", "-A");
        await GitAsync(root, "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "commit", "-m", "files");
        return root;
    }

    /// <summary>What the deleted form answered: <c>cat-file</c>'s output as bytes, and its exit.</summary>
    private static async Task<(int Code, byte[] Bytes)> FormAsync(string root, params string[] arguments)
    {
        var info = new ProcessStartInfo { FileName = "git", WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(TestGit.Past(info))!;
        using var bytes = new MemoryStream();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.StandardOutput.BaseStream.CopyToAsync(bytes);
        await process.WaitForExitAsync();
        await stderr;
        return (process.ExitCode, bytes.ToArray());
    }

    [Fact]
    public async Task Each_blob_reads_as_bytes_equal_to_cat_file_blob_and_past_its_bound_for_the_bytes_it_wants()
    {
        var root = await FilesAsync();

        foreach (var (name, bytes) in Files)
        {
            var read = await GitObjectReads.ReadAsync(root, GitObjectName.At("HEAD", name), 1024 * 1024, CancellationToken.None);
            var (code, form) = await FormAsync(root, "cat-file", "blob", $"HEAD:{name}");

            Assert.Equal(0, code);
            Assert.NotNull(read);
            Assert.Equal(("blob", (long)bytes.Length), (read.Value.Answer.Type, read.Value.Answer.Size));
            Assert.Equal(form, read.Value.Bytes.AsSpan(read.Value.Answer.Start, read.Value.Answer.Count).ToArray());
        }

        var bounded = await GitObjectReads.ReadAsync(root, GitObjectName.At("HEAD", "large.txt"), 1000, CancellationToken.None);
        Assert.Equal((300L * 1024, 1000), (bounded!.Value.Answer.Size, bounded.Value.Answer.Count));
        Assert.Equal(Files[3].Bytes[..1000], bounded.Value.Bytes.AsSpan(bounded.Value.Answer.Start, bounded.Value.Answer.Count).ToArray());
        Assert.Empty(Directory.GetFiles(Path.Combine(root, ".git"), "*.lock", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Each_blobs_text_equals_the_text_cat_file_blob_gave()
    {
        var root = await FilesAsync();

        foreach (var (name, _) in Files)
        {
            var (code, form, _) = await WorkingTree.GitAsync(root, ["cat-file", "blob", $"HEAD:{name}"], CancellationToken.None);

            Assert.Equal(0, code);
            Assert.Equal(form, await GitObjectReads.TextAsync(root, GitObjectName.At("HEAD", name), CancellationToken.None));
        }

        Assert.Null(await GitObjectReads.TextAsync(root, GitObjectName.At("HEAD", "nothing.txt"), CancellationToken.None));
        Assert.Null(await GitObjectReads.TextAsync(root, "HEAD^{tree}", CancellationToken.None));
        Assert.Equal("abcd", await GitObjectReads.TextAsync(root, GitObjectName.At("HEAD", "large.txt"), 4, CancellationToken.None));
    }

    [Fact]
    public async Task Presence_and_type_answer_as_cat_file_e_and_t_did()
    {
        var root = await FilesAsync();
        var blob = await RevAsync(root, "HEAD:large.txt");
        string[] names = ["HEAD^{commit}", "HEAD^{tree}", "HEAD:large.txt", blob, $"{blob}^{{commit}}", "HEAD:nothing.txt", new string('d', 40), $"{new string('d', 40)}^{{commit}}"];

        foreach (var name in names)
        {
            var info = await GitObjectReads.InfoAsync(root, name, CancellationToken.None);
            var (present, _) = await FormAsync(root, "cat-file", "-e", name);
            var (typed, type) = await FormAsync(root, "cat-file", "-t", name);

            Assert.NotNull(info);
            Assert.True(present == 0 == !info.Missing, name);
            Assert.Equal(typed == 0 ? Encoding.UTF8.GetString(type).Trim() : "", info.Type);
        }
    }

    [Fact]
    public async Task A_missing_name_longer_than_a_header_is_missing_and_not_a_failure()
    {
        var root = await FilesAsync();
        var name = GitObjectName.At("HEAD", string.Join('/', Enumerable.Repeat(new string('d', 30), 6)));

        Assert.True((await GitObjectReads.InfoAsync(root, name, CancellationToken.None))!.Missing);
        Assert.True((await GitObjectReads.ReadAsync(root, name, 0, CancellationToken.None))!.Value.Answer.Missing);
        Assert.Null(await GitObjectReads.TextAsync(root, name, CancellationToken.None));
        var batch = await GitObjectReads.BatchAsync(root, [name, "HEAD^{tree}"], 64, CancellationToken.None);
        Assert.True(batch is { Count: 2 } && batch[0].Answer.Missing && batch[1].Answer.Type == "tree");
    }

    [Fact]
    public async Task A_path_holding_a_line_break_is_read_by_its_id_and_never_written_as_a_question()
    {
        var root = await RepositoryAsync("broken");
        var body = Path.Combine(Scratch, "body.txt");
        await File.WriteAllTextAsync(body, "broken\n");
        var blob = (await GitAsync(root, "hash-object", "-w", body)).Trim();
        // Written to the index by its id: no Windows file system takes the name, and git does where it is not told to guard NTFS.
        var held = await FormAsync(root, "-c", "core.protectNTFS=false", "update-index", "--add", "--cacheinfo", $"100644,{blob},a\nb");
        Assert.Equal(0, held.Code);
        await GitAsync(root, "-c", "core.protectNTFS=false", "-c", "user.email=fixture@example.test", "-c", "user.name=Fixture", "commit", "-m", "a line break");

        var name = GitObjectName.At("HEAD", "a\nb");

        Assert.False(name.Framed);
        Assert.Throws<InvalidOperationException>(() => name.Question);
        Assert.Equal("broken\n", await GitObjectReads.TextAsync(root, name, CancellationToken.None));
        Assert.Equal((false, "blob"), (await GitObjectReads.InfoAsync(root, name, CancellationToken.None)) is { } info ? (info.Missing, info.Type) : (true, ""));
        Assert.True((await GitObjectReads.InfoAsync(root, GitObjectName.At("HEAD", "a\nc"), CancellationToken.None))!.Missing);
        Assert.Null(await GitObjectReads.InfoAsync(root, "HEAD\n", CancellationToken.None));
    }

    [Fact]
    public async Task A_filtered_blob_is_asked_again_once_as_a_fetch_and_said_missing_where_its_promisor_is_gone()
    {
        var promised = await RepositoryAsync("promised");
        await CommitAsync(promised, "a.txt", "one\n", "a one");
        await CommitAsync(promised, "b.txt", "two\n", "b two");
        await GitAsync(promised, "config", "uploadpack.allowFilter", "true");
        var clone = Path.Combine(Scratch, "partial");
        await GitAsync(Scratch, "clone", "--quiet", "--filter=blob:none", "--no-checkout", "--no-local", promised, clone);
        await GitSeed.NoMaintenanceAsync(clone);

        string? fetched;
        using (var count = GitLaunch.Count())
        {
            fetched = await GitObjectReads.TextAsync(clone, GitObjectName.At("HEAD", "a.txt"), CancellationToken.None);
            Assert.Equal(2, count.Starts.Count(start => start.Arguments.Contains("--batch")));
        }

        Directory.Move(promised, promised + "-gone");
        var gone = await GitObjectReads.ReadAsync(clone, GitObjectName.At("HEAD", "b.txt"), 64, CancellationToken.None);

        Assert.Equal("one\n", fetched);
        Assert.True(gone is { Answer.Missing: true }, "a blob its promisor no longer gives is missing");
        Assert.True((await GitObjectReads.InfoAsync(clone, GitObjectName.At("HEAD", "b.txt"), CancellationToken.None))!.Missing);
    }
}
