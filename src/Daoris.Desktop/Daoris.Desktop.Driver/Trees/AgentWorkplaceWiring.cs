using System.Runtime.CompilerServices;

namespace Daoris.Driver;

internal sealed class WorkingAgentWorkplace : IAgentWorkplace
{
    [ModuleInitializer]
    internal static void Install() => AgentWorkplace.Current = new WorkingAgentWorkplace();

    public string? ProcessWords(IStartCommits? commits, IStartLanding? plan) => Daoris.Driver.ProcessWords.Said(commits, plan);

    public void HandGit(IDictionary<string, string?> environment, string? home, string root)
    {
        // GITAUTH1b (the git-auth design §4.1): a session's gh or az acts with no token the desktop inherited.
        GitWiring.Untoken(environment);
        // GITAUTH1c (the git-auth design §3.6): a session's commits carry origin's identity's author where its repository names none,
        // read at the start. Off the caller's thread: a home where no identity commits as anyone starts no git for it.
        GitAuthor.Hand(environment, Task.Run(() => GitAuthor.OfAsync(home ?? DaorisHome.Resolve(), root, CancellationToken.None)).GetAwaiter().GetResult());
        // TREELONG1: a session's own git reads its tree with long paths, as Daoris's own calls always do (D109). Handed always, with no
        // read of the person's configuration: that read started a git at every start, which a help start must never (HelpStartReadTests).
        GitLongPaths.Hand(environment, true);
    }
}
