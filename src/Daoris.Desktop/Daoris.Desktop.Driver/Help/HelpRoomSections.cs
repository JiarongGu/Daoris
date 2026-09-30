namespace Daoris.Driver;

/// <summary>
/// One section of Ask Daoris's room (MOD6): the facts it says, described from what the driver holds, and
/// its text, rendered in its place.
/// </summary>
/// <remarks>
/// A section is a class in a file of its own under <c>Help/Room/</c>, registered in
/// <see cref="HelpRoomSections.All"/> by one line; its file also declares on <see cref="HelpMachine"/> the facts
/// it says, so a new section edits no other section's file.
/// </remarks>
internal interface IHelpRoomSection
{
    /// <summary>The facts it says, read from what the driver holds; a section that says none leaves the machine as it is.</summary>
    HelpMachine Describe(HelpMachine machine, HelpMachineSources sources) => machine;

    /// <summary>Its text, whole paragraphs with their blank lines; empty where it has nothing to say.</summary>
    string Render(HelpMachine machine);
}

/// <summary>
/// The room's sections, in the order it renders them (MOD6). A new section is a file under <c>Help/Room/</c>
/// and a line here, where the helper should read it.
/// </summary>
internal static class HelpRoomSections
{
    public static readonly IReadOnlyList<IHelpRoomSection> All =
    [
        new HelpRoomIntro(),
        new HelpRoomMayDo(),
        new HelpRoomMayPropose(),
        new HelpRoomMakingAPlugin(),
        new HelpRoomOwnPlugins(),
        new HelpRoomDoors(),
        new HelpRoomLanding(),
        new HelpRoomPlaces(),
        new HelpRoomWindow(),
        new HelpRoomMachineNow(),
        new HelpRoomWorkspaces(),
        new HelpRoomAgents(),
        new HelpRoomAsks(),
    ];
}
