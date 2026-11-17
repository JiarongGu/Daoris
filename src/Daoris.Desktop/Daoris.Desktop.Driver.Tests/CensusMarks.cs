namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The driver's tests' censuses, each marked by how it judges (<see cref="CensusScope"/>); <c>CensusScopeTests</c> holds that every class
/// carrying the Census trait is here once and every diff-able one reads through the scope. A new census is listed where its rule puts it.
/// </summary>
internal static class CensusMarks
{
    /// <summary>
    /// The censuses whose rule holds of each file alone and that read their files through the scope: at a merge, the merge's files. A
    /// list held both ways judges the listed members of the scope's files; a count of the walk's reach (the walk still finds the
    /// sources) is held by a run with no scope, and a class whose few file facts sit beside behaviour reads those facts through the scope.
    /// </summary>
    internal static readonly string[] Diff =
    [
        "DeclarationBoundaryTests", "DeclarationKeepersCensusTests", "DeclarationLiteralsCensusTests", "GitCommandsCensusTests", "GitEndingsCensusTests",
        "GitKeepersCensusTests", "GitLifetimesTests", "GitObjectFormsCensusTests", "GitStartsCensusTests", "HeldFactsTests", "LandedByIdsCensusTests",
        "LineAccountCallersCensusTests", "LoopbackListensTests", "NoConsoleWindowTests", "OrderReadersCensusTests", "PermissionWritersCensusTests",
        "ProcessHomeCensusTests", "ShellStructureTests", "StepAuthorityCensusTests", "StewardNotesTests", "StructureTests",
    ];

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
        "DriverWorkflowsTextTests", "EveryChildIsHandedTheToolsTests", "GitBranchesReadTests", "GitCeilingCensusTests",
        "GitLaunchTests", "GitRemotesTests", "GitSeedTests", "HelpRoomSettingsPlacesTests", "HelpRoomTests",
        "LandedBranchWithdrawalTests", "LandingKindCensusTests", "LayerCensusTests",
        "LineCourseTests", "LookSaidTests", "MoveStoreCensusTests", "NativeWorkFramesCensusTests", "NeedsTraitTests",
        "NodeDispatchCensusTests", "ParticipantWorkReadersTests",
        "PickerCensusTests", "PluginCatalogTests", "PoolReadersCensusTests", "ProcessEnvironmentCensusTests",
        "ProcessStartsCensusTests", "ProviderCensusTests", "PullRequestOccasionTests", "RoleCatalogCensusTests", "RoleCensusTests",
        "RoleStaffingTests", "RoleVerbUsageTests", "RoomSaysNoActorTests", "RoomsDeclaredCensusTests", "RulePathsCensusTests", "SayKeysCensusTests",
        "ServiceProviderCensusTests", "SessionMarkerTests", "SettledReadersCensusTests", "SharedCompileTests",
        "ShortWaitsCensusTests", "SlotSettledCensusTests", "SourceFilesTests", "SourceTextsTests",
        "StoredOldNamesCensusTests", "SystemWiringTests", "TerminalConversationCensusTests", "TerminalStartsCensusTests",
        "TestHomesCensusTests", "ToolsAtTheStartCensusTests", "WiringLeftOverTests", "WorkFrameCensusTests",
    ];
}
