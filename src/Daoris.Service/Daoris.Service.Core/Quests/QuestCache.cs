using Microsoft.Data.Sqlite;
using static Daoris.Knowledge.QuestRecordCodec;

namespace Daoris.Knowledge;

/// <summary>
/// The <c>quests</c> table: a cache of what each quest's history replays to, written whole by the replay and nowhere else.
/// </summary>
internal sealed class QuestCache(QuestStore store)
{
    private readonly ConnectionGate _db = store.Db;

    /// <summary>
    /// Write a quest's row as its replay gives it. The row is a cache, so it is written whole: a
    /// replay is the only thing that decides what it says.
    /// </summary>
    internal async Task WriteAsync(Quest quest, SqliteTransaction transaction, CancellationToken ct)
    {
        await using var command = _db.Command();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO quests (id, sender, receiver, title, body, status, note, note_parts, filed, updated, workspace, links, attachments, then_steps, parent, conflicts, awaits, published_by, lanes, requirements, answers, accepted, held, short_title, evidence, review, set_up_in, set_ups, verdicts, closure, submission, grew_from, source_reopened, reopened, workflow_choices, weight, paths, request)
            VALUES ($id, $sender, $receiver, $title, $body, $status, $note, $noteParts, $filed, $updated, $workspace, $links, $attachments, $then, $parent, $conflicts, $awaits, $publishedBy, $lanes, $requirements, $answers, $accepted, $held, $short, $evidence, $review, $setUpIn, $setUps, $verdicts, $closure, $submission, $grewFrom, $sourceReopened, $reopened, $workflowChoices, $weight, $paths, $request)
            ON CONFLICT (id) DO UPDATE SET
              sender = excluded.sender, receiver = excluded.receiver, title = excluded.title, body = excluded.body,
              status = excluded.status, note = excluded.note, note_parts = excluded.note_parts, filed = excluded.filed, updated = excluded.updated,
              workspace = excluded.workspace, links = excluded.links, attachments = excluded.attachments,
              then_steps = excluded.then_steps, parent = excluded.parent, conflicts = excluded.conflicts,
              awaits = excluded.awaits, published_by = excluded.published_by, lanes = excluded.lanes,
              requirements = excluded.requirements, answers = excluded.answers, accepted = excluded.accepted,
              held = excluded.held, short_title = excluded.short_title, evidence = excluded.evidence,
              review = excluded.review, set_up_in = excluded.set_up_in, set_ups = excluded.set_ups, verdicts = excluded.verdicts,
              closure = excluded.closure, submission = excluded.submission,
              grew_from = excluded.grew_from, source_reopened = excluded.source_reopened, reopened = excluded.reopened,
              workflow_choices = excluded.workflow_choices, weight = excluded.weight, paths = excluded.paths,
              request = excluded.request
            """;
        command.Parameters.AddWithValue("$request", (object?)quest.Request ?? DBNull.Value);
        command.Parameters.AddWithValue("$weight", (object?)quest.Weight ?? DBNull.Value);
        command.Parameters.AddWithValue("$paths", quest.Paths.Count == 0 ? DBNull.Value : LinksJson(quest.Paths));
        command.Parameters.AddWithValue("$workflowChoices", JsonFields.Written(writer =>
        {
            writer.WriteStartArray();
            foreach (var choice in quest.WorkflowChoices) WorkflowChoices.Write(writer, choice, standing: true);
            writer.WriteEndArray();
        }));
        command.Parameters.AddWithValue("$closure", (object?)quest.Closure ?? DBNull.Value);
        command.Parameters.AddWithValue("$grewFrom", quest.GrewFrom is { } from
            ? JsonFields.Written(writer => { writer.WriteStartObject(); WriteRefFields(writer, from); writer.WriteEndObject(); })
            : DBNull.Value);
        command.Parameters.AddWithValue("$sourceReopened", quest.SourceReopened is { } reopened
            ? JsonFields.Written(writer => { writer.WriteStartObject(); WriteRefFields(writer, reopened); writer.WriteEndObject(); })
            : DBNull.Value);
        command.Parameters.AddWithValue("$submission", quest.Submission is { } submitted
            ? JsonFields.Written(writer =>
            {
                writer.WriteStartObject();
                writer.WriteString("machine", submitted.Machine);
                writer.WriteNumber("sequence", submitted.Sequence);
                writer.WriteString("at", Moments.Write(submitted.At));
                if (submitted.Note is not null) writer.WriteString("note", submitted.Note);
                if (submitted.Session is not null) writer.WriteString("session", submitted.Session);
                writer.WriteEndObject();
            })
            : DBNull.Value);
        command.Parameters.AddWithValue("$reopened", quest.Reopened is { } reopen
            ? JsonFields.Written(writer =>
            {
                writer.WriteStartObject();
                WriteRefFields(writer, new QuestOperationRef(reopen.Machine, reopen.Sequence));
                WriteRef(writer, "of", reopen.Of);
                if (reopen.Note is not null) writer.WriteString("note", reopen.Note);
                writer.WriteString("at", Moments.Write(reopen.At));
                if (reopen.Session is not null) writer.WriteString("session", reopen.Session);
                writer.WriteEndObject();
            })
            : DBNull.Value);
        command.Parameters.AddWithValue(
            "$review", quest.Review is { } chosen ? JsonFields.Written(writer => Reviews.Write(writer, chosen)) : DBNull.Value);
        command.Parameters.AddWithValue("$setUpIn", (object?)quest.SetUpIn ?? DBNull.Value);
        command.Parameters.AddWithValue("$setUps", JsonFields.Written(writer =>
        {
            writer.WriteStartArray();
            foreach (var setUp in quest.SetUps) Reviews.Write(writer, setUp, standing: true);
            writer.WriteEndArray();
        }));
        command.Parameters.AddWithValue("$verdicts", JsonFields.Written(writer =>
        {
            writer.WriteStartArray();
            foreach (var verdict in quest.Verdicts) Reviews.Write(writer, verdict, standing: true);
            writer.WriteEndArray();
        }));
        command.Parameters.AddWithValue("$short", (object?)quest.Short ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "$evidence", quest.Evidence is { } read ? (object)JsonFields.Written(writer => read.Write(writer, standing: true)) : DBNull.Value);
        command.Parameters.AddWithValue("$publishedBy", (object?)quest.PublishedBy ?? DBNull.Value);
        command.Parameters.AddWithValue("$lanes", LinksJson(quest.Lanes));
        command.Parameters.AddWithValue("$requirements", RequirementsJson(quest.Requirements));
        command.Parameters.AddWithValue("$answers", AnswersJson(quest.Answers));
        command.Parameters.AddWithValue("$accepted", (object?)Moments.Write(quest.Accepted) ?? DBNull.Value);
        command.Parameters.AddWithValue("$held", quest.Held ? 1 : 0);
        command.Parameters.AddWithValue("$conflicts", ConflictsJson(quest.Conflicts));
        command.Parameters.AddWithValue("$links", LinksJson(quest.Links));
        command.Parameters.AddWithValue("$attachments", AttachmentsJson(quest.Attachments));
        command.Parameters.AddWithValue("$then", StepsJson(quest.Then));
        command.Parameters.AddWithValue("$parent", (object?)quest.Parent ?? DBNull.Value);
        command.Parameters.AddWithValue("$awaits", (object?)quest.Awaits ?? DBNull.Value);
        command.Parameters.AddWithValue("$workspace", quest.Workspace);
        command.Parameters.AddWithValue("$id", quest.Id);
        command.Parameters.AddWithValue("$sender", quest.From);
        command.Parameters.AddWithValue("$receiver", quest.To);
        command.Parameters.AddWithValue("$title", quest.Title);
        command.Parameters.AddWithValue("$body", quest.Body);
        command.Parameters.AddWithValue("$status", quest.Status.ToString());
        command.Parameters.AddWithValue("$note", (object?)quest.Note ?? DBNull.Value);
        command.Parameters.AddWithValue("$noteParts", (object?)quest.NoteParts ?? DBNull.Value);
        command.Parameters.AddWithValue("$filed", Moments.Write(quest.Filed));
        command.Parameters.AddWithValue("$updated", Moments.Write(quest.Updated));
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }
}
