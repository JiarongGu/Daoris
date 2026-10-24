using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// STEWARDROLE1a-i (D209 §6.1): what the walk reads, it reads from the system. The holder's argument vector, quoted into one command line
/// as the pseudo-console starts it, splits back into the same elements, <c>{"disableAllHooks":true}</c> with its quotes; and the system's
/// reader names this test process's own parent, creation, image, arguments and a variable of its environment as the process itself knows
/// them. The walk's judgement over a process tree is <c>PipeStandingTests</c>'.
/// </summary>
/// <remarks>Nothing is started: the process read is this one.</remarks>
public sealed class HolderWalkTests
{
    [Fact]
    public void The_argument_vector_quoted_into_one_command_line_splits_back_element_for_element()
    {
        var line = (string)typeof(PseudoConsoleTerminals).Assembly.GetType("Daoris.Driver.PseudoConsole")!
            .GetMethod("CommandLine", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(null, [@"C:\home\cache\toolchain\claude-code\2.1.300\bin\claude.exe", HolderTerminal.SessionArguments])!;

        if (!OperatingSystem.IsWindows()) return;
        Assert.Equal(HolderTerminal.SessionArguments, SystemProcesses.Split(line));
        Assert.Contains("{\"disableAllHooks\":true}", SystemProcesses.Split(line)!);
    }

    [Fact]
    public void The_system_s_reader_names_this_process_as_it_knows_itself()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var self = Process.GetCurrentProcess();
        var read = new SystemProcesses().Read(self.Id);

        Assert.NotNull(read);
        Assert.Equal(self.Id, read!.Id);
        Assert.True(read.Parent > 0);
        Assert.Equal(self.StartTime.ToUniversalTime(), read.Created, TimeSpan.FromMilliseconds(1));
        Assert.Equal(Environment.ProcessPath, read.Image, StringComparer.OrdinalIgnoreCase);
        // The line the system keeps for the process, split as the walk splits a harness's, its image's element left out: the host's own
        // arguments first (which .NET's view of its arguments leaves out), then the test host's, element for element.
        var mine = Environment.GetCommandLineArgs().Skip(1).ToList();
        Assert.Equal(mine, read.Arguments!.TakeLast(mine.Count));
        Assert.Equal(Environment.GetEnvironmentVariable("SystemRoot"), read.Variable("SystemRoot"));
        Assert.Null(read.Variable("DAORIS_NOT_SET_" + Guid.NewGuid().ToString("N")));
        Assert.Null(new SystemProcesses().IdOf(null));
    }
}
