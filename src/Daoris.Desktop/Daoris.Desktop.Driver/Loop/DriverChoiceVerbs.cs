namespace Daoris.Driver;

/// <summary>
/// The verbs of <c>daoris-driver driver</c> that the subdomains above the settings own, wired into the terminal's world (FOUNDATION1
/// mod-iii; the design §14.3: a lower subdomain declares a port and the higher implements it, wired by Loop). The settings' command
/// (<see cref="DriverChoicesCommand"/>) names none of them: each host that builds a <see cref="DriverChoicesWorld"/> hands it this set
/// as its <see cref="DriverChoicesWorld.Verbs"/>.
/// </summary>
public static class DriverChoiceVerbs
{
    /// <summary>Every verb the settings' command hands on, each by the subdomain whose acts it drives, a quest's <i>Try again</i> handed no way to read the planner's verdicts.</summary>
    public static IReadOnlyList<ChoiceVerb> All { get; } =
        With((_, _) => throw new DriverException("this command was handed no way to read them"));

    /// <summary>
    /// Every verb, a quest's <i>Try again</i> handed <paramref name="verdicts"/>: the planner's verdict on each quest, over a fresh snapshot
    /// of the service this machine's driver watches.
    /// </summary>
    public static IReadOnlyList<ChoiceVerb> With(Func<DriverConfig, CancellationToken, Task<IReadOnlyList<Consideration>>> verdicts) =>
    [
        new RetryVerb(verdicts),
        new HelpersVerb(),
        new HandOverVerb(),
        new AdapterVerb(),
        new HelperVerb(),
        new IntakeVerb(),
        new ActorVerb(),
        new LandingVerb(),
        new WorkCycleVerb(),
        new WorkflowVerb(),
        new ReviewVerb(),
        new OpinionVerb(home => AdapterSet.Built().WithPlugins(PluginCatalog.Load(home, AdapterSet.Built().Names))),
        new BrainstormsVerb(),
    ];
}
