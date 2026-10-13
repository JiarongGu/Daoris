using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// A workspace plan's file, <c>&lt;home&gt;/setup/&lt;workspace&gt;.json</c> (WSSETUP6, D124 §4.1): the person's choices and
/// the quests the plan published, machine-local under the home (D63).
/// </summary>
/// <remarks>
/// <para><b>The driver's alone</b> (D124 §10: not <c>driver.json</c>, which two twins keep): no other artefact reads it, so it
/// has no twin. The file is named by the rule the intake's room is (<c>IntakeRoom.SafeName</c>), so two workspaces never share
/// one, and it says its workspace's own spelling inside.</para>
///
/// <para><b>Two writers, each changing only its own part.</b> A tick records what it published or skipped and a pilot's
/// pause; a terminal or a screen pauses, resumes and stops. Each re-reads the file and changes it under one lock per
/// process (<see cref="Update"/>), so the window two processes can race in is a read and a write. A lost entry costs little,
/// since progress is read from the quests and the registry: a set-up already open is never asked again whatever the file
/// says.</para>
/// </remarks>
public static class WorkspaceSetupFile
{
    public const string Folder = "setup";

    // One writer at a time in this process: a tick and a terminal in one shell would otherwise drop each other's change.
    private static readonly object Gate = new();

    // LF on every platform, as every Daoris write is: the default new line is the platform's.
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true, NewLine = "\n" };

    /// <summary>Where <paramref name="workspace"/>'s plan is kept.</summary>
    public static string PathOf(string home, string workspace) =>
        Path.Combine(home, Folder, IntakeRoom.SafeName(RemoteTarget.Workspace(workspace)) + ".json");

    /// <summary>The workspace's plan, or none; with why, where a file is there and does not read.</summary>
    public static (WorkspaceSetupPlan? Plan, string? Problem) Load(string home, string workspace)
    {
        var path = PathOf(home, workspace);
        if (!File.Exists(path)) return (null, null);

        var (plan, problem) = ReadFile(path);
        if (plan is null) return (null, $"the plan for workspace `{RemoteTarget.Workspace(workspace)}` does not read: {problem}");
        return string.Equals(plan.Workspace, RemoteTarget.Workspace(workspace), StringComparison.OrdinalIgnoreCase)
            ? (plan, null)
            : (null, $"`{Path.GetFileName(path)}` holds the plan of workspace `{plan.Workspace}`, not `{RemoteTarget.Workspace(workspace)}`.");
    }

    /// <summary>Every plan under the home that reads, by its workspace's name; a file that does not read is passed over.</summary>
    public static IReadOnlyList<WorkspaceSetupPlan> All(string home)
    {
        var folder = Path.Combine(home, Folder);
        if (!Directory.Exists(folder)) return [];
        try
        {
            return
            [
                .. Directory.EnumerateFiles(folder, "*.json")
                    .Select(path => ReadFile(path).Plan)
                    .OfType<WorkspaceSetupPlan>()
                    .OrderBy(plan => plan.Workspace, StringComparer.OrdinalIgnoreCase),
            ];
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>Write <paramref name="plan"/> whole, BOM-less UTF-8 and LF, beside and then renamed.</summary>
    /// <exception cref="IOException">The home could not be written.</exception>
    public static void Save(string home, WorkspaceSetupPlan plan)
    {
        lock (Gate)
        {
            var path = PathOf(home, plan.Workspace);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            AtomicFile.WriteText(path, Write(plan).ToJsonString(Indented) + "\n");
        }
    }

    /// <summary>
    /// Read the workspace's plan as it stands now, change it, and write it: null where none reads, and nothing is written
    /// when the change hands back the plan it was given.
    /// </summary>
    public static WorkspaceSetupPlan? Update(string home, string workspace, Func<WorkspaceSetupPlan, WorkspaceSetupPlan> change)
    {
        lock (Gate)
        {
            if (Load(home, workspace).Plan is not { } plan) return null;
            var next = change(plan);
            if (!ReferenceEquals(next, plan)) Save(home, next);
            return next;
        }
    }

    private static (WorkspaceSetupPlan? Plan, string? Problem) ReadFile(string path)
    {
        try
        {
            return (Read(JsonNode.Parse(File.ReadAllText(path))), null);
        }
        catch (Exception error) when (error is JsonException or InvalidDataException or IOException or UnauthorizedAccessException)
        {
            return (null, error.Message);
        }
    }

    private static WorkspaceSetupPlan Read(JsonNode? node)
    {
        if (node is not JsonObject file) throw new InvalidDataException("it is not a JSON object.");
        var workspace = Text(file["workspace"]) is { Length: > 0 } named ? named : throw new InvalidDataException("it names no workspace.");
        if (file["order"] is not JsonArray order) throw new InvalidDataException("it holds no order.");

        var published = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (file["published"] is JsonObject asked)
        {
            foreach (var (repository, quest) in asked)
            {
                if (Text(quest) is { Length: > 0 } id) published[repository] = id;
            }
        }

        var skipped = new Dictionary<string, SetupSkip>(StringComparer.OrdinalIgnoreCase);
        if (file["skipped"] is JsonObject skips)
        {
            foreach (var (repository, value) in skips)
            {
                if (value is JsonObject skip && Text(skip["code"]) is { Length: > 0 } code)
                {
                    skipped[repository] = new(code, Text(skip["said"]) ?? "", When(skip["at"]) ?? default);
                }
            }
        }

        return new WorkspaceSetupPlan(
            workspace,
            [.. order.Select(Text).OfType<string>().Where(name => name.Length > 0)],
            Count(file["atOnce"]) ?? WorkspaceSetup.DefaultAtOnce,
            Count(file["pilot"]) ?? WorkspaceSetup.DefaultPilot)
        {
            Created = When(file["created"]) ?? default,
            Paused = file["paused"] is JsonObject paused && Text(paused["by"]) is { Length: > 0 } by
                ? new SetupPause(by, Text(paused["said"]) ?? "", When(paused["at"]) ?? default)
                : null,
            PilotResumed = file["pilotResumed"]?.GetValueKind() == JsonValueKind.True,
            Stopped = When(file["stopped"]),
            Published = published,
            Skipped = skipped,
        };
    }

    private static JsonObject Write(WorkspaceSetupPlan plan)
    {
        // Each record in the plan's order, then any the order does not name, so the file reads as the plan runs.
        IEnumerable<string> InOrder(IReadOnlyCollection<string> keys) =>
            keys.Where(key => plan.Order.Contains(key, StringComparer.OrdinalIgnoreCase))
                .OrderBy(key => plan.Order.ToList().FindIndex(named => string.Equals(named, key, StringComparison.OrdinalIgnoreCase)))
                .Concat(keys.Where(key => !plan.Order.Contains(key, StringComparer.OrdinalIgnoreCase)));

        var published = new JsonObject();
        foreach (var repository in InOrder([.. plan.Published.Keys])) published[repository] = plan.Published[repository];

        var skipped = new JsonObject();
        foreach (var repository in InOrder([.. plan.Skipped.Keys]))
        {
            var skip = plan.Skipped[repository];
            skipped[repository] = new JsonObject { ["code"] = skip.Code, ["said"] = skip.Said, ["at"] = Stamp(skip.At) };
        }

        return new JsonObject
        {
            ["workspace"] = plan.Workspace,
            ["created"] = Stamp(plan.Created),
            ["order"] = new JsonArray([.. plan.Order.Select(repository => (JsonNode)JsonValue.Create(repository))]),
            ["atOnce"] = plan.AtOnce,
            ["pilot"] = plan.Pilot,
            ["paused"] = plan.Paused is { } pause
                ? new JsonObject { ["by"] = pause.By, ["said"] = pause.Said, ["at"] = Stamp(pause.At) }
                : null,
            ["pilotResumed"] = plan.PilotResumed,
            ["stopped"] = plan.Stopped is { } stopped ? Stamp(stopped) : null,
            ["published"] = published,
            ["skipped"] = skipped,
        };
    }

    private static string Stamp(DateTimeOffset at) => at.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static DateTimeOffset? When(JsonNode? node) =>
        Text(node) is { } text && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at)
            ? at
            : null;

    private static int? Count(JsonNode? node) =>
        node?.GetValueKind() == JsonValueKind.Number && node.AsValue().TryGetValue<int>(out var count) && count >= 0 ? count : null;

    private static string? Text(JsonNode? node) => node?.GetValueKind() == JsonValueKind.String ? node.GetValue<string>() : null;
}
