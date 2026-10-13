using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

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
/// Reading <c>$DAORIS_HOME/update.json</c> (D139 §3), here so the launcher, which compiles this file, reads it as the
/// application does: one reader. The driver library's <c>InstallUpdate</c> writes it, and is where its rules are told.
/// </summary>
public static class UpdateRequests
{
    public const string FileName = "update.json";

    /// <summary>The request, or null when there is none or it does not read: the default, when idle, then holds.</summary>
    public static UpdateRequest? Read(string home)
    {
        var path = Path.Combine(home, FileName);
        if (!File.Exists(path)) return null;
        try
        {
            if (JsonNode.Parse(File.ReadAllText(path)) is not JsonObject root) return null;
            var mode = root["mode"] is JsonValue value && value.TryGetValue<string>(out var text) ? text.Trim() : null;
            if (mode is null || !UpdateMode.All.Contains(mode, StringComparer.Ordinal)) return null;
            var build = root["build"] is JsonValue named && named.TryGetValue<string>(out var id) && !string.IsNullOrWhiteSpace(id)
                ? id.Trim()
                : null;
            var at = root["at"] is JsonValue stamp && stamp.TryGetValue<string>(out var moment)
                     && DateTimeOffset.TryParse(moment, System.Globalization.CultureInfo.InvariantCulture,
                         System.Globalization.DateTimeStyles.RoundtripKind, out var parsed)
                ? parsed
                : (DateTimeOffset?)null;
            return new UpdateRequest(mode, build, at);
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return null;
        }
    }

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

/// <summary>
/// A move a held file still refused when the wait for it ran out (SWAP2): the swap's <c>busy</c>, and the only one. Any
/// other failure of a move is thrown as it was.
/// </summary>
public sealed class HeldMoveException(SwapHold hold, Exception refusal)
    : IOException($"{hold.Said} ({refusal.Message.TrimEnd('.')})", refusal)
{
    public SwapHold Hold { get; } = hold;
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
/// A build staged beside an install, and the swap that installs it while the application is closed (UPDATE1, D139).
/// </summary>
/// <remarks>
/// <para><b>One source, two programs.</b> The driver library carries it, for the application's check before it closes and
/// for <c>daoris-driver update</c>; and the launcher compiles this same file (<c>Daoris.Desktop.Launcher.csproj</c>), so the
/// launcher still references nothing and the check it runs before it moves a file is this one, not a second copy that
/// could drift. So it uses nothing but the base library.</para>
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

    private static readonly StringComparison PathCase =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    public static string StagedOf(string install) => Path.Combine(install, Folder, Staged);

    public static string JournalOf(string install) => Path.Combine(install, Folder, Journal);

    /// <summary>Whether a build is staged: its manifest is there, readable or not.</summary>
    public static bool IsStaged(string install) => File.Exists(Path.Combine(StagedOf(install), Manifest));

    /// <summary>The staged build's manifest, or null when none is staged or it does not read; <paramref name="problem"/> says which.</summary>
    public static StagedManifest? Read(string install, out StagedProblem? problem)
    {
        problem = null;
        var path = Path.Combine(StagedOf(install), Manifest);
        if (!File.Exists(path)) return null;

        try
        {
            var root = JsonNode.Parse(File.ReadAllText(path)) as JsonObject;
            if (root is null)
            {
                problem = new("manifest", $"{Folder}/{Staged}/{Manifest} is not a JSON object.");
                return null;
            }

            if (Number(root["schema"]) is not { } schema || schema != Schema)
            {
                problem = new("schema",
                    $"{Folder}/{Staged}/{Manifest} is schema {root["schema"]?.ToJsonString() ?? "(none)"}, and this build reads schema {Schema}.");
                return null;
            }

            var id = Text(root["id"]);
            var version = Text(root["version"]);
            if (id is null || version is null || root["files"] is not JsonArray listed)
            {
                problem = new("manifest", $"{Folder}/{Staged}/{Manifest} names no id, version or files.");
                return null;
            }

            var files = new List<StagedFile>();
            foreach (var entry in listed)
            {
                if (entry is not JsonObject file || Text(file["path"]) is not { } filePath
                    || Number(file["size"]) is not { } size || Text(file["sha256"]) is not { } sha)
                {
                    problem = new("manifest", $"{Folder}/{Staged}/{Manifest} lists a file without its path, size or sha256.");
                    return null;
                }

                files.Add(new StagedFile(filePath, size, sha.ToLowerInvariant()));
            }

            var at = Text(root["at"]) is { } stamp && DateTimeOffset.TryParse(
                stamp, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out var moment)
                ? moment
                : (DateTimeOffset?)null;
            return new StagedManifest(id, version, Text(root["commit"]), at, files);
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException or InvalidOperationException or FormatException)
        {
            problem = new("manifest", $"{Folder}/{Staged}/{Manifest} could not be read ({error.Message.TrimEnd('.')}).");
            return null;
        }
    }

    /// <summary>
    /// Check the staged build before anything is replaced (D139 §4): its manifest reads at this schema, every file it names
    /// is there with its size and SHA-256, nothing is there it does not name, it carries <see cref="Required"/>, and the
    /// host when the install carries one. Null when it may be installed, or when nothing is staged.
    /// </summary>
    public static StagedProblem? Check(string install)
    {
        var manifest = Read(install, out var problem);
        if (problem is not null) return problem;
        if (manifest is null) return null;

        var staged = StagedOf(install);
        var named = new HashSet<string>(StringComparer.FromComparison(PathCase));
        foreach (var file in manifest.Files)
        {
            if (!Inside(file.Path))
            {
                return new("path", $"the staged build's manifest names {file.Path}, which is not a path inside the build.");
            }

            named.Add(file.Path);
            var full = Path.Combine(staged, file.Path.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(full)) return new("missing", $"the staged build has no {file.Path}, which its manifest names.");

            var length = new FileInfo(full).Length;
            if (length != file.Size)
            {
                return new("size", $"the staged build's {file.Path} is {length} bytes, and its manifest says {file.Size}.");
            }

            if (!string.Equals(Sha256Of(full), file.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                return new("hash", $"the staged build's {file.Path} is not the file its manifest names: its SHA-256 differs.");
            }
        }

        foreach (var full in Directory.EnumerateFiles(staged, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(staged, full).Replace(Path.DirectorySeparatorChar, '/');
            if (string.Equals(relative, Manifest, PathCase)) continue;
            if (!named.Contains(relative)) return new("unlisted", $"the staged build holds {relative}, which its manifest does not name.");
        }

        if (Required.FirstOrDefault(required => !named.Contains(required)) is { } absent)
        {
            return new("required", $"the staged build carries no {absent}, which every build must.");
        }

        if (File.Exists(Path.Combine(install, Host.Replace('/', Path.DirectorySeparatorChar))) && !named.Contains(Host))
        {
            return new("host",
                "this install carries its HTTP host and the staged build does not: stage it with --service, or the update would leave the install without one.");
        }

        return null;
    }

    /// <summary>A file's SHA-256, lower-case hex.</summary>
    public static string Sha256Of(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    /// <summary>The swap's journal, or null when there is none or it does not read.</summary>
    public static SwapRecord? ReadJournal(string install)
    {
        var path = JournalOf(install);
        if (!File.Exists(path)) return null;
        try
        {
            // Read sharing every access, the delete too: the launcher reads at every poll while the new application writes
            // its confirmation, and a reader that shares less refuses that rename (SWAP2).
            string text;
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var reader = new StreamReader(stream))
            {
                text = reader.ReadToEnd();
            }

            if (JsonNode.Parse(text) is not JsonObject root || Text(root["phase"]) is not { } phase) return null;
            var moves = new List<SwapMove>();
            if (root["moves"] is JsonArray listed)
            {
                foreach (var entry in listed)
                {
                    if (entry is JsonObject move && Text(move["from"]) is { } from && Text(move["to"]) is { } to) moves.Add(new SwapMove(from, to));
                }
            }

            // Absent on a journal written before SWAP2, which reads as it did.
            List<SwapHold>? holds = null;
            if (root["holds"] is JsonArray held)
            {
                holds = [];
                foreach (var entry in held)
                {
                    if (entry is JsonObject hold && Text(hold["from"]) is { } from && Text(hold["to"]) is { } to
                        && Number(hold["ms"]) is { } ms && Number(hold["tries"]) is { } tries)
                    {
                        holds.Add(new SwapHold(from, to, ms, (int)tries,
                            hold["released"] is JsonValue released && released.TryGetValue<bool>(out var gave) && gave));
                    }
                }
            }

            var at = Text(root["at"]) is { } stamp && DateTimeOffset.TryParse(
                stamp, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out var moment)
                ? moment
                : (DateTimeOffset?)null;
            return new SwapRecord(
                phase, Text(root["id"]), Text(root["version"]), Text(root["commit"]), at, moves,
                Number(root["pid"]) is { } pid ? (int)pid : null, Text(root["reason"]), Text(root["detail"]),
                root["confirmed"] is JsonValue confirmed && confirmed.TryGetValue<bool>(out var flag) ? flag : null,
                root["told"] is JsonValue told && told.TryGetValue<bool>(out var said) && said,
                holds is { Count: > 0 } ? holds : null);
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }

    /// <summary>Write the journal: beside, then renamed, so a reader never meets half of one.</summary>
    public static void WriteJournal(string install, SwapRecord record)
    {
        var path = JournalOf(install);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var root = new JsonObject
        {
            ["schema"] = Schema,
            ["phase"] = record.Phase,
            ["id"] = record.Id,
            ["version"] = record.Version,
            ["commit"] = record.Commit,
            ["at"] = record.At?.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture),
            ["moves"] = new JsonArray(record.Moves.Select(move => (JsonNode)new JsonObject { ["from"] = move.From, ["to"] = move.To }).ToArray()),
        };
        if (record.Pid is { } pid) root["pid"] = pid;
        if (record.Reason is { } reason) root["reason"] = reason;
        if (record.Detail is { } detail) root["detail"] = detail;
        if (record.Confirmed is { } confirmed) root["confirmed"] = confirmed;
        if (record.Told) root["told"] = true;
        if (record.Holds is { Count: > 0 } holds)
        {
            root["holds"] = new JsonArray(holds.Select(hold => (JsonNode)new JsonObject
            {
                ["from"] = hold.From,
                ["to"] = hold.To,
                ["ms"] = hold.Ms,
                ["tries"] = hold.Tries,
                ["released"] = hold.Released,
            }).ToArray());
        }

        var staging = path + ".writing";
        File.WriteAllText(staging, root.ToJsonString(Indented).Replace("\r\n", "\n") + "\n", new System.Text.UTF8Encoding(false));
        Replace(staging, path);
    }

    /// <summary>
    /// The journal renamed into place, tried again a moment while a reader holds it open without delete sharing, which
    /// Windows refuses as access denied and which clears in milliseconds (AtomicFile.Replace's REV3 measurement, written here
    /// because the launcher compiles this file alone). A refused write would lose a phase the swap reached, or the new
    /// application's confirmation (SWAP2).
    /// </summary>
    private static void Replace(string staging, string path)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                File.Move(staging, path, overwrite: true);
                return;
            }
            catch (Exception busy) when (attempt < 40
                                          && (busy is UnauthorizedAccessException
                                              || busy is IOException and not FileNotFoundException and not DirectoryNotFoundException))
            {
                Thread.Sleep(5 + attempt * 5);
            }
        }
    }

    /// <summary>Whether the journal says a swap is under way: moving, started, or confirmed and not yet finished.</summary>
    public static bool UnderWay(string install) =>
        ReadJournal(install)?.Phase is SwapPhase.Swapping or SwapPhase.Started or SwapPhase.Confirmed;

    /// <summary>
    /// The new application says it came up (D139 §5): a journal at <see cref="SwapPhase.Started"/> becomes
    /// <see cref="SwapPhase.Confirmed"/>, which the waiting launcher reads. True when this start confirmed a swap, so the
    /// application says once that it was updated; false for every other start, and for a journal a roll-back stopped part
    /// way through, which carries its reason and is finished at the next start, never confirmed (SWAP2).
    /// </summary>
    public static bool Confirm(string install, int pid)
    {
        if (ReadJournal(install) is not { Phase: SwapPhase.Started, Reason: null } record) return false;
        // Told as it is confirmed: this start is the one that says it, and the launcher keeps the mark as it finishes.
        WriteJournal(install, record with { Phase = SwapPhase.Confirmed, Pid = pid, Told = true });
        return true;
    }

    /// <summary>Mark the journal's outcome told, so a later start does not say it again.</summary>
    public static void Tell(string install)
    {
        if (ReadJournal(install) is { Told: false } record) WriteJournal(install, record with { Told = true });
    }

    /// <summary>
    /// The build before a finished swap, removed. The launcher finishes with its own running launcher inside
    /// <c>update/previous/</c>, which Windows will not delete, so its own clearing leaves a whole build there; the application
    /// tries again at each look once the journal says the swap ended, never while one is under way. True when it is gone.
    /// </summary>
    public static bool ClearPrevious(string install)
    {
        var previous = Path.Combine(install, Folder, Previous);
        if (!Directory.Exists(previous)) return true;
        if (ReadJournal(install) is not { Phase: SwapPhase.Installed or SwapPhase.RolledBack or SwapPhase.Refused }) return false;
        try
        {
            Directory.Delete(previous, recursive: true);
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Still held, the launcher not yet gone: the next look tries again.
            return false;
        }
    }

    /// <summary>Remove what is staged (a publish in place supersedes it), leaving the journal and the failed build to read.</summary>
    public static void Unstage(string install)
    {
        var staged = StagedOf(install);
        if (Directory.Exists(staged)) Directory.Delete(staged, recursive: true);
    }

    /// <summary>Every process running from under <paramref name="folder"/>, by id — what the swap waits out (D139 §5).</summary>
    public static IReadOnlyList<int> RunningFrom(string folder)
    {
        var prefix = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder)) + Path.DirectorySeparatorChar;
        var found = new List<int>();
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    if (process.MainModule?.FileName is { } file && file.StartsWith(prefix, PathCase)) found.Add(process.Id);
                }
                catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException)
                {
                    // Another account's, or a process that ended as it was asked: not one of the install's.
                }
            }
        }

        return found;
    }

    /// <summary>A manifest path that stays inside the build: relative, <c>/</c>-separated, no empty, <c>.</c> or <c>..</c> segment.</summary>
    internal static bool Inside(string path) =>
        path.Length > 0 && !path.Contains('\\') && !path.StartsWith('/') && !path.Contains(':')
        && path.Split('/').All(segment => segment.Length > 0 && segment != "." && segment != "..");

    private static string? Text(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text) ? text.Trim() : null;

    private static long? Number(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<long>(out var number) ? number
        : node is JsonValue other && other.TryGetValue<int>(out var small) ? small
        : null;
}

/// <summary>How a swap ended, for the launcher to act on: the application to start, and what the journal now says.</summary>
/// <param name="Phase">The journal's phase at the end: installed, rolled back or refused; or null when nothing was staged or the install was busy.</param>
/// <param name="StartOld">True when the launcher must still start the application in <c>app/</c>, which is the build before it.</param>
public sealed record SwapOutcome(string? Phase, string? Reason, bool StartOld)
{
    public static readonly SwapOutcome Nothing = new(null, null, StartOld: true);
}

/// <summary>
/// The swap itself (D139 §5, §6), run by the launcher while nothing runs from <c>app/</c>: check, move with a journal,
/// start the new application and wait for it to confirm, or put the build before it back.
/// </summary>
/// <remarks>
/// Every effect on a process is handed in, so the tests move real folders in a scratch install and start nothing.
/// </remarks>
public sealed class InstallSwap(
    string install,
    // Starts the application in `app/` with these arguments; its process id, or null when it would not start.
    Func<IReadOnlyList<string>, int?> start,
    // Whether that process still runs.
    Func<int, bool> alive,
    Func<DateTimeOffset>? clock = null,
    Action<TimeSpan>? sleep = null)
{
    /// <summary>How long a new application has to confirm before a launcher stops waiting for it.</summary>
    public TimeSpan ConfirmWithin { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>How often the journal is read while waiting.</summary>
    public TimeSpan Poll { get; init; } = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// How long a move a held file refuses is tried, <see cref="HeldWait"/> apart, before the swap gives it up as
    /// <c>busy</c>, by the clock, for each move. Two minutes (SWAP2, D139's note): ten seconds was outlasted twice by the
    /// deployment rehearsal with a build beside it, and the publish's own staging needed more than ten; two minutes stays
    /// inside the three the launcher already waits for the application to close. Written here, not through the driver's
    /// <c>AtomicFile</c>, because the launcher compiles this file alone.
    /// </summary>
    public TimeSpan HeldWithin { get; init; } = TimeSpan.FromMinutes(2);

    /// <summary>The wait between two tries of a held move.</summary>
    public TimeSpan HeldWait { get; init; } = TimeSpan.FromMilliseconds(200);

    /// <summary>The move itself, from and to full paths, and whether a file may be written over; a test hands in one that refuses.</summary>
    public Action<string, string, bool>? Mover { get; init; }

    /// <summary>Every move this swap found held, journalled with each write (SWAP2): the measurement the wait is sized by.</summary>
    private readonly List<SwapHold> _holds = [];

    /// <summary>A folder or a file moved on the disk, as the swap moves it.</summary>
    public static void MoveOnDisk(string source, string target, bool overwrite)
    {
        if (Directory.Exists(source)) Directory.Move(source, target);
        else File.Move(source, target, overwrite);
    }

    /// <summary>
    /// Whether a move's failure is a hold that may give way: access denied, which <c>Directory.Move</c> throws as an
    /// <see cref="IOException"/> of <c>0x80070005</c> for a folder a file inside is held open in (measured, SWAP2), or a
    /// sharing, lock or busy refusal of a file. Anything else (a path missing, a target already there) no wait mends, and
    /// it fails at once. The codes are Win32's; elsewhere an <see cref="IOException"/> carries an errno, and no hold.
    /// </summary>
    public static bool IsHeld(Exception error) =>
        error is UnauthorizedAccessException
        || (OperatingSystem.IsWindows()
            && error is IOException and not FileNotFoundException and not DirectoryNotFoundException
            && (error.HResult & 0xFFFF) is 5 or 32 or 33 or 170);

    /// <summary>
    /// A folder or a file moved, tried again while a held file refuses it, for <see cref="HeldWithin"/>; every hold met is
    /// kept with how long it lasted. A hold that outlasts the wait throws <see cref="HeldMoveException"/>; any other failure
    /// throws at once.
    /// </summary>
    private void MoveHeld(string source, string target, bool overwrite = false)
    {
        DateTimeOffset? since = null;
        for (var tries = 1; ; tries++)
        {
            try
            {
                (Mover ?? MoveOnDisk)(source, target, overwrite);
                if (since is { } first) _holds.Add(Hold(source, target, Now - first, tries, released: true));
                return;
            }
            catch (Exception refusal) when (IsHeld(refusal))
            {
                since ??= Now;
                if (Now - since.Value >= HeldWithin)
                {
                    var hold = Hold(source, target, Now - since.Value, tries, released: false);
                    _holds.Add(hold);
                    throw new HeldMoveException(hold, refusal);
                }

                Sleep(HeldWait);
            }
        }
    }

    private SwapHold Hold(string source, string target, TimeSpan held, int tries, bool released) =>
        new(Relative(source), Relative(target), (long)held.TotalMilliseconds, tries, released);

    private string Relative(string full) => Path.GetRelativePath(install, full).Replace(Path.DirectorySeparatorChar, '/');

    /// <summary>The journal written with every hold this swap has met.</summary>
    private void Journal(SwapRecord record) =>
        StagedBuild.WriteJournal(install, _holds.Count == 0 ? record : record with { Holds = _holds.ToList() });

    private DateTimeOffset Now => (clock ?? (() => DateTimeOffset.UtcNow))();

    private void Sleep(TimeSpan span) => (sleep ?? Thread.Sleep)(span);

    private string Full(string relative) => Path.Combine(install, relative.Replace('/', Path.DirectorySeparatorChar));

    private static string Under(params string[] segments) => string.Join('/', segments);

    /// <summary>
    /// What a launcher that died left (D139 §6), put right before anything else: a journal still <c>swapping</c> is undone;
    /// one <c>started</c> and never confirmed, with nothing running from <c>app/</c>, is a new application that failed, and
    /// is undone too; one confirmed and never marked installed is installed. The phase it put right to, or null when there
    /// was nothing to put right; either way the launcher goes on as it would have. A roll-back that stopped part way left
    /// its reason in the journal, and is finished with it (SWAP2).
    /// </summary>
    public string? Recover(bool appRunning)
    {
        var record = StagedBuild.ReadJournal(install);
        _holds.Clear();
        _holds.AddRange(record?.Holds ?? []);
        switch (record?.Phase)
        {
            case SwapPhase.Swapping:
                Undo(record, record.Reason ?? "interrupted",
                    record.Detail ?? "the launcher stopped while it was swapping; the moves it made were put back.");
                return SwapPhase.RolledBack;

            case SwapPhase.Started when !appRunning:
                Undo(record, record.Reason ?? "exited",
                    record.Detail ?? "the new build never said it came up, and nothing of it runs; the build before it was put back.");
                return SwapPhase.RolledBack;

            case SwapPhase.Confirmed:
                Finish(record);
                return SwapPhase.Installed;

            default:
                return null;
        }
    }

    /// <summary>
    /// The update met an error (D139 §6; SWAP2): a swap under way is put right from its journal, as a launcher that died is,
    /// keeping the reason it was undoing for; one started whose application runs is left to it. With no swap under way, an
    /// update asked for refuses the staged build as <c>error</c>, so the application is not closed for the same build
    /// again. Never a refusal written over a swap under way, and never <c>busy</c>.
    /// </summary>
    public SwapOutcome AfterError(bool appRunning, bool updating)
    {
        if (Recover(appRunning) is { } put)
        {
            return new SwapOutcome(put, StagedBuild.ReadJournal(install)?.Reason, StartOld: put != SwapPhase.Installed);
        }

        // Still under way: started, and its application runs. It is the application now; the launcher starts no other.
        if (StagedBuild.UnderWay(install)) return new SwapOutcome(null, null, StartOld: false);
        return updating ? Held("the launcher met an error before it could swap.", "error") : SwapOutcome.Nothing;
    }

    /// <summary>
    /// Install the staged build: check it, move it in with a journal, start it and wait for it to confirm, or roll back.
    /// <see cref="SwapOutcome.Nothing"/> when nothing is staged.
    /// </summary>
    public SwapOutcome Run(IReadOnlyList<string> arguments)
    {
        _holds.Clear();
        var manifest = StagedBuild.Read(install, out var unread);
        if (manifest is null && unread is null) return SwapOutcome.Nothing;

        // Checked here, by the program that is about to move the files, whatever the application checked before it closed.
        if ((unread ?? StagedBuild.Check(install)) is { } problem) return Refuse(manifest, problem);

        var previous = Full(Under(StagedBuild.Folder, StagedBuild.Previous));
        try
        {
            if (Directory.Exists(previous)) Directory.Delete(previous, recursive: true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return Refuse(manifest, new StagedProblem("previous", $"the last swap's update/previous could not be cleared ({error.Message.TrimEnd('.')})."));
        }

        var record = new SwapRecord(SwapPhase.Swapping, manifest!.Id, manifest.Version, manifest.Commit, Now, []);
        Journal(record);
        Directory.CreateDirectory(previous);

        var moves = new List<SwapMove>();
        try
        {
            // The build before it aside, then the staged one in: each move journalled before the next is made.
            foreach (var name in StagedBuild.Swapped)
            {
                if (Exists(name)) Move(name, Under(StagedBuild.Folder, StagedBuild.Previous, name));
            }

            foreach (var name in StagedBuild.Swapped)
            {
                Move(Under(StagedBuild.Folder, StagedBuild.Staged, name), name);
            }
        }
        catch (HeldMoveException held)
        {
            // `busy` is this and only this: a hold that outlasted the wait, and the moves before it put back (SWAP2).
            Undo(record with { Moves = moves }, "busy", $"a file the swap had to move was held: {held.Message}; nothing was changed.");
            return new SwapOutcome(SwapPhase.RolledBack, "busy", StartOld: true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Undo(record with { Moves = moves }, "move", $"a move the swap had to make failed ({error.Message.TrimEnd('.')}); nothing was changed.");
            return new SwapOutcome(SwapPhase.RolledBack, "move", StartOld: true);
        }

        // What is left of the staged folder is its manifest, already in the journal: gone, so nothing reads it as staged.
        TryDelete(StagedBuild.StagedOf(install));

        record = record with { Moves = moves, Phase = SwapPhase.Started, At = Now };
        Journal(record);

        var pid = start(arguments);
        if (pid is null)
        {
            Undo(record, "start", "the new build's application would not start; the build before it was put back.");
            return new SwapOutcome(SwapPhase.RolledBack, "start", StartOld: true);
        }

        record = record with { Pid = pid };
        var deadline = Now + ConfirmWithin;
        while (true)
        {
            var read = StagedBuild.ReadJournal(install);
            if (read?.Phase == SwapPhase.Confirmed)
            {
                Finish(read);
                return new SwapOutcome(SwapPhase.Installed, null, StartOld: false);
            }

            if (!alive(pid.Value))
            {
                // One last read: an application that confirmed and then ended is installed; the person may have closed it.
                if (StagedBuild.ReadJournal(install) is { Phase: SwapPhase.Confirmed } late)
                {
                    Finish(late);
                    return new SwapOutcome(SwapPhase.Installed, null, StartOld: false);
                }

                Undo(record, "exited", "the new build's application ended before it came up; the build before it was put back.");
                return new SwapOutcome(SwapPhase.RolledBack, "exited", StartOld: true);
            }

            if (Now >= deadline)
            {
                // Still running and silent: it is the application now, and the record says it never confirmed.
                Finish(record with { Confirmed = false });
                return new SwapOutcome(SwapPhase.Installed, "unconfirmed", StartOld: false);
            }

            Sleep(Poll);
        }

        void Move(string from, string to)
        {
            var source = Full(from);
            var target = Full(to);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            MoveHeld(source, target);
            moves.Add(new SwapMove(from, to));
            Journal(record with { Moves = moves.ToList() });
        }
    }

    /// <summary>
    /// An update asked for while something still runs from <c>app/</c> once the wait is over (D139 §5): the staged build is
    /// refused, <c>busy</c>, and moved aside, so the application started again on the build before it does not drain and
    /// close for the same build again; <paramref name="code"/> names another reason, <c>error</c> for the launcher's.
    /// <see cref="SwapOutcome.Nothing"/> when nothing is staged, or when a swap is under way: its journal is what puts the
    /// install right, and a refusal written over it would lose its moves (SWAP2).
    /// </summary>
    public SwapOutcome Held(string detail, string code = "busy")
    {
        if (StagedBuild.UnderWay(install)) return SwapOutcome.Nothing;
        var manifest = StagedBuild.Read(install, out var unread);
        if (manifest is null && unread is null) return SwapOutcome.Nothing;
        return Refuse(manifest, new StagedProblem(code, detail));
    }

    private bool Exists(string relative) => Directory.Exists(Full(relative)) || File.Exists(Full(relative));

    private SwapOutcome Refuse(StagedManifest? manifest, StagedProblem problem)
    {
        var record = new SwapRecord(
            SwapPhase.Refused, manifest?.Id, manifest?.Version, manifest?.Commit, Now, [], Reason: problem.Code, Detail: problem.Sentence);
        Journal(record);
        // A build refused is not tried again at every start: moved aside, as a failed one is, over the last one aside.
        TryDelete(Full(Under(StagedBuild.Folder, StagedBuild.Failed)));
        var holds = _holds.Count;
        MoveAside(StagedBuild.StagedOf(install));
        if (_holds.Count > holds) Journal(record);
        return new SwapOutcome(SwapPhase.Refused, problem.Code, StartOld: true);
    }

    /// <summary>Done: the build before it removed, and the journal says installed.</summary>
    private void Finish(SwapRecord record)
    {
        TryDelete(Full(Under(StagedBuild.Folder, StagedBuild.Previous)));
        Journal(record with
        {
            Phase = SwapPhase.Installed,
            Confirmed = record.Confirmed ?? true,
            At = Now,
        });
    }

    /// <summary>
    /// Put the build before it back (D139 §6): the journal's moves undone in reverse, a staged file that had moved in going
    /// to <c>update/failed/</c> rather than back to <c>staged/</c>, so it is not tried again.
    /// </summary>
    /// <remarks>
    /// The reason is journalled before the first move back, and the moves still to undo after each one (SWAP2): a move back
    /// that a hold outlasts throws with the journal still under way, so the launcher's <see cref="AfterError"/>, or the next
    /// start, finishes it with the reason it was undoing for and never moves a file it already put back. A journal that
    /// already carries a reason is such a finish, and keeps what the first attempt moved to <c>update/failed/</c>.
    /// </remarks>
    private void Undo(SwapRecord record, string reason, string detail)
    {
        var failed = Full(Under(StagedBuild.Folder, StagedBuild.Failed));
        if (record.Reason is null) TryDelete(failed);
        record = record with { Reason = reason, Detail = detail };
        var remaining = record.Moves.ToList();
        Journal(record with { Moves = remaining.ToList() });

        var stagedPrefix = Under(StagedBuild.Folder, StagedBuild.Staged) + "/";
        while (remaining.Count > 0)
        {
            var move = remaining[^1];
            var back = move.From.StartsWith(stagedPrefix, StringComparison.Ordinal)
                ? Under(StagedBuild.Folder, StagedBuild.Failed, move.From[stagedPrefix.Length..])
                : move.From;
            var source = Full(move.To);
            var target = Full(back);
            if (Directory.Exists(source) || File.Exists(source))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                try
                {
                    MoveHeld(source, target, overwrite: true);
                }
                catch (HeldMoveException)
                {
                    // The hold that stopped it, journalled with what is left to undo, for whoever finishes it.
                    Journal(record with { Moves = remaining.ToList() });
                    throw;
                }
            }

            remaining.RemoveAt(remaining.Count - 1);
            Journal(record with { Moves = remaining.ToList() });
        }

        TryDelete(Full(Under(StagedBuild.Folder, StagedBuild.Previous)));
        MoveAside(StagedBuild.StagedOf(install));
        Journal(record with
        {
            Phase = SwapPhase.RolledBack,
            Moves = [],
            At = Now,
        });
    }

    /// <summary>What is left of a staged build, into <c>update/failed/</c>: never tried again, kept to look at.</summary>
    private void MoveAside(string staged)
    {
        if (!Directory.Exists(staged)) return;
        var failed = Full(Under(StagedBuild.Folder, StagedBuild.Failed));
        try
        {
            Directory.CreateDirectory(failed);
            foreach (var entry in Directory.EnumerateFileSystemEntries(staged))
            {
                var target = Path.Combine(failed, Path.GetFileName(entry));
                TryDelete(target);
                MoveHeld(entry, target, overwrite: true);
            }

            Directory.Delete(staged, recursive: true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Left where it is: the journal already says it was not installed, and a next stage replaces it.
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
            else if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // A leftover the next swap clears first.
        }
    }
}
