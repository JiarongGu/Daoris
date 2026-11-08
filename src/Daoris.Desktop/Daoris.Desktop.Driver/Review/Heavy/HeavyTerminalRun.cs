using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>How a terminal's heavy run in the running Daoris ended: its exit code where it ran, else the refusal's code and sentence.</summary>
public sealed record HeavyTerminalAnswer(int? Exit, string? Refused = null, string? Message = null);

/// <summary>
/// A heavy run a terminal asked the running Daoris for, the pipe's <c>RUN_HEAVY</c>: the terminal's words, folder and environment,
/// run by <see cref="HeavyRun"/> in this process, so each gate is this process's child, in its job and under the machine's
/// admission, and writes to the terminal's own standard handles (<see cref="TerminalHandles"/>), the run's own lines with it. The
/// answer is the run's exit code; the terminal waits on it and says nothing of its own.
/// </summary>
/// <remarks>
/// The payload is <c>{ words, directory, environment, handles: { input, output, error } }</c>, as <see cref="HeavyAppRun"/> sends it.
/// Its caller holds the run to the connection that asked it: that connection's end, or its <c>RUN_STOP</c>, cancels
/// <paramref name="ct"/> of <see cref="RunAsync"/>, and the run ends what it started before it answers.
/// </remarks>
public static class HeavyTerminalRun
{
    /// <summary>The pipe's own request that asks for a run.</summary>
    public const string Type = "RUN_HEAVY";

    /// <summary>The pipe's own request that stops a run the same connection asked for, naming its request's id.</summary>
    public const string StopType = "RUN_STOP";

    /// <summary>The payload is not a run's: nothing ran.</summary>
    public const string Unread = "heavy-ask";

    /// <summary>The terminal's standard handles could not be taken: nothing ran, and the terminal runs it itself.</summary>
    public const string Untaken = "terminal-handles";

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>Run what <paramref name="payload"/> asks, for the terminal process <paramref name="terminal"/>, on <paramref name="home"/>.</summary>
    public static async Task<HeavyTerminalAnswer> RunAsync(string home, JsonElement payload, int terminal, CancellationToken ct)
    {
        if (Read(payload, out var problem) is not { } read) return new HeavyTerminalAnswer(null, Unread, problem);
        var (words, directory, environment, named) = read;

        if (HeavyRun.Read(words, out var wrong) is not { Look: false } ask)
        {
            return new HeavyTerminalAnswer(null, Unread, wrong ?? "a look reads the slot in the terminal itself and runs nothing.");
        }

        if (TerminalHandles.Take(terminal, named, out var untaken) is not { } handles)
        {
            return new HeavyTerminalAnswer(null, Untaken, untaken);
        }

        using (handles)
        {
            var stream = handles.OutputStream();
            await using var output = stream is null ? TextWriter.Null : new StreamWriter(stream, Utf8) { AutoFlush = true };
            var world = new HeavyRunWorld(home, directory, name => environment.GetValueOrDefault(name))
            {
                Asker = handles.Asker,
                Environment = environment,
                Streams = handles,
            };
            try
            {
                return new HeavyTerminalAnswer(await HeavyRun.RunAsync(ask, world, output, ct).ConfigureAwait(false));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                await output.WriteLineAsync($"run-heavy: {error.Message}").ConfigureAwait(false);
                return new HeavyTerminalAnswer(2);
            }
        }
    }

    /// <summary>The payload's parts, or null with what is wrong with it.</summary>
    private static (IReadOnlyList<string> Words, string Directory, IReadOnlyDictionary<string, string> Environment, StandardHandles Handles)? Read(
        JsonElement payload, out string? problem)
    {
        problem = "a run names its words, its folder, its environment and its standard handles.";
        if (payload.ValueKind != JsonValueKind.Object
            || !payload.TryGetProperty("words", out var words) || words.ValueKind != JsonValueKind.Array
            || !payload.TryGetProperty("directory", out var directory) || directory.ValueKind != JsonValueKind.String
            || directory.GetString() is not { Length: > 0 } folder || !Path.IsPathFullyQualified(folder)
            || !payload.TryGetProperty("environment", out var environment) || environment.ValueKind != JsonValueKind.Object
            || !payload.TryGetProperty("handles", out var handles) || handles.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (words.EnumerateArray().Any(word => word.ValueKind != JsonValueKind.String)) return null;
        var variables = new Dictionary<string, string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        foreach (var variable in environment.EnumerateObject())
        {
            if (variable.Value.ValueKind != JsonValueKind.String) return null;
            variables[variable.Name] = variable.Value.GetString()!;
        }

        problem = null;
        return ([.. words.EnumerateArray().Select(word => word.GetString()!)], folder, variables,
            new StandardHandles(Handle(handles, "input"), Handle(handles, "output"), Handle(handles, "error")));
    }

    private static long Handle(JsonElement handles, string name) =>
        handles.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number) ? number : 0;
}
