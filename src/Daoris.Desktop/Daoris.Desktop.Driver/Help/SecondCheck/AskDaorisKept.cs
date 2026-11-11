using System.Text.Json;

namespace Daoris.Driver;

/// <summary>A press of Ask Daoris's as the second check keeps it: the press record's id, its conversation, when, route, place, outcome and whether its card asked.</summary>
/// <param name="Place">The place it named in the terminal's spelling (<see cref="HelpFocus.Said"/>), or null where it named none.</param>
public sealed record KeptPress(string Id, string Session, DateTimeOffset At, string Route, string? Place, string Outcome, bool Asked);

/// <summary>
/// What the selection needs of Ask Daoris's presses and of the calls held for the person and answered, kept beside the conversation's
/// reports, so a turn is read with them past the press record's cap and after a restart, and deleted with the conversation. One line per
/// move, appended; a press is read as its settled outcome whatever order its lines were written in, so a writer need not wait on the press.
/// </summary>
public sealed class AskDaorisKept(string home)
{
    private const string PressesFile = "ask-daoris.presses";
    private const string AnsweredFile = "ask-daoris.answered";

    private static readonly object Gate = new();
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> Interim = new(StringComparer.Ordinal) { "running", "waiting" };

    /// <summary>A press as it moved: began, placed, answered or ended.</summary>
    public void Press(KeptPress press) => Append(press.Session, PressesFile, JsonSerializer.Serialize(press, Options));

    /// <summary>A call of <paramref name="session"/>'s held for the person and answered, by the harness's id of the call.</summary>
    public void Answered(string session, string call) => Append(session, AnsweredFile, JsonSerializer.Serialize(new { call }, Options));

    /// <summary>
    /// <paramref name="session"/>'s presses in the order they began, each with its settled outcome and the newest place it named, the
    /// <paramref name="live"/> ones the press record still holds folded in beside the kept.
    /// </summary>
    public IReadOnlyList<TurnPress> Presses(string session, IEnumerable<KeptPress>? live = null)
    {
        var folded = new Dictionary<string, KeptPress>(StringComparer.Ordinal);
        var order = new List<string>();
        foreach (var press in Lines(session, PressesFile).Select(Read<KeptPress>).OfType<KeptPress>().Concat(live ?? []))
        {
            if (!folded.TryGetValue(press.Id, out var was))
            {
                folded[press.Id] = press;
                order.Add(press.Id);
                continue;
            }

            var outcome = Interim.Contains(press.Outcome) && !Interim.Contains(was.Outcome) ? was.Outcome : press.Outcome;
            folded[press.Id] = was with { Outcome = outcome, Place = press.Place ?? was.Place };
        }

        return [.. order.Select(id => folded[id]).OrderBy(press => press.At).Select(press => new TurnPress(press.At, press.Route, press.Place, press.Outcome, press.Asked))];
    }

    /// <summary>The ids of <paramref name="session"/>'s calls held for the person and answered.</summary>
    public IReadOnlySet<string> Answered(string session) =>
        Lines(session, AnsweredFile).Select(Read<AnsweredCall>).Select(line => line?.Call).OfType<string>().ToHashSet(StringComparer.Ordinal);

    private sealed record AnsweredCall(string? Call);

    private string FolderOf(string session) => DaorisHome.Entry(home, HomeEntry.Sessions, session + QuestionSupervisorDecisions.DecisionsSuffix);

    private void Append(string session, string file, string line)
    {
        if (!SessionEventText.IsId(session)) return;
        lock (Gate)
        {
            Directory.CreateDirectory(FolderOf(session));
            File.AppendAllText(Path.Combine(FolderOf(session), file), line + "\n");
        }
    }

    private IReadOnlyList<string> Lines(string session, string file)
    {
        if (!SessionEventText.IsId(session)) return [];
        var path = Path.Combine(FolderOf(session), file);
        try
        {
            lock (Gate)
            {
                return File.Exists(path) ? File.ReadAllText(path).Split('\n', StringSplitOptions.RemoveEmptyEntries) : [];
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static T? Read<T>(string line)
        where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(line, Options);
        }
        catch (JsonException)
        {
            // A torn or foreign line is nothing kept.
            return null;
        }
    }
}
