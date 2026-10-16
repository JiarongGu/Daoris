namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The shell's power events, as a seam (FABRIC-EVENT2d): the desktop raises <see cref="PowerResume"/> from the system's resume
/// event, a test raises it by hand, and each listener is told once per resume until it lets go.
/// </summary>
public sealed class PowerResumeTests
{
    [Fact]
    public void A_resume_tells_each_listener_until_it_lets_go()
    {
        var power = new PowerResume();
        var told = 0;
        var other = 0;
        var hearing = power.Hear(() => told++);
        using var _ = power.Hear(() => other++);

        power.Raise();
        hearing.Dispose();
        power.Raise();

        Assert.Equal(1, told);
        Assert.Equal(2, other);
    }

    [Fact]
    public void A_listener_that_throws_does_not_stop_the_rest()
    {
        var power = new PowerResume();
        var told = 0;
        using var first = power.Hear(() => throw new InvalidOperationException("no"));
        using var second = power.Hear(() => told++);

        power.Raise();

        Assert.Equal(1, told);
    }
}
