namespace Daoris.Driver;

/// <summary>
/// What a run's work is told when its apply is refused as stale: the plan read versions that moved before the apply, so it plans again
/// from the system as it stands, with each stale resource's present value and version.
/// </summary>
public static class SystemStaleWords
{
    /// <summary>The sentence for <paramref name="applied"/>, a stale apply of plan <paramref name="plan"/> at <paramref name="node"/>.</summary>
    public static string Of(string node, int plan, SystemApplied applied)
    {
        var present = applied.Present.Select(each => each.Exists
            ? $"- `{each.Resource}` is now {Quoted(each.Value)} at version `{each.Version}`"
            : $"- `{each.Resource}` is now absent");
        var done = applied.Applied.Count == 0 ? "Nothing of it was applied." : $"Applied before the refusal: {string.Join(", ", applied.Applied.Select(each => $"`{each}`"))}.";
        return $"Daoris: `{node}` refused plan {plan} as stale at `{applied.Refused}`: the system changed after you read it. {done} Plan again from "
            + $"the system as it stands; your next plan is reviewed again. What it holds now:\n\n{string.Join("\n", present)}";
    }

    private static string Quoted(string? value) => value is null ? "empty" : $"`{value.ReplaceLineEndings(" ")}`";
}
