namespace Daoris.Driver;

/// <summary>What <c>convert</c> was asked: to plan, to apply a plan by its digest (leaving a failed part where one is named), or to undo; with the row's two choices.</summary>
/// <param name="Act"><c>plan</c>, <c>apply</c> or <c>undo</c>.</param>
public sealed record ConvertAsk(string Act, string? Digest, ConversionChoices Choices)
{
    /// <summary>The failed part an apply closes as left, by the person's <i>Leave it</i>.</summary>
    public string? Leave { get; init; }
}

/// <summary>
/// <c>daoris convert --plan | --apply &lt;digest&gt; [--leave &lt;part&gt;] | --undo [&lt;digest&gt;]</c> (ONEWAY1ae; the oneway design §1.1): the home's one
/// conversion door at the terminal. It goes through the running Daoris alone: the plan is read there and the press is the person's, confirmed in
/// its window; with none running it says so and does nothing (a conversion writes the home's files, and the window is where it is confirmed
/// and where it is undone).
/// </summary>
public static class ConvertCommand
{
    public const string Usage =
        "usage: daoris convert --plan [--composed none|review] [--land-automatic review|none]\n"
        + "       daoris convert --apply <digest> [--composed none|review] [--land-automatic review|none]\n"
        + "       daoris convert --apply <digest> --leave <part>\n"
        + "       daoris convert --undo [<digest>]";

    /// <summary>Said when no Daoris answers: nothing was read and nothing written.</summary>
    public const string NoDaoris =
        "convert: the conversion goes through the running Daoris, which plans it and asks you to confirm it in its window, and none answers here. Start Daoris, then ask again.";

    /// <summary>The words after <c>convert</c> read, or null with what is wrong with them.</summary>
    public static ConvertAsk? Read(IReadOnlyList<string> args, out string? problem)
    {
        problem = null;
        string? act = null, digest = null, composed = null, automatic = null, leave = null;
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
                case "--leave" when at + 1 < args.Count: leave = args[++at]; break;
                default:
                    problem = $"`{args[at]}` is not something `convert` takes.";
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

        if (leave is not null && act != "apply")
        {
            problem = "`--leave <part>` closes a failed part of the conversion `--apply <digest>` names.";
            return null;
        }

        var choices = new ConversionChoices(composed ?? ConversionChoices.AsLanded, automatic ?? ConversionChoices.WithReview);
        if (choices.Problem is { } wrong)
        {
            problem = wrong;
            return null;
        }

        if (act == "undo" && (composed is not null || automatic is not null))
        {
            problem = "an undo takes no choices.";
            return null;
        }

        return new ConvertAsk(act, digest, choices) { Leave = leave };
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
            "apply" when ask.Leave is not null => InstanceDoor.ThroughAsync(instance, PersonPresses.ConvertApply, InstanceDoor.Said(("digest", ask.Digest), ("leave", ask.Leave)), Alone, output, ct),
            "apply" => InstanceDoor.ThroughAsync(instance, PersonPresses.ConvertApply, InstanceDoor.Said([("digest", ask.Digest), .. choices]), Alone, output, ct),
            "undo" when ask.Digest is not null => InstanceDoor.ThroughAsync(instance, PersonPresses.ConvertUndo, InstanceDoor.Said(("digest", ask.Digest)), Alone, output, ct),
            "undo" => InstanceDoor.ThroughAsync(instance, OpenRoutes.ConvertPlan, InstanceDoor.Said(("undo", true)), Alone, output, ct),
            _ => InstanceDoor.ThroughAsync(instance, OpenRoutes.ConvertPlan, InstanceDoor.Said(choices), Alone, output, ct),
        };
    }

    /// <summary>A plan in the terminal's words: the records waiting by part, each part's own lines, the parts not read, then how to apply it.</summary>
    public static IReadOnlyList<string> Lines(HomePlan plan)
    {
        var lines = new List<string>
        {
            plan.Ended
                ? "convert: nothing old is left here."
                : $"convert: {plan.Count} old record{(plan.Count == 1 ? "" : "s")} wait for your conversion"
                  + (plan.Parts.Count == 0 ? "." : $" ({string.Join(", ", plan.Parts.Select(each => $"{each.Part} {each.Census.Count}"))})."),
        };
        foreach (var part in plan.Parts.Where(each => !each.Census.Ended || !each.Empty))
        {
            lines.Add($"  {part.Part}:");
            lines.AddRange(part.Lines.Select(each => "  " + each));
            foreach (var clear in part.Clears) lines.Add($"    removed for good  {clear.Said} ({clear.Count})");
        }

        foreach (var unread in plan.Unread) lines.Add($"  {unread.Part}: not read: {unread.Said}");
        lines.Add(plan.Empty
            ? "--plan: nothing is left to write; what is listed is kept as it is. Nothing was written."
            : $"--plan: nothing was written. To apply it: daoris convert --apply {plan.Digest}"
              + (plan.Choices == new ConversionChoices() ? "" : $" --composed {plan.Choices.Composed} --land-automatic {plan.Choices.LandAutomatic}"));
        return lines;
    }

    /// <summary>The workflow part's plan in the terminal's words: what changes by shape, what lowers your part, what stays.</summary>
    public static IReadOnlyList<string> PartLines(ConversionPlan plan)
    {
        var census = plan.Census;
        var lines = new List<string>
        {
            census.Ended
                ? "  nothing old is left here."
                : $"  {census.Versions.Count} saved workflow{(census.Versions.Count == 1 ? "" : "s")} and {census.Places.Count} place{(census.Places.Count == 1 ? "" : "s")} follow old shapes.",
        };
        foreach (var save in plan.Saves) lines.Add($"  save    {(save.Mode == "append" ? $"`{save.Id}` v{save.Base + 1}" : $"`{save.Id}` (new)")} — {save.Said}");
        if (plan.DefaultWorkflow is { } home) lines.Add($"  default `{home}` is the home's default workflow");
        foreach (var use in plan.Uses) lines.Add($"  choose  {use.Said}");
        foreach (var policy in plan.Policies) lines.Add($"  policy  `{policy.Place}`'s line policy is moved out of its landing rule");
        foreach (var removal in plan.Removals) lines.Add($"  remove  `{removal.Place}`: {string.Join(", ", removal.Keys)}");
        foreach (var lowered in plan.Lowers) lines.Add($"  lowers  {lowered}");
        foreach (var kept in plan.Kept) lines.Add($"  kept    {kept.Said}");
        foreach (var leftover in census.Leftovers) lines.Add($"  left    `{leftover.Place}`: {string.Join(", ", leftover.Keys)} — {leftover.Why}");
        return lines;
    }

    /// <summary>The undo's plan in the terminal's words.</summary>
    public static IReadOnlyList<string> UndoLines((HomeReceipt? Receipt, string? Refusal, string? Digest, IReadOnlyList<string> Lines) plan) =>
        plan.Refusal is { } refusal
            ? [$"convert: {refusal}"]
            : ["convert --undo: this puts back:", .. plan.Lines.Select(each => "  " + each), $"--undo: nothing was written. To undo it: daoris convert --undo {plan.Digest}"];

    /// <summary>A press's answer in the terminal's words: each operation not done, and each part settled without one.</summary>
    public static IReadOnlyList<string> Lines(ConvertResult result) =>
    [
        $"convert: {result.Message}",
        .. result.Operations.Where(each => each.Outcome != ConversionOutcomes.Done).Select(each => $"  {each.Outcome}  {each.Said}{(each.Note is { } note ? $" — {note}" : "")}"),
        .. result.Parts.Where(each => each.Operations.Count == 0 && each.Outcome != ConversionOutcomes.Done)
            .Select(each => $"  {each.Outcome}  `{each.Part}`{(each.Said is { } said ? $" — {said}" : "")}"
                + (each.Outcome == ConversionOutcomes.Failed ? $" (again: daoris convert --apply {result.Digest}; or daoris convert --apply {result.Digest} --leave {each.Part})" : "")),
    ];
}
