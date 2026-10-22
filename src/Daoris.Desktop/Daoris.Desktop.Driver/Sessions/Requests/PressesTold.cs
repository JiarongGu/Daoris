using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// A press another process made on what the driver holds (STATEHUB1e, D169 point 8): named by the route a window's press of it is,
/// with the session it was about where it was one session's, and when.
/// </summary>
/// <param name="Route">The window's route the press is, as its mark names it (<see cref="PressesTold.Routes"/>).</param>
/// <param name="Session">The session it was about, a session id of this machine's; null where it was not one session's.</param>
/// <param name="At">When it was told: one nobody took for <see cref="PressesTold.Lifetime"/> is dropped.</param>
public sealed record PressTold(string Route, string? Session, DateTimeOffset At);

/// <summary>
/// The presses a terminal tells (STATEHUB1e, D169 point 8): <c>daoris-driver trees land|remove|clean|sync|hand</c> run in a process
/// with no bus of the desktop's, so each, once it has acted, leaves a note of what it pressed in the request folder every loop on the
/// home already watches (<see cref="SessionRequestWatch"/>, woken as the note arrives, FABRIC-EVENT2), and the shell's loop publishes it as the window's press of
/// that route is published: the same scopes, so a terminal's press reads in an open window as a window's does.
/// </summary>
/// <remarks>
/// <para><b>The route, not its scopes.</b> What a route changes is declared once, beside its mark in the shell's modules, and this
/// names the route; the loop reads the declaration. So a terminal carries no copy of the table to drift.</para>
///
/// <para><b>Told after the press, whatever it answered,</b> as the publisher reads a window's (D169 point 5): a refusal the terminal
/// says moved nothing that matters, and an invalidation is cheap. A press that failed before it acted tells nothing.</para>
///
/// <para><b>Taken once, by the loop that hears them.</b> A loop takes a note by opening it alone, its close deleting it, which only
/// one open can do. A loop that hears none (a headless one, with no window) leaves them, and drops only what nobody took in time, as
/// requests are dropped: with no shell running, a note waits a minute and goes.</para>
///
/// <para><b>Never a failure of the press.</b> Writing the note is best effort: a folder that cannot be written loses the note, and
/// the window reads it at the loop's next tick, as before.</para>
/// </remarks>
public sealed partial class PressesTold(string home)
{
    /// <summary><c>trees land &lt;session&gt;</c>: the review's Accept.</summary>
    public const string Land = "LAND_SESSION_TREE";

    /// <summary><c>trees remove &lt;path&gt;</c>: a tree by its path.</summary>
    public const string DiscardTree = "DISCARD_SESSION_TREE";

    /// <summary><c>trees remove &lt;session|branch&gt;</c>: a session's branch, its tree with it (LAND3).</summary>
    public const string DiscardBranch = "DISCARD_SESSION_BRANCH";

    /// <summary><c>trees clean --yes</c>: the clean-up's press.</summary>
    public const string Sweep = "SWEEP";

    /// <summary><c>trees sync --yes</c>: bringing the repositories up to date.</summary>
    public const string TreesSync = "TREES_SYNC";

    /// <summary><c>trees hand &lt;session|branch&gt;</c>: a landed branch handed to a landing plugin.</summary>
    public const string HandOff = "HANDOFF";

    /// <summary><c>trees land --entry &lt;id&gt;</c>: a line of work's one Accept (ACCEPTALL1e).</summary>
    public const string LandEntry = "LAND_ENTRY";

    /// <summary><c>trees land --ask &lt;id&gt;</c>: an ask's one Accept (ACCEPTALL1e).</summary>
    public const string LandAsk = "LAND_ASK";

    /// <summary><c>work recovery drop</c>: the person's delete of the recovery refs nothing holds (RECOVERYDROP1).</summary>
    public const string RecoveryDrop = "RECOVERY_DROP";

    /// <summary>Every route a terminal tells: the modules hold each to a route that declares what it changes.</summary>
    public static readonly IReadOnlyList<string> Routes = [Land, DiscardTree, DiscardBranch, Sweep, TreesSync, HandOff, LandEntry, LandAsk, RecoveryDrop];

    /// <summary>A note's ending, beside the requests' <c>.json</c>, so neither reads the other.</summary>
    public const string Suffix = ".told";

    /// <summary>How long a note nobody took stands: the loop looks as it arrives or within a few seconds, and a minute is ample, as for a request.</summary>
    public static readonly TimeSpan Lifetime = SessionRequests.Lifetime;

    /// <summary>The request folder every loop watches.</summary>
    public string Folder => new SessionRequests(home).Folder;

    [GeneratedRegex("^[A-Z][A-Z_]{0,63}$")]
    private static partial Regex RouteShape();

    /// <summary>
    /// Tell it, once the press has acted: written whole, beside the requests, under a name of its own. False where it could not be
    /// written or names no route, which the press never fails for. A session that is no id of this machine's is written as none.
    /// </summary>
    public bool Tell(PressTold press)
    {
        if (!RouteShape().IsMatch(press.Route)) return false;
        try
        {
            Directory.CreateDirectory(Folder);
            AtomicFile.WriteText(Path.Combine(Folder, SessionRequests.NewKey() + Suffix), ToJson(press));
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>A press of no one session's, told now in <paramref name="home"/>: what a verb says once it has acted (RECOVERYDROP1).</summary>
    public static bool Tell(string home, string route) => new PressesTold(home).Tell(new PressTold(route, null, DateTimeOffset.UtcNow));

    /// <summary>
    /// Every note waiting, taken once and removed, oldest first: what the loop that hears them publishes. One that does not read as
    /// a press, or that nobody took for <see cref="Lifetime"/>, is removed and not returned.
    /// </summary>
    public IReadOnlyList<PressTold> Take(DateTimeOffset now)
    {
        if (!Directory.Exists(Folder)) return [];
        var taken = new List<PressTold>();
        foreach (var path in Directory.EnumerateFiles(Folder, "*" + Suffix))
        {
            string text;
            try
            {
                // 🔴 Taken by holding it alone, its close deleting it: of two loops looking at once one has it and the other is
                // refused. A rename to take it, as a request is taken, lost one note in twenty in PressesToldTests when two loops
                // took at once (its read came back empty-handed after its rename had succeeded: the other loop's rename, holding it
                // to move it, refused the read, and the first then removed it unread; FIX-LOG 2026-10-16, REQTAKE1).
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None, 4096, FileOptions.DeleteOnClose);
                using var reader = new StreamReader(stream, Encoding.UTF8);
                text = reader.ReadToEnd();
            }
            catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException or IOException or UnauthorizedAccessException)
            {
                // Another loop has it, or something holds it for a moment: the next look reads what is left.
                continue;
            }

            if (Parse(text) is { } press && now - press.At <= Lifetime) taken.Add(press);
        }

        return [.. taken.OrderBy(press => press.At)];
    }

    /// <summary>What a loop that hears none does: leaves each note for one that does, and removes only what nobody took in time.</summary>
    public void Drop(DateTimeOffset now)
    {
        if (!Directory.Exists(Folder)) return;
        foreach (var path in Directory.EnumerateFiles(Folder, "*" + Suffix))
        {
            var at = Read(path)?.At ?? (File.Exists(path) ? new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero) : now);
            if (now - at > Lifetime) Remove(path);
        }
    }

    private static void Remove(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Held for a moment by whoever read it: the next look removes it.
        }
    }

    /// <summary>One note's file, or null where it does not read.</summary>
    private static PressTold? Read(string path)
    {
        try
        {
            return Parse(AtomicFile.ReadText(path));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>One note, or null where the text does not read as a press: a route of the marks' shape and a moment are required.</summary>
    private static PressTold? Parse(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            string? Text(string name) =>
                root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

            if (Text("route") is not { } route || !RouteShape().IsMatch(route) || Moments.Read(Text("at")) is not { } at) return null;
            // Never a path on the wire (D169 point 3): a session that is no id of this machine's is about any session.
            var session = Text("session") is { } named && SessionEventText.IsLocalId(named) ? named : null;
            return new PressTold(route, session, at);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Written by hand, as the driver's other files are, for the AOT reason <see cref="DriverConfig"/> gives.</summary>
    private static string ToJson(PressTold press) => JsonFile.Serialize(writer =>
    {
        writer.WriteStartObject();
        writer.WriteString("route", press.Route);
        if (press.Session is { } session && SessionEventText.IsLocalId(session)) writer.WriteString("session", session);
        writer.WriteString("at", Moments.Write(press.At));
        writer.WriteEndObject();
    });
}
