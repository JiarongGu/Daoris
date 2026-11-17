namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The driver's tests' censuses, each marked by how it judges (<see cref="CensusScope"/>); <c>CensusScopeTests</c> holds that every class
/// carrying the Census trait is here once and every diff-able one reads through the scope. A new census is listed where its rule puts it.
/// </summary>
internal static class CensusMarks
{
    /// <summary>
    /// The censuses whose rule holds of each file alone and that read their files through the scope: at a merge, the merge's files, and
    /// for a semantic census (one that binds the compilations) each file that names a type of theirs too (<see cref="CensusReach"/>);
    /// the keepers' flow, which crosses the files of one project, runs whole in each project the scope reaches.
    /// </summary>
    internal static readonly string[] Diff =
    [
        "DeclarationBoundaryTests", "DeclarationKeepersCensusTests", "DeclarationLiteralsCensusTests", "GitEndingsCensusTests", "GitKeepersCensusTests",
        "GitLifetimesTests", "GitObjectFormsCensusTests", "GitStartsCensusTests", "ShellStructureTests", "StructureTests",
    ];

    /// <summary>
    /// The semantic censuses marked whole, each with why its rule compares across the whole tree, so its walk binds every tree at a merge
    /// too; a semantic census not here reads the scope. None today.
    /// </summary>
    internal static readonly Dictionary<string, string> SemanticWhole = new(StringComparer.Ordinal);

    /// <summary>
    /// The censuses that judge the whole tree whatever the scope: a rule over several files (a list held both ways by file, a graph of
    /// the modules, the compilations' banned names with every allowance used, a count), a census of a table or of the tests themselves,
    /// and the censuses that do not read through the scope yet, each diff-able once its walk does.
    /// </summary>
    internal static readonly string[] Whole =
    [
        "AccountSetupTests", "ActorChoicesTests", "BuildDoorConsolesCensusTests", "CategoryTests", "CensusScopeTests", "CensusTraitTests",
        "CensusReachTests", "CheckoutWritersCensusTests", "ConfigSeamTests", "ContinuedReadersCensusTests", "ConversationMembersCensusTests",
        "ConversationRunCensusTests", "CourseCensusTests", "DaorisDriverWordsTests", "DeclarationFileCensusTests",
        "DeclarationGitCensusTests", "DeclarationParsesCensusTests",
        "DeclarationViewsCensusTests", "DelegateKeyStripTests", "DriverChoicesTextTests", "DriverFilesTextTests", "DriverRegistryTextTests",
        "DriverWorkflowsTextTests", "EveryChildIsHandedTheToolsTests", "GitBranchesReadTests", "GitCeilingCensusTests", "GitCommandsCensusTests",
        "GitLaunchTests", "GitRemotesTests", "GitSeedTests", "HeldFactsTests", "HelpRoomSettingsPlacesTests", "HelpRoomTests",
        "LandedBranchWithdrawalTests", "LandedByIdsCensusTests", "LandingKindCensusTests", "LayerCensusTests", "LineAccountCallersCensusTests",
        "LineCourseTests", "LookSaidTests", "LoopbackListensTests", "MoveStoreCensusTests", "NativeWorkFramesCensusTests", "NeedsTraitTests",
        "NoConsoleWindowTests", "NodeDispatchCensusTests", "OrderReadersCensusTests", "ParticipantWorkReadersTests", "PermissionWritersCensusTests",
        "PickerCensusTests", "PluginCatalogTests", "PoolReadersCensusTests", "ProcessEnvironmentCensusTests", "ProcessHomeCensusTests",
        "ProcessStartsCensusTests", "ProviderCensusTests", "PullRequestOccasionTests", "RoleCatalogCensusTests", "RoleCensusTests",
        "RoleStaffingTests", "RoleVerbUsageTests", "RoomSaysNoActorTests", "RoomsDeclaredCensusTests", "RulePathsCensusTests", "SayKeysCensusTests",
        "ServiceProviderCensusTests", "SessionMarkerTests", "SettledReadersCensusTests", "SharedCompileTests",
        "ShortWaitsCensusTests", "SlotSettledCensusTests", "SourceFilesTests", "SourceTextsTests", "StepAuthorityCensusTests", "StewardNotesTests",
        "StoredOldNamesCensusTests", "SystemWiringTests", "TerminalConversationCensusTests", "TerminalStartsCensusTests",
        "TestHomesCensusTests", "ToolsAtTheStartCensusTests", "WiringLeftOverTests", "WorkFrameCensusTests",
    ];
}
