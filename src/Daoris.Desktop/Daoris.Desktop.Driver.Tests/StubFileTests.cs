namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// 🔴 REV3's final gate run: a chat test failed on a sharing violation reading the file its stub agent
/// was still appending to. The stub holds the file open to write, sharing it the way node does, and
/// <c>File.ReadAllLines</c> asks that nobody write while it reads — which Windows refuses outright.
/// </summary>
public sealed class StubFileTests : IDisposable
{
    private readonly string _path = Path.Combine(
        Path.GetTempPath(), "daoris-stub-file-" + Guid.NewGuid().ToString("N")[..8] + ".txt");

    public void Dispose()
    {
        try { File.Delete(_path); } catch (IOException) { }
    }

    [Fact]
    public void A_file_a_writer_holds_open_reads_whole_while_it_is_held()
    {
        File.WriteAllText(_path, "initialize\nsession/new\n");
        using var writer = new FileStream(
            _path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);

        if (OperatingSystem.IsWindows())
        {
            // The defect, shown: the plain read is refused while the writer holds the file.
            Assert.Throws<IOException>(() => File.ReadAllLines(_path));
        }

        Assert.Equal(["initialize", "session/new"], StubFile.Lines(_path));
        Assert.Equal("initialize\nsession/new\n", StubFile.Text(_path));
    }

    [Fact]
    public void A_file_not_written_yet_reads_as_nothing()
    {
        Assert.Empty(StubFile.Lines(_path));
        Assert.Equal("", StubFile.Text(_path));
    }
}
