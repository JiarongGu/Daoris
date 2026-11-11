namespace Daoris.Driver;

/// <summary>
/// The routes the standing choices' command sends while Daoris runs, each the window twin of the choice it edits, and the verbs no
/// route takes, each with why it acts alone. Every one is the person's. Held to the census by <c>PipeCensusTests</c>.
/// </summary>
public static class ChoiceRoutes
{
    private static InstanceRoute Person(string route) => new(InstanceRoute.DriverModule, route, RouteClass.Person);

    public static InstanceRoute Drivable { get; } = Person("SET_DRIVABLE");
    public static InstanceRoute Hold { get; } = Person("SET_HOLD");
    public static InstanceRoute Notify { get; } = Person("SET_NOTIFY");
    public static InstanceRoute CoolOff { get; } = Person("SET_COOLOFF");
    public static InstanceRoute Quiet { get; } = Person("SET_QUIET");
    public static InstanceRoute Cap { get; } = Person("SET_CAP");
    public static InstanceRoute Terminal { get; } = Person("SET_TERMINAL");
    public static InstanceRoute Manifest { get; } = Person("SET_MANIFEST");
    public static InstanceRoute Retry { get; } = Person("RETRY_QUEST");
    public static InstanceRoute Line { get; } = Person("SET_LINE");
    public static InstanceRoute Language { get; } = Person("SET_LANGUAGE");
    public static InstanceRoute ReadAcross { get; } = Person("SET_READ_ACROSS");
    public static InstanceRoute WriteAcross { get; } = Person("SET_WRITE_ACROSS");
    public static InstanceRoute Standing { get; } = Person("SET_STANDING");
    public static InstanceRoute Review { get; } = Person("SET_REVIEW");
    public static InstanceRoute Opinion { get; } = Person("SET_OPINION");
    public static InstanceRoute Helpers { get; } = Person("SET_HELPERS");
    public static InstanceRoute HandOver { get; } = Person("SET_HAND_OVER");
    public static InstanceRoute Commits { get; } = Person("SET_COMMITS");
    public static InstanceRoute Keep { get; } = Person("SET_KEEP");
    public static InstanceRoute Role { get; } = Person("SET_ROLE");
    public static InstanceRoute WorkflowSave { get; } = Person("WORKFLOW_SAVE");
    public static InstanceRoute WorkflowUse { get; } = Person("WORKFLOW_USE");
    public static InstanceRoute WorkflowKind { get; } = Person("WORKFLOW_KIND");
    public static InstanceRoute WorkflowLanded { get; } = Person("WORKFLOW_LANDED");

    public static IReadOnlyList<(string Verb, InstanceRoute? Route, string Reason)> Verbs { get; } =
    [
        ("driver drive|undrive <repo>", Drivable, "the window's drive switch"),
        ("driver hold|resume <repo>", Hold, "the window's hold"),
        ("driver notify on|off", Notify, "the window's notify switch"),
        ("driver cooloff <minutes>", CoolOff, "the window's cool-off"),
        ("driver quiet <minutes>", Quiet, "the window's quiet bound"),
        ("driver cap <n>", Cap, "the window's session cap"),
        ("driver terminal ask|act", Terminal, "the window's tier of a terminal's presses"),
        ("driver manifest files|minutes <n>", Manifest, "the window's bounds on a folder's manifest read"),
        ("driver retry <quest>", Retry, "the quest's *Try again*, which judges from the planner's verdict whether to mark or release it"),
        ("driver line", Line, "the repository's or the workspace's line"),
        ("driver language", Language, "the session language"),
        ("driver across … read", ReadAcross, "reading across"),
        ("driver across … write-to", WriteAcross, "writing across"),
        ("driver standing", Standing, "the standing answer"),
        ("driver review", Review, "the review rule; its procedure is looked for by the instance in the registry's checkouts"),
        ("driver opinion", Opinion, "the second-opinion rule"),
        ("driver helpers", Helpers, "the helpers rule"),
        ("driver handover", HandOver, "the hand-over rule"),
        ("driver commits", Commits, "the commit rule"),
        ("driver keep <kind> …", Keep, "the Disk space card's rule of that kind"),
        ("driver role set|remove|default|policy|rules …", Role, "a working role's record, choices and policy, or a place's supervisor rules"),
        ("driver retry <quest> --at <n>", null, "alone: RETRY_QUEST marks at the failures its verdict counts, never at a number the person gives"),
        ("driver retry <quest> --session <id>", null, "alone: RETRY_QUEST releases the stop its verdict names, never a session the person names"),
        ("driver brainstorms", null, "alone: no route sets the brainstorm policy"),
        ("driver workflow new|edit|apply|import", ChoiceRoutes.WorkflowSave, "the window's workflow save, a version at a time"),
        ("driver workflow use", ChoiceRoutes.WorkflowUse, "the window's choice of a workflow for a place"),
        ("driver workflow kind", ChoiceRoutes.WorkflowKind, "the window's declaration of a kind of task"),
        ("driver workflow use … --landed", ChoiceRoutes.WorkflowLanded, "the window's landed route, whose gates the instance reads at the line's tip"),
    ];
}
