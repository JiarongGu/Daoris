namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A post to a window's own thread from another, as every window of the shell makes it: nothing posted where the window is gone or
/// goes between the look and the post, and false then, so a timer thread never carries an exception to the finalizer.
/// </summary>
public sealed class WindowPostTests
{
    private static Action<Action> Posts(List<Action> queue) => queue.Add;

    [Fact]
    public void A_live_window_is_handed_the_work_and_the_post_says_so()
    {
        var queue = new List<Action>();
        var ran = false;

        Assert.True(WindowPost.Run(() => true, Posts(queue), () => ran = true));

        Assert.False(ran);
        queue.Single()();
        Assert.True(ran);
    }

    [Fact]
    public void A_window_that_is_gone_is_handed_nothing()
    {
        var queue = new List<Action>();

        Assert.False(WindowPost.Run(() => false, Posts(queue), () => { }));

        Assert.Empty(queue);
    }

    /// <summary>The handle went between the look and the post: the framework throws, and the poster never sees it.</summary>
    [Fact]
    public void A_handle_that_goes_between_the_look_and_the_post_is_a_false_and_never_a_throw()
    {
        Assert.False(WindowPost.Run(
            () => true, _ => throw new InvalidOperationException("Invoke or BeginInvoke cannot be called on a control until the window handle has been created."),
            () => { }));
    }

    [Fact]
    public void Any_other_failure_is_not_this_posts_to_hide()
    {
        Assert.Throws<ArgumentException>(() => WindowPost.Run(() => true, _ => throw new ArgumentException("x"), () => { }));
    }
}
