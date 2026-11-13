using Daoris.Desktop.Driver.Tests;
using Daoris.Driver;
using Daoris.Ports.Git;
using Daoris.Testing;

namespace Daoris.Desktop.Driver.Rehearsals;

/// <summary>
/// The git port's contract on real git, the side <c>ScriptedGit</c>'s twin is held to: the driver's own runs of git as this machine's
/// processes, which the roots hand, placed, bounded and ended as every git of Daoris's is. Real git runs here, so the class is the
/// rehearsal tier's and never a unit half's.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Git)]
public sealed class GitProcessRunsContractTests : GitContract
{
    protected override string Folder => AppContext.BaseDirectory;

    protected override IGit Backend(params Script[] script) => GitProcessRuns.Shared;
}
