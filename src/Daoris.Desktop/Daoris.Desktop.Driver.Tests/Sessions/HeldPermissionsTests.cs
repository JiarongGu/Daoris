using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// AUTONOMY1b3a (D184 point 3): a permission request a driven session's agent sends under its repository's asking permission prompts,
/// held in memory for the person by session and request until the person answers it with an option the agent offered, or it is released
/// by the session or at its own bound. Nothing here starts a process or writes a file.
/// </summary>
public sealed class HeldPermissionsTests
{
    private static readonly IReadOnlyList<PermissionChoice> Offered =
    [
        new("always", "Always allow", PermissionChoice.AllowAlways),
        new("yes", "Allow", PermissionChoice.AllowOnce),
        new("no", "Reject", PermissionChoice.RejectOnce),
    ];

    /// <summary>
    /// A hold is listed for its session with the choices the person may make, a standing allow left out (a rule is proposed, D74),
    /// until the person answers it with an offered option by its id: the hold ends with that option, and it is said as it moves.
    /// </summary>
    [Fact]
    public async Task A_hold_is_listed_until_the_person_answers_it_with_an_offered_option()
    {
        var registry = new HeldPermissions();
        var moved = new List<string>();
        registry.Changed += moved.Add;
        var holds = registry.For("s1");

        var held = holds.Hold("900", "c9", "npm start", Offered)!;

        var listed = Assert.Single(registry.Held("s1"));
        Assert.Equal(("s1", "900", "c9", "npm start"), (listed.Session, listed.Request, listed.Call, listed.Title));
        Assert.Equal(["yes", "no"], listed.Choices.Select(choice => choice.Id));
        Assert.Empty(registry.Held("s2"));
        Assert.False(held.IsCompleted);

        var answer = registry.Answer("s1", "900", "yes");

        Assert.True(answer.Answered);
        var end = await held.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal((PermissionWhy.Answered, "yes"), (end.Why, end.Chosen?.Id));
        Assert.Empty(registry.Held("s1"));
        Assert.Equal(["s1", "s1"], moved);
    }

    /// <summary>
    /// 🔴 Never an invented allow: an option the agent did not offer, or its standing allow, is refused by its code, and the hold
    /// waits on as it was.
    /// </summary>
    [Theory]
    [InlineData("maybe", "unoffered")]
    [InlineData("always", "standing")]
    public void An_option_the_person_may_not_choose_is_refused_and_the_hold_waits_on(string option, string why)
    {
        var registry = new HeldPermissions();
        var held = registry.For("s1").Hold("900", "c9", "npm start", Offered)!;

        var answer = registry.Answer("s1", "900", option);

        Assert.False(answer.Answered);
        Assert.Equal(why, answer.Why);
        Assert.Contains("Nothing was answered", answer.Message);
        Assert.False(held.IsCompleted);
        Assert.Single(registry.Held("s1"));
    }

    /// <summary>
    /// The person's refusal with no option named is the offered one-time rejection, and where the agent offered none, no option at all
    /// (the wire's <c>cancelled</c>): no allow offered means the person can only refuse.
    /// </summary>
    [Fact]
    public async Task A_refusal_naming_no_option_takes_the_offered_rejection_or_none()
    {
        var registry = new HeldPermissions();
        var holds = registry.For("s1");
        var rejecting = holds.Hold("900", null, "npm start", Offered)!;
        var allowing = holds.Hold("901", null, "rm -rf", [new("always", "Always allow", PermissionChoice.AllowAlways)])!;

        Assert.Empty(registry.Held("s1").Single(held => held.Request == "901").Choices);
        Assert.True(registry.Answer("s1", "900", option: null).Answered);
        Assert.True(registry.Answer("s1", "901", option: null).Answered);

        Assert.Equal("no", (await rejecting.WaitAsync(TimeSpan.FromSeconds(5))).Chosen?.Id);
        var cancelled = await allowing.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal((PermissionWhy.Answered, (PermissionChoice?)null), (cancelled.Why, cancelled.Chosen));
    }

    /// <summary>
    /// The session's release ends each of its holds with why, and only its own; an answer after it finds nothing held, and says so by
    /// its code. A request already held is not held twice.
    /// </summary>
    [Fact]
    public async Task A_release_ends_the_sessions_own_holds_and_an_answer_after_it_finds_none()
    {
        var registry = new HeldPermissions();
        var mine = registry.For("s1");
        var theirs = registry.For("s2");
        var held = mine.Hold("900", "c9", "npm start", Offered)!;
        var other = theirs.Hold("900", "c1", "npm test", Offered)!;
        Assert.Null(mine.Hold("900", "c9", "npm start", Offered));

        mine.ReleaseAll(PermissionWhy.Cancelled);

        var end = await held.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal((PermissionWhy.Cancelled, (PermissionChoice?)null), (end.Why, end.Chosen));
        Assert.Empty(registry.Held("s1"));
        Assert.False(other.IsCompleted);
        var late = registry.Answer("s1", "900", "yes");
        Assert.Equal((false, "unheld"), (late.Answered, late.Why));
    }

    /// <summary>
    /// 🔴 An unanswered request is a turn hung for ever (REV3): a hold nobody answers is released at its own bound, and nothing the
    /// person says after it lands.
    /// </summary>
    [Fact]
    public async Task A_hold_nobody_answers_is_released_at_its_bound()
    {
        var registry = new HeldPermissions();
        var held = registry.For("s1", bound: TimeSpan.FromMilliseconds(50)).Hold("900", "c9", "npm start", Offered)!;

        var end = await held.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal((PermissionWhy.Bound, (PermissionChoice?)null), (end.Why, end.Chosen));
        Assert.Empty(registry.Held("s1"));
        Assert.False(registry.Answer("s1", "900", "yes").Answered);
    }

    /// <summary>One registry per record of this process's sessions, as <see cref="RunningCalls"/> is: the driver's and the page's.</summary>
    [Fact]
    public void The_registry_is_one_per_record()
    {
        var events = new SessionEvents(Path.Combine("X:/daoris", "sessions"));

        Assert.Same(HeldPermissions.Of(events), HeldPermissions.Of(events));
        Assert.Equal(HeldPermissions.Bound, HeldPermissions.Of(events).For("s1").Bound);
    }
}
