using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Daoris.Driver;

/// <summary>
/// A role a found change moved (FOUNDATION1w-s part 5, the design §19.15's "What was found, offered"): the role as the projection last wrote
/// it and as the found file shows it, each read by today's reader, and whether the check applied it at once (a turn-off of Intake or Ask
/// Daoris).
/// </summary>
public sealed record StaffingChange(string Role, ActorView Was, ActorView Now, bool Applied);

/// <summary>
/// A change of <c>driver.json</c>'s staffing another writer made, kept whole in <c>role_staffing_found</c> until the person takes it or
/// dismisses it: the four paths as the file showed them and <c>written</c> as it stood, and what changed between them.
/// </summary>
public sealed record StaffingFound(long Id, string At, StaffingPaths Paths, StaffingPaths Written, IReadOnlyList<StaffingChange> Changes);

/// <summary>
/// The staffing's projection and its rows (FOUNDATION1w-s parts 4 and 5, §19.15): the four paths the file today's build writes for a staffing
/// (<c>ActorChoices.Write</c> through <c>DriverConfig</c>'s writer), paths read back as today's reader reads them, and the store's rows.
/// </summary>
internal static class StaffingProjection
{
    private static readonly JsonWriterOptions Compacted = new() { Indented = false, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>Intake and Ask Daoris: the roles that may be off, whose turn-off a check applies at once.</summary>
    private static readonly string[] MayBeOff = ["intake", "ask-daoris"];

    /// <summary>The store's staffing as the five fields of a loaded record hold it.</summary>
    public static DriverConfig Config(StaffingRead read) => DriverConfig.Empty with
    {
        Adapter = read.Adapter ?? DriverConfig.Empty.Adapter,
        IntakeAdapter = read.IntakeAdapter,
        HelperAdapter = read.HelperAdapter,
        Actors = read.Actors,
        ActorErrors = read.Errors,
    };

    /// <summary>Four staffing paths read as today's reader reads a file that holds them and nothing else.</summary>
    public static DriverConfig Config(StaffingPaths paths) => DriverConfig.Parse(Text(writer =>
    {
        writer.WriteStartObject();
        foreach (var (name, json) in Named(paths))
        {
            if (json is null) continue;
            writer.WritePropertyName(name);
            writer.WriteRawValue(json);
        }

        writer.WriteEndObject();
    }));

    /// <summary>
    /// The projection of <paramref name="staffing"/>: <c>actors</c> with every seeded role's entry in the screen's order, an entry that does
    /// not read written back as found, an id this build does not know kept from <paramref name="writtenActors"/>, absent where none is; the
    /// three keys as <c>DriverConfig</c>'s writer writes them, <c>adapter</c> always and the other two only when named.
    /// </summary>
    public static StaffingPaths Project(DriverConfig staffing, IReadOnlyDictionary<string, string> asFound, string? writtenActors)
    {
        var any = false;
        var actors = Text(writer =>
        {
            writer.WriteStartObject();
            foreach (var id in ActorChoices.Ids)
            {
                if (staffing.ActorErrors.ContainsKey(id))
                {
                    if (!asFound.TryGetValue(id, out var raw)) continue;
                    writer.WritePropertyName(id);
                    writer.WriteRawValue(raw);
                    any = true;
                }
                else if (staffing.Actors.TryGetValue(id, out var choices))
                {
                    writer.WritePropertyName(id);
                    writer.WriteRawValue(RoleStaffing.Write(choices));
                    any = true;
                }
            }

            foreach (var (id, raw) in Entries(writtenActors).Where(each => !ActorChoices.Ids.Contains(each.Key, StringComparer.Ordinal)))
            {
                writer.WritePropertyName(id);
                writer.WriteRawValue(raw);
                any = true;
            }

            writer.WriteEndObject();
        });

        return new StaffingPaths(
            any ? actors : null, Quote(staffing.Adapter), staffing.IntakeAdapter is { } intake ? Quote(intake) : null,
            staffing.HelperAdapter is { } helper ? Quote(helper) : null);
    }

    /// <summary>Whether <paramref name="actor"/> starts anything under <paramref name="config"/>, read by today's reader.</summary>
    public static bool Starts(string actor, DriverConfig config) =>
        !config.ActorErrors.ContainsKey(actor) && ActorKey.FirstAgent(actor, config) is not null;

    /// <summary>The roles a found file turns off against <paramref name="written"/>: what the check applies with no press.</summary>
    public static IReadOnlyList<string> TurnedOff(StaffingPaths written, StaffingPaths found)
    {
        var was = Config(written);
        var now = Config(found);
        return [.. MayBeOff.Where(actor => Starts(actor, was) && !Starts(actor, now))];
    }

    /// <summary>Each role the found paths read otherwise than <c>written</c>, in the screen's order.</summary>
    public static IReadOnlyList<StaffingChange> Changes(StaffingPaths written, StaffingPaths found)
    {
        var was = Config(written);
        var now = Config(found);
        var off = TurnedOff(written, found);
        var changes = new List<StaffingChange>();
        foreach (var id in ActorChoices.Ids)
        {
            var before = ActorView.Of(id, was);
            var after = ActorView.Of(id, now);
            if (before.State == after.State && before.Error == after.Error && before.Choices.SequenceEqual(after.Choices)) continue;
            changes.Add(new StaffingChange(id, before, after, off.Contains(id)));
        }

        return changes;
    }

    /// <summary>
    /// <i>Take it</i> (§19.15, C8-1.3, O8-F4): the store's staffing with each entry the found paths show changed against <c>written</c> set to
    /// what they show, absent included, and each first-agent key so changed set to what they show, as the store's key alone; no
    /// <c>ActorChoices.With</c> and no <c>Keep</c>, since the found text already holds what its writer's did.
    /// </summary>
    public static (DriverConfig After, IReadOnlyDictionary<string, string> AsFound) Taken(StaffingRead read, StaffingPaths found, StaffingPaths written)
    {
        var shown = Config(found);
        var foundEntries = Entries(found.Actors);
        var writtenEntries = Entries(written.Actors);
        var actors = new Dictionary<string, IReadOnlyList<ActorChoice>>(read.Actors, StringComparer.Ordinal);
        var errors = new Dictionary<string, string>(read.Errors, StringComparer.Ordinal);
        var asFound = new Dictionary<string, string>(read.AsFound, StringComparer.Ordinal);
        foreach (var id in ActorChoices.Ids)
        {
            if (foundEntries.GetValueOrDefault(id) == writtenEntries.GetValueOrDefault(id)) continue;
            actors.Remove(id);
            errors.Remove(id);
            asFound.Remove(id);
            if (shown.ActorErrors.TryGetValue(id, out var why))
            {
                errors[id] = why;
                asFound[id] = foundEntries[id];
            }
            else if (shown.Actors.TryGetValue(id, out var choices)) actors[id] = choices;
        }

        var after = Config(read) with { Actors = actors, ActorErrors = errors };
        if (found.Adapter != written.Adapter) after = after with { Adapter = shown.Adapter };
        if (found.IntakeAdapter != written.IntakeAdapter) after = after with { IntakeAdapter = shown.IntakeAdapter };
        if (found.HelperAdapter != written.HelperAdapter) after = after with { HelperAdapter = shown.HelperAdapter };
        return (after, asFound);
    }

    /// <summary>The staffing's rows rewritten as <paramref name="staffing"/> holds them, as the import writes them.</summary>
    public static void Rows(DesktopWrite write, DriverConfig staffing, IReadOnlyDictionary<string, string> asFound, string at)
    {
        write.Execute("DELETE FROM role_staffing");
        foreach (var id in ActorChoices.Ids)
        {
            var first = id switch
            {
                "build" => staffing.Adapter,
                "intake" => staffing.IntakeAdapter,
                "ask-daoris" => staffing.HelperAdapter,
                _ => null,
            };
            (string State, string? Choices, string? Why)? row =
                staffing.ActorErrors.TryGetValue(id, out var why) ? ("unreadable", asFound.GetValueOrDefault(id) ?? "null", why)
                : staffing.Actors.TryGetValue(id, out var choices) ? ("set", RoleStaffing.Write(choices), null)
                : first is not null ? ("before", null, null)
                : null;
            if (row is not { } each) continue;
            write.Execute(
                "INSERT INTO role_staffing (role, state, choices, why, first_agent, at) VALUES ($role, $state, $choices, $why, $first, $at)",
                ("$role", id), ("$state", each.State), ("$choices", each.Choices), ("$why", each.Why), ("$first", first), ("$at", at));
        }
    }

    /// <summary><c>written</c> set to <paramref name="paths"/>.</summary>
    public static void Written(DesktopWrite write, StaffingPaths paths, string at) => write.Execute(
        """
        UPDATE role_staffing_projection
        SET actors = $actors, adapter = $adapter, intake_adapter = $intake, helper_adapter = $helper, at = $at
        WHERE one = 1
        """,
        ("$actors", paths.Actors), ("$adapter", paths.Adapter), ("$intake", paths.IntakeAdapter), ("$helper", paths.HelperAdapter), ("$at", at));

    /// <summary>
    /// A found row for <paramref name="found"/> against <paramref name="written"/>, or the newest one not yet answered where it is the same:
    /// a change is recorded once, however many checks find it.
    /// </summary>
    public static long Recorded(DesktopWrite write, StaffingPaths found, StaffingPaths written, string at)
    {
        var newest = write.Query(
            """
            SELECT id, actors, adapter, intake_adapter, helper_adapter, written_actors, written_adapter, written_intake_adapter, written_helper_adapter
            FROM role_staffing_found WHERE answered_at IS NULL ORDER BY id DESC LIMIT 1
            """,
            row => (row.GetInt64(0), Paths(row, 1), Paths(row, 5))).FirstOrDefault();
        if (newest.Item2 == found && newest.Item3 == written) return newest.Item1;
        write.Execute(
            """
            INSERT INTO role_staffing_found (at, actors, adapter, intake_adapter, helper_adapter,
                                             written_actors, written_adapter, written_intake_adapter, written_helper_adapter)
            VALUES ($at, $actors, $adapter, $intake, $helper, $wActors, $wAdapter, $wIntake, $wHelper)
            """,
            ("$at", at), ("$actors", found.Actors), ("$adapter", found.Adapter), ("$intake", found.IntakeAdapter), ("$helper", found.HelperAdapter),
            ("$wActors", written.Actors), ("$wAdapter", written.Adapter), ("$wIntake", written.IntakeAdapter), ("$wHelper", written.HelperAdapter));
        return write.Scalar<long>("SELECT last_insert_rowid()");
    }

    /// <summary>The four paths from <paramref name="first"/>'s column of a row, in the store's order.</summary>
    public static StaffingPaths Paths(SqliteDataReader row, int first) =>
        new(Column(row, first), Column(row, first + 1), Column(row, first + 2), Column(row, first + 3));

    private static string? Column(SqliteDataReader row, int at) => row.IsDBNull(at) ? null : row.GetString(at);

    /// <summary>Each entry of an <c>actors</c> object's text, compact, by its id; none where there is no object.</summary>
    private static Dictionary<string, string> Entries(string? actors)
    {
        var entries = new Dictionary<string, string>(StringComparer.Ordinal);
        if (actors is null) return entries;
        using var document = JsonDocument.Parse(actors);
        if (document.RootElement.ValueKind != JsonValueKind.Object) return entries;
        foreach (var property in document.RootElement.EnumerateObject()) entries.TryAdd(property.Name, RoleStaffing.Compact(property.Value));
        return entries;
    }

    private static IEnumerable<(string Name, string? Json)> Named(StaffingPaths paths) =>
        [("actors", paths.Actors), ("adapter", paths.Adapter), ("intakeAdapter", paths.IntakeAdapter), ("helperAdapter", paths.HelperAdapter)];

    private static string Quote(string value) => Text(writer => writer.WriteStringValue(value));

    private static string Text(Action<Utf8JsonWriter> write)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, Compacted)) write(writer);
        return Encoding.UTF8.GetString(stream.GetBuffer(), 0, (int)stream.Length);
    }
}
