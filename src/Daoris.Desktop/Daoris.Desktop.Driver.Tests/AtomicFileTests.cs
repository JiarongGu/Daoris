using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The one writer every file the driver keeps goes through (REV3 driver F11, C1). Writers each used a
/// fixed beside-name, so two writing one file at once collided — and the throw failed a finished session.
/// </summary>
public sealed class AtomicFileTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "daoris-atomic-" + Guid.NewGuid().ToString("N")[..8]);

    public AtomicFileTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        try { Directory.Delete(_folder, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public async Task Many_writers_of_one_file_at_once_all_land_and_leave_nothing_beside()
    {
        var path = Path.Combine(_folder, "usage.json");
        var writers = Enumerable.Range(0, 32).Select(n => Task.Run(() => AtomicFile.WriteText(path, $"writer {n}\n")));

        await Task.WhenAll(writers);

        Assert.Matches(@"^writer \d+\n$", File.ReadAllText(path));
        Assert.Equal(["usage.json"], Directory.GetFiles(_folder).Select(Path.GetFileName));
    }

    [Fact]
    public void Text_is_written_without_a_byte_order_mark()
    {
        var path = Path.Combine(_folder, "rules.json");
        AtomicFile.WriteText(path, "{}\n");

        Assert.Equal("{}\n"u8.ToArray(), File.ReadAllBytes(path));
    }
}
