using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The reads the home's own files make beside their writer (BINDRACE1, READSHARE1), declared in Platform so it names nothing of Store
/// (FOUNDATION1 mod-ii): each reads a file while the move that landed it still holds it (<see cref="MoveHold"/>), and
/// <see cref="AtomicFile"/>'s spellings answer as they do.
/// </summary>
public sealed class BesideReadsTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "daoris-beside-reads-" + Guid.NewGuid().ToString("N")[..8]);

    public BesideReadsTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        try { Directory.Delete(_folder, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public void A_file_is_read_as_text_bytes_and_a_stream_while_its_move_into_place_still_holds_it()
    {
        var path = Path.Combine(_folder, "held.json");
        File.WriteAllBytes(path, [0x7b, 0x7d, 0x0a]);

        using (MoveHold.Take(path))
        {
            Assert.Equal("{}\n", BesideReads.Text(path));
            Assert.Equal([0x7b, 0x7d, 0x0a], BesideReads.Bytes(path));
            using var stream = BesideReads.Open(path);
            Assert.Equal(3, stream.Length);
        }
    }

    [Fact]
    public void Store_s_spellings_of_the_reads_answer_what_the_reads_answer_and_throw_what_they_throw()
    {
        var path = Path.Combine(_folder, "same.json");
        AtomicFile.WriteText(path, "{\"a\": 1}\n");

        Assert.Equal(BesideReads.Text(path), AtomicFile.ReadText(path));
        Assert.Equal(BesideReads.Bytes(path), AtomicFile.ReadBytes(path));
        using (var read = AtomicFile.OpenRead(path)) Assert.Equal(BesideReads.Bytes(path).Length, read.Length);

        var missing = Path.Combine(_folder, "missing.json");
        Assert.Throws<FileNotFoundException>(() => BesideReads.Text(missing));
        Assert.Throws<FileNotFoundException>(() => BesideReads.Bytes(missing));
        Assert.Throws<FileNotFoundException>(() => BesideReads.Open(missing));
    }

    [Fact]
    public void A_byte_order_mark_is_not_part_of_the_text()
    {
        var path = Path.Combine(_folder, "bom.json");
        File.WriteAllBytes(path, [0xef, 0xbb, 0xbf, 0x7b, 0x7d]);

        Assert.Equal("{}", BesideReads.Text(path));
    }
}
