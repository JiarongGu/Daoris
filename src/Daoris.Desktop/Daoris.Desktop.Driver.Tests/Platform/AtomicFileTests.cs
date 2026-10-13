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
    public void A_folder_move_refused_while_something_holds_a_file_is_tried_again_until_it_gives_way()
    {
        foreach (Exception held in new Exception[] { new UnauthorizedAccessException("held"), new IOException("held") })
        {
            var calls = 0;
            AtomicFile.MoveFolder("a.part", "a", tries: 5, waitMs: 1, move: (_, _) =>
            {
                if (++calls <= 2) throw held;
            });
            Assert.Equal(3, calls);
        }
    }

    [Fact]
    public void A_folder_move_refused_for_any_other_reason_throws_at_once_and_a_held_one_after_its_tries()
    {
        var missing = 0;
        Assert.Throws<DirectoryNotFoundException>(() => AtomicFile.MoveFolder("a.part", "a", tries: 5, waitMs: 1, move: (_, _) =>
        {
            missing++;
            throw new DirectoryNotFoundException("gone");
        }));
        Assert.Equal(1, missing);

        var held = 0;
        Assert.Throws<UnauthorizedAccessException>(() => AtomicFile.MoveFolder("a.part", "a", tries: 3, waitMs: 1, move: (_, _) =>
        {
            held++;
            throw new UnauthorizedAccessException("held");
        }));
        Assert.Equal(3, held);
    }

    [Fact]
    public void A_folder_moves_into_place()
    {
        var from = Path.Combine(_folder, "1.0.part");
        Directory.CreateDirectory(from);
        File.WriteAllText(Path.Combine(from, "tool.json"), "{}\n");

        AtomicFile.MoveFolder(from, Path.Combine(_folder, "1.0"));

        Assert.True(File.Exists(Path.Combine(_folder, "1.0", "tool.json")));
        Assert.False(Directory.Exists(from));
    }

    [Fact]
    public void Text_is_written_without_a_byte_order_mark()
    {
        var path = Path.Combine(_folder, "rules.json");
        AtomicFile.WriteText(path, "{}\n");

        Assert.Equal("{}\n"u8.ToArray(), File.ReadAllBytes(path));
    }
}
