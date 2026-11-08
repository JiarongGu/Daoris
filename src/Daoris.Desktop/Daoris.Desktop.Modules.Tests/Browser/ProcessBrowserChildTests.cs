using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>The browser's process as the shell holds it: joined to the app's kill-on-close job as it starts, and ended through the one management.</summary>
public sealed class ProcessBrowserChildTests
{
    private static Process StartWaiting()
    {
        var info = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "cmd.exe"))
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
        };
        foreach (var word in new[] { "/d", "/c", "set /p line=" }) info.ArgumentList.Add(word);
        return Process.Start(info)!;
    }

    [Fact]
    public async Task The_process_joins_the_browsers_job_and_ending_it_ends_it()
    {
        if (!OperatingSystem.IsWindows()) return;
        var owned = new OwnedProcesses();
        using var process = StartWaiting();
        try
        {
            var child = new ProcessBrowserChild(process, owned);

            Assert.Equal(ProcessPurpose.Browser, owned.Holding(process));
            Assert.False(child.HasExited);

            child.End();
            await child.WaitForExitAsync(CancellationToken.None).WaitAsync(Poll.Patience);

            Assert.True(child.HasExited);
        }
        finally
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
    }
}
