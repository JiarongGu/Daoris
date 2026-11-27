namespace Daoris.Driver;

/// <summary>What a check asked with no gate and no command runs in its asker's tree, or why it runs nothing.</summary>
/// <param name="Handback">The repository's required hand-back commands (<see cref="HandbackGates"/>), in the order it declares them; empty where it requires none.</param>
/// <param name="Heavy">Whether the repository's heavy gates are run instead (<see cref="HeavyGates"/>): it requires no hand-back command and marks or is mapped heavy gates.</param>
/// <param name="Empty">Why there is nothing to run, as the sentence the asker reads; null where something runs.</param>
internal sealed record CheckPlan(IReadOnlyList<DeclaredGate> Handback, bool Heavy, string? Empty);

/// <summary>
/// What a check asked with no command runs, judged from the tree's own declaration before the ask is queued or admitted, so an ask with nothing
/// to run is answered at once and never waits for a slot to be told so.
/// </summary>
/// <remarks>
/// The repository's required hand-back commands are what a session runs before it is done (<see cref="HandbackGates.Told"/>), so they are what
/// asking for "the checks" means; <c>run-heavy</c>'s gates are run only where the repository or this machine names them.
/// </remarks>
internal static class CheckPlans
{
    /// <summary>The plan for <paramref name="folder"/>, <paramref name="mapped"/> asked for this machine's heavy gates only where its file marks none.</summary>
    internal static CheckPlan Of(string folder, Func<IReadOnlyList<HeavyGate>?> mapped)
    {
        var handback = HandbackGates.OfFolder(folder);
        if (handback.FirstOrDefault(gate => gate.Run.Contains("{base}", StringComparison.Ordinal) || gate.Run.Contains("{tier}", StringComparison.Ordinal)) is { } unbound)
        {
            return new CheckPlan([], false,
                $"the required hand-back gate `{unbound.Name}` runs a command with a `{{base}}` or `{{tier}}` that a check asked with no command does not bind: "
                + "declare a `handback` command for it that names its base, or ask `check_run` for a command of your own as its `command`.");
        }

        if (handback.Count > 0) return new CheckPlan(handback, false, null);

        var marks = HeavyGates.Marks(folder);
        if (marks.Marks is null) return new CheckPlan([], false, $"{marks.Refusal} A check asked with no command has nothing to run until it reads.");
        if (marks.Marks == true || mapped() is { Count: > 0 }) return new CheckPlan([], true, null);

        return new CheckPlan([], false,
            $"{DeclarationNames.Gates} marks no gate `\"handback\"` and none `\"heavy\"`, and this machine sets no heavy gates for it, so a check asked with no command "
            + "has nothing to run. Run the checks your change reaches yourself, or ask `check_run` for a declared gate by its `gate` name or for a command "
            + "of your own as its `command`.");
    }
}
