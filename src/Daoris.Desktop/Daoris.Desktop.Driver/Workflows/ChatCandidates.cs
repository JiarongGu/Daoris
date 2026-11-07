using System.Text.Json;

namespace Daoris.Driver;

/// <summary>One candidate of a graph-bound chat run: the tip a press submitted, and the earlier tips it superseded in place.</summary>
public sealed record ChatCandidate(int Number, string Tip, DateTimeOffset At, IReadOnlyList<string> Superseded)
{
    /// <summary>The id the run's <c>claimed</c> observes it by, <c>chat-&lt;first&gt;#&lt;n&gt;</c>, one per candidate of the run.</summary>
    public string IdIn(string run) => $"{run}#{Number}";
}

/// <summary>A chat run's candidates and the branch they are on.</summary>
public sealed record ChatCandidateFile(string Run, string Repository, string? Workspace, string Branch, IReadOnlyList<ChatCandidate> Candidates)
{
    /// <summary>The newest candidate: the one the run is judged on; null before a first press.</summary>
    public ChatCandidate? Newest => Candidates.Count == 0 ? null : Candidates[^1];
}

/// <summary>What a press's submission came to.</summary>
public enum CandidateSubmitted
{
    /// <summary>A new candidate, numbered one past the last landed.</summary>
    Opened,

    /// <summary>The candidate submitted and unlanded took the new tip, keeping its number.</summary>
    Superseded,

    /// <summary>The tip is the one already submitted: nothing changed.</summary>
    Unchanged,
}

/// <summary>
/// A graph-bound chat run's candidates, <c>&lt;home&gt;/workflows/candidates/chat-&lt;first&gt;.json</c>: the
/// subject a conversation's <c>claimed</c> observes in place of a quest. Submitted only at the person's press, never at a turn's end; a press at a
/// new tip over a candidate that is submitted and unlanded supersedes it in place, and one past a landed candidate opens the next. Machine-local,
/// written whole and atomically, and taken with the record its run is keyed by (<see cref="ChatRuns.Files"/>).
/// </summary>
public static class ChatCandidates
{
    private static readonly object Gate = new();

    /// <summary>Where a chat run's candidate file is kept.</summary>
    public static string PathOf(string home, string run) =>
        WorkplaceRunFiles.IsChatRun(run) && SafeNames.IsPlainId(run)
            ? Path.Combine(WorkplaceRunFiles.CandidatesFolder(home), run + ".json")
            : throw new DriverException($"`{run}` is no chat run.");

    /// <summary>The run's candidates; null where none is submitted or the file does not read.</summary>
    public static ChatCandidateFile? Read(string home, string run)
    {
        if (!WorkplaceRunFiles.IsChatRun(run) || !SafeNames.IsPlainId(run)) return null;
        var path = PathOf(home, run);
        if (!File.Exists(path)) return null;
        try
        {
            using var document = JsonDocument.Parse(AtomicFile.ReadText(path));
            var root = document.RootElement;
            if (Json.Text(root, "repository") is not { Length: > 0 } repository || Json.Text(root, "branch") is not { Length: > 0 } branch) return null;
            var candidates = Json.Items(root, "candidates").Select(CandidateOf).OfType<ChatCandidate>().ToList();
            return new ChatCandidateFile(run, repository, Json.Text(root, "workspace"), branch, candidates);
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// A press's submission at <paramref name="tip"/>: the next candidate where the newest has landed (or none is submitted), the newest taking the
    /// tip in place where it is submitted and unlanded at another, and nothing where it is the tip already submitted.
    /// </summary>
    /// <param name="landed">Whether a tip landed on this branch (as a landing record says it, squashed or not).</param>
    public static (ChatCandidate Candidate, CandidateSubmitted What) Submit(
        string home, string run, string repository, string? workspace, string branch, string tip, DateTimeOffset at, Func<string, bool> landed)
    {
        lock (Gate)
        {
            var file = Read(home, run);
            List<ChatCandidate> all = file is { } kept && string.Equals(kept.Branch, branch, StringComparison.Ordinal) ? [.. kept.Candidates] : [];
            ChatCandidate candidate;
            CandidateSubmitted what;
            if (all.Count > 0 && !landed(all[^1].Tip))
            {
                var newest = all[^1];
                if (string.Equals(newest.Tip, tip, StringComparison.OrdinalIgnoreCase)) return (newest, CandidateSubmitted.Unchanged);
                candidate = newest with { Tip = tip, At = at, Superseded = [.. newest.Superseded, newest.Tip] };
                all[^1] = candidate;
                what = CandidateSubmitted.Superseded;
            }
            else
            {
                candidate = new ChatCandidate(all.Count == 0 ? 1 : all[^1].Number + 1, tip, at, []);
                all.Add(candidate);
                what = CandidateSubmitted.Opened;
            }

            JsonFile.Write(PathOf(home, run), writer =>
            {
                writer.WriteStartObject();
                writer.WriteString("run", run);
                writer.WriteString("repository", repository);
                if (workspace is not null) writer.WriteString("workspace", workspace);
                writer.WriteString("branch", branch);
                writer.WriteStartArray("candidates");
                foreach (var each in all)
                {
                    writer.WriteStartObject();
                    writer.WriteNumber("number", each.Number);
                    writer.WriteString("tip", each.Tip);
                    writer.WriteString("at", OpinionViews.Moment(each.At));
                    writer.WriteStartArray("superseded");
                    foreach (var earlier in each.Superseded) writer.WriteStringValue(earlier);
                    writer.WriteEndArray();
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            });
            HomeWrites.Mine(PathOf(home, run));
            return (candidate, what);
        }
    }

    private static ChatCandidate? CandidateOf(JsonElement element) =>
        Json.Int(element, "number") is { } number && Json.Text(element, "tip") is { Length: > 0 } tip && Moments.Read(element, "at") is { } at
            ? new ChatCandidate(number, tip, at, Json.Strings(element, "superseded"))
            : null;
}
