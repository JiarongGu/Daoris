using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A place's process reads as exited the moment it has, as its runner sees it: a run returning at its git's exit is never counted as a
/// git that outlived its kill, which would hold its repository stalled to every background ask until the place's own wait caught up.
/// </summary>
public sealed class GitPlaceExitTests
{
    [Fact]
    public void A_bound_process_reads_exited_as_soon_as_it_has_exited()
    {
        for (var round = 0; round < 20; round++)
        {
            var place = new GitPlace(StartClass.Background, GitWait.Read, 0);
            var info = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "cmd.exe")) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true };
            using var process = Process.Start(info)!;
            place.Bind(process);
            Assert.False(place.Exited);

            process.StandardInput.Close();
            process.WaitForExit();

            Assert.True(place.Exited, $"round {round}: the place read its exited process as running");
        }
    }
}
