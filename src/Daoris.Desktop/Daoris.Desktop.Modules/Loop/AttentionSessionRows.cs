using System.Text.Json.Nodes;
using Daoris.Driver;
using static Daoris.Desktop.AttentionJson;

namespace Daoris.Desktop;

/// <summary>The rows of what waits on the person in a session the driver runs: a helper's ask, a hand-over, a quiet session, a held call, an answer, a memory change.</summary>
internal static class AttentionSessionRows
{
    /// <summary>The states in which a session's turn runs: an agent's ask to hand it over holds nothing up until it ends.</summary>
    public static readonly HashSet<string> TurnRuns = new(StringComparer.Ordinal) { "queued", "starting", "working" };

    /// <summary>The rows, in the order <see cref="NeedsAPerson.Rows"/> judges them; <paramref name="sessionScope"/> is the workspace of a session whose record is in hand.</summary>
    public static IEnumerable<JsonObject> Rows(AttentionFacts facts, Dictionary<string, JsonObject> sessions, Func<string?, string?> sessionScope)
    {
        var rows = new List<JsonObject>();
        // An agent's ask for a helper that waits on the person's yes: its question's first line as written, the session that
        // asked, and its repository where the session's record is in hand.
        foreach (var wait in facts.Helpers)
        {
            var record = sessions.GetValueOrDefault(Text(wait, "session") ?? "");
            var question = Text(wait, "question") ?? "";
            var line = question.Split('\n')[0].Trim();
            var row = Row("helper", Text(wait, "id"), Truthy(wait, "at"), sessionScope(Text(wait, "session")));
            row["where"] = Text(record, "repository") ?? "";
            row["session"] = Text(wait, "session");
            row["title"] = line.Length > 0 ? line : question;
            rows.Add(row);
        }

        // An agent's ask to hand its session over that waits on the person's yes or no: by the ask's own id, its
        // note's first line as written, the session that asked and the agent it goes to. It stands still on it once that session's turn
        // is no longer running, since the session goes on only by their word; while its turn runs it holds nothing up yet.
        foreach (var wait in facts.HandOvers)
        {
            var record = sessions.GetValueOrDefault(Text(wait, "session") ?? "");
            var note = Text(wait, "note") ?? "";
            var line = note.Split('\n')[0].Trim();
            var row = Row("hand-over", Text(wait, "id"), Truthy(wait, "at"), sessionScope(Text(wait, "session")));
            row["where"] = Text(record, "repository") ?? "";
            row["session"] = Text(wait, "session");
            row["title"] = line.Length > 0 ? line : note;
            row["agent"] = Text(wait, "agent");
            row["stands"] = record is not null && !TurnRuns.Contains(Text(record, "state") ?? "");
            rows.Add(row);
        }

        // A session the watcher brings to the person: keyed by the session, since
        // when it is last heard, its repository, its quest, and the minutes, whether it is busy (then offered no end), whether it is an
        // intake and whether the person's words wait to reach it. Its turn stands still on the person while it is quiet.
        foreach (var wait in facts.Quiet)
        {
            var session = Text(wait, "session");
            var record = sessions.GetValueOrDefault(session ?? "");
            var row = Row("quiet", session, Truthy(wait, "since"), sessionScope(session));
            row["where"] = Text(record, "repository") ?? "";
            row["session"] = session;
            row["quest"] = Text(wait, "quest");
            row["quiet"] = new JsonObject
            {
                ["minutes"] = wait["minutes"]?.DeepClone(),
                ["busy"] = wait["busy"]?.DeepClone(),
                ["intake"] = Text(wait, "kind") == SessionWatch.Intake,
                ["words"] = wait["words"]?.DeepClone(),
            };
            // It waits for its repository's heavy-run slot behind a holder, named by the holder's session, empty for a terminal's run.
            if (wait["heavy"] is JsonValue heavy) row["quiet"]!["heavy"] = heavy.DeepClone();
            rows.Add(row);
        }

        // A session that waited on its own helper, whose helper ended with no answer and whose move back to the person the service refused:
        // it stays `completed`, so no park row says it, and it holds its tree
        // until the person looks. Keyed by the session, since the first refusal, its repository, its quest and the service's words as
        // said. A record the page holds that has moved on (the move landed, a park's row now) is none; the look lets its file go next.
        foreach (var back in facts.Backs)
        {
            var session = Text(back, "session");
            var record = sessions.GetValueOrDefault(session ?? "");
            if (record is not null && Text(record, "state") != "completed") continue;
            var row = Row("helper-ended", session, Truthy(back, "at"), sessionScope(session));
            row["where"] = Text(record, "repository") ?? "";
            row["session"] = session;
            row["quest"] = Text(back, "quest");
            row["detail"] = Text(back, "words");
            rows.Add(row);
        }

        // A tool call a session holds for the person's answer under asking, keyed by the session and the request, since a
        // request's id is only its agent's wire's.
        foreach (var wait in facts.Permissions)
        {
            var record = sessions.GetValueOrDefault(Text(wait, "session") ?? "");
            var row = Row("permission", $"{Text(wait, "session")}#{Text(wait, "request")}", Truthy(wait, "since"), sessionScope(Text(wait, "session")));
            row["where"] = Text(record, "repository") ?? "";
            row["session"] = Text(wait, "session");
            row["permission"] = wait.DeepClone();
            rows.Add(row);
        }

        // A third answer in a row with nobody between, which its session's work waits on the person for:
        // keyed by the session, its wait whole, the agent that answered among it.
        foreach (var wait in facts.Answers)
        {
            var record = sessions.GetValueOrDefault(Text(wait, "session") ?? "");
            var row = Row("answer", Text(wait, "session"), Truthy(wait, "since"), sessionScope(Text(wait, "session")));
            row["where"] = Text(record, "repository") ?? "";
            row["session"] = Text(wait, "session");
            row["answer"] = wait.DeepClone();
            rows.Add(row);
        }

        // A session whose memory changes wait on the person: an end or a replacement for its
        // approver, or a stopped session's staged rows for Keep or Drop. One row by the session, a door to its page's Memory section.
        foreach (var wait in facts.Memory)
        {
            var record = sessions.GetValueOrDefault(Text(wait, "session") ?? "");
            var row = Row("memory", Text(wait, "session"), Truthy(wait, "since"), sessionScope(Text(wait, "session")));
            row["where"] = Text(record, "repository") ?? "";
            row["session"] = Text(wait, "session");
            row["memory"] = new JsonObject
            {
                ["ends"] = wait["ends"]?.DeepClone() ?? 0, ["replacements"] = wait["replacements"]?.DeepClone() ?? 0,
                ["staged"] = wait["staged"]?.DeepClone() ?? 0,
            };
            rows.Add(row);
        }

        return rows;
    }
}
