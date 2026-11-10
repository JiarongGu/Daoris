using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The system points' frames and the reading of their answers, by shape: <see cref="HookPoints.PlanRead"/> takes the intended changes and
/// answers typed operations, each with its precondition; <see cref="HookPoints.ApplyPerform"/> takes the reviewed operations and answers
/// applied, stale or failed. An answer of any other shape is none, never a guess.
/// </summary>
public static class SystemWire
{
    private static readonly IReadOnlyList<string> Ops = ["create", "update", "delete"];

    /// <summary><c>plan/read</c>'s params: the system and each intended change, a value or a removal.</summary>
    public static object Plan(string system, IEnumerable<SystemIntent> intents) => new
    {
        system,
        changes = intents.Select(intent => intent.Remove
            ? (object)new { resource = intent.Resource, remove = true }
            : new { resource = intent.Resource, value = intent.Value }).ToList(),
    };

    /// <summary><c>apply/perform</c>'s params: the system, the plan's number and its operations in their order.</summary>
    public static object Apply(string system, SystemChangeSet changes) => new
    {
        system,
        plan = changes.Plan,
        operations = changes.Operations.Select(operation => new
        {
            resource = operation.Resource,
            op = operation.Op,
            before = operation.Before,
            after = operation.After,
            precondition = new { kind = operation.Precondition.Kind, version = operation.Precondition.Version },
        }).ToList(),
    };

    /// <summary><c>plan/read</c>'s answer, <c>{ operations: [...] }</c>, as typed operations; null where it is not one.</summary>
    public static IReadOnlyList<SystemOperation>? ReadPlan(JsonElement answer)
    {
        if (answer.ValueKind != JsonValueKind.Object || !answer.TryGetProperty("operations", out var listed) || listed.ValueKind != JsonValueKind.Array) return null;
        var operations = new List<SystemOperation>();
        foreach (var row in listed.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Object || Text(row, "resource") is not { Length: > 0 } resource) return null;
            if (Text(row, "op") is not { } op || !Ops.Contains(op, StringComparer.Ordinal)) return null;
            if (!row.TryGetProperty("precondition", out var guard) || guard.ValueKind != JsonValueKind.Object || Text(guard, "kind") is not { Length: > 0 } kind) return null;
            operations.Add(new SystemOperation(resource, op, Text(row, "before"), Text(row, "after"), new SystemPrecondition(kind, Text(guard, "version"))));
        }

        return operations;
    }

    /// <summary><c>apply/perform</c>'s answer as what the apply came to; null where it is not one.</summary>
    public static SystemApplied? ReadApplied(JsonElement answer)
    {
        if (answer.ValueKind != JsonValueKind.Object || Text(answer, "state") is not { } state) return null;
        var applied = Strings(answer, "applied");
        if (applied is null) return null;
        switch (state)
        {
            case SystemApplyStates.Applied:
                return new SystemApplied(state, applied, null, [], null);
            case SystemApplyStates.None:
                return applied.Count == 0 ? new SystemApplied(state, [], null, [], null) : null;
            case SystemApplyStates.Stale:
                if (Text(answer, "refused") is not { Length: > 0 } refused || !answer.TryGetProperty("present", out var listed) || listed.ValueKind != JsonValueKind.Array) return null;
                var present = new List<SystemPresent>();
                foreach (var row in listed.EnumerateArray())
                {
                    if (row.ValueKind != JsonValueKind.Object || Text(row, "resource") is not { Length: > 0 } resource) return null;
                    var exists = row.TryGetProperty("exists", out var held) && held.ValueKind == JsonValueKind.True;
                    present.Add(new SystemPresent(resource, exists, Text(row, "value"), Text(row, "version")));
                }

                return new SystemApplied(state, applied, refused, present, null);
            case SystemApplyStates.Failed:
                return new SystemApplied(state, applied, Text(answer, "refused"), [], Text(answer, "message") ?? "the adapter gave no reason");
            default:
                return null;
        }
    }

    private static string? Text(JsonElement row, string name) =>
        row.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static IReadOnlyList<string>? Strings(JsonElement row, string name)
    {
        if (!row.TryGetProperty(name, out var listed)) return [];
        if (listed.ValueKind != JsonValueKind.Array || listed.EnumerateArray().Any(each => each.ValueKind != JsonValueKind.String)) return null;
        return [.. listed.EnumerateArray().Select(each => each.GetString()!)];
    }
}
