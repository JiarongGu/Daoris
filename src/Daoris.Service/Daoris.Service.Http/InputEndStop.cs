using Microsoft.Extensions.Hosting;

namespace Daoris.Knowledge.Http;

/// <summary>
/// A host the shell started stops when its standard input ends (LOG2a): the shell starts it with that
/// input redirected and <see cref="Variable"/> set, and closes the input to stop it. The stop is the
/// lifetime's own, as Ctrl+C's is, so everything the host does on a clean stop runs, <c>app.stopped</c>
/// in the machine log among it.
/// </summary>
/// <remarks>
/// <para><b>Why the input and not a route.</b> The shell used to end the host by killing it, so the
/// first real machine log held an <c>app.started</c> for every start and no <c>app.stopped</c>. A route
/// to stop the host would be a new door any caller on the machine could press; the input is held by
/// the one process that started the host, and closes by itself if that process dies.</para>
///
/// <para><b>Asked, never assumed.</b> Only a host started with the variable watches, so a host started
/// from a terminal is as it was, and its standard input is never opened. The input is read to its end
/// and nothing read is used.</para>
///
/// <para><b>A twin</b> (<c>.claude/knowledge/twins.md</c>): the shell's <c>HostSupervisor.StopOnInputEnd</c>
/// spells the same variable and sets it to <c>1</c>, duplicated on purpose since the artefacts share no
/// code, and each side's tests hold the same spelling.</para>
/// </remarks>
public static class InputEndStop
{
    /// <summary>The variable the shell sets on the host it starts.</summary>
    public const string Variable = "DAORIS_STOP_ON_INPUT_END";

    /// <summary>Whether a value of <see cref="Variable"/> asks for the stop: <c>1</c>, and nothing else.</summary>
    public static bool Asks(string? value) => value == "1";

    /// <summary>
    /// The watch, when <paramref name="value"/> asks for it; null otherwise, and then
    /// <paramref name="input"/> is never called.
    /// </summary>
    public static Task? WatchWhenAsked(string? value, Func<Stream> input, IHostApplicationLifetime lifetime) =>
        Asks(value) ? Watch(input(), lifetime) : null;

    /// <summary>
    /// Read <paramref name="input"/> to its end on a background thread, then ask
    /// <paramref name="lifetime"/> to stop. The task completes once the stop was asked, and never faults.
    /// </summary>
    public static Task Watch(Stream input, IHostApplicationLifetime lifetime)
    {
        var asked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // A thread of its own, background so it never holds the process open: the read blocks for the
        // host's whole life, which is no work for the thread pool.
        var watcher = new Thread(() =>
        {
            try
            {
                var buffer = new byte[256];
                while (input.Read(buffer, 0, buffer.Length) > 0)
                {
                    // Read past: the input is a signal, and what was written on it is nobody's.
                }
            }
            catch (Exception error) when (error is IOException or ObjectDisposedException)
            {
                // A pipe whose writer died with its process breaks rather than ends: the process that
                // started this host is as gone either way.
            }
            finally
            {
                lifetime.StopApplication();
                asked.TrySetResult();
            }
        })
        {
            IsBackground = true,
            Name = "daoris: stop on input end",
        };
        watcher.Start();
        return asked.Task;
    }
}
