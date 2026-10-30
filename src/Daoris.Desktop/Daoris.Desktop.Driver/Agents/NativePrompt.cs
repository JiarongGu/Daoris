using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>
/// A driven native session's prompt, written on its standard input (AGENTLYNTAI1s-i; the agent-lyntai design §9.3, §5.5): the
/// harness reads it there under a bare <c>-p</c>, where an argument would be readable by any process on the machine. The prompt is
/// the session's, so it goes on the input of every native start the driver makes: the start, the answer's resume, the run that
/// carries words said while it worked, and the resume's one retry.
/// </summary>
internal static class NativePrompt
{
    /// <summary>
    /// Write <paramref name="prompt"/> to <paramref name="process"/> in one call, so a surrogate pair is never split, and close its
    /// input so the harness reads it to its end; a start that did not redirect its input, or an adapter that is not the pipe door
    /// (the protocol door writes its own frames), is left as it is.
    /// </summary>
    /// <remarks>
    /// Called once the run's output is being read, so a harness that writes before it reads can never fill a pipe and wait (the note
    /// at <c>ChildProcess.RunAsync</c>). A harness that ended before it read it all says why in its own exit: the write's refusal is
    /// swallowed, never raised in its place. The encoding is the start's own, BOM-less UTF-8 (<see cref="ClaudeCodeAdapter"/>).
    /// </remarks>
    internal static async Task WriteAsync(Process process, ISessionAdapter adapter, string prompt, CancellationToken ct)
    {
        if (adapter.Wire != SessionWire.Pipe || !process.StartInfo.RedirectStandardInput) return;
        try
        {
            await process.StandardInput.WriteAsync(prompt.AsMemory(), ct).ConfigureAwait(false);
            process.StandardInput.Close();
        }
        catch (IOException)
        {
            // The pipe closed under the write: the harness is ending, and its exit is the answer that counts.
        }
        catch (ObjectDisposedException)
        {
            // The same, where its end disposed the stream first.
        }
    }
}
