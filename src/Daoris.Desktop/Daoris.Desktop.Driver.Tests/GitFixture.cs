using System.Diagnostics;
using System.Text;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// git for a test's fixture (TESTGIT1): the runner the driver's test classes share, so each reads git's two streams the
/// one way that cannot wait forever.
/// </summary>
/// <remarks>
/// 🔴 Both reads start before anything is written to git or waited on. A helper that read stdout to its end and only then
/// stderr waited on git forever once git wrote more to stderr than its pipe holds: `add -A` where line endings convert
/// writes a warning per file, and one such helper held the merge's real-process half for 1 h 33 m (FIX-LOG 2026-10-04).
/// Classes kept their own copies of that read, and a copy is where it comes back, so a class calls this instead of keeping
/// one.
///
/// Both streams are read as UTF-8, which is what git writes. Nothing here judges the exit: a step whose failure matters
/// asserts on <see cref="GitRun.ExitCode"/> itself.
/// </remarks>
internal static class GitFixture
{
    private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    /// <summary>git in <paramref name="cwd"/>, and its stdout: the shape of a fixture step that only wants the answer.</summary>
    public static async Task<string> GitAsync(string cwd, params string[] arguments) =>
        (await RunAsync(cwd, arguments).ConfigureAwait(false)).Stdout;

    /// <summary>git in <paramref name="cwd"/>: what it printed on each stream, and how it exited.</summary>
    public static Task<GitRun> RunAsync(string cwd, params string[] arguments) => RunWithAsync(cwd, null, null, arguments);

    /// <summary>The same, with variables set for git, and <paramref name="input"/> written to its stdin when given.</summary>
    public static Task<GitRun> RunWithAsync(
        string cwd, IReadOnlyDictionary<string, string>? environment, string? input, params string[] arguments)
    {
        var info = new ProcessStartInfo("git") { WorkingDirectory = cwd };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        if (environment is not null)
        {
            foreach (var (name, value) in environment) info.Environment[name] = value;
        }

        return StartAsync(info, input);
    }

    /// <summary>
    /// git with its arguments as one command line, for <see cref="GitTree"/>, whose callers write them that way and wait
    /// for the answer. Blocking on it is safe: nothing below resumes on the caller's context.
    /// </summary>
    public static GitRun RunLine(string cwd, string commandLine) =>
        StartAsync(new ProcessStartInfo("git", commandLine) { WorkingDirectory = cwd }, null).GetAwaiter().GetResult();

    private static async Task<GitRun> StartAsync(ProcessStartInfo info, string? input)
    {
        info.UseShellExecute = false;
        info.RedirectStandardOutput = true;
        info.RedirectStandardError = true;
        info.StandardOutputEncoding = Utf8;
        info.StandardErrorEncoding = Utf8;
        if (input is not null)
        {
            info.RedirectStandardInput = true;
            info.StandardInputEncoding = Utf8;
        }

        using var process = Process.Start(info) ?? throw new InvalidOperationException($"git did not start in {info.WorkingDirectory}");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (input is not null)
        {
            await process.StandardInput.WriteAsync(input).ConfigureAwait(false);
            process.StandardInput.Close();
        }

        await process.WaitForExitAsync().ConfigureAwait(false);
        return new GitRun(await stdout.ConfigureAwait(false), process.ExitCode, await stderr.ConfigureAwait(false));
    }
}

/// <summary>What one git run printed on each stream, and how it exited.</summary>
internal sealed record GitRun(string Stdout, int ExitCode, string Stderr);
