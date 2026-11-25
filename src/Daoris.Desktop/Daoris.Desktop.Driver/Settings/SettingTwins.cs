namespace Daoris.Driver;

/// <summary>
/// The verbs that write a setting the window writes without sending its route: each writes the same file alone, through the
/// judge the route's handler calls before it writes, so the two refuse alike. A verb that sends its route is its table's
/// (<see cref="ChoiceRoutes"/> and the rest); the modules' <c>SettingDoorsCensusTests</c> holds every setting to one or the other.
/// </summary>
public static class SettingTwins
{
    private static InstanceRoute Driver(string route) => new(InstanceRoute.DriverModule, route, RouteClass.Person);

    /// <summary>Each verb in its words, the window's route it twins, and the judge both call.</summary>
    public static IReadOnlyList<(string Verb, InstanceRoute Route, string Judge)> Alone { get; } =
    [
        ("place access <repository>|--workspace <name> full|<pause>=<value>…|--clear", Driver("SET_ACCESS"), "AccessRules.Apply"),
        ("place checks <repository> light|heavy-at-end|--clear", Driver("SET_SESSION_CHECKS"), "SessionChecks.Apply"),
        ("place checkout <repository> on|off", Driver("SET_CHECKOUT_WRITES"), $"{nameof(DriverConfig)}.{nameof(DriverConfig.WithCheckoutWrites)}"),
        ("place heavy <repository> <name>=<command>…|--clear", Driver("SET_HEAVY_GATES"), "HeavyMapping.Apply"),
        ("workflow values <repository> <workflow> <node> <name>=<json>…|--clear --why <reason>", Driver("SET_WORKFLOW_VALUES"), "WorkflowValuesKey.Apply"),
        ("settings checks <repository> pin|memoryGb|alone|timeoutMs|treeBaseline|devkit|localInputs|patternsFile … [--clear] [--why <reason>]", Driver("SET_CHECK_BUDGETS"), "CheckBudgetsDoor.Apply"),
        ("ask-daoris mode restricted|full", Driver("SET_HELP_MODE"), $"{nameof(DriverConfig)}.{nameof(DriverConfig.WithHelpMode)}"),
        ("gate verify <repository> [--branch <name>|--commit <sha>]", Driver("WORKFLOW_VERIFY"), "VerifyCommand.BindAsync"),
        ("remote add <workspace> --url <url> [--key <key>]", new("DAORIS.REMOTES", "SET", RouteClass.Person), "RemoteOperations.Set"),
        ("remote remove <workspace>", new("DAORIS.REMOTES", "REMOVE", RouteClass.Person), "RemoteOperations.Remove"),
    ];
}
