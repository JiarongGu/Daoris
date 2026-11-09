using System.Diagnostics;
using System.Text;

namespace Daoris.Driver;

/// <summary>
/// A git start as <see cref="GitScheduler"/> places it, read from the start built whole before it queues: its repository by the
/// common directory, what its run waits on, whether it may be ended, whether it writes, what it removes, and the key it shares by.
/// </summary>
internal static class GitStartAsks
{
    // The options of `worktree add` whose value is the next word.
    private static readonly HashSet<string> AddValued = new(StringComparer.Ordinal) { "-b", "-B", "--reason" };

    /// <summary>
    /// The ask of <paramref name="info"/>, git started with <paramref name="arguments"/> and <paramref name="input"/> by <paramref name="runner"/>
    /// under <paramref name="bound"/>; <paramref name="shares"/> where an identical start's answer may stand for its own.
    /// </summary>
    internal static GitRunAsk For(ProcessStartInfo info, IReadOnlyList<string> arguments, GitRunner runner, TimeSpan bound, bool shares, string? input = null)
    {
        var folder = Path.GetFullPath(string.IsNullOrEmpty(info.WorkingDirectory) ? "." : info.WorkingDirectory);
        var repository = RefFiles.CommonDirOf(folder) is { } common ? Path.GetFullPath(common) : folder;
        GitPlace.Known(info.FileName);
        var read = GitCommands.Read(folder, arguments, input);
        var writes = read.Class != GitClass.Read;
        var fetches = WaitOf(arguments, writes) is GitWait.Read && GitLazyFetch.Fetches(info, folder);
        return new GitRunAsk(folder, repository, fetches ? GitWait.Outside : WaitOf(arguments, writes))
        {
            Start = shares && runner == GitRunner.Whole ? Key(info, runner, bound) : null,
            Endable = runner != GitRunner.Held && !fetches && GitEndable.Of(arguments),
            Writes = writes,
            Removes = Removed(folder, arguments),
            Makes = Made(folder, arguments),
            Input = input,
            Command = read,
        };
    }

    /// <summary>What a git's run waits on: a remote command, and every write that may run a hook, wait outside Daoris.</summary>
    internal static GitWait WaitOf(IReadOnlyList<string> arguments, bool writes)
    {
        var command = GitLaunch.Command(arguments);
        if (command.Count == 0) return writes ? GitWait.Outside : GitWait.Read;
        if (command[0] == "ls-remote") return GitWait.Outside;
        if (!writes || GitCommands.IsLocal(command[0])) return GitWait.Read;
        return command is ["worktree", "remove" or "prune" or "lock" or "unlock" or "move", ..] ? GitWait.Read : GitWait.Outside;
    }

    /// <summary>The working tree a <c>worktree remove</c> removes, as a full path; null for every other git.</summary>
    internal static string? Removed(string folder, IReadOnlyList<string> arguments)
    {
        var command = GitLaunch.Command(arguments);
        if (command is not ["worktree", "remove", ..]) return null;
        var path = command.Skip(2).LastOrDefault(word => !word.StartsWith('-'));
        return path is null ? null : Path.GetFullPath(Path.Combine(folder, path));
    }

    /// <summary>The working tree a <c>worktree add</c> makes, its first word past the options, as a full path; null for every other git.</summary>
    internal static string? Made(string folder, IReadOnlyList<string> arguments)
    {
        var command = GitLaunch.Command(arguments);
        if (command is not ["worktree", "add", ..]) return null;
        for (var at = 2; at < command.Count; at++)
        {
            var word = command[at];
            if (word == "--") return at + 1 < command.Count ? Path.GetFullPath(Path.Combine(folder, command[at + 1])) : null;
            if (AddValued.Contains(word)) at++;
            else if (!word.StartsWith('-')) return Path.GetFullPath(Path.Combine(folder, word));
        }

        return null;
    }

    /// <summary>The whole start, as one key: the file, the folder, every argument as handed, every variable, the runner and the bound.</summary>
    private static string Key(ProcessStartInfo info, GitRunner runner, TimeSpan bound)
    {
        var key = new StringBuilder();
        key.Append(runner).Append('\0').Append(bound.Ticks).Append('\0').Append(info.FileName).Append('\0').Append(info.WorkingDirectory);
        foreach (var argument in info.ArgumentList) key.Append('\0').Append(argument);
        key.Append('\u0001');
        foreach (var (name, value) in info.Environment.OrderBy(each => each.Key, StringComparer.Ordinal)) key.Append('\0').Append(name).Append('=').Append(value);
        return key.ToString();
    }
}
