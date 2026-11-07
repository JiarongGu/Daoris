using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// The supervisors' records in the shape this build reads: every stored supervisor record, in <c>roles</c> and each revision in
/// <c>role_revisions</c>, has its bare <c>answer</c> granted as <c>question.answer</c> with <c>course.go-on</c> and <c>limit.move</c>
/// beside it, the three decisions a terminal made before grants were read, and <c>defer</c> dropped, since leaving needs no grant. Every
/// supervisor decision file gains its <c>kind</c>: <c>question</c> where it names a question, else <c>course</c>.
/// </summary>
/// <remarks>
/// An older build reads the new shape wrongly (it takes any <c>authority</c>, ignores <c>decides</c> and reads no grant), so before it
/// converts anything the step keeps this build's version as the home's newest build, the record the launcher refuses an older build by,
/// and reads it back; a write or read-back that fails throws, the step stays unrecorded, and the role door writes no supervisor record
/// until it is (<see cref="SupervisorConversion"/>).
/// </remarks>
public sealed class SupervisorStep(Func<string?>? running = null) : IHomeMigrationStep
{
    public const string StepName = SupervisorConversion.Step;

    private const string Answer = "answer";
    private const string Defer = "defer";

    public string Name => StepName;

    public void Run(HomeMigrationRun run)
    {
        KeepNewestBuild(run.Home, (running ?? (() => UpdateRefusal.Running))());
        ConvertStore(run);
        ConvertDecisions(run);
    }

    private static void KeepNewestBuild(string home, string? version)
    {
        // A build naming no numbered version has none an older build is refused by, so there is nothing to keep.
        if (HomeLastBuild.Numbered(version) is not { } built) return;
        HomeLastBuild.Keep(home, version);
        if (HomeLastBuild.Numbered(HomeLastBuild.Read(home)) is not { } kept || kept < built)
        {
            throw new DriverException($"the home's newest build was not kept as {version}, so its supervisors are not converted until it is.");
        }
    }

    private static void ConvertStore(HomeMigrationRun run)
    {
        var path = DesktopStore.LocationIn(run.Home);
        List<(string Id, long? Revision, string Record)> converted = [];
        using (var read = DesktopStoreReader.Open(path))
        {
            if (read is null || read.Version < RoleCatalog.Step) return;
            foreach (var (id, record) in read.Query("SELECT id, record FROM roles WHERE state = 'set'", row => (row.GetString(0), row.GetString(1))))
            {
                if (Granted(record) is { } next) converted.Add((id, null, next));
            }

            const string Revisions = "SELECT id, revision, record FROM role_revisions WHERE record IS NOT NULL";
            foreach (var (id, revision, record) in read.Query(Revisions, row => (row.GetString(0), row.GetInt64(1), row.GetString(2))))
            {
                if (Granted(record) is { } next) converted.Add((id, revision, next));
            }
        }

        if (converted.Count == 0) return;
        run.Keep(path);
        using var store = DesktopStore.Open(path, StoreProcess.Options(() => StoreSettled.Yes));
        store.Write("roles", write =>
        {
            foreach (var (id, revision, record) in converted)
            {
                if (revision is { } kept)
                {
                    write.Execute("UPDATE role_revisions SET record = $record WHERE id = $id AND revision = $revision", ("$record", record), ("$id", id), ("$revision", kept));
                }
                else
                {
                    write.Execute("UPDATE roles SET record = $record WHERE id = $id", ("$record", record), ("$id", id));
                }
            }

            write.Moved("roles");
        });
        run.Said(path);
    }

    /// <summary>A supervisor record with its acts granted by kind, or null where it is none or already holds no old act.</summary>
    internal static string? Granted(string record)
    {
        JsonObject root;
        try
        {
            if (JsonNode.Parse(record) is not JsonObject read || HomeMigrationRun.Text(read["kind"]) != AppointedRoles.Supervisor) return null;
            root = read;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }

        if (root["authority"] is not JsonArray authority) return null;
        var acts = authority.Select(HomeMigrationRun.Text).ToList();
        if (!acts.Contains(Answer) && !acts.Contains(Defer)) return null;
        var granted = new List<string>();
        foreach (var act in acts.OfType<string>())
        {
            IReadOnlyList<string> next = act switch { Answer => SupervisorKinds.Seeded, Defer => [], _ => [act] };
            granted.AddRange(next.Where(each => !granted.Contains(each, StringComparer.Ordinal)));
        }

        root["authority"] = new JsonArray([.. granted.Select(act => (JsonNode?)act)]);
        return root.ToJsonString();
    }

    private static void ConvertDecisions(HomeMigrationRun run)
    {
        var sessions = DaorisHome.Entry(run.Home, HomeEntry.Sessions);
        if (!Directory.Exists(sessions)) return;
        foreach (var folder in Directory.EnumerateDirectories(sessions, "*" + QuestionSupervisorDecisions.DecisionsSuffix).Order(StringComparer.Ordinal))
        {
            foreach (var file in Directory.EnumerateFiles(folder, "*.json").Order(StringComparer.Ordinal))
            {
                run.ChangeJson(file, root =>
                {
                    if (root.ContainsKey("kind")) return false;
                    root["kind"] = root["question"] is JsonObject ? SupervisorKinds.Question : SupervisorKinds.Course;
                    return true;
                });
            }
        }
    }
}
