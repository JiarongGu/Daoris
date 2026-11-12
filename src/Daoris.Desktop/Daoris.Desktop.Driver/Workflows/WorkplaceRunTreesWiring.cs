using System.Runtime.CompilerServices;

namespace Daoris.Driver;

/// <summary>The Workflows' side of the workplace's sweep of run-owned trees: a run is over for its tree once its record says so.</summary>
internal sealed class WorkplaceRunTreesOperations : IWorkplaceRunTrees
{
    [ModuleInitializer]
    internal static void Install() => WorkplaceRunTrees.Current = new WorkplaceRunTreesOperations();

    public bool Due(string home, string run)
    {
        var unit = RunUnits.IdOf(run);
        return !File.Exists(NodeRecordFiles.PathOf(home, unit)) || (new NodeRecordFiles(home).Read(unit) is { } record && StepRefs.Releases(record));
    }
}
