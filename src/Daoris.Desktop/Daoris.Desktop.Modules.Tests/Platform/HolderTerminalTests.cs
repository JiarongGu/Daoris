using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// SUPERVISOR1a-ii (D208 point 2; the supervisor design §2.2 and §11 row 1a-ii): the holder's terminal, the one start Daoris builds that
/// adds the key. Its folder is its own, outside every repository and emptied at each start; its shell loads no profile; its lines say
/// what it is and how to start the supervisor's session, and none holds the key; and nothing is started where no shell is found.
/// </summary>
public sealed class HolderTerminalTests : Bridge
{
    private const string Sentinel = "5e17e1a15e17e1a15e17e1a15e17e1a15e17e1a15e17e1a15e17e1a15e17e1a1";

    [Fact]
    public void The_start_is_the_shell_with_no_profile_in_its_emptied_folder_and_the_key_its_one_added_variable()
    {
        var folder = HolderTerminal.Folder(Home, Roles.Supervisor);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, ".mcp.json"), """{"mcpServers":{"planted":{"command":"node"}}}""");
        File.WriteAllText(Path.Combine(folder, "CLAUDE.md"), "planted");

        var info = HolderTerminal.Build(Home, Roles.Supervisor, Sentinel, DateTimeOffset.Parse("2026-10-24T14:02:00Z"), _ => "pwsh.exe")!;

        Assert.Equal("pwsh.exe", info.FileName);
        Assert.False(info.UseShellExecute);
        Assert.Equal(folder, info.WorkingDirectory);
        Assert.Empty(Directory.EnumerateFileSystemEntries(folder));
        Assert.StartsWith(DaorisHome.Entry(Home, HomeEntry.RoleRooms, Roles.Supervisor), folder, StringComparison.Ordinal);
        Assert.Equal(["-NoProfile", "-NoExit", "-Command"], info.ArgumentList.Take(3));
        Assert.Equal(Sentinel, info.Environment[HolderKey.Variable]);
        // Every start's hand applied first: no prompt can be asked of the holder's git, and nothing inherited traces a secret.
        Assert.Equal("0", info.Environment["GIT_TERMINAL_PROMPT"]);

        var script = info.ArgumentList[3];
        Assert.DoesNotContain(Sentinel, script, StringComparison.Ordinal);
        Assert.Contains(HolderTerminal.Title.Replace("'", "''", StringComparison.Ordinal), script, StringComparison.Ordinal);
        Assert.Contains("start only the supervisor''s session here", script, StringComparison.Ordinal);
        Assert.Contains("never print the environment", script, StringComparison.Ordinal);
        Assert.Contains("--setting-sources user --strict-mcp-config", script, StringComparison.Ordinal);
        Assert.Contains("daoris-driver sessions supervise", script, StringComparison.Ordinal);
    }

    [Fact]
    public void No_shell_found_starts_nothing()
    {
        Assert.Null(HolderTerminal.Build(Home, Roles.Supervisor, Sentinel, DateTimeOffset.UtcNow, _ => null));
        Assert.False(HolderTerminal.Start(null, _ => throw new InvalidOperationException("not to be started")));
    }
}

/// <summary>
/// SUPERVISOR1a-ii: the holder's terminal's environment as a real process reads it. The start the window builds, its command swapped for
/// one that writes what the shell sees and exits, carries the sentinel key; the same parent's ordinary start (<see cref="GitWiring.Hand(ProcessStartInfo)"/>,
/// every start's seam) carries none. Starts real shells, so a Process class.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class HolderTerminalProcessTests : Bridge
{
    [Fact]
    public async Task The_key_is_in_the_holders_shell_and_in_no_other_start_of_the_same_parent()
    {
        if (!OperatingSystem.IsWindows()) return;
        const string sentinel = "5e17e1a15e17e1a15e17e1a15e17e1a15e17e1a15e17e1a15e17e1a15e17e1a1";
        Environment.SetEnvironmentVariable(HolderKey.Variable, sentinel);
        try
        {
            var holder = HolderTerminal.Build(Home, Roles.Supervisor, sentinel, DateTimeOffset.UtcNow)!;
            var other = new ProcessStartInfo(holder.FileName) { UseShellExecute = false };
            GitWiring.Hand(other);

            Assert.Equal(sentinel, await SeenAsync(holder, "holder.txt"));
            Assert.Equal("", await SeenAsync(other, "other.txt"));
        }
        finally
        {
            Environment.SetEnvironmentVariable(HolderKey.Variable, null);
        }
    }

    /// <summary>The variable as the started shell reads it, written to a file in the home and read back.</summary>
    private async Task<string> SeenAsync(ProcessStartInfo info, string name)
    {
        var written = Path.Combine(Home, name);
        info.ArgumentList.Clear();
        info.ArgumentList.Add("-NoProfile");
        info.ArgumentList.Add("-Command");
        info.ArgumentList.Add($"[IO.File]::WriteAllText('{written.Replace("'", "''", StringComparison.Ordinal)}', [string]$env:{HolderKey.Variable})");
        info.CreateNoWindow = true;
        using var process = Process.Start(info)!;
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromMinutes(1));
        return File.ReadAllText(written).Trim();
    }
}
