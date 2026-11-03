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
/// <c>settings actor found</c>: the staffing copy's found changes, kept while the copy is (the register's <c>staffing-copy</c>). A role's
/// agents are the role door's alone.
/// </summary>
public sealed class StaffingFoundVerb : ChoiceVerb
{
    public override string Word => "actor";

    private const string Usage = "`settings actor` takes `found`, `found take <n>` or `found dismiss <n>`.";

    /// <summary><c>actor found</c> (FOUNDATION1w-s, §19.15): the staffing copy's found changes, acting alone; null for the rest.</summary>
    public override Task<int>? RunAsync(IReadOnlyList<string> args, DriverConfig before, DriverChoicesWorld world, CancellationToken ct) =>
        args.Count > 1 && args[1] == "found" ? Task.FromResult(StaffingVerbs.Run(args, before, world)) : null;

    public override ChoicePress Plan(IReadOnlyList<string> args, DriverConfig before, DriverChoicesWorld world) => throw new ChoiceWords(Usage);

    public override int? Said(IReadOnlyList<string> args, DriverConfig choices, DriverChoicesWorld world) => throw new ChoiceWords(Usage);
}
