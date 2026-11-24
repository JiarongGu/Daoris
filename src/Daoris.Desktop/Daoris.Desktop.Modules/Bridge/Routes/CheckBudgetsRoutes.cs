using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// One change to a repository's check budgets (<c>SET_CHECK_BUDGETS</c>): <c>Member</c> is a <see cref="CheckBudgetsDoor.Members"/> word,
/// <c>Name</c> the gate (the set as <c>gate/set</c> for <c>timeoutMs</c>), <c>Value</c> a number, a boolean, a string or a list of strings by
/// the member, <c>Clear</c> takes it away, and <c>Why</c> is required with a pin.
/// </summary>
public sealed record SetCheckBudgets(string? Repository, string? Member, string? Name, JsonElement? Value, bool Clear = false, string? Why = null);

/// <summary>A repository's check budgets read (<c>CHECK_BUDGETS</c>): <c>Gates</c> names declared gates to show beside those kept, <c>Lane</c> where their defaults come from.</summary>
public sealed record CheckBudgetsRead(string? Repository, IReadOnlyList<string>? Gates, string? Lane);

/// <summary>
/// A repository's check budgets on this machine, kept in the home beside the merge tool's own writes: the pin, memory and load of a gate, a
/// set's timeout, the tree baseline, the devkit version, the local inputs and the patterns file. The person's alone (a pin and a bound file
/// are this machine's grant), judged by <see cref="CheckBudgetsDoor.Apply"/>, the judge <c>daoris settings checks</c> calls too.
/// </summary>
public sealed class CheckBudgetsRoutes(DriverContext context)
{
    private readonly DriverLoop _loop = context.Loop;

    /// <summary>One change, answered as the repository's record as <c>CHECK_BUDGETS</c> says it.</summary>
    [DriverRoute("SET_CHECK_BUDGETS", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("SET_CHECK_BUDGETS", Repository = "repository")]
    [GroundRepository("SET_CHECK_BUDGETS", "repository")]
    [Says("SET_CHECK_BUDGETS", "repository:repository")]
    [Publishes("SET_CHECK_BUDGETS", StateScopes.Driver)]
    private object? SetCheckBudgets(SetCheckBudgets press) =>
        CheckBudgetsDoor.Apply(_loop.Home, new CheckBudgetsAsk(press.Repository, press.Member, press.Name, press.Value, press.Clear, press.Why));

    /// <summary>
    /// A repository's budgets: <c>{ repository, gates: { name: { budget: { minutes, source, why?, rise? }, greens, pin?, memoryGb?, alone } },
    /// setTimeoutMs: { "gate/set": ms }, treeBaseline?, devkit?, localInputs?, patternsFile? }</c>; <c>source</c> is <c>pin</c>, <c>learned</c> or
    /// <c>default</c>, and a member this machine keeps none of is null. Read from the home's file, which the merge tool writes too.
    /// </summary>
    [DriverRoute("CHECK_BUDGETS", PipeClass.Open)]
    [ByEvent("CHECK_BUDGETS", Untold = "the home's check-budgets file, written by the merge tool beside the door")]
    private async Task<object?> CheckBudgetsAsync(CheckBudgetsRead press, CancellationToken cancellationToken)
    {
        var repository = (press.Repository ?? "").Trim();
        if (repository.Length == 0) throw new DriverException("a repository's check budgets are read for a repository: name one.");
        return await Task.Run(() => CheckBudgetsDoor.View(_loop.Home, repository, press.Gates, press.Lane), cancellationToken);
    }
}
