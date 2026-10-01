using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// Why a module said no, in the one form the person actually receives.
/// </summary>
/// <remarks>
/// <para><b>A thrown exception's message never reaches anybody.</b> The host maps an unhandled
/// exception to a generic <c>UNKNOWN_ERROR</c> carrying only the exception TYPE, so every carefully
/// written refusal sentence in these modules was being dropped on the floor — the page showed a
/// generic failure and the person learned nothing. Found by the first tests this half ever had; it
/// survived because the page's own suite mocks the bridge and asserts against a string it invented.</para>
///
/// <para><b>The framework's own shape is a code plus parameters</b>, translated client-side
/// (`errors.&lt;CODE&gt;`), with the message kept as the developer's fallback. So a refusal is declared
/// here once, thrown as <see cref="ShenoraException"/>, and rendered from the catalogue — which also
/// means a refusal speaks the reader's language, like every other sentence the platform owns.</para>
///
/// <para>Every code here must have an entry in BOTH locale catalogues; a test asserts it, because a
/// refusal with no translation is a refusal that reads as a bare code.</para>
/// </remarks>
public static class Refusals
{
    /// <summary>A remote with an address but no key, or the reverse — no remote in any loader.</summary>
    public const string RemoteHalfDeclared = "REMOTE_HALF_DECLARED";

    /// <summary>A declaration form pointed at a folder that has not adopted Daoris.</summary>
    public const string RepositoryNotAdopted = "REPOSITORY_NOT_ADOPTED";

    /// <summary>A conversation asked for before the loop's service is answering.</summary>
    public const string DriverNotReady = "DRIVER_NOT_READY";

    /// <summary>A harness action this build does not have.</summary>
    public const string HarnessActionUnknown = "HARNESS_ACTION_UNKNOWN";
    public const string HarnessProfileNeeded = "HARNESS_PROFILE_NEEDED";

    /// <summary>An answer or a stop for a harness action that is not running.</summary>
    public const string HarnessActionIdle = "HARNESS_ACTION_IDLE";

    /// <summary>
    /// A harness action started while another still runs (REV3). One at a time: two installers racing
    /// over one PATH is not a thing to make easy, and a second login under the same name took the
    /// first's place in the map, so the first could no longer be answered or stopped.
    /// </summary>
    public const string HarnessActionBusy = "HARNESS_ACTION_BUSY";

    /// <summary>
    /// The driver itself refused, in its own words — an unknown adapter, a harness with no toolchain,
    /// an installer that would not start.
    /// </summary>
    /// <remarks>
    /// <b>Rendered verbatim, not translated.</b> `DriverException` is documented as "a driver error a
    /// person can act on", and those sentences name what exists and what to run — the same class as
    /// the service's refusals, which this platform has always shown word for word. Translating them
    /// would mean re-authoring every one in two languages and letting the two drift; carrying the
    /// message as a parameter keeps the one the driver wrote.
    /// </remarks>
    public const string DriverRefused = "DRIVER_REFUSED";

    /// <summary>
    /// A move on a parked session that is not one of the person's three (design §4). The ledger
    /// allows a fourth — back to `working` — and that one is the driver's observation rather than a
    /// button: answering a session so it carries on is a message, not a move.
    /// </summary>
    public const string SessionMoveNotYours = "SESSION_MOVE_NOT_YOURS";

    /// <summary>A decline with nothing in it. The same rule the quest door holds, for its reason.</summary>
    public const string SessionDeclineNeedsReason = "SESSION_DECLINE_NEEDS_REASON";

    /// <summary>
    /// A review asked for where this machine cannot answer it (SURF6): the record names no tree or no
    /// base commit, or the tree is gone. It is INFORMATION rather than a fault — a session whose
    /// record travelled here from another machine has nothing to diff here and never will, and so
    /// does one that predates the base commit being recorded.
    /// </summary>
    public const string SessionNotReviewable = "SESSION_NOT_REVIEWABLE";
    public const string SessionNoBase = "SESSION_NO_BASE";
    public const string SessionRangeUnreadable = "SESSION_RANGE_UNREADABLE";

    /// <summary>
    /// A review asked for where the session's tree is gone from this machine and no landing of it is recorded here
    /// (REVIEW2, D113): a tidy after a merge, or a discard, took it. INFORMATION, in the review's class, and its own
    /// code so the page offers nothing that acts on a tree: there is none to land or to discard. A session whose
    /// landing made a branch is reviewed from that branch instead.
    /// </summary>
    public const string SessionTreeGone = "SESSION_TREE_GONE";

    /// <summary>
    /// The files a person may `@` asked for where git cannot list them (CONV4d): the record names no
    /// tree here, or the tree is not a repository of its own. INFORMATION, like the review's: a path
    /// typed after `@` still reaches the agent, because both doors expand it as typed.
    /// </summary>
    public const string SessionTreeUnlisted = "SESSION_TREE_UNLISTED";

    /// <summary>
    /// A file asked for its preview (PREVIEW1, D111) that the preview does not read: a path outside the
    /// session's tree, a path through a link inside the tree that leads out of it, or a path under `.git`,
    /// which is git's own. Three codes, since each is a different fact about the path.
    /// </summary>
    public const string PreviewOutsideTree = "PREVIEW_OUTSIDE_TREE";
    public const string PreviewLinkLeavesTree = "PREVIEW_LINK_LEAVES_TREE";
    public const string PreviewGitFolder = "PREVIEW_GIT_FOLDER";

    /// <summary>
    /// A preview asked for where there is nothing to read: the record names no tree on this machine or the
    /// tree is gone, or the path is not a file now. INFORMATION, in the review's class.
    /// </summary>
    public const string PreviewNoTree = "PREVIEW_NO_TREE";
    public const string PreviewNotAFile = "PREVIEW_NOT_A_FILE";

    /// <summary>
    /// A preview read from a session's landed branch, once its tree is gone (REVIEW2, D113), of a path that branch holds
    /// no file at: deleted or moved there, a folder, or a link. INFORMATION, like <see cref="PreviewNotAFile"/>, whose
    /// sentence speaks of the tree; this one names the branch.
    /// </summary>
    public const string PreviewNotOnBranch = "PREVIEW_NOT_ON_BRANCH";

    /// <summary>
    /// A window name this build does not open (SURF8). Refused rather than sanitised: the name
    /// becomes both an address and a filename, and "nearly a window name" is not one.
    /// </summary>
    public const string WindowUnknown = "WINDOW_UNKNOWN";

    /// <summary>A plugin id this machine has no folder for (D64).</summary>
    public const string PluginUnknown = "PLUGIN_UNKNOWN";

    /// <summary>A plugin action this build does not have.</summary>
    public const string PluginActionUnknown = "PLUGIN_ACTION_UNKNOWN";
    public const string PluginBusy = "PLUGIN_BUSY";

    /// <summary>The system would not open a plugin's folder, or the plugins folder, in the file manager; its own reason goes with it (PLUGUI1e).</summary>
    public const string PluginFolderNotOpened = "PLUGIN_FOLDER_NOT_OPENED";

    /// <summary>
    /// A plugin's data folder asked to open where it never made one (PLUGUI1e, D119 §4.1). INFORMATION, in the review's
    /// class (D48 §6): the page shows no Open folder then, so this answers a race, a press made as the folder went.
    /// </summary>
    public const string PluginNothingKept = "PLUGIN_NOTHING_KEPT";

    /// <summary>A favorite that is no web page: the bar's rule keeps only `http` and `https` pages (CHR5).</summary>
    public const string BrowserNotAPage = "BROWSER_NOT_A_PAGE";

    /// <summary>A favorites or settings file that could not be read, which an edit will not write over.</summary>
    public const string BrowserFileUnreadable = "BROWSER_FILE_UNREADABLE";

    /// <summary>An extensions setting that is neither `offer` nor `refuse` (CHR7).</summary>
    public const string BrowserSettingUnknown = "BROWSER_SETTING_UNKNOWN";

    /// <summary>A browser choice that is neither `daoris` nor `edge` (BRW12).</summary>
    public const string BrowserChoiceUnknown = "BROWSER_CHOICE_UNKNOWN";

    /// <summary>A links setting that is neither `system` nor `daoris` (BRW7).</summary>
    public const string BrowserLinksUnknown = "BROWSER_LINKS_UNKNOWN";

    /// <summary>
    /// A link the page asked Daoris's browser to open that is no web page (BRW7): the favorites' rule,
    /// so a script, a file or a credential in the address never reaches a browser agents drive.
    /// </summary>
    public const string BrowserLinkNotAPage = "BROWSER_LINK_NOT_A_PAGE";

    /// <summary>A terminal asked for a shell this build does not offer (CONSOLE4a): not one of `pwsh`, `powershell`, `cmd`, `bash`.</summary>
    public const string TerminalShellUnknown = "TERMINAL_SHELL_UNKNOWN";

    /// <summary>A terminal asked for a shell this build offers and this machine does not have on PATH.</summary>
    public const string TerminalShellMissing = "TERMINAL_SHELL_MISSING";

    /// <summary>A terminal asked for where this machine has no shell at all on PATH.</summary>
    public const string TerminalNoShell = "TERMINAL_NO_SHELL";

    /// <summary>The system would not start the shell, or make its console; the system's own reason goes with it.</summary>
    public const string TerminalNotStarted = "TERMINAL_NOT_STARTED";

    /// <summary>
    /// A filter the machine log's reader cannot use (LOG1c): a span that is not <c>30m</c>, <c>2h</c> or
    /// <c>3d</c>, a source no process writes, a level that is not a floor. Refused rather than read as no
    /// filter, which would show lines the person did not ask for as if they had.
    /// </summary>
    public const string LogFilterUnknown = "LOG_FILTER_UNKNOWN";

    /// <summary>The system would not open the log's folder in the file manager; its own reason goes with it.</summary>
    public const string LogFolderNotOpened = "LOG_FOLDER_NOT_OPENED";

    /// <summary>
    /// A managed version asked for where it is not downloaded (TOOLS7, D121 §4.1): a delete of one, or what a switch of
    /// git to it would change, which is asked of that git and so needs it here.
    /// </summary>
    public const string ToolNotDownloaded = "TOOL_NOT_DOWNLOADED";

    /// <summary>A tool named as a file that is not a whole path, or holds no file (TOOLS7): it is never written.</summary>
    public const string ToolFileMissing = "TOOL_FILE_MISSING";

    /// <summary>
    /// A tool named as a file that did not start, or started and answered no version (TOOLS7, §4.1: a named file starts
    /// with a version). The system's own reason goes with it, and nothing is written.
    /// </summary>
    public const string ToolFileNoVersion = "TOOL_FILE_NO_VERSION";

    /// <summary>A download or a use of a version no list names for this machine, or one that is no exact version (TOOLS7).</summary>
    public const string ToolVersionUnknown = "TOOL_VERSION_UNKNOWN";

    /// <summary>
    /// A version refused before it is fetched (TOOLS7): two lists disagree on it. The check that refused it travels with
    /// it, and so does the driver's sentence, which names both lists and both values.
    /// </summary>
    public const string ToolDownloadRefused = "TOOL_DOWNLOAD_REFUSED";

    /// <summary>A resource location that is no address Daoris reads from: https://, or http:// to this machine (TOOLS7, rule 5).</summary>
    public const string ToolLocationRefused = "TOOL_LOCATION_REFUSED";

    /// <summary>A delete of the version a tool runs (TOOLS7, §3.6): another way or version is chosen first.</summary>
    public const string ToolInUse = "TOOL_IN_USE";

    /// <summary>A delete the system refused, since something still holds a file in that version's folder; its reason goes with it.</summary>
    public const string ToolHeld = "TOOL_HELD";

    /// <summary>A download, or a use that downloads, started while that tool already has one running (TOOLS7, §3.6).</summary>
    public const string ToolBusy = "TOOL_BUSY";

    /// <summary>
    /// Every code a module here can raise — what the catalogue test enumerates. Read off the
    /// declarations above (REFUSE1): a list kept by hand let a code left out of it escape the check.
    /// </summary>
    public static IReadOnlyList<string> All { get; } = typeof(Refusals)
        .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
        .Where(field => field.IsLiteral && field.FieldType == typeof(string))
        .Select(field => (string)field.GetRawConstantValue()!)
        .ToArray();

    /// <summary>
    /// Refuse, in the shape that survives the trip: a code the page translates, the values it
    /// interpolates, and a fallback sentence for a log.
    /// </summary>
    public static ShenoraException Because(
        string code, string message, params (string Key, string Value)[] parameters) =>
        new(code, parameters.ToDictionary(p => p.Key, p => p.Value), message);
}
