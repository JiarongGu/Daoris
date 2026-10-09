namespace Daoris.Driver;

/// <summary>What happened to a step between two versions (the workflow design §8.2).</summary>
public static class WorkflowChange
{
    public const string Added = "added";
    public const string Removed = "removed";
    public const string Kept = "kept";
}

/// <summary>One step in a diff: matched by id, added, removed or kept, the fields changed in its kind's order, and whether it moved.</summary>
public sealed record WorkflowDiffEntry(string Id, string Kind, string Change, IReadOnlyList<string> Fields, bool Moved);

/// <summary>A diff's entries, and the lines it is said in.</summary>
public sealed record WorkflowDiffSaid(IReadOnlyList<WorkflowDiffEntry> Entries, IReadOnlyList<string> Said);

/// <summary>
/// Two versions of a workflow compared by step id (WORKFLOW1d, the workflow design §2.5, §8.2): each entry in the newer one's
/// order, a removed step where it stood, then the lines it is said in — the person's part first, then the steps, then what
/// leaves this machine with no press of theirs that did not before. Ask Daoris's card (WORKFLOW1h) draws the same.
/// </summary>
/// <remarks>
/// A TWIN with the CLI's <c>workflowDiff</c> (<c>namedworkflows.ts</c>): both hold this suite's tests'
/// <c>fixtures/workflow-named.json</c> <c>diffs</c>, cell for cell, every line said included.
/// </remarks>
public static class WorkflowDiff
{
    /// <summary>The diff from <paramref name="before"/> to <paramref name="after"/>; a null <paramref name="before"/> is a new workflow.</summary>
    public static WorkflowDiffSaid Of(IReadOnlyList<NamedStep>? before, IReadOnlyList<NamedStep> after)
    {
        var old = before ?? [];
        var afterIds = after.Select(step => step.Id).ToHashSet(StringComparer.Ordinal);
        var oldById = old.ToDictionary(step => step.Id, StringComparer.Ordinal);
        var kept = after.Where(step => oldById.ContainsKey(step.Id)).Select(step => step.Id).ToHashSet(StringComparer.Ordinal);
        var moved = Moved(
            [.. old.Where(step => kept.Contains(step.Id)).Select(step => step.Id)],
            [.. after.Where(step => kept.Contains(step.Id)).Select(step => step.Id)]);

        // Each removed step after the kept step that stood before it, or first where none did ("" stands for none).
        var removedAfter = new Dictionary<string, List<NamedStep>>(StringComparer.Ordinal);
        var anchor = "";
        foreach (var step in old)
        {
            if (afterIds.Contains(step.Id))
            {
                anchor = step.Id;
                continue;
            }

            if (!removedAfter.TryGetValue(anchor, out var list)) removedAfter[anchor] = list = [];
            list.Add(step);
        }

        var entries = new List<WorkflowDiffEntry>();
        var pairs = new List<(NamedStep? Was, NamedStep? Now)>();
        void Removed(string at)
        {
            foreach (var step in removedAfter.GetValueOrDefault(at) ?? [])
            {
                entries.Add(new WorkflowDiffEntry(step.Id, step.Kind, WorkflowChange.Removed, [], false));
                pairs.Add((step, null));
            }
        }

        Removed("");
        foreach (var step in after)
        {
            var was = oldById.GetValueOrDefault(step.Id);
            if (was is null)
            {
                entries.Add(new WorkflowDiffEntry(step.Id, step.Kind, WorkflowChange.Added, [], false));
            }
            else
            {
                var fields = step.Fields.Where(each => !Same(was.Field(each.Name), each.Value)).Select(each => each.Name).ToList();
                entries.Add(new WorkflowDiffEntry(step.Id, step.Kind, WorkflowChange.Kept, fields, moved.Contains(step.Id)));
            }

            pairs.Add((was, step));
            if (was is not null) Removed(step.Id);
        }

        var part = new List<string>();
        foreach (var (was, now) in pairs)
        {
            var id = (now ?? was)!.Id;
            var wasYours = PartOf(was);
            var isYours = PartOf(now);
            if (wasYours is not null && wasYours == isYours)
            {
                part.Add($"  = {id}: you still {isYours}.");
            }
            else
            {
                if (wasYours is not null) part.Add($"  − {id}: you no longer {wasYours}.");
                if (isYours is not null) part.Add($"  + {id}: you {isYours}.");
            }
        }

        var steps = entries.Select((entry, index) =>
        {
            var (was, now) = pairs[index];
            var mark = " ";
            var detail = "";
            if (entry.Change == WorkflowChange.Added)
            {
                mark = "+";
                var row = WorkflowKindTable.Of(now!.Kind)!;
                var shown = now.Fields.Where(each => !Same(each.Value, row.Fields.First(field => field.Name == each.Name).Default)).ToList();
                if (shown.Count > 0) detail = ": " + string.Join("; ", shown.Select(each => $"{each.Name} {Rendered(each.Value)}"));
            }
            else if (entry.Change == WorkflowChange.Removed)
            {
                mark = "−";
            }
            else if (entry.Fields.Count > 0)
            {
                mark = "~";
                detail = ": " + string.Join("; ", entry.Fields.Select(name => $"{name} {Rendered(was!.Field(name))} → {Rendered(now!.Field(name))}"))
                    + (entry.Moved ? "; moved" : "");
            }
            else if (entry.Moved)
            {
                mark = "↕";
            }

            return $"  {mark} {entry.Id} · {entry.Kind}{detail}";
        });

        var standingBefore = StandingOf(old);
        var standing = StandingOf(after).Where(sentence => !standingBefore.Contains(sentence, StringComparer.Ordinal)).ToList();
        IEnumerable<string> outward = standing.Count > 0
            ? standing.Select(sentence => $"  {sentence}")
            : new[] { "  nothing new leaves this machine without you." };
        List<string> said =
        [
            "Your part:",
            .. part.Count > 0 ? part : new List<string> { "  nothing in it is yours." },
            "Steps, by id:",
            .. steps,
            "Without asking you each time:",
            .. outward,
        ];
        return new WorkflowDiffSaid(entries, said);
    }

    /// <summary>
    /// The kept steps that moved: those outside the longest run both orders share, found by the table every side builds the same
    /// way, a tie skipping the newer order's step, so a step moved earlier is the one marked.
    /// </summary>
    private static HashSet<string> Moved(IReadOnlyList<string> before, IReadOnlyList<string> after)
    {
        var longest = new int[before.Count + 1, after.Count + 1];
        for (var i = before.Count - 1; i >= 0; i--)
        {
            for (var j = after.Count - 1; j >= 0; j--)
            {
                longest[i, j] = before[i] == after[j] ? longest[i + 1, j + 1] + 1 : Math.Max(longest[i + 1, j], longest[i, j + 1]);
            }
        }

        var shared = new HashSet<string>(StringComparer.Ordinal);
        int at = 0, to = 0;
        while (at < before.Count && to < after.Count)
        {
            if (before[at] == after[to])
            {
                shared.Add(before[at]);
                at++;
                to++;
            }
            else if (longest[at + 1, to] > longest[at, to + 1])
            {
                at++;
            }
            else
            {
                to++;
            }
        }

        return after.Where(id => !shared.Contains(id)).ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>Two field values the same: text, a switch, none, names in order, or a group's members by their text.</summary>
    internal static bool Same(object? a, object? b) => (a, b) switch
    {
        (IReadOnlyList<NamedStep> x, IReadOnlyList<NamedStep> y) => x.Count == y.Count
            && x.Zip(y).All(pair => WorkflowNamed.Canonical([pair.First]) == WorkflowNamed.Canonical([pair.Second])),
        (string x, string y) => x == y,
        (IReadOnlyList<string> x, IReadOnlyList<string> y) => x.SequenceEqual(y, StringComparer.Ordinal),
        _ => Equals(a, b),
    };

    /// <summary>The person's part in a step, as the diff says it after <i>you</i>, or null where they take none (design §3.1).</summary>
    private static string? PartOf(NamedStep? step) => step?.Kind switch
    {
        WorkflowKinds.Look => "look at it running before it lands",
        WorkflowKinds.Landing => (string?)step.Field("accept") == "you" ? "accept each piece of work before it lands" : null,
        WorkflowKinds.PullRequest => "merge its pull request on the platform",
        WorkflowKinds.GoAhead => $"answer a go-ahead: \"{step.Field("act")}\"",
        WorkflowKinds.Stage => (string?)step.Field("start") == "you" ? $"start `{step.Field("stage")}`" : null,
        _ => null,
    };

    /// <summary>A field's value as a diff says it: absence as declared, a switch, text and names in backticks, a group by its size.</summary>
    private static string Rendered(object? value) => value switch
    {
        null => "as declared",
        bool flag => flag ? "true" : "false",
        string text => $"`{text}`",
        IReadOnlyList<NamedStep> members => $"a group of {members.Count}",
        IEnumerable<string> names => string.Join(", ", names.Select(name => $"`{name}`")),
        _ => throw new ArgumentException($"a field is text, a switch or a list, not {value.GetType().Name}."),
    };

    /// <summary>What a version makes leave this machine with no press of the person's (design §8.2): a push, a stage started.</summary>
    private static List<string> StandingOf(IReadOnlyList<NamedStep> steps)
    {
        var said = new List<string>();
        foreach (var step in steps.Concat(steps.SelectMany(each => each.Members)))
        {
            if (step.Kind == WorkflowKinds.Landing && (string?)step.Field("form") == LandingForm.Branch && (string?)step.Field("accept") == "automatic")
            {
                var plugin = (string?)step.Field("plugin");
                if (plugin is null) said.Add("the plugin the landing rule names, if any, pushes each branch with no press of yours.");
                else if (plugin != "none") said.Add($"`{plugin}` pushes each branch with no press of yours.");
            }

            if (step.Kind == WorkflowKinds.Stage && (string?)step.Field("start") == "automatic")
            {
                said.Add($"`{step.Field("plugin")}` starts `{step.Field("stage")}` with no press of yours.");
            }
        }

        return said;
    }
}
