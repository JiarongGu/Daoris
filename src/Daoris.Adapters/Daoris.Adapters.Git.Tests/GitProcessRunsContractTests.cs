using Daoris.Adapters.Machine;
using Daoris.Ports.Git;
using Daoris.Testing;

namespace Daoris.Adapters.Git.Tests;

/// <summary>
/// The git port's contract on real git, the side <c>ScriptedGit</c>'s twin is held to: the adapter's runs of git through the machine's
/// process host, under a policy that resolves the git on the path and places nothing. Real git runs here, so the class is the
/// dependency tier's and never a unit half's.
/// </summary>
public sealed class GitProcessRunsContractTests : GitContract
{
    protected override string Folder => AppContext.BaseDirectory;

    protected override IGit Backend(params Script[] script) =>
        new GitProcessRuns(new MachineProcessHost(new StartPlaces(4, 3), (_, _) => null), new PlainGitPolicy());
}
