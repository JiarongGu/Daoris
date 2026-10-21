using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// A host verb run in this Daoris for the terminal that sent its route over the pipe (RPC1d, RPC1e, the terminal RPC design §7.3): a
/// route asked for its words (<c>said: true</c>) runs the verb's own path with this Daoris's world, never looking for an instance, and
/// answers the lines it prints and its exit code beside nothing else, so the terminal says what it says alone, case for case.
/// </summary>
internal static class TerminalSaid
{
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
