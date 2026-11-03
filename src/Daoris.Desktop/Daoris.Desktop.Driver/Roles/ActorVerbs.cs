namespace Daoris.Driver;

/// <summary>What the staffing's own verbs share: one actor's order in the terminal's words.</summary>
internal static class ActorPress
{
    /// <summary>One actor's order in the terminal's words.</summary>
    public static string Said(ActorView view) => view.State switch
    {
        "unreadable" => $"unreadable: driver.json's `actors.{view.Id}` {view.Error}",
        "off" => "off",
        _ when view.Choices.Count == 0 => view.Id == "helper"
            ? "as before: every agent of another maker than the asker's, at account default"
            : "as before: the agents a rule or an ask names, at account default",
        _ => string.Join(", ", view.Choices.Select((entry, i) => Entry(i + 1, entry.Agent, entry.Model, entry.Effort)))
             + (view.State == "before" ? " (as before)" : ""),
    };

    private static string Entry(int at, string agent, string? model, string? effort) =>
        $"{at} {agent} · {model ?? "account default"} · {effort ?? "account default"}";
}

/// <summary>
/// <c>settings actor</c>, which set a role's agents before the role door was the one door (ONEWAY1a, the oneway design §2 S1): refused
/// naming the role door that sets and says them now. Its <c>export|import|found</c> are the staffing copy's own verbs, which ONEWAY1c-ii
/// retires with the copy. <c>adapter</c>, <c>intake</c> and <c>helper</c> are the tree's moved forms, refused before they reach here.
/// </summary>
public sealed class RetiredStaffingVerb(string word) : ChoiceVerb
{
    public override string Word => word;

    /// <summary>The refusal a retired staffing verb says: the role door's words.</summary>
    public static string Refusal(string verb) =>
        $"`settings {verb}` is retired: a role's agents are set through the role door, `daoris role set <role> \"<agent> [<model>|-] [<effort>|-]\" …` "
        + "(`off` for Intake and Ask Daoris), and said by `daoris role <role>`; nothing was changed.";

    /// <summary><c>actor export|import|found</c> (FOUNDATION1w-s, §19.15): the staffing copy's own verbs, each acting alone; null for the rest.</summary>
    public override Task<int>? RunAsync(IReadOnlyList<string> args, DriverConfig before, DriverChoicesWorld world, CancellationToken ct) =>
        word == "actor" && args.Count > 1 && args[1] is "export" or "import" or "found" ? Task.FromResult(StaffingVerbs.Run(args, before, world)) : null;

    public override ChoicePress Plan(IReadOnlyList<string> args, DriverConfig before, DriverChoicesWorld world) => throw new ChoiceWords(Refusal(word));

    public override int? Said(IReadOnlyList<string> args, DriverConfig choices, DriverChoicesWorld world) => throw new ChoiceWords(Refusal(word));
}
