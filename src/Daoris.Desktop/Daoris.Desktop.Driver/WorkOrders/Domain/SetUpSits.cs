namespace Daoris.Driver;

/// <summary>
/// The planner's read of a set-up step (REVIEWENV1c, D154 points 4 and 5; the review environment design §2.1, §2.2): the environment its
/// rule declares, and why it sits one it cannot honestly start. Work orders owns it, the planner being its lowest reader; the review's
/// <see cref="ReviewSetUps"/> answers through it, and the rule standing for a repository is read through the review facts Knowledge declares.
/// </summary>
internal static class SetUpSits
{
    /// <summary>
    /// The environment a set-up step shows its chain's work in, as the rule standing for its repository here declares it (design
    /// §2.2: handed from the rule as it stands at the start); null for a quest that is no set-up step, and where no rule names it.
    /// </summary>
    internal static ReviewEnvironment? Environment(DriverConfig config, QuestView quest, string? workspace) =>
        quest.SetUpIn is { } named && KnowledgeReviews.Current.Review(config, quest.To, workspace) is { Rule.IsNone: false } rule
            ? rule.Rule.Environments.FirstOrDefault(environment => environment.Name == named)
            : null;

    /// <summary>
    /// Why the planner sits a set-up step it cannot honestly start (design §2.1), or null where it may start: no rule here names its
    /// environment; or, for a local one, no address to show it at, or no window here to show it in (a headless loop, where Daoris's
    /// browser is not handed). Whether its tree holds the procedure is read at the spawn, once the tree exists
    /// (<see cref="ReviewSetUps.ProcedureMissing"/>).
    /// </summary>
    /// <param name="window">Whether this loop carries Daoris's browser: the desktop's does, a headless one does not.</param>
    internal static CannotShowSit? Sits(DriverConfig config, QuestView quest, string? workspace, bool window)
    {
        if (quest.SetUpIn is not { } named) return null;
        if (Environment(config, quest, workspace) is not { } environment)
        {
            return new(new CannotShowFact(CannotShowFact.Unnamed, named),
                $"set-up step `#{quest.Id}` shows its chain's work in `{named}`, and no review rule here names `{named}` for "
                + $"`{quest.To}`: `daoris driver review {quest.To} {named} --kind local|deployed --procedure <path>` declares it.");
        }

        if (environment.Kind != "local") return null;
        if (environment.Address is null)
        {
            return new(new CannotShowFact(CannotShowFact.NoAddress, named),
                $"set-up step `#{quest.Id}` shows its chain's work in `{named}`, which declares no address to show it at: "
                + $"`daoris driver review {quest.To} {named} --address <url>` declares where the app normally runs.");
        }

        return window
            ? null
            : new(new CannotShowFact(CannotShowFact.NoWindow, named, environment.Address),
                $"set-up step `#{quest.Id}` shows its chain's work in Daoris's browser at `{environment.Address}`, and this loop has no "
                + "window to show it in: the desktop's loop starts it.");
    }
}
