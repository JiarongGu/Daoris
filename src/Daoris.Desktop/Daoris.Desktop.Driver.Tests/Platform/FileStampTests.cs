using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A file's content by its stat under git's rule for a racy entry: a stat is trusted only where the volume's clock stands two seconds
/// past its write time as the bytes are hashed, so a same-length rewrite inside the clock's grain is read at the next ask, and a share
/// whose clock lags (through the clock's seam) has every stat hashed again.
/// </summary>
public sealed class FileStampTests : IDisposable
{
    private readonly string _folder = Path.Combine(TestHomes.Root, "file-stamp-tests", Guid.NewGuid().ToString("N")[..8]);

    public FileStampTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private string Written(string name, string text, TimeSpan? age = null)
    {
        var path = Path.Combine(_folder, name);
        File.WriteAllText(path, text);
        if (age is { } back) File.SetLastWriteTimeUtc(path, DateTime.UtcNow - back);
        return path;
    }

    /// <summary>A second write of one length whose write time is the first's, as a coarse clock leaves it.</summary>
    private static void Rewritten(string path, string text)
    {
        var written = File.GetLastWriteTimeUtc(path);
        File.WriteAllText(path, text);
        File.SetLastWriteTimeUtc(path, written);
    }

    [Fact]
    public void A_same_length_rewrite_inside_the_clocks_grain_is_read_at_the_next_ask()
    {
        var path = Written("racy.txt", "one");
        var first = FileStamp.Of(path);
        Rewritten(path, "two");

        var second = FileStamp.Of(path);

        Assert.Equal(first.Stat, second.Stat);
        Assert.NotEqual(first.Content, second.Content);
        Assert.NotNull(second.Bytes);
    }

    [Fact]
    public void A_settled_stat_answers_its_content_without_reading_the_file()
    {
        var path = Written("settled.txt", "one", TimeSpan.FromSeconds(30));
        var first = FileStamp.Of(path);

        var second = FileStamp.Of(path);

        Assert.NotNull(first.Bytes);
        Assert.Null(second.Bytes);
        Assert.Equal(first.Content, second.Content);
    }

    [Fact]
    public void A_shares_clock_lagging_behind_has_its_racy_stat_hashed_again_at_every_ask()
    {
        var path = Written("share.txt", "one", TimeSpan.FromSeconds(30));
        VolumeClock.Seam.Value = _ => DateTime.UtcNow - TimeSpan.FromMinutes(5);
        try
        {
            FileStamp.Of(path);
            Rewritten(path, "two");

            var second = FileStamp.Of(path);

            Assert.NotNull(second.Bytes);
            Assert.Equal("two", System.Text.Encoding.UTF8.GetString(second.Bytes!));
        }
        finally
        {
            VolumeClock.Seam.Value = null;
        }
    }

    [Fact]
    public void A_volume_with_no_clock_trusts_no_stat()
    {
        var path = Written("unclocked.txt", "one", TimeSpan.FromSeconds(30));
        VolumeClock.Seam.Value = _ => null;
        try
        {
            FileStamp.Of(path);
            Assert.NotNull(FileStamp.Of(path).Bytes);
        }
        finally
        {
            VolumeClock.Seam.Value = null;
        }
    }

    [Fact]
    public void A_parse_is_made_once_a_content_and_again_when_the_content_moves()
    {
        var path = Written("parsed.txt", "one", TimeSpan.FromSeconds(30));
        var made = 0;
        string Parse(byte[] bytes)
        {
            made++;
            return System.Text.Encoding.UTF8.GetString(bytes);
        }

        Assert.Equal("one", FileStamp.Parsed(path, Parse).Parsed);
        Assert.Equal("one", FileStamp.Parsed(path, Parse).Parsed);
        File.WriteAllText(path, "three");
        Assert.Equal("three", FileStamp.Parsed(path, Parse).Parsed);
        Assert.Equal(2, made);
    }

    [Fact]
    public void An_absent_file_has_no_content_and_a_folder_is_no_file()
    {
        Assert.Null(FileStamp.Of(Path.Combine(_folder, "absent")).Content);
        Assert.Null(FileStamp.Of(_folder).Content);
        Assert.NotNull(FileStamp.Of(_folder).Stat);
    }
}
