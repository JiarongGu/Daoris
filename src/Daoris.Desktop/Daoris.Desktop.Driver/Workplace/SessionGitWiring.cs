using System.Runtime.CompilerServices;

namespace Daoris.Driver;

/// <summary>The session's git as Workplace hands it to every spawn the transports' shell completes (<see cref="ISessionGit"/>).</summary>
internal sealed class WorkingSessionGit : ISessionGit
{
    [ModuleInitializer]
    internal static void Install() => SessionGit.Current = new WorkingSessionGit();

    public IReadOnlyList<string> Names { get; } = [GitSessionHolds.GhFolder, GitSessionHolds.AzFolder, "GIT_CONFIG_COUNT"];

    public IReadOnlyList<string> Families { get; } = ["GIT_CONFIG_KEY_", "GIT_CONFIG_VALUE_"];

    public IReadOnlyList<string> Tokens => GitWiring.SessionTokens;

    public void Hand(IDictionary<string, string?> environment, string? home, string root)
    {
        // A session's gh or az acts with no token the desktop inherited.
        GitWiring.Untoken(environment);
        // Nor does it reach the person's sign-ins: its gh and az configuration folders are empty ones of Daoris's.
        GitSessionHolds.Hand(environment, home ?? DaorisHome.Resolve());
        // A session's commits carry origin's identity's author where its repository names none,
        // read at the start. Off the caller's thread: a home where no identity commits as anyone starts no git for it.
        GitAuthor.Hand(environment, Task.Run(() => GitAuthor.OfAsync(home ?? DaorisHome.Resolve(), root, CancellationToken.None)).GetAwaiter().GetResult());
        // A session's own git reads its tree with long paths, as Daoris's own calls always do. Handed always, with no
        // read of the person's configuration: that read started a git at every start, which a help start must never (HelpStartReadTests).
        GitLongPaths.Hand(environment, true);
    }
}
