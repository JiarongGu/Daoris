using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// The one atomic write the service core uses (DEDUP9a, <see cref="AtomicFile"/>): beside under a name of its own, renamed over
/// the target, nothing left behind.
/// </summary>
public sealed class AtomicFileTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "atomic-" + Guid.NewGuid().ToString("N"));

    public AtomicFileTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public void A_write_leaves_only_the_file()
    {
        var path = Path.Combine(_folder, "a.json");
        AtomicFile.WriteText(path, "one\n");
        AtomicFile.WriteBytes(Path.Combine(_folder, "b.bin"), [1, 2, 3]);

        Assert.Equal("one\n", File.ReadAllText(path));
        Assert.Equal(2, Directory.GetFiles(_folder).Length);
        Assert.Empty(Directory.GetFiles(_folder, "*.writing"));
        Assert.Empty(Directory.GetFiles(_folder, "*.tmp"));
    }

    [Fact]
    public void Text_is_BOM_less_and_kept_as_given()
    {
        var path = Path.Combine(_folder, "a.json");
        AtomicFile.WriteText(path, "x\ny\n");

        var bytes = File.ReadAllBytes(path);
        Assert.Equal("x\ny\n"u8.ToArray(), bytes);
    }

    [Fact]
    public void An_existing_file_is_replaced()
    {
        var path = Path.Combine(_folder, "a.json");
        File.WriteAllText(path, "old");
        AtomicFile.WriteText(path, "new");

        Assert.Equal("new", File.ReadAllText(path));
    }

    [Fact]
    public async Task Two_concurrent_writes_of_one_path_both_finish_and_one_is_whole()
    {
        var path = Path.Combine(_folder, "a.bin");
        var one = Enumerable.Repeat((byte)1, 200_000).ToArray();
        var two = Enumerable.Repeat((byte)2, 200_000).ToArray();

        await Task.WhenAll(
            Enumerable.Range(0, 20).Select(i => Task.Run(async () =>
                await AtomicFile.WriteBytesAsync(path, i % 2 == 0 ? one : two))));

        var kept = File.ReadAllBytes(path);
        Assert.True(kept.SequenceEqual(one) || kept.SequenceEqual(two));
        Assert.Single(Directory.GetFiles(_folder));
    }
}
