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
    /// The files a person may `@` asked for where git cannot list them (CONV4d): the record names no
    /// tree here, or the tree is not a repository of its own. INFORMATION, like the review's: a path
    /// typed after `@` still reaches the agent, because both doors expand it as typed.
    /// </summary>
    public const string SessionTreeUnlisted = "SESSION_TREE_UNLISTED";

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
