using System.Diagnostics;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// One answer for Daoris and every child, started for real (TOOLS5, D121 §2.4; the design's §7 proof): with git set
/// to a file the person names, Daoris's own git, a hook's child and a session's child each run THAT file when they
/// say <c>git</c>. The file is a stub that answers with its own name, so the answer is the proof of which git ran.
/// </summary>
/// <remarks>
/// The fast half holds the shapes (<see cref="ToolsChildrenTests"/>); this holds that a real child's own lookup, by
/// its own shell, lands on the tools' <c>PATH</c>. A session's start reads the home from this process's
/// <c>DAORIS_HOME</c>, D63's one seam, so that case sets it for its duration — in the process-PATH collection, kept
/// apart from the classes that change this process's environment.
/// </remarks>
[Collection(ProcessPath.Name)]
[Trait(Category.Name, Category.Process)]
public sealed class ToolsChildProcessTests : IDisposable
{
    private const string Answer = "stub-git";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "daoris-tools-child-" + Guid.NewGuid().ToString("N")[..8]);

    private string Home => Path.Combine(_root, "data");

    private readonly string _git;

    public ToolsChildProcessTests()
    {
        Directory.CreateDirectory(Home);
        var folder = Path.Combine(_root, "stub-git");
        Directory.CreateDirectory(folder);
        if (OperatingSystem.IsWindows())
        {
            _git = Path.Combine(folder, "git.cmd");
            File.WriteAllText(_git, $"@echo {Answer} %*\r\n");
        }
        else
        {
            _git = Path.Combine(folder, "git");
            File.WriteAllText(_git, $"#!/bin/sh\necho \"{Answer} $*\"\n");
            File.SetUnixFileMode(_git, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        File.WriteAllText(Path.Combine(Home, Tools.FileName), """{"tools":{"git":{"use":"file","file":""" + JsonSerializer.Serialize(_git) + "}}}");
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>A node script that asks its own shell for <c>git --version</c> and writes the answer to the file it is given.</summary>
    private string Probe()
    {
        var probe = Path.Combine(_root, "probe.mjs");
        File.WriteAllText(probe,
            "import { execSync } from 'node:child_process';\n"
            + "import { writeFileSync } from 'node:fs';\n"
            + "writeFileSync(process.argv[2], execSync('git --version', { encoding: 'utf8' }));\n");
        return probe;
    }

    private static async Task<string> RunAsync(ProcessStartInfo info, string? marker = null)
    {
        using var process = Process.Start(info) ?? throw new InvalidOperationException("the child did not start");
        if (info.RedirectStandardInput) process.StandardInput.Close();
        var stdout = info.RedirectStandardOutput ? process.StandardOutput.ReadToEndAsync() : Task.FromResult("");
        var stderr = info.RedirectStandardError ? process.StandardError.ReadToEndAsync() : Task.FromResult("");
        using var bound = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await process.WaitForExitAsync(bound.Token);
        var said = await stdout + await stderr;
        return marker is null ? said : File.Exists(marker) ? File.ReadAllText(marker) : $"(no answer; the child said: {said})";
    }

    [Fact]
    public async Task Daoris_own_git_runs_the_file_the_person_named()
    {
        var (info, refusal) = WorkingTree.GitStart(_root, ["--version"], Home);
        Assert.Null(refusal);

        var said = await RunAsync(info!);

        Assert.Contains($"{Answer} -c core.longpaths=true --version", said);
    }

    [Fact]
    public async Task A_hooks_own_git_is_the_file_the_person_named()
    {
        var marker = Path.Combine(_root, "hook.txt");
        var plugin = new PluginEntry(
            new PluginManifest("probe", 1, "probe", "1.0.0", "", [], new PluginHooks(["node", Probe(), marker], ["quest/consider"]), []),
            _root, Path.Combine(_root, ".data"), Enabled: true, Problem: null);

        var said = await RunAsync(HookProcess.StartInfo(plugin, Home), marker);

        Assert.StartsWith($"{Answer} --version", said.Trim());
    }

    [Fact]
    public async Task A_sessions_own_git_is_the_file_the_person_named()
    {
        var marker = Path.Combine(_root, "session.txt");
        var target = new SessionTarget("q1", "Title", "Body", "Asker", "engine", _root, "http://localhost:5177");
        var saved = Environment.GetEnvironmentVariable(DaorisHome.Variable);
        ProcessStartInfo info;
        Environment.SetEnvironmentVariable(DaorisHome.Variable, Home);
        try
        {
            info = new StubAdapter().Prepare(target, ["node", Probe(), marker]);
        }
        finally
        {
            Environment.SetEnvironmentVariable(DaorisHome.Variable, saved);
        }

        var said = await RunAsync(info, marker);

        Assert.StartsWith($"{Answer} --version", said.Trim());
    }
}
