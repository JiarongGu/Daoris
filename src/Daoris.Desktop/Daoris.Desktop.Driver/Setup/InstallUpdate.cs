using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// An install's update, the machine-local half (UPDATE1, D139): the request both doors write under the home, which
/// mode holds for what is staged, and what a draining look plans with.
/// </summary>
/// <remarks>
/// <para><b>Two doors, one file</b> (D50): the application's banner and <c>daoris-driver update</c> write
/// <c>$DAORIS_HOME/update.json</c> through <see cref="Write"/>, and the application reads it at each of its looks. Absent or
/// unreadable is the default, a staged build installed when idle, so a torn file never stops an update nor starts one now.</para>
///
/// <para><b>The drain holds starts, never looks</b> (D139 §2, rejected: skipping looks). A look still reports endings, tells
/// plugins and syncs; <see cref="Drained"/> is the config it plans with, and <see cref="HeldFor"/> says the hold in its own
/// words where the person's hold would otherwise be named.</para>
/// </remarks>
public static class InstallUpdate
{
    /// <summary>The request's file, under the home.</summary>
    public const string FileName = UpdateRequests.FileName;

    /// <summary>
    /// Why a quest sits while an update drains — the driver's sentence, which the page says in the reader's language by the
    /// tick's <c>forUpdate</c> (UX5 U27), never by these words.
    /// </summary>
    public const string HoldReason =
        "an update is waiting for this machine's sessions to end: nothing new starts until it is installed and Daoris "
        + "starts again — Not now, or `daoris-driver update --cancel`, lets work start meanwhile.";

    public static string PathOf(string home) => Path.Combine(home, FileName);

    /// <summary>The request, or null when there is none or it does not read: the default then holds. The launcher's reader.</summary>
    public static UpdateRequest? Read(string home) => UpdateRequests.Read(home);

    /// <summary>Write the request, atomically, LF; refused for a mode this build does not know.</summary>
    /// <exception cref="ArgumentException">A mode that is not one of <see cref="UpdateMode.All"/>.</exception>
    public static void Write(string home, UpdateRequest request)
    {
        if (!UpdateMode.All.Contains(request.Mode, StringComparer.Ordinal))
        {
            throw new ArgumentException($"`{request.Mode}` is not an update mode: one of {string.Join(", ", UpdateMode.All)}.");
        }

        var root = new JsonObject
        {
            ["mode"] = request.Mode,
            ["build"] = request.Build,
            ["at"] = (request.At ?? DateTimeOffset.UtcNow).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
        };
        Directory.CreateDirectory(home);
        AtomicFile.WriteText(PathOf(home), root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }).Replace("\r\n", "\n") + "\n");
    }

    /// <summary>The request said and done with: removed as the application closes for the update, so it never outlives its build.</summary>
    public static void Clear(string home)
    {
        try
        {
            File.Delete(PathOf(home));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // A request naming an installed build holds for no later one (ModeFor), so a leftover changes nothing.
        }
    }

    /// <summary>
    /// The mode that holds for the build staged now: the request's, where it was said of this build or of whatever is
    /// staged; <see cref="UpdateMode.WhenIdle"/> otherwise, so a newer stage drains again after a *Not now* (D139 §3).
    /// </summary>
    public static string ModeFor(UpdateRequest? request, string? staged) => UpdateRequests.ModeFor(request, staged);

    /// <summary>
    /// The install a home belongs to (D63): the folder above it when that is a marked install and the home is its
    /// <c>data</c> — or null, for a home named for itself (a gate's, a terminal's), which a terminal points at with <c>--install</c>.
    /// </summary>
    public static string? InstallOf(string home)
    {
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(home));
        return string.Equals(Path.GetFileName(full), "data", StringComparison.OrdinalIgnoreCase)
               && Path.GetDirectoryName(full) is { } parent
               && File.Exists(Path.Combine(parent, StagedBuild.Marker))
            ? parent
            : null;
    }

    /// <summary>
    /// The config a draining look plans with (D139 §2): every drivable repository held, so no quest starts, resumes or
    /// carries on and no answered park goes on; and no intake, which a hold does not reach. The cap is left, since a
    /// workspace's set-up plan reads it to publish, which starts nothing.
    /// </summary>
    public static DriverConfig Drained(DriverConfig config) => config with
    {
        Holds = [.. config.Holds, .. config.Drivable.Where(repository => !config.Holds.Contains(repository, StringComparer.OrdinalIgnoreCase))],
        IntakeAdapter = null,
    };

    /// <summary>
    /// What a draining look said of each quest, with the drain's hold said as the drain's: a <see cref="StartVerdict.Held"/>
    /// in a repository the person did not hold becomes <see cref="StartVerdict.Blocked"/> with <see cref="HoldReason"/>. A
    /// hold the person made keeps its own sentence; nothing else is touched.
    /// </summary>
    public static IReadOnlyList<Consideration> HeldFor(IReadOnlyList<Consideration> considered, DriverConfig config) =>
        considered
            .Select(consideration =>
                consideration.Verdict == StartVerdict.Held
                && !config.Holds.Contains(consideration.Quest.To, StringComparer.OrdinalIgnoreCase)
                    ? consideration with { Verdict = StartVerdict.Blocked, Reason = HoldReason }
                    : consideration)
            .ToList();

    /// <summary>Whether a consideration is the drain's hold: what the tick marks <c>forUpdate</c> for the page.</summary>
    public static bool IsHeldForUpdate(Consideration consideration) =>
        consideration.Verdict == StartVerdict.Blocked && string.Equals(consideration.Reason, HoldReason, StringComparison.Ordinal);

    /// <summary>Whether the work allows the update (D139 §2): no driven session running and no conversation's turn in flight.</summary>
    public static bool Idle(int driven, int turns) => driven <= 0 && turns <= 0;
}
