using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The paths under the home no <see cref="HomeEntry"/> names : a file in a place the table dropped is named with its size,
/// an entry's children are not, and <c>daoris home</c> says them in its own section.
/// </summary>
public sealed class HomeStraysTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-strays-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private void Put(string relative, int bytes)
    {
        var path = Path.Combine(_home, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new byte[bytes]);
    }

    [Fact]
    public void A_file_at_a_place_no_entry_names_is_named_with_its_size()
    {
        Put("data/harnesses/claude-code/acct/state.json", 700);
        Put("data/harnesses/claude-code/acct/more.json", 300);

        var stray = Assert.Single(HomeStrays.Read(_home).Paths);
        Assert.Equal(("data/harnesses", 1000L), (stray.Path, stray.Bytes));
    }

    [Fact]
    public void An_entrys_children_are_not_named_and_a_pattern_covers_its_items()
    {
        Put("data/driver.json", 10);
        Put("data/plugins/one/manifest.json", 10);
        Put("data/act-files/step-1/bytes.bin", 10);
        Put("run/sessions/7.pid", 1);
        Put("work/rooms/help-2/CLAUDE.md", 1);
        Put("profiles/agents/claude/acct/home/.claude.json", 1);

        Assert.Empty(HomeStrays.Read(_home).Paths);
    }

    [Fact]
    public void The_installs_own_files_are_not_the_homes()
    {
        Put("INSTALLED.md", 1);
        Put("app/Daoris.exe", 1);
        Put("Daoris.exe", 1);
        Put("update/staged/x", 1);
        Put("data/desktop.db-wal", 1);
        Put("data/desktop.db.wake", 1);
        Put("data/stray.txt", 5);

        var stray = Assert.Single(HomeStrays.Read(_home).Paths);
        Assert.Equal("data/stray.txt", stray.Path);
    }

    [Fact]
    public void A_junction_is_not_followed()
    {
        var outside = Path.Combine(TestHomes.Root, "daoris-strays-out-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(outside);
        File.WriteAllBytes(Path.Combine(outside, "big.bin"), new byte[5000]);
        try
        {
            Directory.CreateDirectory(Path.Combine(_home, "data"));
            try
            {
                Directory.CreateSymbolicLink(Path.Combine(_home, "data", "linked"), outside);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                return; // This account may not make links.
            }

            var stray = Assert.Single(HomeStrays.Read(_home).Paths);
            Assert.Equal(("data/linked", 0L), (stray.Path, stray.Bytes));
        }
        finally
        {
            Directory.Delete(outside, recursive: true);
        }
    }

    [Fact]
    public void The_home_door_says_them_in_a_section_not_in_the_table()
    {
        Put("data/old/thing.bin", 2048);

        var code = HomePurgeCommand.Run([], _home, () => default, null, null, out var said);

        Assert.Equal(0, code);
        Assert.Contains("Not in the table", said);
        Assert.Contains("data/old", said);
    }

    [Fact]
    public void A_home_with_none_says_so()
    {
        Put("data/driver.json", 1);

        var code = HomePurgeCommand.Run([], _home, () => default, null, null, out var said);

        Assert.Equal(0, code);
        Assert.Contains("Not in the table: nothing", said);
    }
}
