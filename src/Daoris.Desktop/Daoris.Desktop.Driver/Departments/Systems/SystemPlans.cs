using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The change sets a session's turns in a system place ended with, each kept whole as <c>plan-&lt;n&gt;.json</c> under
/// <c>&lt;home&gt;/work/plans/&lt;record&gt;/</c>, numbered from 1, with what its apply came to beside it as <c>applied-&lt;n&gt;.json</c>.
/// A plan is never rewritten: a stale one is followed by the next.
/// </summary>
public sealed class SystemPlans(string home)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private string FolderOf(string record) => DaorisHome.Entry(home, HomeEntry.Plans, record);

    /// <summary>Keep <paramref name="operations"/> as <paramref name="record"/>'s next plan for <paramref name="place"/>, and answer it.</summary>
    public SystemChangeSet Keep(string record, string place, IReadOnlyList<SystemOperation> operations, DateTimeOffset at)
    {
        var plan = new SystemChangeSet(place, (Latest(record)?.Plan ?? 0) + 1, operations, at);
        Directory.CreateDirectory(FolderOf(record));
        AtomicFile.WriteText(Path.Combine(FolderOf(record), $"plan-{plan.Plan}.json"), JsonSerializer.Serialize(plan, Json) + "\n");
        return plan;
    }

    /// <summary><paramref name="record"/>'s newest plan, or null where it has none.</summary>
    public SystemChangeSet? Latest(string record)
    {
        var folder = FolderOf(record);
        if (!Directory.Exists(folder)) return null;
        var newest = Directory.EnumerateFiles(folder, "plan-*.json").Select(file => (File: file, Number: NumberOf(file, "plan-"))).Where(each => each.Number > 0)
            .OrderByDescending(each => each.Number).FirstOrDefault();
        return newest.File is null ? null : JsonSerializer.Deserialize<SystemChangeSet>(AtomicFile.ReadText(newest.File), Json);
    }

    /// <summary>Keep what <paramref name="record"/>'s plan <paramref name="plan"/> came to when applied.</summary>
    public void Applied(string record, int plan, SystemApplied applied) =>
        AtomicFile.WriteText(Path.Combine(FolderOf(record), $"applied-{plan}.json"), JsonSerializer.Serialize(applied, Json) + "\n");

    /// <summary>What <paramref name="record"/>'s plan <paramref name="plan"/> came to, or null before its apply.</summary>
    public SystemApplied? AppliedOf(string record, int plan)
    {
        var file = Path.Combine(FolderOf(record), $"applied-{plan}.json");
        return File.Exists(file) ? JsonSerializer.Deserialize<SystemApplied>(AtomicFile.ReadText(file), Json) : null;
    }

    private static int NumberOf(string file, string prefix) =>
        Path.GetFileNameWithoutExtension(file) is { } name && name.StartsWith(prefix, StringComparison.Ordinal) && int.TryParse(name[prefix.Length..], out var number) ? number : 0;
}
