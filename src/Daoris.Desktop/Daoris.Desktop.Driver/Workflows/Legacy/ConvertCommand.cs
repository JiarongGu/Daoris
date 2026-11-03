namespace Daoris.Driver;

/// <summary>What <c>workflow convert</c> was asked: to plan, to apply a plan by its digest, or to undo; with the row's two choices.</summary>
/// <param name="Act"><c>plan</c>, <c>apply</c> or <c>undo</c>.</param>
public sealed record ConvertAsk(string Act, string? Digest, ConversionChoices Choices);

/// <summary>
/// <c>daoris-driver workflow convert --plan | --apply &lt;digest&gt; | --undo [&lt;digest&gt;]</c> (WORKFLOWCLEAN1 1e; the cleanup design §4.4): the conversion
/// plan's terminal twin. It goes through the running Daoris alone: the plan is read there and the press is the person's, confirmed in its window;
/// with none running it says so and does nothing (a conversion writes the driver's file and the saved workflows, and the window is where it is
/// confirmed and where it is undone).
/// </summary>
public static class ConvertCommand
{
    public const string Usage =
        "usage: daoris-driver workflow convert --plan [--composed none|review] [--land-automatic review|none]\n"
        + "       daoris-driver workflow convert --apply <digest> [--composed none|review] [--land-automatic review|none]\n"
        + "       daoris-driver workflow convert --undo [<digest>]";

    /// <summary>Said when no Daoris answers: nothing was read and nothing written.</summary>
    public const string NoDaoris =
        "workflow convert: the conversion goes through the running Daoris, which plans it and asks you to confirm it in its window, and none answers here. Start Daoris, then ask again.";

    /// <summary>The words after <c>convert</c> read, or null with what is wrong with them.</summary>
    public static ConvertAsk? Read(IReadOnlyList<string> args, out string? problem)
    {
        problem = null;
        string? act = null, digest = null, composed = null, automatic = null;
        for (var at = 0; at < args.Count; at++)
        {
            switch (args[at])
            {
                case "--plan" when act is null: act = "plan"; break;
                case "--apply" when act is null:
                    act = "apply";
                    if (at + 1 < args.Count && !args[at + 1].StartsWith("--", StringComparison.Ordinal)) digest = args[++at];
                    break;
                case "--undo" when act is null:
                    act = "undo";
                    if (at + 1 < args.Count && !args[at + 1].StartsWith("--", StringComparison.Ordinal)) digest = args[++at];
                    break;
                case "--composed" when at + 1 < args.Count: composed = args[++at]; break;
                case "--land-automatic" when at + 1 < args.Count: automatic = args[++at]; break;
                default:
                    problem = $"`{args[at]}` is not something `workflow convert` takes.";
                    return null;
            }
        }

        if (act is null)
        {
            problem = "name what to do: `--plan` to read the plan, `--apply <digest>` to apply it, or `--undo` to take it back.";
            return null;
        }

        if (act == "apply" && digest is null)
        {
            problem = "`--apply` names the plan's digest, as `--plan` says it.";
            return null;
        }

        var choices = new ConversionChoices(composed ?? ConversionChoices.AsLanded, automatic ?? ConversionChoices.WithReview);
        if (choices.Problem is { } wrong)
        {
            problem = wrong;
            return null;
        }

        if (act != "apply" && (composed is not null || automatic is not null) && act == "undo")
        {
            problem = "an undo takes no choices.";
            return null;
        }

        return new ConvertAsk(act, digest, choices);
    }

    /// <summary>The verb: the plan or the undo's plan read through the running Daoris, or a press sent to it and confirmed in its window.</summary>
    public static Task<int> RunAsync(ConvertAsk ask, TextWriter output, InstanceWorld? instance, CancellationToken ct = default)
    {
        Task<int> Alone()
        {
            output.WriteLine(NoDaoris);
            return Task.FromResult(1);
        }

        var choices = new (string, object?)[] { ("composed", ask.Choices.Composed), ("landAutomatic", ask.Choices.LandAutomatic) };
        return ask.Act switch
        {
            "apply" => InstanceDoor.ThroughAsync(instance, PersonPresses.ConvertApply, InstanceDoor.Said([("digest", ask.Digest), .. choices]), Alone, output, ct),
            "undo" when ask.Digest is not null => InstanceDoor.ThroughAsync(instance, PersonPresses.ConvertUndo, InstanceDoor.Said(("digest", ask.Digest)), Alone, output, ct),
            "undo" => InstanceDoor.ThroughAsync(instance, OpenRoutes.ConvertPlan, InstanceDoor.Said(("undo", true)), Alone, output, ct),
            _ => InstanceDoor.ThroughAsync(instance, OpenRoutes.ConvertPlan, InstanceDoor.Said(choices), Alone, output, ct),
        };
    }

    /// <summary>A plan in the terminal's words: what changes by shape, what lowers your part, what stays, then how to apply it.</summary>
    public static IReadOnlyList<string> Lines(ConversionPlan plan)
    {
        var lines = new List<string>();
        var census = plan.Census;
        lines.Add(census.Ended
            ? "workflow convert: nothing old is left here."
            : $"workflow convert: {census.Versions.Count} saved workflow{(census.Versions.Count == 1 ? "" : "s")} and {census.Places.Count} place{(census.Places.Count == 1 ? "" : "s")} follow old shapes.");
        foreach (var save in plan.Saves) lines.Add($"  save    {(save.Mode == "append" ? $"`{save.Id}` v{save.Base + 1}" : $"`{save.Id}` (new)")} — {save.Said}");
        if (plan.DefaultWorkflow is { } home) lines.Add($"  default `{home}` is the home's default workflow");
        foreach (var use in plan.Uses) lines.Add($"  choose  {use.Said}");
        foreach (var policy in plan.Policies) lines.Add($"  policy  `{policy.Place}`'s line policy is moved out of its landing rule");
        foreach (var removal in plan.Removals) lines.Add($"  remove  `{removal.Place}`: {string.Join(", ", removal.Keys)}");
        foreach (var lowered in plan.Lowers) lines.Add($"  lowers  {lowered}");
        foreach (var kept in plan.Kept) lines.Add($"  kept    {kept.Said}");
        foreach (var leftover in census.Leftovers) lines.Add($"  left    `{leftover.Place}`: {string.Join(", ", leftover.Keys)} — {leftover.Why}");
        lines.Add(plan.Empty
            ? "--plan: nothing is left to write; what is listed is kept as it is. Nothing was written."
            : $"--plan: nothing was written. To apply it: workflow convert --apply {plan.Digest}"
              + (plan.Choices == new ConversionChoices() ? "" : $" --composed {plan.Choices.Composed} --land-automatic {plan.Choices.LandAutomatic}"));
        return lines;
    }

    /// <summary>The undo's plan in the terminal's words.</summary>
    public static IReadOnlyList<string> UndoLines((ConversionReceipt? Receipt, string? Refusal, string? Digest, IReadOnlyList<string> Lines) plan) =>
        plan.Refusal is { } refusal
            ? [$"workflow convert: {refusal}"]
            : ["workflow convert --undo: this puts back:", .. plan.Lines.Select(each => "  " + each), $"--undo: nothing was written. To undo it: workflow convert --undo {plan.Digest}"];

    /// <summary>A press's answer in the terminal's words.</summary>
    public static IReadOnlyList<string> Lines(ConvertResult result) =>
        [$"workflow convert: {result.Message}", .. result.Operations.Where(each => each.Outcome != ConversionOutcomes.Done).Select(each => $"  {each.Outcome}  {each.Said}{(each.Note is { } note ? $" — {note}" : "")}")];
}
