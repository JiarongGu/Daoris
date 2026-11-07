namespace Daoris.Driver;

/// <summary>
/// The session's own git on a spawn: the tokens it inherited cleared, its gh and az configuration
/// folders, its commits' author and its long paths. Declared by the shell and implemented by Workplace, which owns those names, so the
/// shell names no type of Workplace.
/// </summary>
public interface ISessionGit
{
    /// <summary>Hands <paramref name="environment"/> the session's git, after the tools' environment is on it.</summary>
    void Hand(IDictionary<string, string?> environment, string? home, string root);

    /// <summary>The variables it sets by a fixed name.</summary>
    IReadOnlyList<string> Names { get; }

    /// <summary>The prefixes of the numbered families it sets (<c>GIT_CONFIG_KEY_n</c>), which no fixed list can hold.</summary>
    IReadOnlyList<string> Families { get; }

    /// <summary>The session's tokens, which it only clears: a provider may declare one as its key, set after the clear.</summary>
    IReadOnlyList<string> Tokens { get; }
}

/// <summary>The session's git installed when the driver loads (Workplace installs it, an unset port throws).</summary>
public static class SessionGit
{
    private static ISessionGit? _current;

    public static ISessionGit Current
    {
        get => _current ?? throw new InvalidOperationException("no session git is installed: Workplace installs it when the driver loads");
        set => _current = value;
    }
}
