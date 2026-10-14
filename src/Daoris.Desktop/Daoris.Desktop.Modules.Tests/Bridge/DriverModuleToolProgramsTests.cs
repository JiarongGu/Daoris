using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// Settings → Tools asking real programs (TOOLS7, D121 §4.1): the version a named file answers before it is written, the
/// version each tool runs at in the list, and what a switch of git changes from each git's own
/// <c>config --system --list</c>. Stub programs, written by the test as batch files, stand in for the makers'.
/// </summary>
/// <remarks>In the suite's Process half (MOD8): each case starts programs. A batch file is Windows', where an install runs.</remarks>
[Trait(Category.Name, Category.Process)]
public sealed class DriverModuleToolProgramsTests : DriverModuleBridge
{
    /// <summary>A stub program that prints <paramref name="lines"/> whatever it is asked.</summary>
    private string Stub(string name, params string[] lines)
    {
        var folder = Path.Combine(Home, "stubs", Path.GetFileNameWithoutExtension(name));
        Directory.CreateDirectory(folder);
        var file = Path.Combine(folder, name);
        File.WriteAllText(file, "@echo off\r\n" + string.Concat(lines.Select(line => $"echo {line}\r\n")));
        return file;
    }

    [Fact]
    public async Task A_named_file_that_answers_its_version_is_written_and_the_list_says_the_version_it_runs_at()
    {
        if (!OperatingSystem.IsWindows()) return;
        var gh = Stub("gh.cmd", "gh version 2.63.0 (2024-11-27)");
        var module = Module();

        var answered = await AnswerAsync(module, "TOOLS_USE", new { tool = "gh", action = "file", file = gh });
        Assert.Equal("2.63.0", answered.GetProperty("version").GetString());
        Assert.Equal(gh, Tools.Read(Home).Entries["gh"].File);

        var listed = (await AnswerAsync(module, "TOOLS_LIST", new { ask = true })).GetProperty("tools").EnumerateArray()
            .Single(tool => tool.GetProperty("tool").GetString() == "gh");
        Assert.Equal(gh, listed.GetProperty("resolved").GetString());
        Assert.Equal("2.63.0", listed.GetProperty("asked").GetString());
    }

    [Fact]
    public async Task A_git_switch_names_the_checkout_keys_the_git_it_would_run_reads_differently()
    {
        if (!OperatingSystem.IsWindows()) return;
        var now = Stub("git.cmd", "core.autocrlf=true", "core.symlinks=false", "core.fscache=true");
        var then = Stub("git-other.cmd", "core.autocrlf=input", "core.symlinks=false");
        Tools.UseFile(Home, "git", now);

        var answered = await AnswerAsync(Module(), "TOOLS_GIT", new { way = "file", file = then });

        Assert.Equal(now, answered.GetProperty("now").GetProperty("file").GetString());
        Assert.Equal(then, answered.GetProperty("then").GetProperty("file").GetString());
        var keys = answered.GetProperty("keys").EnumerateArray().ToList();
        Assert.Equal(["core.autocrlf"], keys.Select(key => key.GetProperty("key").GetString()!));
        Assert.Equal("true", keys[0].GetProperty("now").GetString());
        Assert.Equal("input", keys[0].GetProperty("then").GetString());
        Assert.False(answered.GetProperty("same").GetBoolean());
    }

    [Fact]
    public async Task A_git_switch_to_the_git_that_runs_now_changes_nothing()
    {
        if (!OperatingSystem.IsWindows()) return;
        var now = Stub("git.cmd", "core.autocrlf=true");
        Tools.UseFile(Home, "git", now);

        var answered = await AnswerAsync(Module(), "TOOLS_GIT", new { way = "file", file = now });

        Assert.True(answered.GetProperty("same").GetBoolean());
        Assert.Empty(answered.GetProperty("keys").EnumerateArray());
    }
}
