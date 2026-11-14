using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The run ends the service could not take, kept under the home one file a run until it takes them: a run's end is written once on its
/// record, and its quests' Done and its sessions' end are owed until the service answers, so none is lost to a host down at the time.
/// </summary>
public sealed class OwedRunEnds(string home)
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <summary>The folder the owed ends sit in, beside the node records.</summary>
    public string Folder => DaorisHome.Entry(home, HomeEntry.Workflows, "run-ends-owed");

    /// <summary>Whether any end is owed: one stat, so a home that owes none reads nothing.</summary>
    public bool Any => Directory.Exists(Folder) && Directory.EnumerateFiles(Folder, "*.json").Any();

    /// <summary>Keeps <paramref name="end"/> owed, replacing an earlier one of its run.</summary>
    public void Owe(RunFinish end)
    {
        Directory.CreateDirectory(Folder);
        AtomicFile.WriteText(PathOf(end.Run), JsonSerializer.Serialize(end, Options));
    }

    /// <summary>The run's end is taken: nothing of it is owed any more.</summary>
    public void Paid(string run)
    {
        if (File.Exists(PathOf(run))) File.Delete(PathOf(run));
    }

    /// <summary>The ends owed, oldest written first; a file that does not read is left for a person and skipped.</summary>
    public IReadOnlyList<RunFinish> Read()
    {
        if (!Directory.Exists(Folder)) return [];
        return [.. new DirectoryInfo(Folder).EnumerateFiles("*.json").OrderBy(each => each.LastWriteTimeUtc)
            .Select(file => ReadOne(file.FullName)).OfType<RunFinish>()];
    }

    private static RunFinish? ReadOne(string path)
    {
        try
        {
            return JsonSerializer.Deserialize<RunFinish>(AtomicFile.ReadText(path), Options) is { Run.Length: > 0 } end ? end : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    private string PathOf(string run) => Path.Combine(Folder, WorkplaceRunFiles.UnitKey(run) + ".json");
}
