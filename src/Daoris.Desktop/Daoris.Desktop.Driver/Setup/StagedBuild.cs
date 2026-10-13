namespace Daoris.Driver;

/// <summary>How the person wants a staged build installed (UPDATE1, D139 §2, §3), as <c>update.json</c> writes it.</summary>
public static class UpdateMode
{
    /// <summary>Start nothing new, let what runs end or park, then install: the default for a staged build.</summary>
    public const string WhenIdle = "when-idle";

    /// <summary>Install it now: the close ends what runs, as any close does (D104).</summary>
    public const string Now = "now";

    /// <summary>Keep it staged and drive on; for that build only.</summary>
    public const string NotNow = "not-now";

    public static readonly IReadOnlyList<string> All = [WhenIdle, Now, NotNow];
}

/// <summary>The person's word on a staged build: its mode, the build it was said of (null for whatever is staged), and when.</summary>
public sealed record UpdateRequest(string Mode, string? Build, DateTimeOffset? At);

/// <summary>
/// <c>$DAORIS_HOME/update.json</c> (D139 §3): its name, and which mode holds for what is staged. The launcher compiles this
/// file, and reads the request through <c>UpdateRequests.Read</c> (<see cref="UpdateRequestFiles"/>) as the application does:
/// one reader. The driver library's <c>InstallUpdate</c> writes it, and is where its rules are told.
/// </summary>
public static class UpdateRequests
{
    public const string FileName = "update.json";

    /// <summary>
    /// The mode that holds for the build staged now: the request's, where it was said of this build or of whatever is
    /// staged; when idle otherwise, so a newer stage drains again after a *Not now* (D139 §3).
    /// </summary>
    public static string ModeFor(UpdateRequest? request, string? staged) =>
        request is null ? UpdateMode.WhenIdle
        : request.Build is null || string.Equals(request.Build, staged, StringComparison.Ordinal) ? request.Mode
        : UpdateMode.WhenIdle;
}

/// <summary>One file a staged build carries, as its manifest names it: a path under the build with <c>/</c>, its size and SHA-256.</summary>
public sealed record StagedFile(string Path, long Size, string Sha256);

/// <summary>
/// What <c>update/staged/build.json</c> says of the build beside it (UPDATE1, D139 §1): its id, the release version it
/// was packed at, the commit it was built from where the publish could read one, when, and every file it carries.
/// </summary>
public sealed record StagedManifest(string Id, string Version, string? Commit, DateTimeOffset? At, IReadOnlyList<StagedFile> Files);

/// <summary>Why a staged build will not be installed: a code the screen and the machine log say it by, and a sentence for a terminal.</summary>
/// <param name="Code">
/// <c>manifest</c> (no readable <c>build.json</c>), <c>schema</c> (one this build does not read), <c>path</c> (a path that
/// leaves the build), <c>missing</c>, <c>size</c>, <c>hash</c>, <c>unlisted</c> (a file the manifest does not name),
/// <c>required</c> (the launcher, the application or its library not among them), <c>host</c> (the install carries its
/// HTTP host and the build does not), <c>busy</c> (a file the swap must move is held), <c>previous</c> (the last swap's
/// leftovers could not be cleared).
/// </param>
public sealed record StagedProblem(string Code, string Sentence);

/// <summary>A move the swap made, relative to the install's root, journalled before the next one (D139 §5).</summary>
public sealed record SwapMove(string From, string To);

/// <summary>
/// The launcher's record of the last swap, <c>update/swap.json</c> (D139 §5, §6): which build, how far it got, and how
/// it ended. The application reads it at its start to say the outcome once, and marks it told.
/// </summary>
/// <param name="Phase"><see cref="SwapPhase"/>'s words.</param>
/// <param name="Reason">
/// For a roll-back or a refusal, its code: a <see cref="StagedProblem"/>'s (<c>busy</c> only for a hold that outlasted the
/// wait, or something still running from <c>app/</c>), or <c>exited</c>, <c>start</c>, <c>move</c> (a move or its journal
/// failed, not held), <c>interrupted</c>, <c>error</c> (the launcher met an error before it could swap). On a journal still
/// under way, the reason a roll-back that stopped part way was undoing for, which the next start finishes it with (SWAP2).
/// </param>
/// <param name="Holds">Every move the swap found held, and how long (SWAP2); null when none was.</param>
public sealed record SwapRecord(
    string Phase, string? Id, string? Version, string? Commit, DateTimeOffset? At, IReadOnlyList<SwapMove> Moves,
    int? Pid = null, string? Reason = null, string? Detail = null, bool? Confirmed = null, bool Told = false,
    IReadOnlyList<SwapHold>? Holds = null);

/// <summary>
/// A move the swap found held (SWAP2), relative to the install's root: how long it was refused, from the first refusal to
/// the move or to the end of the wait, over how many tries, and whether it gave way. The measurement the wait is sized by.
/// </summary>
public sealed record SwapHold(string From, string To, long Ms, int Tries, bool Released)
{
    /// <summary>The hold in a terminal's words: <c>app for 12.3 s</c>, and whether it never gave way.</summary>
    public string Said =>
        $"{From} for {(Ms / 1000.0).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)} s"
        + (Released ? "" : ", and it did not give way");
}

/// <summary>The phases a swap passes through, as <c>swap.json</c> writes them.</summary>
public static class SwapPhase
{
    /// <summary>Moving: the journal lists each move made, so a launcher that died here is undone at the next start.</summary>
    public const string Swapping = "swapping";

    /// <summary>The new application is started and has not yet said it came up.</summary>
    public const string Started = "started";

    /// <summary>The new application came up: written by it, once composed (D139 §5).</summary>
    public const string Confirmed = "confirmed";

    /// <summary>Done: the build before it removed.</summary>
    public const string Installed = "installed";

    /// <summary>Undone: the build before it is back, and the one that failed is in <c>update/failed/</c>.</summary>
    public const string RolledBack = "rolled-back";

    /// <summary>The launcher's check refused the build before anything moved.</summary>
    public const string Refused = "refused";
}

/// <summary>
/// A build staged beside an install, and the swap that installs it while the application is closed (UPDATE1, D139): the
/// layout's names, what a build must carry, and which manifest paths stay inside it.
/// </summary>
/// <remarks>
/// <para><b>One source, two programs.</b> The driver library carries it, for the application's check before it closes and
/// for <c>daoris-driver update</c>; and the launcher compiles this same file (<c>Daoris.Desktop.Launcher.csproj</c>), so the
/// launcher still references nothing and the check it runs before it moves a file is this one, not a second copy that
/// could drift. So it uses nothing but the base library.</para>
///
/// <para><b>Its records and names are pure</b> (STRUCT3e): the reads and writes of the staged build and the journal are
/// <see cref="StagedBuildFiles"/>', declared as this type's own static members (<c>StagedBuild.Read</c>,
/// <c>StagedBuild.Check</c>, <c>StagedBuild.WriteJournal</c>…), and the swap is <see cref="InstallSwap"/>; the launcher
/// compiles all three files, each on the base library alone.</para>
///
/// <para><b>The layout is a twin</b> of <c>tools/desktop-publish.mjs</c>'s <c>STAGE</c>, <c>STAGED</c> and
/// <c>BUILD_MANIFEST</c>, which write it, and of the manifest its <c>stagedManifest</c> builds; <c>desktop-publish.test.ts</c>
/// reads this file for the spellings and the required names.</para>
/// </remarks>
public static class StagedBuild
{
    /// <summary>The folder beside <c>app/</c> the publish's <c>--stage</c> and the swap own.</summary>
    public const string Folder = "update";

    /// <summary>The staged build, inside <see cref="Folder"/>: renamed into place whole by the publish.</summary>
    public const string Staged = "staged";

    /// <summary>What the last swap replaced, until the new application confirms.</summary>
    public const string Previous = "previous";

    /// <summary>A build that failed to start, moved aside so it is not tried again.</summary>
    public const string Failed = "failed";

    /// <summary>The staged build's manifest, at its root.</summary>
    public const string Manifest = "build.json";

    /// <summary>The swap's journal and outcome, inside <see cref="Folder"/>.</summary>
    public const string Journal = "swap.json";

    /// <summary>The manifest's schema this build reads.</summary>
    public const int Schema = 1;

    /// <summary>The application's folder, the launcher and the marker: the three names at an install's root a swap replaces (D93).</summary>
    public const string AppFolder = "app";

    public const string Launcher = "Daoris.exe";

    public const string Marker = "INSTALLED.md";

    /// <summary>The files a staged build must carry, by their manifest paths: the launcher, the marker, the application and its library.</summary>
    public static readonly IReadOnlyList<string> Required =
        [Launcher, Marker, "app/Daoris.Desktop.exe", "app/Daoris.Desktop.App.dll"];

    /// <summary>The HTTP host, by its manifest path: required of a build staged for an install that carries one.</summary>
    public const string Host = "app/daoris-knowledge-http/daoris-knowledge-http.exe";

    /// <summary>What a swap moves at the root, in order.</summary>
    public static readonly IReadOnlyList<string> Swapped = [AppFolder, Launcher, Marker];

    public static string StagedOf(string install) => Path.Combine(install, Folder, Staged);

    public static string JournalOf(string install) => Path.Combine(install, Folder, Journal);

    /// <summary>A manifest path that stays inside the build: relative, <c>/</c>-separated, no empty, <c>.</c> or <c>..</c> segment.</summary>
    internal static bool Inside(string path) =>
        path.Length > 0 && !path.Contains('\\') && !path.StartsWith('/') && !path.Contains(':')
        && path.Split('/').All(segment => segment.Length > 0 && segment != "." && segment != "..");
}

/// <summary>How a swap ended, for the launcher to act on: the application to start, and what the journal now says.</summary>
/// <param name="Phase">The journal's phase at the end: installed, rolled back or refused; or null when nothing was staged or the install was busy.</param>
/// <param name="StartOld">True when the launcher must still start the application in <c>app/</c>, which is the build before it.</param>
public sealed record SwapOutcome(string? Phase, string? Reason, bool StartOld)
{
    public static readonly SwapOutcome Nothing = new(null, null, StartOld: true);
}
