using System.Text.Json;

namespace Daoris.Driver;

/// <summary>The written form of one listing, as <c>SESSION_GROUPS</c> answers it.</summary>
internal static class SessionsJson
{
    /// <summary>
    /// <c>{"sessions": [...]}</c>, and with <paramref name="entries"/> the entries beside the rows, which stay what they were.
    /// </summary>
    internal static string Json(IEnumerable<SessionGrouping> rows, IEnumerable<SessionEntry>? entries = null)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteStartArray("sessions");
            foreach (var row in rows)
            {
                writer.WriteStartObject();
                writer.WriteString("session", row.Session);
                writer.WriteString("group", row.Group);
                writer.WriteString("state", row.State);
                writer.WriteString("reason", row.Reason);
                writer.WriteString("shown", row.Shown);
                writer.WriteBoolean("archived", row.Archived);
                writer.WriteBoolean("teammate", row.Teammate);
                if (row.Strikes is { } strikes) writer.WriteNumber("strikes", strikes); else writer.WriteNull("strikes");
                writer.WriteString("awaits", row.Awaits);
                writer.WriteString("awaitsOf", row.AwaitsOf);
                if (row.Work is { } work)
                {
                    writer.WriteStartObject("work");
                    if (work.Commits is { } commits) writer.WriteNumber("commits", commits); else writer.WriteNull("commits");
                    if (work.Uncommitted is { } uncommitted) writer.WriteNumber("uncommitted", uncommitted); else writer.WriteNull("uncommitted");
                    writer.WriteString("branch", work.Branch);
                    writer.WriteEndObject();
                }
                else
                {
                    writer.WriteNull("work");
                }

                writer.WriteBoolean("holdsQuest", row.HoldsQuest);
                if (row.PausedBy is { } pause)
                {
                    writer.WriteStartObject("pausedBy");
                    writer.WriteString("scope", pause.Word);
                    writer.WriteString("id", pause.Id);
                    writer.WriteEndObject();
                }
                else
                {
                    writer.WriteNull("pausedBy");
                }

                // What holds the person's words on a record that resumes later.
                if (row.Holds is { } holds)
                {
                    writer.WriteStartObject("holds");
                    writer.WriteString("why", holds.Why);
                    writer.WriteString("reason", holds.Reason);
                    writer.WriteString("repository", holds.Repository);
                    if (holds.Until is { } until) writer.WriteString("until", until); else writer.WriteNull("until");
                    writer.WriteEndObject();
                }
                else
                {
                    writer.WriteNull("holds");
                }

                writer.WriteBoolean("deletable", row.Deletable);
                // What its own tree offers to land, whatever group it rests in.
                if (row.Lands is { } lands)
                {
                    writer.WriteStartObject("lands");
                    writer.WriteString("branch", lands.Branch);
                    writer.WriteString("tree", lands.Tree);
                    writer.WriteNumber("commits", lands.Commits);
                    if (lands.Uncommitted is { } paths) writer.WriteNumber("uncommitted", paths); else writer.WriteNull("uncommitted");
                    writer.WriteEndObject();
                }
                else
                {
                    writer.WriteNull("lands");
                }

                // Where the line holds its commits by content, its discard instead, with what the page says.
                if (row.Discards is { } discards)
                {
                    writer.WriteStartObject("discards");
                    writer.WriteString("branch", discards.Branch);
                    writer.WriteString("tree", discards.Tree);
                    writer.WriteString("says", discards.Says);
                    writer.WriteString("keeps", discards.Work.Keeps);
                    writer.WriteString("keptAt", discards.KeptAt);
                    writer.WriteEndObject();
                }
                else
                {
                    writer.WriteNull("discards");
                }

                // The call a working session's agent is inside, where the process that read the rows heard one; whether it waits on
                // the person's answer to its permission request.
                if (row.Calling is { } call)
                {
                    writer.WriteStartObject("calling");
                    writer.WriteString("title", call.Title);
                    writer.WriteString("since", call.Since);
                    writer.WriteBoolean("waiting", call.Waiting);
                    writer.WriteEndObject();
                }
                else
                {
                    writer.WriteNull("calling");
                }

                // The steps of the run it stands for that wait on the person, by which it is placed.
                if (row.Waits is { } waits)
                {
                    writer.WriteStartArray("waits");
                    foreach (var wait in waits)
                    {
                        writer.WriteStartObject();
                        writer.WriteString("run", wait.Run);
                        writer.WriteString("node", wait.Node);
                        writer.WriteString("why", wait.Why);
                        if (wait.Since is { } since) writer.WriteString("since", since); else writer.WriteNull("since");
                        writer.WriteEndObject();
                    }

                    writer.WriteEndArray();
                }
                else
                {
                    writer.WriteNull("waits");
                }

                // Its line's state, and the part or co-worker it is read from.
                if (row.Line is { } line)
                {
                    writer.WriteStartObject("line");
                    writer.WriteString("state", line.State);
                    writer.WriteString("group", line.Group);
                    writer.WriteString("reason", line.Reason);
                    writer.WriteString("by", line.By);
                    writer.WriteString("role", line.Role);
                    writer.WriteEndObject();
                }
                else
                {
                    writer.WriteNull("line");
                }

                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            if (entries is not null)
            {
                writer.WriteStartArray("entries");
                foreach (var entry in entries)
                {
                    writer.WriteStartObject();
                    writer.WriteString("entry", entry.Key);
                    writer.WriteString("origin", entry.Origin);
                    writer.WriteString("repository", entry.Repository);
                    writer.WriteString("group", entry.Group);
                    writer.WriteString("leading", entry.Leading);
                    writer.WriteString("head", entry.Head);
                    writer.WriteString("opens", entry.Opens);
                    writer.WriteStartArray("parts");
                    foreach (var part in entry.Parts)
                    {
                        writer.WriteStartObject();
                        writer.WriteString("session", part.Session);
                        writer.WriteString("quest", part.Quest);
                        writer.WriteString("shown", part.Shown);
                        writer.WriteString("group", part.Group);
                        writer.WriteString("began", part.Began);
                        writer.WriteString("after", part.After);
                        writer.WriteString("askedBy", part.AskedBy);
                        writer.WriteString("head", part.Head);
                        writer.WriteEndObject();
                    }

                    writer.WriteEndArray();
                    writer.WriteStartArray("coworkers");
                    foreach (var coworker in entry.Coworkers)
                    {
                        writer.WriteStartObject();
                        writer.WriteString("session", coworker.Session);
                        writer.WriteString("role", coworker.Role);
                        writer.WriteString("serves", coworker.Serves);
                        writer.WriteString("shown", coworker.Shown);
                        writer.WriteString("state", coworker.State);
                        writer.WriteEndObject();
                    }

                    writer.WriteEndArray();
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
            }

            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray()).Replace("\r\n", "\n");
    }
}
