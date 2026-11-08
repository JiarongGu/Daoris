namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A site of the census: a file under the desktop's sources, a pattern its text holds, the row that moves it and what it reads there.
/// </summary>
/// <param name="File">The file's path under <c>src/Daoris.Desktop</c>, with forward slashes.</param>
/// <param name="Token">A regular expression the file's text matches while the site stands.</param>
/// <param name="Row">The pool's row that moves the site to ask the pool where a line is.</param>
/// <param name="Reads">What it reads there.</param>
internal sealed record PoolReader(string File, string Token, string Row, string Reads);

/// <summary>
/// The three lists the census holds, each reader named with its row. All are <i>not yet moved</i>: a row that moves one takes it out of
/// its list or marks it moved, and a site that is in no list fails the census.
/// </summary>
internal static class PoolReaders
{
    private const string Driver = "Daoris.Desktop.Driver/";

    private const string Modules = "Daoris.Desktop.Modules/";

    /// <summary>Readers that open a record's tree as a folder: a pooled line's record names a line, so each asks the pool where it stands.</summary>
    internal static IReadOnlyList<PoolReader> OpenAsFolder { get; } =
    [
        new(Driver + "Loop/Run/RunState.cs", @"public string WorkTree", "c", "the run's tree, which the start's record, agent and trust read as a folder"),
        new(Driver + "Loop/Run/Open/OpenRecord.cs", @"state\.WorkTree", "c", "the tree the record opens with"),
        new(Driver + "Loop/Run/Hand/HandToSession.cs", @"state\.WorkTree", "c", "the tree the agent is handed"),
        new(Driver + "Loop/Run/Trust/TrustAsk.cs", @"run\.State\.WorkTree", "c", "the tree the agent's trust is asked for"),
        new(Driver + "Loop/Chat/ChatStarts.cs", @"!Directory\.Exists\(tree\)", "c", "the conversation's tree, found or made again"),
        new(Driver + "Course/ConversationTrees.cs", @"Directory\.Exists\(tree\)", "c", "whether a conversation's tree stands, or can be brought back"),
        new(Driver + "Course/Domain/Continuation.cs", @"treeStands\(tree\)", "c", "whether words going on have a tree to go on in"),
        new(Driver + "Loop/DriverContinuations.cs", @"park\.Tree", "c", "the tree a parked line goes on in"),
        new(Driver + "Course/Chat/BriefedMove.cs", @"ConversationTrees\.Stands", "c", "the tree a briefed move goes on in"),
        new(Driver + "Course/Chat/LimitMove.cs", @"prior\.Tree", "c", "the tree a limit move goes on in"),
        new(Driver + "Loop/Helpers/HelperSettling.cs", @"CaptureAsync\(asker\.Tree", "c", "the asker's HEAD and status, its own in a slot, which the helper wait holds"),
        new(Driver + "Workplace/ToolHomeLeftovers.cs", @"ProjectFolders", "c", "a slot's project folder in an agent's home, gone only with the slot"),
        new(Driver + "Workplace/TreeReturns.cs", @"Directory\.Exists\(full\)", "e", "TREEGONE1's test of a gone tree and its remake"),
        new(Driver + "Workplace/TreeFollows.cs", @"LearnTreesAsync", "c", "the branch a line's folder shows at each tidy pass"),
        new(Driver + "Workplace/SessionLocks.cs", @"HolderOf\(tree\)", "b", "the lock of the tree a session holds"),
        new(Driver + "Workplace/TreeSetAside.cs", @"Directory\.Exists\(tree\)", "d", "the tidy's set-aside of a finished line's tree"),
        new(Driver + "Workplace/TreeSweeps.cs", @"Directory\.Exists\(tree\)", "d", "the sweep's read of a finished line's tree"),
        new(Driver + "Workplace/TreeRemovals.cs", @"Directory\.Exists\(full\)", "d", "the person's press removing a tree"),
        new(Driver + "Workplace/SystemSpace.cs", @"HomeEntry\.Trees", "d", "the Disk space card's reading of the trees folder"),
        new(Driver + "Workplace/ContentHolds.cs", @"Directory\.GetCreationTimeUtc\(tree\)", "c", "when the tree was opened, for a slot the moment its present line took it"),
        new(Driver + "Workplace/LandPerformer.cs", @"TreeFolders\.TreeGone", "e", "the landing's two gone reads"),
        new(Driver + "Workplace/SessionReviews.cs", @"TreeFolders\.TreeGone", "c", "a session's review of a tree that may be gone"),
        new(Driver + "Review/ReviewDesk.cs", @"ServeAsync", "e", "the review served from a tree, and Show it again"),
        new(Modules + "Windows/TerminalModule.cs", @"TreeVisit\.Take", "d", "the terminal opened in a tree"),
        new(Driver + "Loop/Run/Tree/GrowTree.cs", @"BranchNowAsync\(parentTree", "c", "a chain's branch now, asked of the parent's tree"),
        new(Driver + "Loop/Session/PartsHanded.cs", @"BranchNowAsync\(tree", "c", "a part's branch now, asked of its tree"),
        .. FolderGoneReaders,
    ];

    /// <summary>Readers that derive a branch from a folder git lists: a slot's folder would answer a branch nobody made.</summary>
    internal static IReadOnlyList<PoolReader> BranchFromFolder { get; } =
    [
        new(Driver + "Review/ChatPresses.cs", @"BranchOfTree", "c", "the branch of a conversation's tree a press acts on"),
        new(Driver + "Workflows/Domain/WorkflowGraphRun.cs", @"BranchOfTree", "c", "the branch a run's tree names"),
        new(Driver + "Workplace/SessionBranchDiscard.cs", @"BranchOfTree", "c", "the branch a discarded tree names"),
        new(Driver + "Workplace/SessionTrees.cs", @"BranchOfTree", "c", "the front's branch of a tree"),
        new(Driver + "Workplace/TreeBranches.cs", @"BranchOfTree", "c", "a tree's branch in the branch reads"),
        new(Driver + "Workplace/TreeFollows.cs", @"BranchOfTree", "c", "what finds a session's branch once its tree is gone"),
        new(Driver + "Workplace/TreeRemovals.cs", @"BranchOfTree", "d", "the branch a removed tree names"),
        new(Driver + "Workplace/TreeReturns.cs", @"BranchOfTree", "e", "the branch a gone tree is remade on"),
        new(Driver + "Workplace/TreeUpdates.cs", @"BranchOfTree", "c", "the branch a tree's update names"),
        new(Driver + "Workplace/TreeWorkReads.cs", @"BranchOfTree", "c", "the branch of the tree a work read opens"),
    ];

    /// <summary>
    /// Every in-use set built from records and every test of a listed folder against one: a slot's folder is no record's path, so each set
    /// adds, for each held line, the slot the pool names, and the landing's set and the follow's test are among them.
    /// </summary>
    internal static IReadOnlyList<PoolReader> InUseSets { get; } =
    [
        new(Driver + "Workplace/TidyHolders.cs", InUse, "c", "the tidy's in-use set and the trees sessions hold"),
        new(Driver + "Workplace/BranchTidying.cs", InUse, "c", "the tidy's front over that set"),
        new(Driver + "Loop/Watch/WatchRun.cs", InUse, "c", "the headless look's set"),
        new(Driver + "Help/HelpHousekeeping.cs", InUse, "c", "Ask Daoris's housekeeping set"),
        new(Driver + "Knowledge/HelpWorkFacts.cs", @"var inUse", "c", "the knowledge room's set from the active sessions"),
        new(Driver + "WorkOrders/Inventory/WorkInventories.cs", InUse, "c", "the inventory's set"),
        new(Driver + "WorkOrders/Inventory/WorkUnits.cs", InUse, "c", "the inventory's test of a tree against the set"),
        new(Driver + "Review/ReviewLandingWorld.cs", InUse, "c", "the landing's set: the active sessions' trees"),
        new(Driver + "Workplace/LandPerformer.cs", InUse, "c", "the landing handed that set"),
        new(Driver + "Workplace/WorkplaceReview.cs", InUse, "c", "the seam that gives the landing its set"),
        new(Driver + "Workplace/LandingPress.cs", InUse, "c", "the landing press and LANDSYNC1's follow"),
        new(Driver + "Workplace/TreeLandSyncs.cs", InUse, "c", "the follow's test that would detach a slot under its running line"),
        new(Driver + "Workplace/TreeLands.cs", InUse, "c", "the landing's use of the set"),
        new(Driver + "Workplace/TogetherAccepts.cs", InUse, "c", "a together acceptance's landing set"),
        new(Driver + "Workplace/AutoTidies.cs", InUse, "c", "the auto tidy's set"),
        new(Driver + "Workplace/TidyEffect.cs", InUse, "c", "the tidy's effect over the set"),
        new(Driver + "Workplace/LandedCleanups.cs", InUse, "c", "the landed cleanup's set"),
        new(Driver + "Workplace/SessionLocks.cs", InUse, "c", "the look's let-go test of a session's lock"),
        new(Driver + "Workplace/SessionTrees.cs", InUse, "c", "the front's use of the set"),
        new(Driver + "Workplace/TreeBranches.cs", InUse, "c", "the branch reads' test against the set"),
        new(Driver + "Workplace/TreeStrays.cs", InUse, "c", "the strays' busy folders"),
        new(Driver + "Workplace/TreeSweeps.cs", InUse, "c", "the sweep's test of a listed folder against the set"),
        new(Driver + "Workplace/TreeSync.cs", InUse, "c", "the sync's use of the set"),
        new(Modules + "Bridge/Routes/HelpScreenDoors.cs", InUse, "c", "the help screen's set"),
        new(Modules + "Bridge/Routes/LinesRoutes.cs", InUse, "c", "the lines' set"),
        new(Modules + "Bridge/Routes/TreesTidyDesk.cs", InUse, "c", "the trees desk's set"),
    ];

    /// <summary>A file that matches a list's signal and is no site of it, and why.</summary>
    internal static IReadOnlyDictionary<string, string> NotSites { get; } = new Dictionary<string, string>
    {
        [Driver + "Plugins/Relay/RelayCopy.cs"] = "its in-use is a marker file in a copy's folder",
    };

    /// <summary>The signal of each list: a file whose text matches it is a site and is listed.</summary>
    internal const string InUseSignal = @"\bInUseAsync\b|\bHeldTreesAsync\b|IReadOnlySet<string>>?\??\s+inUse\b|HashSet<string>\??\s+inUse\b|\binUse\.Contains\b";

    /// <summary>What a listed in-use site holds while it stands: any of the set's names.</summary>
    private const string InUse = @"\binUse\b|\bInUseAsync\b|\bHeldTreesAsync\b";

    internal const string FolderGone = @"TreeFolders\.(?:TreeGone|Walkable|SetAside)\b";

    internal const string BranchOf = @"\bBranchOfTree\b";

    /// <summary>The readers found by the folder test alone, each a site on that test.</summary>
    private static IEnumerable<PoolReader> FolderGoneReaders =>
    [
        new(Driver + "Loop/OpinionDeliverer.cs", FolderGone, "c", "an opinion's copy of a tree"),
        new(Driver + "Review/OpinionLook.cs", FolderGone, "c", "an opinion's look at a tree"),
        new(Driver + "Review/OpinionPresses.cs", FolderGone, "c", "an opinion's press on a tree"),
        new(Driver + "Workflows/WorkflowKeepCommand.cs", FolderGone, "c", "a workflow's keep of a tree"),
        new(Driver + "Workplace/HistoryClearing.cs", FolderGone, "c", "the history clearing's walk of a tree"),
        new(Driver + "Workplace/KnowledgeWorkplaceReads.cs", FolderGone, "c", "the knowledge server's reads of a tree"),
        new(Driver + "Workplace/OnlyHereProof.cs", FolderGone, "c", "the proof that work is only in a tree"),
        new(Driver + "Workplace/ReviewGraphReads.cs", FolderGone, "c", "the review graph's read of a tree"),
        new(Driver + "Workplace/SessionDeletion.cs", FolderGone, "c", "a deleted session's tree"),
        new(Driver + "Workplace/TreeFolders.cs", @"static class TreeFolders", "c", "the folder test itself"),
        new(Driver + "Workplace/TreeWorkReads.cs", FolderGone, "c", "the reads of a tree's work"),
        new(Driver + "Workplace/TreeReadMemo.cs", FolderGone, "c", "the memo of a tree's reads"),
        new(Modules + "Bridge/Routes/SessionsGroupsDesk.cs", FolderGone, "c", "the sessions desk's tree facts"),
        new(Modules + "Bridge/Routes/TreesLandDesk.cs", FolderGone, "e", "the landing desk's gone-tree read"),
        new(Modules + "Bridge/Routes/TreesReviewWires.cs", FolderGone, "c", "the review wires' tree facts"),
        new(Modules + "Bridge/Routes/TreesRoutes.cs", FolderGone, "c", "the trees routes' folder test"),
    ];
}
