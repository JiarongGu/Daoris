using System.Globalization;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// What the look judged it may drop of a tree it sets aside, beside where the folder goes (KEEP1a, D195 point 3; the cleanup design §2.3
/// and §5, Codex 2.4): <c>trees/.tidied/&lt;ws&gt;/&lt;repo&gt;/&lt;name&gt;-&lt;moment&gt;.slim.json</c>, written by the look before the move
/// (<see cref="SlimStates.Judged"/>) and after it (<see cref="SlimStates.Moved"/>), then by the slim as each path goes. It lives and goes
/// with its folder; a file, so the folders' enumerations pass it over.
/// </summary>
/// <remarks>
/// Only a manifest at <see cref="SlimStates.Moved"/> is ever slimmed: one left at <see cref="SlimStates.Judged"/>, where the process
/// stopped between the move and that write, is unknown, so its folder is kept whole for its days. An older build reads no manifest: its
/// purge keeps a slimmed folder as written, which keeps more than it should, never less (<c>compatible-builds.md</c>). A manifest of
/// another schema is never read as this one.
/// <para>KEEPSIZE1b (the keep-size design §3.3): the move writes one for <b>every finished tree</b>, schema 2, its paths possibly empty,
/// <c>whole</c> included, carrying the record's inputs (<see cref="Finished"/>) and its steps (<c>judged</c>, <c>moved</c>, <c>refused</c>,
/// <c>deleted</c>, <c>recorded</c>). This build reads both schemas: a schema-1 manifest is a folder from before this build, unmarked
/// (<see cref="SetAsideEligibility"/>), slimmed as before. An older build reads no schema-2 manifest and purges its folder by its days.</para>
/// </remarks>
/// <param name="Judged">When the look judged it, before the move.</param>
/// <param name="Moved">When the move ended and git let go of the tree; null before.</param>
/// <param name="Paths">Each package folder judged, relative to the tree's top in git's spelling.</param>
internal sealed record SlimManifest(
    string State, DateTimeOffset Judged, DateTimeOffset? Moved, string Repository, string Branch, IReadOnlyList<SlimPath> Paths)
{
    /// <summary>The shape a folder from before KEEPSIZE1b carries: the slim's alone.</summary>
    internal const int Schema = 1;

    /// <summary>The shape of a finished tree's manifest (KEEPSIZE1b): the slim's, and the record's inputs and steps.</summary>
    internal const int Finishing = 2;

    /// <summary>The finished tree's facts, the record's inputs; null for a schema-1 manifest.</summary>
    internal FinishedTree? Finished { get; init; }

    /// <summary>When the branch's delete succeeded (<c>deleted</c>).</summary>
    internal DateTimeOffset? Deleted { get; init; }

    /// <summary>When the service answered the record's post (<c>recorded</c>): the hour's clock (§3.4).</summary>
    internal DateTimeOffset? Recorded { get; init; }

    /// <summary>Why the record is not kept yet: the last post's words; null where nothing is said.</summary>
    internal string? Why { get; init; }

    /// <summary>What follows the folder's name in its manifest's.</summary>
    internal const string Suffix = ".slim.json";

    private const string Moment = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

    /// <summary>Where the manifest of <paramref name="folder"/> is: beside it, its name with <see cref="Suffix"/> after it.</summary>
    internal static string PathFor(string folder) => Path.TrimEndingDirectorySeparator(folder) + Suffix;

    /// <summary>The folder a manifest is of; null where its name is no manifest's.</summary>
    internal static string? FolderOf(string manifest) =>
        manifest.EndsWith(Suffix, StringComparison.Ordinal) ? manifest[..^Suffix.Length] : null;

    /// <summary>The manifest at <paramref name="path"/>: none where there is no file; else it, or why it does not read as one.</summary>
    internal static (SlimManifest? Manifest, string? Problem) Read(string path)
    {
        string text;
        try
        {
            if (!File.Exists(path)) return (null, null);
            text = AtomicFile.ReadText(path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return (null, error.Message);
        }

        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("schema", out var schema) || !schema.TryGetInt32(out var stamped) || stamped is not (Schema or Finishing)
                || Text(root, "state") is not { } state || At(root, "judged") is not { } judged
                || Text(root, "repository") is not { } repository || Text(root, "branch") is not { } branch
                || !root.TryGetProperty("paths", out var paths) || paths.ValueKind != JsonValueKind.Array)
            {
                return (null, "it is no manifest of this build's shape");
            }

            var read = new List<SlimPath>();
            foreach (var each in paths.EnumerateArray())
            {
                if (each.ValueKind != JsonValueKind.Object || Text(each, "path") is not { } relative || Text(each, "state") is not { } at)
                {
                    return (null, "a path in it does not read");
                }

                read.Add(new(relative, at, Text(each, "why"), At(each, "tried"), At(each, "ended")));
            }

            FinishedTree? finished = null;
            if (stamped == Finishing)
            {
                if (At(root, "read") is not { } judgedRead || Text(root, "workspace") is not { } workspace || Text(root, "tree") is not { } tree
                    || Text(root, "tip") is not { } tip || !root.TryGetProperty("hold", out var hold) || hold.ValueKind != JsonValueKind.Object
                    || Text(hold, "kind") is not { } kind || !root.TryGetProperty("left", out var left) || left.ValueKind != JsonValueKind.Object
                    || !left.TryGetProperty("names", out var names) || names.ValueKind != JsonValueKind.Array)
                {
                    return (null, "it is no finished tree's manifest of this build's shape");
                }

                finished = new FinishedTree(
                    judgedRead, workspace, tree, Text(root, "line"), tip, Text(root, "grewFrom"), Text(root, "unit"),
                    new FinishedHold(kind, Text(hold, "where"), Text(hold, "form")), Text(root, "ref"),
                    new FinishedLeft(
                        [.. names.EnumerateArray().Where(each => each.ValueKind == JsonValueKind.String).Select(each => each.GetString()!)],
                        left.TryGetProperty("count", out var count) && count.TryGetInt32(out var counted) ? counted : 0,
                        left.TryGetProperty("cut", out var cut) && cut.ValueKind == JsonValueKind.True),
                    root.TryGetProperty("whole", out var whole) && whole.ValueKind == JsonValueKind.True)
                {
                    Landing = root.TryGetProperty("landing", out var landing) && landing.ValueKind == JsonValueKind.Object
                        && Text(landing, "branch") is { } landed && At(landing, "at") is { } landedAt
                        ? new FinishedLanding(landed, Text(landing, "line"), landedAt, Text(landing, "pullRequest"))
                        : null,
                    LandingWhy = Text(root, "landingWhy"),
                };
            }

            return (new(state, judged, At(root, "moved"), repository, branch, read)
            {
                Finished = finished,
                Deleted = At(root, "deleted"),
                Recorded = At(root, "recorded"),
                Why = Text(root, "why"),
            }, null);
        }
        catch (JsonException error)
        {
            return (null, error.Message);
        }

        static string? Text(JsonElement element, string name) =>
            element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

        static DateTimeOffset? At(JsonElement element, string name) =>
            Text(element, name) is { } text && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var at)
                ? at
                : null;
    }

    /// <summary>Written whole, beside then renamed (<see cref="AtomicFile"/>).</summary>
    internal void Write(string path) => AtomicFile.WriteText(path, JsonFile.Serialize(writer =>
    {
        writer.WriteStartObject();
        writer.WriteNumber("schema", Finished is null ? Schema : Finishing);
        writer.WriteString("state", State);
        writer.WriteString("judged", Said(Judged));
        WriteMoment(writer, "moved", Moved);
        writer.WriteString("repository", Repository);
        writer.WriteString("branch", Branch);
        if (Finished is { } finished)
        {
            writer.WriteString("read", Said(finished.Read));
            writer.WriteString("workspace", finished.Workspace);
            writer.WriteString("tree", finished.Tree);
            Optional(writer, "line", finished.Line);
            writer.WriteString("tip", finished.Tip);
            Optional(writer, "grewFrom", finished.GrewFrom);
            Optional(writer, "unit", finished.Unit);
            writer.WriteStartObject("hold");
            writer.WriteString("kind", finished.Hold.Kind);
            Optional(writer, "where", finished.Hold.Where);
            Optional(writer, "form", finished.Hold.Form);
            writer.WriteEndObject();
            Optional(writer, "ref", finished.Ref);
            writer.WriteStartObject("left");
            writer.WriteStartArray("names");
            foreach (var name in finished.Left.Names) writer.WriteStringValue(name);
            writer.WriteEndArray();
            writer.WriteNumber("count", finished.Left.Count);
            writer.WriteBoolean("cut", finished.Left.Cut);
            writer.WriteEndObject();
            writer.WriteBoolean("whole", finished.Whole);
            if (finished.Landing is { } landing)
            {
                writer.WriteStartObject("landing");
                writer.WriteString("branch", landing.Branch);
                Optional(writer, "line", landing.Line);
                writer.WriteString("at", Said(landing.At));
                Optional(writer, "pullRequest", landing.PullRequest);
                writer.WriteEndObject();
            }
            else
            {
                writer.WriteNull("landing");
            }

            Optional(writer, "landingWhy", finished.LandingWhy);
            WriteMoment(writer, "deleted", Deleted);
            WriteMoment(writer, "recorded", Recorded);
            Optional(writer, "why", Why);
        }

        writer.WriteStartArray("paths");
        foreach (var each in Paths)
        {
            writer.WriteStartObject();
            writer.WriteString("path", each.Path);
            writer.WriteString("state", each.State);
            if (each.Why is { } why) writer.WriteString("why", why);
            else writer.WriteNull("why");
            WriteMoment(writer, "tried", each.Tried);
            WriteMoment(writer, "ended", each.Ended);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }));

    private static void Optional(Utf8JsonWriter writer, string name, string? text)
    {
        if (text is not null) writer.WriteString(name, text);
        else writer.WriteNull(name);
    }

    private static string Said(DateTimeOffset at) => at.UtcDateTime.ToString(Moment, CultureInfo.InvariantCulture);

    private static void WriteMoment(Utf8JsonWriter writer, string name, DateTimeOffset? at)
    {
        if (at is { } when) writer.WriteString(name, Said(when));
        else writer.WriteNull(name);
    }

    /// <summary>The paths the slim removed, in the manifest's order.</summary>
    internal IReadOnlyList<string> Removed => [.. Paths.Where(path => path.State == SlimStates.Removed).Select(path => path.Path)];
}

/// <summary>
/// A finished tree's facts in its schema-2 manifest (KEEPSIZE1b, the keep-size design §3.3), written at <c>judged</c>: the record's inputs,
/// so the pass, the press and a restart compose the same post from it alone. Never a session: the service resolves those from its rows.
/// </summary>
/// <param name="Read">The moment before the judgement's read of what the tree holds: the folder's threshold (§3.5's race).</param>
/// <param name="Tree">Its path before the move.</param>
/// <param name="Tip">The commit judged.</param>
/// <param name="GrewFrom">The commit it grew from, from <c>session-branches.json</c>; null where none is kept.</param>
/// <param name="Unit">Its work unit, where <c>session-branches.json</c> names one.</param>
/// <param name="Ref">The recovery ref at the tip, or null where <c>refs/heads/&lt;line&gt;</c> held it by ancestry (§3.1).</param>
/// <param name="Whole">Whether <c>whole</c> was set at the move.</param>
internal sealed record FinishedTree(
    DateTimeOffset Read, string Workspace, string Tree, string? Line, string Tip, string? GrewFrom, string? Unit, FinishedHold Hold, string? Ref,
    FinishedLeft Left, bool Whole)
{
    /// <summary>
    /// The landing that carried its work, captured before the move from the landings' record (Codex's read of the design, finding 6), so the
    /// record says it after that trace retires; null where none could be established, and <see cref="LandingWhy"/> says why. Never the
    /// judgement's or the move's moment in place of the landing's.
    /// </summary>
    internal FinishedLanding? Landing { get; init; }

    /// <summary>Why no landing is named: none recorded for its unit or branch.</summary>
    internal string? LandingWhy { get; init; }
}

/// <summary>A landing as the record names it: the branch it made, the line, when it landed, and its pull request where one was opened.</summary>
internal sealed record FinishedLanding(string Branch, string? Line, DateTimeOffset At, string? PullRequest);

/// <summary>
/// A removal's own record beside its folder in <c>.tidied/.going</c> (Codex's read of the design, finding 5): which door removed it, when, the
/// threshold a later write is read against, and whether its delete began. A stop before the delete moves the folder back whole; one after
/// it finishes only where nothing was written in it since and, for the hour's door, its holding check passes again; else what remains goes
/// back, saying why. Its manifest moves into <c>.going</c> with it, and back with it.
/// </summary>
/// <param name="Door"><c>hour</c>, <c>days</c> or <c>press</c>.</param>
internal sealed record GoingRecord(string Door, DateTimeOffset At, DateTimeOffset Threshold, bool Started)
{
    internal const string Suffix = ".going.json";

    private const string Moment = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

    internal static string PathFor(string folder) => Path.TrimEndingDirectorySeparator(folder) + Suffix;

    /// <summary>The record at <paramref name="path"/>; null where there is none or it does not read.</summary>
    internal static GoingRecord? Read(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            using var document = JsonDocument.Parse(AtomicFile.ReadText(path));
            var root = document.RootElement;
            return root.ValueKind == JsonValueKind.Object && Text(root, "door") is { } door && At(root, "at") is { } at && At(root, "threshold") is { } threshold
                ? new GoingRecord(door, at, threshold, root.TryGetProperty("started", out var started) && started.ValueKind == JsonValueKind.True)
                : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }

        static string? Text(JsonElement element, string name) =>
            element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

        static DateTimeOffset? At(JsonElement element, string name) =>
            Text(element, name) is { } text && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var at)
                ? at
                : null;
    }

    internal void Write(string path) => AtomicFile.WriteText(path, JsonFile.Serialize(writer =>
    {
        writer.WriteStartObject();
        writer.WriteString("door", Door);
        writer.WriteString("at", At.UtcDateTime.ToString(Moment, CultureInfo.InvariantCulture));
        writer.WriteString("threshold", Threshold.UtcDateTime.ToString(Moment, CultureInfo.InvariantCulture));
        writer.WriteBoolean("started", Started);
        writer.WriteEndObject();
    }));
}

/// <summary>Which hold kept the commits at the judgement (§1.1): <c>line</c>, <c>landing</c>, <c>squash</c>, <c>content</c>, <c>carried</c> or <c>empty</c>.</summary>
internal sealed record FinishedHold(string Kind, string? Where, string? Form);

/// <summary>What the tree held that no commit holds, by name, at most <see cref="TreeScratch.ScratchWalkLimit"/>, the rest counted (§2.1).</summary>
internal sealed record FinishedLeft(IReadOnlyList<string> Names, int Count, bool Cut)
{
    /// <summary>The judgement's <c>Ignored</c>, <c>Written</c> and <c>TooLarge</c>, a build folder's contents unnamed, bounded.</summary>
    internal static FinishedLeft Of(ContentHolds.TreeHolds? holds)
    {
        var all = holds is null ? [] : holds.Ignored.Concat(holds.Written).Concat(holds.TooLarge)
            .Where(path => !TreeScratch.UnderBuildFolder(path)).Distinct(StringComparer.Ordinal).ToList();
        return new([.. all.Take(TreeScratch.ScratchWalkLimit)], all.Count, all.Count > TreeScratch.ScratchWalkLimit);
    }
}

/// <summary>One package folder in a manifest, and its state (the cleanup design §5).</summary>
/// <param name="Path">Relative to the tree's top, in git's spelling: <c>node_modules</c>, <c>web/node_modules</c>.</param>
/// <param name="Why">Kept: why, a <see cref="SlimKept"/> code; removing: the last failure's words.</param>
/// <param name="Tried">When a removal last failed for the same reason as the try before it: it waits a day from here (§3.1).</param>
/// <param name="Ended">Removed: when its removal ended, against which the purge reads the folder above it (Fable F1.1).</param>
internal sealed record SlimPath(string Path, string State, string? Why = null, DateTimeOffset? Tried = null, DateTimeOffset? Ended = null)
{
    /// <summary>Whether its end is written: removed, or kept.</summary>
    internal bool Final => State is SlimStates.Removed or SlimStates.Kept;
}

/// <summary>A manifest's states, and each path's (the cleanup design §2.3).</summary>
public static class SlimStates
{
    /// <summary>The manifest: written before the move. Unknown on its own, so its folder is kept whole.</summary>
    public const string Judged = "judged";

    /// <summary>The manifest: the move ended and git let go of the tree. Only such a manifest is slimmed.</summary>
    public const string Moved = "moved";

    /// <summary>A finished tree's (KEEPSIZE1b): the move done, the branch's delete refused, the branch standing. Never armed.</summary>
    public const string Refused = "refused";

    /// <summary>A finished tree's (KEEPSIZE1b): its branch deleted, its record not yet kept.</summary>
    public const string Deleted = "deleted";

    /// <summary>A finished tree's (KEEPSIZE1b): its branch deleted, its record kept. Its folder goes an hour after.</summary>
    public const string Recorded = "recorded";

    /// <summary>
    /// A finished tree's whose holding check failed (KEEPSIZE1b, the keep-size design §3.4: its ref dropped, its line rewound, its repository
    /// not resolved): unmarked from then on, it waits its days, and <see cref="SlimManifest.Why"/> says why.
    /// </summary>
    public const string Unmarked = "unmarked";

    /// <summary>A path: waiting for the slim.</summary>
    public const string Pending = "pending";

    /// <summary>A path: its removal began and has not ended; the purge's read passes over it.</summary>
    public const string Removing = "removing";

    /// <summary>A path: removed, with when that ended.</summary>
    public const string Removed = "removed";

    /// <summary>A path: kept for its folder's days, with why.</summary>
    public const string Kept = "kept";
}

/// <summary>Why the slim kept a path (the cleanup design §5): a manifest's <c>why</c>, and the machine log's <c>tidied.slimmed</c> count.</summary>
public static class SlimKept
{
    /// <summary>Something in its folder was written after the move: the folder is kept whole for its days (step 1).</summary>
    public const string Written = "written";

    /// <summary>A link lies on the way from the trees home to it, it included, or its folder holds a link outside a build folder (step 2).</summary>
    public const string Linked = "linked";

    /// <summary>Its path is rooted, climbs with <c>..</c>, does not lie under its folder by whole segments, or names no package folder (step 2).</summary>
    public const string Refused = "refused";

    /// <summary>It is no longer there, or no longer a folder.</summary>
    public const string Gone = "gone";
}
