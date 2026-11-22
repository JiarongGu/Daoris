using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A blob read by its full id, or by a commit's or tree's full id and a path, comes from the object store with no git started, byte for byte
/// what the file committed held (raw: no filter, no line-ending change), cut at the asker's limit; a missing path is git's "missing". What the
/// library does not answer (a tree, a commit, a missing object, an odd path, a name that is no id) is one git start, as before.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class GitLibraryObjectsTests : LandedFixture
{
    private static readonly string[] Who = ["-c", "user.email=fixture@example.test", "-c", "user.name=Fixture"];

    private async Task<(string Root, string Commit, string Tree, Dictionary<string, (string Id, byte[] Bytes)> Blobs)> CommittedAsync()
    {
        var (root, _) = await RepositoryWithOriginAsync("objects");
        var big = new byte[300 * 1024];
        new Random(7).NextBytes(big);
        var held = new Dictionary<string, byte[]>
        {
            // A lone carriage return: git's own autocrlf would turn a CRLF pair into the line feed it stores.
            ["crlf.txt"] = "hello\rworld\n"u8.ToArray(),
            ["empty.bin"] = [],
            ["docs/deep/big.bin"] = big,
            ["bom.txt"] = [0xEF, 0xBB, 0xBF, .. "café\n"u8.ToArray()],
        };
        foreach (var (path, bytes) in held)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(root, path))!);
            await File.WriteAllBytesAsync(Path.Combine(root, path), bytes);
        }

        await GitAsync(root, "add", ".");
        await GitAsync(root, [.. Who, "commit", "-m", "files"]);
        var commit = (await GitAsync(root, "rev-parse", "HEAD")).Trim();
        var tree = (await GitAsync(root, "rev-parse", "HEAD^{tree}")).Trim();
        var blobs = new Dictionary<string, (string, byte[])>();
        foreach (var (path, bytes) in held) blobs[path] = ((await GitAsync(root, "rev-parse", $"HEAD:{path}")).Trim(), bytes);
        return (root, commit, tree, blobs);
    }

    [GitLegFact]
    public async Task A_blob_is_read_from_the_store_as_the_file_committed_held_it()
    {
        var (root, commit, tree, blobs) = await CommittedAsync();
        using var count = GitLaunch.Count();
        foreach (var (path, (id, bytes)) in blobs)
        {
            foreach (var name in new[] { new GitObjectName(id), GitObjectName.At(commit, path), GitObjectName.At(tree, path) })
            {
                var read = await GitObjectReads.ReadAsync(root, name, GitObjectReads.Whole, CancellationToken.None);
                Assert.NotNull(read);
                var (answer, got) = read.Value;
                Assert.Equal(("blob", (long)bytes.Length, bytes.Length), (answer.Type, answer.Size, answer.Count));
                Assert.Equal(bytes, got.AsSpan(answer.Start, answer.Count).ToArray());
            }

            var cut = await GitObjectReads.ReadAsync(root, id, 10, CancellationToken.None);
            Assert.Equal(Math.Min(10, bytes.Length), cut!.Value.Answer.Count);
            Assert.Equal(bytes.Take(10), cut.Value.Bytes.AsSpan(cut.Value.Answer.Start, cut.Value.Answer.Count).ToArray());
        }

        var absent = await GitObjectReads.ReadAsync(root, GitObjectName.At(commit, "docs/absent.txt"), 100, CancellationToken.None);
        Assert.True(absent!.Value.Answer.Missing);
        var batch = await GitObjectReads.BatchAsync(root, [blobs["crlf.txt"].Id, GitObjectName.At(commit, "bom.txt"), GitObjectName.At(commit, "nothing")], 1024, CancellationToken.None);
        Assert.Equal([false, false, true], batch!.Select(each => each.Answer.Missing));
        Assert.Equal(0, count.Starts.Count);
    }

    [GitLegFact]
    public async Task What_the_library_does_not_say_is_one_git_start()
    {
        var (root, commit, tree, _) = await CommittedAsync();
        foreach (var (name, type) in new (GitObjectName, string)[] { (commit, "commit"), (tree, "tree"), (GitObjectName.At(commit, "docs"), "tree") })
        {
            using var count = GitLaunch.Count();
            var read = await GitObjectReads.ReadAsync(root, name, 4096, CancellationToken.None);
            Assert.Equal(type, read!.Value.Answer.Type);
            Assert.Equal(1, count.Starts.Count);
        }

        using var missing = GitLaunch.Count();
        var gone = await GitObjectReads.ReadAsync(root, new string('1', 40), 100, CancellationToken.None);
        Assert.True(gone!.Value.Answer.Missing);
        Assert.Equal(1, missing.Starts.Count);
    }
}
