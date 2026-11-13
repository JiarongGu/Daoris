using System.Diagnostics;
using System.Text;
using Daoris.Ports.Git;
using static Daoris.Driver.WorkingTree;

namespace Daoris.Driver;

/// <summary>
/// How Daoris's own git starts, and the three runners every git is asked through: each answers from this flow's memos where one
/// holds and asks the git port the root chose (<see cref="MachinePorts.Git"/>) for the rest; <see cref="WorkingTree"/> is the door.
/// </summary>
internal static class GitRuns
{
    /// <summary>
    /// How Daoris's own git starts: the file Tools resolves for git, by its whole path, with the tools' environment, or the refusal
    /// of a git the person chose that cannot run, which never falls back to <c>PATH</c>.
    /// </summary>
    /// <param name="home">The Daoris home whose <c>tools.json</c> says which git; null is a machine with no home, whose git is the system's.</param>
    internal static (ProcessStartInfo? Info, string? Refusal) GitStart(string root, IReadOnlyList<string> arguments, string? home)
    {
        var read = home is null ? null : Tools.Read(home);
        var git = read is null
            ? new ToolResolution("git", ToolWay.System, null, CommandPresence.Resolve("git", startable: true), false, null)
            : Tools.Resolve(read, home!, "git");
        if (git.Refused) return (null, git.Problem);

        var info = new ProcessStartInfo
        {
            // The system's git PATH does not find keeps the bare name, so the start fails in the system's own words.
            FileName = git.File ?? "git",
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true, // git runs every tick; from a window it must not flash a console (Adapters.Shell)
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        // 🔴 Long paths, for every call: a session tree's prefix is longer than its root's, so a file that fits under the root can pass
        // Windows' 260 characters in a tree, and git then reads committed files as changes it cannot read. Said on the command line, so
        // nothing in the repository's own configuration changes.
        info.ArgumentList.Add("-c");
        info.ArgumentList.Add("core.longpaths=true");
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        if (read is not null) Tools.Hand(info, read, home!);
        else GitWiring.Hand(info); // with no home there is no tools' file, and still nothing may prompt
        // Git for Windows' launcher, the system's or a managed one, starts as the git behind it with the launcher's variables, one
        // process rather than two. A git the person named as a file starts as it is.
        if (git.Way is ToolWay.System or ToolWay.Managed) GitForWindows.Direct(info);
        return (info, null);
    }

    /// <summary>
    /// The git Daoris's own git runs, as a held remote names it: its file and when that file is last written, so a git chosen in Tools or
    /// updated in place is another git. Null where no git resolves.
    /// </summary>
    internal static string? GitInUse(string? home)
    {
        var read = home is null ? null : Tools.Read(home);
        var git = read is null
            ? new ToolResolution("git", ToolWay.System, null, CommandPresence.Resolve("git", startable: true), false, null)
            : Tools.Resolve(read, home!, "git");
        if (git.Refused || git.File is not { } path) return null;
        var file = new FileInfo(path);
        return file.Exists ? $"{file.FullName}|{file.LastWriteTimeUtc.Ticks}" : file.FullName;
    }

    /// <summary>One git as the port is asked it: in the program's home, never the variable the process inherited.</summary>
    private static Ports.Git.GitAsk AskOf(
        string root, IReadOnlyList<string> arguments, TimeSpan bound, string? input = null, IReadOnlyDictionary<string, string>? environment = null)
    {
        var writes = GitCommands.Writes(arguments);
        return new(root, arguments, GitStartAsks.WaitOf(arguments, writes), writes, input, bound, ProgramHome.Named) { Environment = environment };
    }

    internal static Task<(int Code, string Stdout, string Stderr)> GitAsync(
        string root, IReadOnlyList<string> arguments, CancellationToken ct) =>
        GitAsync(root, arguments, environment: null, timeout: null, ct);

    /// <summary>git with variables of its own and a bound: a fetch that waits on a credential nobody is there to type must end.</summary>
    internal static Task<(int Code, string Stdout, string Stderr)> GitAsync(
        string root, IReadOnlyList<string> arguments, IReadOnlyDictionary<string, string>? environment, TimeSpan? timeout,
        CancellationToken ct) =>
        GitAsync(root, arguments, environment, timeout, input: null, ct);

    /// <summary>git reading <paramref name="input"/>, BOM-less UTF-8: <c>update-ref --stdin</c> checks a delete and its proof together.</summary>
    internal static Task<(int Code, string Stdout, string Stderr)> GitAsync(
        string root, IReadOnlyList<string> arguments, string input, CancellationToken ct) =>
        GitAsync(root, arguments, environment: null, timeout: null, input, ct);

    /// <summary>git with variables of its own reading <paramref name="input"/>: an index named by <c>GIT_INDEX_FILE</c>, filled.</summary>
    internal static Task<(int Code, string Stdout, string Stderr)> GitAsync(
        string root, IReadOnlyList<string> arguments, IReadOnlyDictionary<string, string> environment, string input, CancellationToken ct) =>
        GitAsync(root, arguments, environment, timeout: null, input, ct);

    private static Task<(int Code, string Stdout, string Stderr)> GitAsync(
        string root, IReadOnlyList<string> arguments, IReadOnlyDictionary<string, string>? environment, TimeSpan? timeout,
        string? input, CancellationToken ct)
    {
        // Within a landing or a judgement a read is asked of git once; a call with a variable, a bound or an input of its own never is.
        Func<CancellationToken, Task<(int Code, string Stdout, string Stderr)>> run = token => RunGitAsync(root, arguments, environment, timeout, input, token);
        if (environment is not null || timeout is not null || input is not null) return run(ct);
        return GitFacts.Asked(root, arguments, run, ct) ?? RefSnapshot.Asked(root, arguments, run, ct) ?? GitReadOnce.Asked(root, arguments, run, ct) ?? run(ct);
    }

    private static async Task<(int Code, string Stdout, string Stderr)> RunGitAsync(
        string root, IReadOnlyList<string> arguments, IReadOnlyDictionary<string, string>? environment, TimeSpan? timeout,
        string? input, CancellationToken ct)
    {
        // A git that may move a ref drops what this flow is told, before it starts as well as after it ends.
        GitFacts.Ran(arguments);
        try
        {
            var answer = await MachinePorts.Git.RunAsync(AskOf(root, arguments, GitBounds.Of(arguments, timeout), input, environment), ct).ConfigureAwait(false);
            return (answer.Exit ?? -1, answer.Output, answer.Error);
        }
        finally
        {
            RefSnapshot.Ran(arguments);
            GitFacts.Ran(arguments);
        }
    }

    /// <summary>The first <paramref name="limit"/> bytes git writes, a blob that may be binary; null where git failed or did not start.</summary>
    internal static Task<(byte[] Bytes, int Count)?> GitBytesAsync(
        string root, IReadOnlyList<string> arguments, int limit, CancellationToken ct) =>
        GitBytesAsync(root, arguments, limit, input: null, ct);

    internal static Task<(byte[] Bytes, int Count)?> GitBytesAsync(
        string root, IReadOnlyList<string> arguments, int limit, string? input, CancellationToken ct) =>
        BytesAsync(MachinePorts.Git.ReadBytesAsync(AskOf(root, arguments, GitBounds.Of(arguments), input), limit, ct));

    private static async Task<(byte[] Bytes, int Count)?> BytesAsync(Task<GitBytes> reading) =>
        await reading.ConfigureAwait(false) is { Read: true } bytes ? (bytes.Content, bytes.Content.Length) : null;

    /// <summary>
    /// git read as it answers, each piece handed to <paramref name="take"/> until it answers false: 0 then, git's code otherwise, -1
    /// where git did not start or could not be read. A range's patch has no upper size.
    /// </summary>
    internal static Task<int> ReadGitAsync(
        string root, IReadOnlyList<string> arguments, Func<ReadOnlyMemory<char>, bool> take, CancellationToken ct) =>
        GitReadOnce.Streamed(root, arguments, take, ct) ?? ReadGitAsync(root, arguments, environment: null, take, ct);

    internal static Task<int> ReadGitAsync(
        string root, IReadOnlyList<string> arguments, IReadOnlyDictionary<string, string>? environment, Func<ReadOnlyMemory<char>, bool> take,
        CancellationToken ct) =>
        ExitAsync(MachinePorts.Git.ReadAsync(AskOf(root, arguments, GitBounds.Of(arguments), environment: environment), take, ct));

    private static async Task<int> ExitAsync(Task<int?> reading) => await reading.ConfigureAwait(false) ?? -1;
}
