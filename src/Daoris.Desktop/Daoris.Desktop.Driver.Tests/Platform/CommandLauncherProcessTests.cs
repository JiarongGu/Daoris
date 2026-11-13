using System.Diagnostics;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// 🔴 The install's <c>daoris.exe</c> hands the desktop's program every argument whole (SAYLINES1). The command was a batch file, whose
/// <c>%*</c> ends at the first line break: <c>daoris session say &lt;id&gt; "several lines"</c> delivered the first line and said nothing
/// of the rest. The program here is the native fixture <see cref="DriverProgramWordsProcessTests.Echo"/> builds, laid out as the desktop's
/// program is, and the command is the build of <c>Daoris.Desktop.Command</c> laid out in an install's <c>app/bin/</c>, started as a terminal
/// starts <c>daoris</c>: the program reads its words back by .NET's own reading of its command line, and the command's exit is its own.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
public sealed class CommandLauncherProcessTests(DriverProgramWordsProcessTests.Echo echo) : IClassFixture<DriverProgramWordsProcessTests.Echo>, IDisposable
{
    private readonly string _install = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "command-launcher", Guid.NewGuid().ToString("N")[..8]);

    public void Dispose() => GitSeed.Discard(_install);

    public static TheoryData<string, string[]> Words => new()
    {
        { "session say", ["session", "say", "s-1", "first line\nsecond line\r\nthird line", "--level", "urgent"] },
        { "ask with a requirement", ["ask", "--to", "engine", "do it\nand say so", "--require", "merge it\nonce green", "it is merged\nto main"] },
        { "quest done", ["quest", "done", "q1", "--note", "closed\nby hand"] },
        { "session reword", ["session", "reword", "s-1", "go-ahead", "not yet:\nthe steps first"] },
        { "session helper", ["session", "helper", "s-1", "which of the two?\nand why", "--to-me"] },
        { "a blank line and a final break", ["session", "say", "s-1", "one\n\ntwo\n"] },
        { "the characters a command line quotes", ["session", "say", "s-1", "a \"quoted\" word", "ends\\", "", "R&D %PATH% ^caret|pipe", "道衍\n言"] },
    };

    [Theory]
    [MemberData(nameof(Words))]
    public void Every_argument_reaches_the_program_whole_with_its_line_breaks(string verb, string[] words)
    {
        if (!OperatingSystem.IsWindows()) return;
        var command = Lay(withProgram: true);

        var (code, said, _) = Run(command, words, exit: null);

        Assert.True(code == 0, $"{verb}: {said}");
        Assert.Equal(words, JsonSerializer.Deserialize<Echoed>(said, new JsonSerializerOptions(JsonSerializerDefaults.Web))!.Args);
    }

    [Fact]
    public void The_programs_exit_is_the_commands()
    {
        if (!OperatingSystem.IsWindows()) return;

        var (code, _, _) = Run(Lay(withProgram: true), ["session", "say", "s-1", "x"], exit: "7");

        Assert.Equal(7, code);
    }

    [Fact]
    public void An_install_without_its_program_says_where_it_looked_and_ends_with_two()
    {
        if (!OperatingSystem.IsWindows()) return;

        var (code, _, error) = Run(Lay(withProgram: false), ["session", "say", "s-1", "x"], exit: null);

        Assert.Equal(CommandLauncher.NoProgram, code);
        Assert.Contains(Path.Combine("app", "daoris-driver", "daoris-driver.exe"), error);
    }

    private sealed record Echoed(string[] Args);

    /// <summary>The command's build in <c>app/bin/</c>, and the program the fixture built in <c>app/daoris-driver/</c> where asked; the command's path.</summary>
    private string Lay(bool withProgram)
    {
        var bin = Path.Combine(_install, "app", "bin");
        Directory.CreateDirectory(bin);
        var built = Path.GetDirectoryName(Built())!;
        foreach (var file in Directory.EnumerateFiles(built)) File.Copy(file, Path.Combine(bin, Path.GetFileName(file)));
        if (withProgram)
        {
            var program = Path.Combine(_install, "app", "daoris-driver");
            Directory.CreateDirectory(program);
            var fixture = Path.GetDirectoryName(echo.Program)!;
            foreach (var file in Directory.EnumerateFiles(fixture)) File.Copy(file, Path.Combine(program, Path.GetFileName(file)));
        }

        return Path.Combine(bin, CommandLauncher.Program);
    }

    /// <summary>The command's build beside this test's own: built before these tests, and never referenced.</summary>
    private static string Built()
    {
        var build = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var desktop = build.Parent!.Parent!.Parent!.Parent!.FullName;
        var built = Path.Combine(desktop, "Daoris.Desktop.Command", "bin", build.Parent.Name, build.Name, CommandLauncher.Program);
        Assert.True(File.Exists(built), "the command is built before these tests: " + built);
        return built;
    }

    private static (int Code, string Out, string Error) Run(string command, IEnumerable<string> words, string? exit)
    {
        var info = new ProcessStartInfo(command) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var word in words) info.ArgumentList.Add(word);
        info.Environment["ECHO_EXIT"] = exit;
        using var process = Process.Start(info)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        Assert.True(process.WaitForExit(TimeSpan.FromMinutes(3)), $"{command} did not end");
        return (process.ExitCode, output.Result, error.Result);
    }
}
