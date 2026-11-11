using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>
/// The system configuration file each git in use names, asked of it once (<c>git var GIT_CONFIG_SYSTEM</c>), since a git names one place
/// for its life. Only an answer is kept: a start that did not run (a timeout, a stalled repository, a git that would not start) is
/// asked again at the next stamp, so a failed first ask never leaves the file unstamped for the process.
/// </summary>
internal sealed class GitSystemFiles(Func<string, CancellationToken, Task<(int Code, string Stdout)>> ask)
{
    private readonly ConcurrentDictionary<string, string> _files = new(StringComparer.Ordinal);

    /// <summary>The ask the product makes: git's own, placed as every git is.</summary>
    internal static GitSystemFiles Shared { get; } = new(async (folder, ct) =>
    {
        var (code, stdout, _) = await WorkingTree.GitAsync(folder, ["var", "GIT_CONFIG_SYSTEM"], ct).ConfigureAwait(false);
        return (code, stdout);
    });

    /// <summary>The system file <paramref name="git"/> named, empty where it names none; null where it has not been asked yet or did not answer.</summary>
    internal string? Of(string git) => _files.GetValueOrDefault(git);

    /// <summary>Asks <paramref name="git"/> for its system file from <paramref name="folder"/> unless it answered already.</summary>
    internal async Task LearnAsync(string git, string folder, CancellationToken ct)
    {
        if (_files.ContainsKey(git)) return;
        var (code, stdout) = await ask(folder, ct).ConfigureAwait(false);
        if (code < 0) return;
        _files[git] = code == 0 && stdout.Trim() is { Length: > 0 } named ? Path.GetFullPath(named) : "";
    }
}
