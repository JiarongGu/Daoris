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
/// Every transitional read the driver keeps, in one plain list (oneway design §1.3): what is left of the old workflow logic, and the handed
/// sections the page still names, which their rows delete with the list (D232).
/// </summary>
public static class TransitionalRegister
{
    public static IReadOnlyList<TransitionalEntry> Entries { get; } =
    [
        new()
        {
            Id = "current-followed",
            Kept = "Workplace/Landing.cs:887, Workflows/Nodes/NodeGraphs.cs:1121",
            Reaches = "A place on Current, and a unit bound to Current: its landing rule chosen and drawn as the workflow it follows.",
            Ends = "No place resolves to Current, no Current-bound unit is open, and no leftover rule is held.",
            Row = "WORKFLOWCLEAN1z",
        },
        new()
        {
            Id = "auto-lander",
            Kept = "Workplace/AutoLander.cs:57",
            Reaches = "Work landed by the old doors (the auto lander's choosing, the presses' landing in place, the tidy) for a run on Current or `handed`.",
            Ends = "As the read above ends.",
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
            Id = "handed-before-block",
            Kept = "Events/Domain/HandedCodes.cs:26, Events/Domain/HandedCodes.cs:44, Events/Domain/HandedCodes.cs:53, Events/Domain/HandedCodes.cs:57",
            Reaches = "The page's account of what a session was handed, whose fixtures and cuts still name a `map`, `look`, `indexes` or `standing` "
                + "section: the codes stay declared, and the page's `work.handed.section.*` rows word them. The migration writes the logs' accounts as the block.",
            Ends = "The page's handed account, its fixtures and its cuts name the standing block alone.",
            Row = "NOCOMPAT1",
        },
        new()
        {
            Id = "routes-by-hand",
            Kept = "Service/ServiceClient.cs:57",
            Reaches = "Every HTTP route not yet declared in Daoris.Contracts: sent by a method of its own here, and mapped by hand in the HTTP "
                + "host's Program.cs, whose own marker stands where its declared routes are mapped.",
            Ends = "No app.Map* call is left in the host's Program.cs and no method that writes a route's path is left in ServiceClient.",
            Row = "CODESHAPE1",
        },
    ];
}
