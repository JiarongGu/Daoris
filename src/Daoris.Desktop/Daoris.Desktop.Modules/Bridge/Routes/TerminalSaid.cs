using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// A host verb run in this Daoris for the terminal that sent its route over the pipe: a
/// route asked for its words (<c>said: true</c>) runs the verb's own path with this Daoris's world, never looking for an instance, and
/// answers the lines it prints and its exit code beside nothing else, so the terminal says what it says alone, case for case.
/// </summary>
internal static class TerminalSaid
{
    /// <summary>
    /// <see cref="RunAsync(Func{TextWriter, Task{int}})"/> for a verb that says its refusals on its error, which the terminal prints on its own
    /// error. Not asked for its words (<paramref name="said"/> false) a press answers <c>{ done, message }</c>: whether the verb exited 0, and
    /// every line it said.
    /// </summary>
    public static async Task<object> RunAsync(Func<TextWriter, TextWriter, Task<int>> run, bool said = true)
    {
        var output = new StringWriter { NewLine = "\n" };
        var error = new StringWriter { NewLine = "\n" };
        var code = await run(output, error).ConfigureAwait(false);
        string[] Lines(StringWriter writer) => writer.ToString().Split('\n') is [.. var whole, ""] ? whole : writer.ToString().Split('\n');
        if (said) return new { Said = new TreesSaid(Lines(output), Lines(error), code).ToWire() };
        return new { Done = code == 0, Message = string.Join('\n', Lines(output).Concat(Lines(error))) };
    }

    /// <summary>A person's press that runs a verb's own path: its answer, then the loop nudged to look again.</summary>
    public static async Task<object?> PressAsync(DriverLoop loop, bool said, Func<TextWriter, TextWriter, Task<int>> run)
    {
        var answer = await RunAsync(run, said).ConfigureAwait(false);
        loop.Nudge();
        return answer;
    }

    /// <summary>The lines <paramref name="run"/> printed and the exit code it returned, as an answer carries them (<see cref="TreesSaid.ToWire"/>).</summary>
    public static async Task<object> RunAsync(Func<TextWriter, Task<int>> run)
    {
        var output = new StringWriter { NewLine = "\n" };
        var code = await run(output).ConfigureAwait(false);
        var lines = output.ToString().Split('\n');
        // The writer ends every line with its newline, so the last piece is the empty one after it.
        var said = lines[^1].Length == 0 ? lines[..^1] : lines;
        return new { Said = new TreesSaid(said, [], code).ToWire() };
    }
}
