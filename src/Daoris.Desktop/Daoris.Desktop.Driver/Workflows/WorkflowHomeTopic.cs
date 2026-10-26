using System.Runtime.CompilerServices;

namespace Daoris.Driver;

/// <summary>
/// The workflows' answer to where the <see cref="ChangeTopics.Gates"/> topic's files are (FOUNDATION1 mod-vii, the design §14.3): the second
/// opinions' records and the workflows, whose runs are listed by the names the due entries' gates read (FABRIC-END7b,
/// <see cref="LandingJudged.Runs"/>), so the deadline's listing grows with the due list, never with every run there ever was. Installed when
/// the driver's assembly loads, before any watch is built.
/// </summary>
internal static class WorkflowHomeTopic
{
    [ModuleInitializer]
    internal static void Install() => HomeTopics.Answer(
        ChangeTopics.Gates,
        home =>
        [
            HomePart.Records(DaorisHome.Entry(home, HomeEntry.OpinionGates), subfolders: false),
            Workflows(WorkflowStore.FolderOf(home), () => LandingJudged.Runs(home)),
        ]);

    /// <summary>
    /// The workflows (FABRIC-END7, FABRIC-END7b): each version's file by its stamp, and of the runs' bindings under <c>runs/</c>, kept one
    /// per run for good, only those <paramref name="runs"/> names, by name: the runs a due entry's gate read, which alone can release a
    /// landing. Every <c>*.json</c> under the folder is heard, and its judge reads the same.
    /// </summary>
    internal static HomePart Workflows(string folder, Func<IEnumerable<string>> runs)
    {
        var full = Path.GetFullPath(folder);
        return HomePart.Records(full, subfolders: true) with
        {
            Stamps = () => Directory.Exists(full)
                ? Directory.EnumerateFiles(full, "*.json", SearchOption.TopDirectoryOnly).Select(file => KeyValuePair.Create(file, HomeWrites.Stamp(file)))
                : [],
            ByName = () => runs().Where(WorkflowRunBindings.IsRunId).Select(run => Path.Combine(full, WorkflowRunBindings.Folder, run + ".json")),
        };
    }
}
