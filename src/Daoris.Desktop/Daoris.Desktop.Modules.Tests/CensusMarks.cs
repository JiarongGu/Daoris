namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The modules' tests' censuses, each marked by how it judges (<see cref="CensusScope"/>, linked from the driver's tests);
/// <c>CensusMarksTests</c> holds that every class carrying the Census trait is here once. A new census is listed where its rule puts it.
/// </summary>
internal static class CensusMarks
{
    /// <summary>The censuses whose rule holds of each file alone and that read their files through the scope: none here yet.</summary>
    internal static readonly string[] Diff = [];

    /// <summary>
    /// The censuses that judge the whole tree whatever the scope: a rule over several files (the modules' lines and routes, a table held
    /// both ways), a census of the tests themselves, and the censuses that do not read through the scope yet.
    /// </summary>
    internal static readonly string[] Whole =
    [
        "AccountsStateSliceTests", "CensusMarksTests", "DriverLoopChatTakenUpTests", "DriverModuleLinesTests", "DriverModuleRoutesTests",
        "EveryModuleChildIsHandedTheToolsTests", "MachineSettingsCensusTests", "OwnerSetCensusTests", "ParticipantWorkReadersTests",
        "PipeCensusTests", "PipeGroundsCensusTests", "PluginRecordWiringTests", "ProcessStartsCensusTests", "RefusalCatalogueTests",
        "SettingDoorsCensusTests", "StartStagesTests", "StepWaitCensusTests", "StructureTests", "UsageStateSliceTests", "WindowCensusTests",
    ];
}
