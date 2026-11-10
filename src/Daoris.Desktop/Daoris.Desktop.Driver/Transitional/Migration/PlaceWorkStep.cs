using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// The per-repository trees switch and the line policies' <c>overlap</c> as an <c>isolate</c> step (D254, D258): each saved workflow whose
/// followers all had trees on, under one overlap, gains a version that starts with the step; where its followers differ, a copy with the
/// step is saved for those with trees on (one per overlap) and chosen as each one's own; then <c>trees</c> and every <c>overlap</c> leave
/// <c>driver.json</c>. The workflows and the file are copied to the salvage first; run again over what it converted, it changes nothing.
/// </summary>
public sealed class PlaceWorkStep : IHomeMigrationStep
{
    public string Name => "place-work";

    public void Run(HomeMigrationRun run)
    {
        var file = DaorisHome.Entry(run.Home, HomeEntry.DriverConfig);
        if (!File.Exists(file) || JsonNode.Parse(AtomicFile.ReadText(file)) is not JsonObject root) return;
        var trees = (root["trees"] as JsonArray ?? []).Select(HomeMigrationRun.Text).OfType<string>().ToList();
        var waits = Waiting(root);
        if (root["trees"] is null && waits.Count == 0) return;

        var store = WorkflowPorts.Store(run.Home);
        var folder = WorkflowStore.FolderOf(run.Home);
        if (Directory.Exists(folder))
        {
            foreach (var path in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)) run.Keep(path);
        }

        var config = DriverConfig.Load(file);
        var registry = WorkflowsStep.Registry(run.Home);
        var places = registry.Select(known => (known.Repository, known.Workspace))
            .Concat(trees.Where(name => !registry.Any(known => Same(known.Repository, name))).Select(name => (Repository: name, Workspace: (string?)null)))
            .ToList();
        var followers = places
            .Select(place => (place.Repository, place.Workspace, Workflow: WorkflowSelection.Resolve(config, place.Repository, place.Workspace, task: null).Workflow))
            .Where(each => WorkflowNamed.IsId(each.Workflow))
            .GroupBy(each => each.Workflow, StringComparer.Ordinal);
        foreach (var flow in followers)
        {
            var apart = flow.Where(each => trees.Any(name => Same(name, each.Repository))).ToList();
            if (apart.Count == 0 || store.Load(flow.Key) is not { File: JsonObject saved }) continue;
            string Overlap((string Repository, string? Workspace, string Workflow) each) =>
                waits.Contains(each.Repository) || (each.Workspace is { } workspace && waits.Contains($"workspace:{workspace}")) ? IsolateStep.Wait : IsolateStep.Go;
            var overlaps = apart.Select(Overlap).Distinct(StringComparer.Ordinal).ToList();
            if (apart.Count == flow.Count() && overlaps.Count == 1)
            {
                if (IsolatedFiles.Isolated(saved, overlaps[0]) is { } isolated) store.Write(flow.Key, isolated);
                continue;
            }

            foreach (var overlap in overlaps)
            {
                var copy = $"{flow.Key}-apart{(overlap == IsolateStep.Wait ? "-wait" : "")}";
                if (store.Load(copy) is null && IsolatedFiles.Copy(saved, copy, overlap) is { } made) store.Write(copy, made);
                foreach (var each in apart.Where(each => Overlap(each) == overlap))
                {
                    config = WorkflowSelection.Apply(config, new WorkflowUse(copy, each.Repository, null, null));
                }
            }
        }

        run.Keep(file);
        config.Save(file);
        run.ChangeJson(file, after => (after.Remove("trees") ? 1 : 0) + Overlaps(after).Count(policy => policy.Remove("overlap")) > 0);
        run.Said(file);
        if (Directory.Exists(folder)) run.Said(folder);
    }

    private static bool Same(string one, string other) => string.Equals(one, other, StringComparison.OrdinalIgnoreCase);

    /// <summary>The repositories, and the workspaces as <c>workspace:&lt;name&gt;</c>, whose line policy says <c>overlap: wait</c>.</summary>
    private static HashSet<string> Waiting(JsonObject root)
    {
        var waits = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, prefix) in new[] { ("linePolicies", ""), ("workspaceLinePolicies", "workspace:") })
        {
            if (root[key] is not JsonObject policies) continue;
            foreach (var (name, policy) in policies)
            {
                if (HomeMigrationRun.Text(policy?["integrate"]?["overlap"]) == LandingIntegrate.Wait) waits.Add(prefix + name);
            }
        }

        return waits;
    }

    /// <summary>Every line policy's <c>integrate</c> object in <paramref name="root"/>.</summary>
    private static IEnumerable<JsonObject> Overlaps(JsonObject root) =>
        new[] { "linePolicies", "workspaceLinePolicies" }
            .SelectMany(key => root[key] is JsonObject policies ? policies.Select(each => each.Value?["integrate"] as JsonObject) : [])
            .OfType<JsonObject>()
            .ToList();
}
