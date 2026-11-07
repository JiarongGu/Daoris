using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>What a start's migration did: the steps it ran, the files it changed, and why it ran nothing or stopped where it did.</summary>
/// <param name="Held">Why nothing ran this start: the home's lease was held, so the next start runs it.</param>
/// <param name="Failed">The step that stopped and why; the steps before it are recorded, it and those after run at the next start.</param>
public sealed record HomeMigrated(string Migration, IReadOnlyList<string> Ran, IReadOnlyList<string> Changed, string? Held = null, (string Step, string Said)? Failed = null)
{
    /// <summary>Whether this start found anything to do or to say.</summary>
    public bool Any => Ran.Count > 0 || Held is not null || Failed is not null;
}

/// <summary>
/// This release's migration of the home (D232 point 2): at the first start, before anything reads the home, each step converts its records
/// to the shape this build reads, a copy of every file it changes kept in the salvage folder, and <see cref="HomeEntry.Migration"/> records
/// each step as it finishes, so a home is converted once. The next release that changes a stored shape replaces the steps and the name.
/// </summary>
public static class HomeMigration
{
    /// <summary>This release's migration, as its record and its copies' folder name it.</summary>
    public const string Name = "nocompat1";

    /// <summary>The steps, in the order they run: each later one may read what an earlier one wrote.</summary>
    public static IReadOnlyList<IHomeMigrationStep> Steps { get; } =
    [
        new StaffingStep(),
        new RulesStep(),
        new WorkflowsStep(),
        new PushStepsStep(),
        new RunRebindsStep(),
        new DriverKeysStep(),
        new GitPushStep(),
        new HarnessesStep(),
        new RequestsStep(),
        new SolicitedAttemptsStep(),
        new HeldWordsStep(),
        new EventLogsStep(),
        new SessionRecordsStep(),
        new NoteRecordsStep(),
        new ClosedQuestsStep(),
        new TurnMarksStep(),
        new StoreNamesStep(),
        new BindingFilesStep(),
        new WiredFilesStep(),
        new UsageFileStep(),
        new LineageFilesStep(),
        new BrainstormFilesStep(),
        new StandingGrantStep(),
    ];

    /// <summary>
    /// Run the steps <paramref name="home"/>'s record does not hold, under the home's lease, recording each as it finishes. A lease still
    /// held after <paramref name="leaseWait"/> runs nothing; a step that throws stops the run there, recorded as not done.
    /// </summary>
    public static HomeMigrated Run(string home, IReadOnlyList<IHomeMigrationStep>? steps = null, TimeSpan? leaseWait = null, Func<DateTimeOffset>? clock = null)
    {
        steps ??= Steps;
        var record = DaorisHome.Entry(home, HomeEntry.Migration);
        if (Done(record) is { } done && steps.All(step => done.Contains(step.Name))) return new(Name, [], []);

        // The launcher's swap holds the home until this very application confirms, which comes after this run, so waiting for it can only
        // time out; nothing else runs from the folder it is replacing, so the swap's hold is the guard and the steps run under it.
        using var lease = HomeLease.TryAcquire(home, LeaseKind.Migration, out var holder, "migration")
            ?? (holder is { Kind: LeaseKind.Swap } ? null : HomeLease.Await(home, LeaseKind.Migration, "migration", leaseWait ?? HomeLease.Wait, out holder));
        if (lease is null && holder is not { Kind: LeaseKind.Swap })
        {
            return new(Name, [], [], $"{holder!.Named} held this home, so it was not converted; the next start converts it");
        }

        // Read again under the lease: another start may have converted it while this one waited.
        var ran = new List<string>();
        var kept = Done(record) ?? [];
        var run = new HomeMigrationRun(home, Name);
        foreach (var step in steps)
        {
            if (kept.Contains(step.Name)) continue;
            try
            {
                step.Run(run);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or DriverException
                                              or DesktopStoreException or Microsoft.Data.Sqlite.SqliteException or InvalidOperationException)
            {
                return new(Name, ran, run.Changed, Failed: (step.Name, error.Message));
            }

            kept.Add(step.Name);
            ran.Add(step.Name);
            Write(record, kept, (clock ?? (() => DateTimeOffset.UtcNow))());
        }

        return new(Name, ran, run.Changed);
    }

    /// <summary>What the run did, into the machine log, by count and name, never a path.</summary>
    public static void Log(HomeMigrated migrated, MachineLog log)
    {
        if (migrated.Held is { } held) log.Info("home.migration-held", ("migration", migrated.Migration), ("reason", held));
        if (migrated.Ran.Count > 0)
        {
            log.Info("home.migrated", ("migration", migrated.Migration), ("steps", string.Join(",", migrated.Ran)), ("files", migrated.Changed.Count));
        }

        if (migrated.Failed is { } failed) log.Warn("home.migration-failed", ("migration", migrated.Migration), ("step", failed.Step), ("reason", failed.Said));
    }

    /// <summary>The steps the record holds for this migration; null where there is no record, or one of another migration.</summary>
    private static List<string>? Done(string record)
    {
        try
        {
            if (!File.Exists(record) || JsonNode.Parse(AtomicFile.ReadText(record)) is not JsonObject root) return null;
            if ((string?)root["migration"] != Name || root["steps"] is not JsonArray steps) return null;
            return [.. steps.Select(each => each?.GetValue<string>()).OfType<string>()];
        }
        catch (Exception error) when (error is JsonException or IOException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }

    private static void Write(string record, IReadOnlyList<string> steps, DateTimeOffset at)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(record)!);
        var root = new JsonObject
        {
            ["migration"] = Name,
            ["steps"] = new JsonArray([.. steps.Select(step => (JsonNode?)step)]),
            ["at"] = Moments.Write(at),
        };
        AtomicFile.WriteText(record, JsonFile.Serialize(root));
    }
}
