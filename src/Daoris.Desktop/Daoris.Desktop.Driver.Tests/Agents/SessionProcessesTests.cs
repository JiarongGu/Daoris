using System.ComponentModel;
using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>
/// What a kill's failure may be and still mean the tree was already going (MOD8's finding, FIX-LOG): a whole-tree
/// kill reports a process that refused access mid-exit inside an <see cref="AggregateException"/>, which a catch
/// of the single types let through a person's stop and a chat's cleanup. The kill itself cannot be provoked on
/// demand; the rule deciding what to swallow can, and is held here.
/// </summary>
public sealed class SessionProcessesTests
{
    [Fact]
    public void A_root_that_exited_or_a_tree_refusing_access_mid_exit_is_the_tree_already_going()
    {
        Assert.True(SessionProcesses.Ending(new InvalidOperationException("No process is associated with this object.")));
        Assert.True(SessionProcesses.Ending(new Win32Exception(5, "Access is denied.")));
        Assert.True(SessionProcesses.Ending(new AggregateException(new Win32Exception(5, "Access is denied."))));
        Assert.True(SessionProcesses.Ending(new AggregateException(
            new InvalidOperationException(), new AggregateException(new Win32Exception(5)))));
    }

    [Fact]
    public void Any_other_failure_is_not_swallowed()
    {
        Assert.False(SessionProcesses.Ending(new IOException("the disk went away")));
        Assert.False(SessionProcesses.Ending(new AggregateException(new Win32Exception(5), new IOException())));
        Assert.False(SessionProcesses.Ending(new UnauthorizedAccessException()));
    }
}
