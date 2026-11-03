namespace Daoris.Driver;

/// <summary>
/// One transitional read the driver keeps (D228; the oneway design §1.3): an older way kept for an old record or an older build,
/// with the condition that ends it and the row that deletes it. Its kept sites carry <c>// transitional: &lt;Id&gt;</c>, and
/// <c>tools/transitional-markers.mjs</c> holds the markers, the entries and the rows together.
/// </summary>
public sealed record TransitionalEntry
{
    /// <summary>The name the markers carry.</summary>
    public required string Id { get; init; }

    /// <summary>The code it keeps, file:line against the driver's folder.</summary>
    public required string Kept { get; init; }

    /// <summary>What reaches it: the records or the builds that still need the old way.</summary>
    public required string Reaches { get; init; }

    /// <summary>The condition that ends it, in words.</summary>
    public required string Ends { get; init; }

    /// <summary>The row that deletes the read, its marker and this entry; the gate fails once the row is closed.</summary>
    public required string Row { get; init; }
}

/// <summary>
/// Every transitional read the driver keeps, in one plain list (oneway design §1.3): the old workflow engine's and the old command names',
/// which their own rows delete with the list (D232).
/// </summary>
public static class TransitionalRegister
{
    public static IReadOnlyList<TransitionalEntry> Entries { get; } =
    [
        // WORKFLOWCLEAN1 §4.7's kept reads: the old workflow engine's, which the workflow step of the migration converts and its row deletes.
        new()
        {
            Id = "steps-engine",
            Kept = "Workflows/WorkflowProcesses.cs:106",
            Reaches = "A run bound to a version saved as steps, and a new start on a steps workflow not converted yet: D157's engine runs it.",
            Ends = "No open steps run and no steps newest.",
            Row = "WORKFLOWCLEAN1z",
        },
        new()
        {
            Id = "current-followed",
            Kept = "Workplace/Landing.cs:887, Workflows/Nodes/NodeGraphs.cs:1121, Workflows/Domain/WorkflowCurrent.cs:162",
            Reaches = "A place on Current, and a unit bound to Current, on a home not converted: its landing rule chosen and drawn as the workflow it follows.",
            Ends = "No place resolves to Current, no Current-bound unit is open, and no leftover rule is held.",
            Row = "WORKFLOWCLEAN1z",
        },
        new()
        {
            Id = "auto-lander",
            Kept = "Workplace/AutoLander.cs:57",
            Reaches = "Work landed by the old doors (the auto lander's choosing, the presses' landing in place, the tidy) for a run on Current, steps or `handed`.",
            Ends = "As the three reads above end.",
            Row = "WORKFLOWCLEAN1z",
        },
        new()
        {
            Id = "work-cycles",
            Kept = "Workflows/Nodes/NodeGraphs.cs:1205",
            Reaches = "A place on Current whose quests close by a work cycle, which a saved workflow does not run yet.",
            Ends = "driver.json holds no `workCycles` and no `workspaceWorkCycles`, once a saved workflow runs a work cycle and the place is converted.",
            Row = "WORKFLOWCLEAN1j",
        },
        new()
        {
            Id = "conversion-readers",
            Kept = "Conversion/WorkflowLegacy.cs:44, Conversion/LegacyGraphs.cs:23",
            Reaches = "A home whose old workflow records were never converted: the workflow conversion's census and translator still read them.",
            Ends = "The migration's workflow step converts them, once.",
            Row = "ONEWAY1ag",
        },
        new()
        {
            Id = "roles-mode-setup",
            Kept = "../../Daoris.Web/src/agents/modes.ts:31",
            Reaches = "A window whose browser storage still holds the retired Roles mode: it opens on Setup, which replaced it.",
            Ends = "One release after SETUPUX1y. The storage is the window's, so its row deletes it.",
            Row = "WORKFLOWCLEAN1z",
        },
        new()
        {
            Id = "handed-before-block",
            Kept = "Events/Domain/HandedCodes.cs:26, Events/Domain/HandedCodes.cs:44, Events/Domain/HandedCodes.cs:53, Events/Domain/HandedCodes.cs:57",
            Reaches = "The page's account of what a session was handed, whose fixtures and cuts still name a `map`, `look`, `indexes` or `standing` "
                + "section: the codes stay declared, and the page's `work.handed.section.*` rows word them. The migration writes the logs' accounts as the block.",
            Ends = "The page's handed account, its fixtures and its cuts name the standing block alone.",
            Row = "NOCOMPAT1",
        },
        new()
        {
            Id = "old-command-names",
            Kept = "Platform/CommandWords.cs:93, Platform/CommandWords.cs:214, ../Daoris.Desktop.Driver.Host/Program.cs:43",
            Reaches = "A session an older build briefed with `daoris-driver …` or a nested family (`daoris driver …`), carried on over the update: "
                + "each old form is refused naming its new one, and the install's `app/bin/daoris-driver` launcher, kept one release, runs nothing "
                + "and says the same.",
            Ends = "No open session was started before this install took the command tree's build (ONEWAY1ai), so none was briefed with an old name.",
            Row = "ONEWAY1af",
        },
    ];
}
