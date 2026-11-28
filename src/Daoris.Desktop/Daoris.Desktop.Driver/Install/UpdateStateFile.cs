using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>The last change of state of an install's update, as the application wrote it.</summary>
public sealed record UpdateStateStep(string From, string To, string Why, DateTimeOffset At, string? Detail);

/// <summary>What a stopping application waits on, as of the moment it wrote the file.</summary>
public sealed record UpdateStateWait(string On, int Seconds, int Bound);

/// <summary>
/// Where an install's update stands, as the running application wrote it: what <c>daoris update</c> prints without asking the application,
/// since a stopping one refuses every ask.
/// </summary>
/// <param name="State">One of the words of an update (none, staged, draining, deferred, stopping, applying, refused, installed).</param>
/// <param name="Pid">The application that wrote it, with <paramref name="Started"/> (UTC ticks) so a reader can tell one that has gone.</param>
/// <param name="At">When the file last written.</param>
public sealed record UpdateStateRecord(
    string State, string? Why, string? Build, string? Version, string? Mode, DateTimeOffset? Entered, int Driven, int Turns, int Steps,
    DateTimeOffset? Until, UpdateStateWait? Waiting, string? ProblemCode, string? Problem, UpdateStateStep? Last, int Pid, long Started, DateTimeOffset At)
{
    /// <summary>The time of the person's word the look that wrote this state took, so a terminal knows its word counted.</summary>
    public DateTimeOffset? Word { get; init; }
}

/// <summary>
/// The file the application keeps under the install's <c>update/</c> (<c>state.json</c>), and how a terminal says it. It is the install's, not
/// the home's: the application writes it at each look, the terminal reads it, and nothing else does.
/// </summary>
public static class UpdateStateFile
{
    public const string FileName = "state.json";

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string PathOf(string install) => Path.Combine(install, StagedBuild.Folder, FileName);

    /// <summary>Write the state, whole or not at all; a file that cannot be written leaves the last one, and the next look writes again.</summary>
    public static void Write(string install, UpdateStateRecord record)
    {
        try
        {
            JsonFile.Write(PathOf(install), JsonSerializer.SerializeToNode(record, Options)!);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // The terminal reads the last one.
        }
    }

    /// <summary>The state the application last wrote, or null where there is none or it does not read.</summary>
    public static UpdateStateRecord? Read(string install)
    {
        try
        {
            return JsonNode.Parse(AtomicFile.ReadText(PathOf(install)))?.Deserialize<UpdateStateRecord>(Options);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    /// <summary>Whether the application that wrote <paramref name="state"/> still runs.</summary>
    public static bool Running(UpdateStateRecord state) => ProcessLiveness.Alive(state.Pid, state.Started);

    /// <summary>The state in the words `daoris update` prints: the state, why, what it waits on, and the last transition.</summary>
    public static IEnumerable<string> Said(UpdateStateRecord state, bool running)
    {
        var why = state.Why is null ? "" : $" ({state.Why})";
        var since = state.Entered is { } entered ? $", since {Moment(entered)}" : "";
        yield return $"update: state {state.State}{why}{since}.";
        if (state.Waiting is { } waiting)
        {
            yield return $"  Waiting on {WaitWords(waiting.On)}: {waiting.Seconds} s of at most {waiting.Bound} s; a refusal, a word or a newer build ends it, and work then goes on.";
        }
        else if (state.State is "draining" or "deferred")
        {
            var runs = $"{state.Driven} driven session(s) running a turn, {state.Turns} conversation turn(s), {state.Steps} plugin step(s)";
            yield return state.State == "draining"
                ? $"  Waiting on: {runs}; nothing new starts{(state.Until is { } until ? $" until {Moment(until)} at the latest" : "")}."
                : $"  Waiting on: {runs}; new work starts again, and it installs at the first look nothing runs a turn.";
        }

        if (state.Problem is not null) yield return $"  Because: {state.Problem}";
        if (state.Last is { } last)
        {
            yield return $"  The last transition: {last.From} -> {last.To} at {Moment(last.At)}: {last.Why}{(last.Detail is null ? "" : $" ({last.Detail})")}.";
        }

        if (!running) yield return $"  The application that wrote this (written {Moment(state.At)}) is not running.";
    }

    private static string WaitWords(string on) => on switch
    {
        "place" => "a place in the machine's admission, which the check of the staged build takes first",
        "check" =>"the check of the staged build (its files, and its start on a copy of this home)",
        "launcher" => "the launcher to start",
        "close" => "the application to close",
        _ => on,
    };

    private static string Moment(DateTimeOffset moment) =>
        moment.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture);
}
