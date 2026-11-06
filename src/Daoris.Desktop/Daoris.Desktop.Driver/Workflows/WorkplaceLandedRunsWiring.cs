using System.Runtime.CompilerServices;

namespace Daoris.Driver;

/// <summary>The Workflows' side of the workplace's history clear: the verification runs it may take, read from the runs themselves.</summary>
internal sealed class WorkplaceLandedRunsOperations : IWorkplaceLandedRuns
{
    [ModuleInitializer]
    internal static void Install() => WorkplaceLandedRuns.Current = new WorkplaceLandedRunsOperations();

    public IReadOnlyList<(string Path, bool Folder)> Left(string home) => LandedRunsLeft.Of(home);
}
