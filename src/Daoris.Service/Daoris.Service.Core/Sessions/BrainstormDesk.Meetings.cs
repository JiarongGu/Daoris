using System.Globalization;
using System.Text.Json;

namespace Daoris.Knowledge;

/// <summary>
/// A meeting is a kind of brainstorm (ONEWAY1t, D177 points 1, 2, 4 and 7): the same desk writes its request, each round's send and
/// its stop under <c>brainstorms/meetings/&lt;id&gt;/</c>, the files the driver's <c>MeetingFiles</c> reads. The packets, the state, the
/// transcript and the turns are the driver's alone.
/// </summary>
public sealed partial class BrainstormDesk
{
    /// <summary>The folder under the brainstorms' entries that holds every meeting: the driver's <c>MeetingFiles.Under</c>, duplicated deliberately.</summary>
    public const string MeetingsFolder = "meetings";

    /// <summary>A meeting's fewest agents: one alone is no meeting. The most is a brainstorm's.</summary>
    public const int FewestMeetingAgents = 2;

    /// <summary>The meeting's request written once, as the driver's look takes it; the same id and words asked again are the same meeting.</summary>
    public BrainstormOutcome AskMeeting(string? question, string? repository, IReadOnlyList<string?>? agents, DateTimeOffset at, string? own = null)
    {
        var words = question?.Trim() ?? "";
        if (words.Length == 0)
        {
            return new(BrainstormRefusal.BadShape, "A meeting needs its question: what you want the agents to talk over. Nothing was asked.");
        }

        if (words.Length > MostQuestion)
        {
            return new(BrainstormRefusal.BadShape, $"A meeting's question is 1 to {MostQuestion} characters; this one is {words.Length}. Nothing was asked.");
        }

        var named = (agents ?? []).Select(agent => agent?.Trim() ?? "").Where(agent => agent.Length > 0).ToList();
        var different = named.Distinct(StringComparer.OrdinalIgnoreCase).Count();
        if (different != named.Count || different < FewestMeetingAgents || different > MostAgents)
        {
            return new(BrainstormRefusal.BadShape,
                $"A meeting names {FewestMeetingAgents} to {MostAgents} different agents, each once; {named.Count} were named. Nothing was asked.");
        }

        if (named.FirstOrDefault(agent => agent.Any(c => char.IsWhiteSpace(c) || char.IsControl(c))) is { } spaced)
        {
            return new(BrainstormRefusal.BadShape,
                $"An agent is named as one word, as it is listed in Settings, and `{spaced}` is not. Nothing was asked.");
        }

        if (own is not null && !PlainName.IsMatch(own))
        {
            return new(BrainstormRefusal.BadShape,
                $"A meeting's own id is a letter or digit, then letters, digits and dashes, at most 32; `{own}` is not. Nothing was asked.");
        }

        if (Home is null) return new(BrainstormRefusal.NoHome, $"{DaorisHome.Sentence} Nothing was asked.");

        var of = string.IsNullOrWhiteSpace(repository) ? null : repository.Trim();
        var id = own ?? NewId(MeetingsFolder);
        if (own is not null && Parse(MeetingPath(id, "request.json")) is { } on)
        {
            return Same(on, words, of, named, [])
                ? new(BrainstormRefusal.None, $"Meeting `{id}` is already asked: the same meeting, never a second spend.", FindMeeting(id))
                : new(BrainstormRefusal.State, $"Meeting `{id}` was asked with other words; an id names one meeting. Nothing was asked.", FindMeeting(id));
        }

        Write(MeetingPath(id, "request.json"), writer =>
        {
            writer.WriteNumber("meeting", 1);
            writer.WriteString("id", id);
            writer.WriteString("question", words);
            if (of is null) writer.WriteNull("repository");
            else writer.WriteString("repository", of);
            writer.WriteStartArray("agents");
            foreach (var agent in named) writer.WriteStringValue(agent);
            writer.WriteEndArray();
            writer.WriteString("at", Moments.Write(at));
        });

        return new(BrainstormRefusal.None,
            $"Meeting `{id}` asked of {string.Join(", ", named.Select(agent => $"`{agent}`"))}, in that order. Daoris freezes its first round's packet at its next look, from "
            + (of is null ? "the question alone" : $"its knowledge of `{of}`")
            + "; with no Daoris running, it waits. Read the packet before you send it: nothing goes to any agent until you do.",
            FindMeeting(id));
    }

    /// <summary>
    /// The person's press for one round (D177 point 4): the first round's by itself; a later round's only named, so a send repeated
    /// from a terminal's history never spends another round; refused while a round runs, and after a stop or the end.
    /// </summary>
    public BrainstormOutcome SendMeeting(string id, int? round, DateTimeOffset at)
    {
        if (FindMeeting(id) is not { } meeting) return NotFoundMeeting(id);
        if (Parse(MeetingPath(id, "state.json")) is not { } stood || Text(stood, "state") is not { } state)
        {
            return new(BrainstormRefusal.State,
                $"Meeting `{id}` has no packet yet: Daoris freezes its first round's at its next look, and you read it before it goes.", meeting);
        }

        var current = meeting.Round ?? 1;
        var stopped = File.Exists(MeetingPath(id, "stop.json"));
        if (stopped || state == Ended)
        {
            return new(BrainstormRefusal.State, stopped || meeting.Code == "stopped"
                ? $"Meeting `{id}` was stopped: nothing more goes to any agent."
                : $"Meeting `{id}` ended ({meeting.Code ?? "after its last round"}): nothing more goes to any agent.", meeting);
        }

        var wanted = round ?? 1;
        if (state == Running)
        {
            return wanted == current
                ? new(BrainstormRefusal.None, $"Meeting `{id}`'s round {wanted} is already sent, and running.", meeting)
                : new(BrainstormRefusal.State, $"Meeting `{id}`'s round {current} is running; round {wanted} can be sent once it ends and you read its packet.", meeting);
        }

        if (state != Frozen || meeting.Packet is not { } digest)
        {
            return new(BrainstormRefusal.State, $"Meeting `{id}` has no round to send ({state}).", meeting);
        }

        if (wanted != current)
        {
            return new(BrainstormRefusal.State, round is null
                ? $"Meeting `{id}` waits for round {current}: read its packet, then send it naming round {current}."
                : $"Meeting `{id}` waits for round {current}, not round {wanted}.", meeting);
        }

        if (meeting.Sent == digest)
        {
            return new(BrainstormRefusal.None, $"Meeting `{id}`'s round {current} is already sent: the same round, never a second spend.", meeting);
        }

        Write(MeetingPath(id, "sent", string.Create(CultureInfo.InvariantCulture, $"{current}.json")), writer =>
        {
            writer.WriteNumber("round", current);
            writer.WriteString("packet", digest);
            writer.WriteString("at", Moments.Write(at));
        });
        return new(BrainstormRefusal.None,
            $"Meeting `{id}`'s round {current} sent, packet `{digest}`: its turns start at Daoris's next look, one participant at a time.", FindMeeting(id));
    }

    /// <summary>The person's stop (D177's trap, the person leaving): the turn that runs ends, what was said is kept, and nothing more goes.</summary>
    public BrainstormOutcome StopMeeting(string id, DateTimeOffset at)
    {
        if (FindMeeting(id) is not { } meeting) return NotFoundMeeting(id);
        if (meeting.State == Ended)
        {
            return new(BrainstormRefusal.None, $"Meeting `{id}` already ended ({meeting.Code ?? "after its last round"}).", meeting);
        }

        Write(MeetingPath(id, "stop.json"), writer => writer.WriteString("at", Moments.Write(at)));
        return new(BrainstormRefusal.None,
            $"Meeting `{id}`: stop asked. The turn that runs ends where Daoris runs it, what was said is kept, and nothing more goes to any agent.", meeting);
    }

    /// <summary>The meeting cleared: its records, its packets, its transcript and its rooms; never while a round runs.</summary>
    public BrainstormOutcome ClearMeeting(string id)
    {
        if (Home is null || !PlainName.IsMatch(id)) return NotFoundMeeting(id);
        var folder = DaorisHome.Entry(Home, HomeEntry.Brainstorms, MeetingsFolder, id);
        var meeting = FindMeeting(id);
        if (meeting is null && !Directory.Exists(folder)) return NotFoundMeeting(id);
        if (meeting is { State: Running })
        {
            return new(BrainstormRefusal.State, $"Meeting `{id}` is running; it is cleared once its round ends.", meeting);
        }

        if ((Remove(DaorisHome.Entry(Home, HomeEntry.BrainstormRooms, MeetingsFolder, id)) ?? Remove(folder)) is { } left)
        {
            return new(BrainstormRefusal.NotWhole, $"Meeting `{id}` could not be cleared whole: {left}", meeting);
        }

        return new(BrainstormRefusal.None, $"Meeting `{id}` cleared: its records, its packets and its transcript are gone from this machine.", meeting);
    }

    /// <summary>One meeting as its records stand, or null where there is no readable request by that id; its state is <c>asked</c> before a look froze a round.</summary>
    public BrainstormStanding? FindMeeting(string id)
    {
        if (Home is null || !PlainName.IsMatch(id) || Parse(MeetingPath(id, "request.json")) is not { } request) return null;
        if (Text(request, "question") is not { Length: > 0 } question) return null;

        var agents = request.TryGetProperty("agents", out var listed) && listed.ValueKind == JsonValueKind.Array
            ? listed.EnumerateArray().Where(agent => agent.ValueKind == JsonValueKind.String).Select(agent => agent.GetString()!).ToList()
            : [];
        var state = Parse(MeetingPath(id, "state.json")) is { } stood && Text(stood, "state") is { } named ? (named, stood) : (Asked, default(JsonElement?));
        var round = state.Item2 is { } s && s.TryGetProperty("round", out var number) && number.ValueKind == JsonValueKind.Number && number.TryGetInt32(out var read)
            ? read
            : 1;
        var sent = Parse(MeetingPath(id, "sent", string.Create(CultureInfo.InvariantCulture, $"{round}.json"))) is { } send ? Text(send, "packet") : null;
        return new BrainstormStanding(
            id, question, Text(request, "repository"), agents, Moments.Read(request, "at") ?? DateTimeOffset.UnixEpoch, state.Item1,
            state.Item2 is { } c ? Text(c, "code") : null, state.Item2 is { } p ? Text(p, "packet") : null, sent, MeetingKind, round);
    }

    private string MeetingPath(string id, params string[] parts) => Path.Combine(DaorisHome.Entry(Home!, HomeEntry.Brainstorms, MeetingsFolder, id), Path.Combine(parts));

    private static BrainstormOutcome NotFoundMeeting(string id) => new(BrainstormRefusal.NotFound, $"There is no meeting `{id.Trim()}` on this machine.");
}
