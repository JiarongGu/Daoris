using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// 🔴 A chat test must not fail on a sharing violation reading the file its stub agent
/// is still appending to. The stub holds the file open to write, sharing it the way node does, and
/// <c>File.ReadAllLines</c> asks that nobody write while it reads — which Windows refuses outright.
/// </summary>
public sealed class StubFileTests : IDisposable
{
    private readonly string _path = Path.Combine(
        TestHomes.Root, "daoris-stub-file-" + Guid.NewGuid().ToString("N")[..8] + ".txt");

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

    [Theory]
    [InlineData("Make the port.\n\nDaoris's standing block for this session: where things are.\n\nRouting standing here: not driven. Go.", "Make the port.")]
    [InlineData("Make the port. | Before this turn. | Daoris's standing block for this session: where. Routing standing here: open.", "Make the port. | Before this turn.")]
    [InlineData("Make the port.\\n\\nDaoris's standing block for this session: where things are.", "Make the port.")]
    [InlineData("Make the port.", "Make the port.")]
    public void A_prompt_is_read_without_the_standing_block_Daoris_appends(string prompt, string words)
    {
        Assert.Equal(words, StubFile.WithoutBlock(prompt));
        Assert.StartsWith(StubFile.BlockLead, StandingBlock.Opening, StringComparison.Ordinal);
    }

    [Fact]
    public void A_file_not_written_yet_reads_as_nothing()
    {
        Assert.Empty(StubFile.Lines(_path));
        Assert.Equal("", StubFile.Text(_path));
    }
}
