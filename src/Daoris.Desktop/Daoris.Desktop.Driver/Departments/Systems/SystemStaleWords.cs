namespace Daoris.Driver;

/// <summary>
/// What a run's work is told when its apply goes back: refused as stale, the plan having read versions that moved before the apply,
/// or changed while applying, a resource of an unguarded system read back other than its write. Either way it plans again from
/// the system as it stands, with each such resource's present value and version.
/// </summary>
public static class SystemStaleWords
{
    /// <summary>The sentence for <paramref name="applied"/>, a stale or changed apply of plan <paramref name="plan"/> at <paramref name="node"/>.</summary>
    public static string Of(string node, int plan, SystemApplied applied)
    {
        var present = applied.Present.Select(each => each.Exists
            ? $"- `{each.Resource}` is now {Quoted(each.Value)} at version `{each.Version}`"
            : $"- `{each.Resource}` is now absent");
        var changed = applied.State == SystemApplyStates.Changed;
        var done = applied.Applied.Count == 0
            ? changed ? "Nothing before it was applied." : "Nothing of it was applied."
            : $"Applied before {(changed ? "it" : "the refusal")}: {string.Join(", ", applied.Applied.Select(each => $"`{each}`"))}.";
        var what = changed
            ? $"Daoris: `{node}` wrote `{applied.Refused}` of plan {plan} and read it back changed while applying: something else wrote it at the same time."
            : $"Daoris: `{node}` refused plan {plan} as stale at `{applied.Refused}`: the system changed after you read it.";
        return $"{what} {done} Plan again from the system as it stands; your next plan is reviewed again. What it holds now:\n\n{string.Join("\n", present)}";
    }

    private static string Quoted(string? value) => value is null ? "empty" : $"`{value.ReplaceLineEndings(" ")}`";
}
