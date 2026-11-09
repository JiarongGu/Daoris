using System.Diagnostics;
using System.Text;
using Daoris.Driver;
using LibGit2Sharp;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A read asked of git and of the library, compared as values: git's exit code and its output as git printed it, never normalised.
/// The test's own git starts outside the product's door, past the checkout this suite runs in.
/// </summary>
internal static class GitLibraryParity
{
    private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    /// <summary>git in <paramref name="cwd"/>, with variables of its own and <paramref name="input"/> on its input where given.</summary>
    internal static async Task<(int Code, string Stdout)> RunAsync(
        string cwd, IReadOnlyDictionary<string, string>? environment, string? input, params string[] arguments)
    {
        var info = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = cwd,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = input is not null,
            StandardOutputEncoding = Utf8,
            StandardInputEncoding = input is null ? null : Utf8,
        };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        foreach (var (name, value) in environment ?? new Dictionary<string, string>()) info.Environment[name] = value;
        using var process = Process.Start(TestGit.Past(info))!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (input is not null)
        {
            await process.StandardInput.WriteAsync(input);
            process.StandardInput.Close();
        }

        await process.WaitForExitAsync();
        await stderr;
        return (process.ExitCode, await stdout);
    }

    /// <summary>The read answered by the library as git answers it, and by the product with no git started.</summary>
    internal static async Task SameAsync(string folder, params string[] arguments)
    {
        var git = await RunAsync(folder, null, null, arguments);
        var asked = string.Join(' ', arguments);
        var library = await GitLibraryReads.AnswerAsync(folder, arguments, CancellationToken.None);
        Assert.True(library is not null, $"the library answered no `{asked}`");
        Assert.Equal((git.Code, git.Stdout), (library!.Code, library.Stdout));
        await ProductAsync(folder, arguments, git, started: false);
    }

    /// <summary>The read refused by the library and answered by the product as git answers it, through one start.</summary>
    internal static async Task GitsAsync(string folder, params string[] arguments)
    {
        var git = await RunAsync(folder, null, null, arguments);
        Assert.Null(await GitLibraryReads.AnswerAsync(folder, arguments, CancellationToken.None));
        await ProductAsync(folder, arguments, git, started: true);
    }

    /// <summary>What the library would answer were its admission not asked; null where its call cannot.</summary>
    internal static GitLibraryAnswer? Unadmitted(string folder, params string[] arguments)
    {
        try
        {
            using var repository = new Repository(folder);
            return GitLibraryReads.Asked(arguments)!.Value.Answer(new GitLibraryCall(repository, RefFiles.GitDirOf(folder)!, RefFiles.CommonDirOf(folder)!));
        }
        catch (LibGit2SharpException)
        {
            return null;
        }
    }

    private static async Task ProductAsync(string folder, string[] arguments, (int Code, string Stdout) git, bool started)
    {
        using var count = GitLaunch.Count();
        var (code, stdout, _) = await WorkingTree.GitAsync(folder, arguments, CancellationToken.None);
        Assert.Equal(git, (code, stdout));
        Assert.True(count.Starts.Count == (started ? 1 : 0), $"`{string.Join(' ', arguments)}` started {count.Starts.Count} gits");
    }
}
