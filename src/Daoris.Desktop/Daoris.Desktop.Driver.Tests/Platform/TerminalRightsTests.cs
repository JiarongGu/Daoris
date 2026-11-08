using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A terminal's rights read against this process's: a terminal of the same token's kind holds them, so the running Daoris may start
/// its work; one whose token cannot be read does not.
/// </summary>
public sealed class TerminalRightsTests
{
    [Fact]
    public void A_terminal_with_this_processs_token_holds_its_rights()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var self = Process.GetCurrentProcess();

        Assert.Null(TerminalRights.Differ(self.Handle));
    }

    [Fact]
    public void A_terminal_whose_token_cannot_be_read_does_not()
    {
        if (!OperatingSystem.IsWindows()) return;

        Assert.Equal("the terminal's rights could not be read", TerminalRights.Differ(IntPtr.Zero));
    }
}
