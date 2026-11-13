using Microsoft.Data.Sqlite;

namespace Daoris.Driver;

/// <summary>The move table's columns by store version, and a row read as a <see cref="MoveRecord"/>.</summary>
internal static class MoveRows
{
    internal static readonly (MoveRecordPhase Phase, string Column)[] Moments =
    [
        (MoveRecordPhase.Decided, "decided_at"), (MoveRecordPhase.Quiescing, "quiescing_at"), (MoveRecordPhase.OldEnded, "old_ended_at"),
        (MoveRecordPhase.Opening, "opening_at"), (MoveRecordPhase.Opened, "opened_at"), (MoveRecordPhase.Copied, "copied_at"),
        (MoveRecordPhase.Verified, "verified_at"), (MoveRecordPhase.Delivering, "delivering_at"), (MoveRecordPhase.Delivered, "delivered_at"),
        (MoveRecordPhase.FellBack, "fell_back_at"), (MoveRecordPhase.Held, "held_at"), (MoveRecordPhase.Closed, "closed_at"),
    ];

    /// <summary>The store's step that added the columns a box's move carries and a quiet move's telling.</summary>
    internal const int BoxColumnsSince = 5;

    internal static readonly string StepFourColumns =
        "id, record_left, executor, phase, claim_run, claim_generation, how, from_why, from_by, account, recipe_why, recipe_bound, opens, destination, "
        + "effect, effect_begun, effect_outcome, effect_ended, fell_back_code, held_why, closed_why, "
        + string.Join(", ", Moments.Select(each => each.Column));

    /// <summary>The store version that carries the offered choice's model and effort columns.</summary>
    internal const int EntryColumnsSince = 18;

    internal static readonly string Columns = StepFourColumns + ", agent, refused_bound, quiet_at, model, effort";

    /// <summary>
    /// The columns a read names in a store of <paramref name="version"/>: a store a step-4 build left, not yet opened for writing by this
    /// one, has no step-5 column, and one from before step 17 no model or effort, which read as empty.
    /// </summary>
    internal static string ColumnsAt(int version) =>
        version >= EntryColumnsSince ? Columns
        : version >= BoxColumnsSince ? StepFourColumns + ", agent, refused_bound, quiet_at, NULL AS model, NULL AS effort"
        : StepFourColumns + ", NULL AS agent, NULL AS refused_bound, NULL AS quiet_at, NULL AS model, NULL AS effort";

    internal static MoveRecord? ReadIn(DesktopWrite write, long id) =>
        write.Query($"SELECT {ColumnsAt(write.Version)} FROM moves WHERE id = $id", Map, ("$id", id)).FirstOrDefault();

    internal static MoveRecord? StandingIn(DesktopWrite write, string left) =>
        write.Query($"SELECT {ColumnsAt(write.Version)} FROM moves WHERE record_left = $left AND {DesktopStore.StandingMoves}", Map, ("$left", left)).FirstOrDefault();

    internal static MoveRecord? StandingToIn(DesktopWrite write, string destination) =>
        write.Query($"SELECT {ColumnsAt(write.Version)} FROM moves WHERE destination = $destination AND {DesktopStore.StandingMoves} ORDER BY id", Map, ("$destination", destination))
            .FirstOrDefault();

    internal static StoreRun? RunIn(DesktopWrite write, string run) =>
        write.Query(
            "SELECT run, kind, pid, started, began FROM store_runs WHERE run = $run",
            row => new StoreRun(row.GetString(0), row.GetString(1), row.GetInt32(2), row.IsDBNull(3) ? null : row.GetInt64(3), row.GetString(4)),
            ("$run", run)).FirstOrDefault();

    internal static MoveRecord Map(DbDataReader row)
    {
        string? Text(string column)
        {
            var at = row.GetOrdinal(column);
            return row.IsDBNull(at) ? null : row.GetString(at);
        }

        var moments = new Dictionary<MoveRecordPhase, string>();
        foreach (var (phase, column) in Moments)
        {
            if (Text(column) is { } moment) moments[phase] = moment;
        }

        int? Number(string column)
        {
            var at = row.GetOrdinal(column);
            return row.IsDBNull(at) ? null : row.GetInt32(at);
        }

        var bound = row.GetOrdinal("recipe_bound");
        return new MoveRecord(
            row.GetInt64(row.GetOrdinal("id")), Text("record_left")!, Text("executor")!, Text("phase")!, Text("claim_run")!,
            row.GetInt64(row.GetOrdinal("claim_generation")), Text("how")!, Text("from_why")!, Text("from_by")!)
        {
            Moments = moments,
            Opens = Text("opens"),
            Destination = Text("destination"),
            Account = Text("account"),
            RecipeWhy = Text("recipe_why"),
            RecipeBound = row.IsDBNull(bound) ? null : row.GetInt32(bound),
            FellBackCode = Text("fell_back_code"),
            HeldWhy = Text("held_why"),
            ClosedWhy = Text("closed_why"),
            Effect = Text("effect") is { } what ? new MoveEffect(what, Text("effect_begun") ?? "", Text("effect_outcome"), Text("effect_ended")) : null,
            Agent = Text("agent"),
            RefusedBound = Number("refused_bound"),
            QuietAt = Text("quiet_at"),
            Model = Text("model"),
            Effort = Text("effort"),
        };
    }
}
