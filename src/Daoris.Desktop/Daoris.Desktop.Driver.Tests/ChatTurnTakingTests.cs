using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The person's half of a conversation (D49 §3): a line goes into the harness's stdin, and closing
/// that stream ends the conversation rather than killing it.
/// </summary>
/// <remarks>
/// Over a REAL process, because this is a pipe and a pipe is exactly the thing a fake would get
/// wrong — flushing, line endings, and what a closed stream does to the far side. Node is the
/// stand-in harness: every gate in this repository already needs it, and it echoes without a model.
/// </remarks>
public sealed class ChatTurnTakingTests
{
    /// <summary>A harness that answers what it hears and exits when the person stops talking.</summary>
    private static Process Echo(bool listening = true)
    {
        var info = new ProcessStartInfo
        {
            FileName = "node",
            RedirectStandardInput = listening,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        info.ArgumentList.Add("-e");
        info.ArgumentList.Add(listening
            ? "process.stdin.on('data', d => process.stdout.write(d)); "
              + "process.stdin.on('end', () => process.exit(0));"
            // A driven session's shape: told everything at once, with nobody to take turns with.
            : "process.stdout.write('one-shot\\n');");

        return Process.Start(info)!;
    }

    [Fact]
    public void Sending_to_a_session_nobody_is_running_is_an_answer_not_an_error()
    {
        Assert.False(new SessionProcesses().Send("never-started", "hello"));
        Assert.False(new SessionProcesses().CloseInput("never-started"));
    }

    [Fact]
    public async Task A_line_reaches_the_harness_and_end_of_input_ends_the_conversation()
    {
        var processes = new SessionProcesses();
        using var process = Echo();
        using var tracked = processes.Track("chat1", process);

        Assert.True(processes.Send("chat1", "what is this repository for?"));
        var heard = await process.StandardOutput.ReadLineAsync();
        Assert.Equal("what is this repository for?", heard);

        // Ending, not killing: the harness gets end-of-input and exits on its own, which is what makes
        // the record `completed` rather than `stopped`.
        Assert.True(processes.CloseInput("chat1"));
        await process.WaitForExitAsync();
        Assert.Equal(0, process.ExitCode);
        Assert.False(processes.WasStopRequested("chat1"));
    }

    /// <summary>
    /// A DRIVEN session has no input stream at all — it was given its whole target at once and has
    /// nobody to take turns with. The answer is structural, not a policy: there is nothing to write to.
    /// </summary>
    [Fact]
    public async Task A_driven_session_has_no_channel_to_speak_into()
    {
        var processes = new SessionProcesses();
        using var process = Echo(listening: false);
        using var tracked = processes.Track("driven1", process);

        Assert.False(processes.Send("driven1", "hello?"));

        await process.WaitForExitAsync();
    }

    /// <summary>
    /// Stop is the other verb, and it means something else: the person cut the conversation off. The
    /// flag is what lets the record say whose decision the end was.
    /// </summary>
    [Fact]
    public async Task Stopping_a_chat_is_recorded_as_the_person_s_decision()
    {
        var processes = new SessionProcesses();
        using var process = Echo();
        using var tracked = processes.Track("chat2", process);

        Assert.True(processes.Stop("chat2"));

        await process.WaitForExitAsync();
        Assert.True(processes.WasStopRequested("chat2"));
    }
}
