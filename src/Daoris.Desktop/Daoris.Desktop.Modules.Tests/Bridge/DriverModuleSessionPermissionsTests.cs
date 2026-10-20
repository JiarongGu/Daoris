using System.Globalization;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// AUTONOMY1b3a (D184 point 3) over the bridge: what this machine's driven sessions hold for the person under their repository's asking
/// permission prompts, as *What needs you* will read it, and the person's answer to one, an option the agent offered by its id or a
/// refusal naming none. Never an invented allow: an option not offered and a standing allow are refused by code, and the hold waits on.
/// </summary>
public sealed class DriverModuleSessionPermissionsTests : DriverModuleBridge
{
    private static readonly IReadOnlyList<PermissionChoice> Offered =
    [
        new("always", "Always allow", PermissionChoice.AllowAlways),
        new("yes", "Allow", PermissionChoice.AllowOnce),
        new("no", "Reject", PermissionChoice.RejectOnce),
    ];

    /// <summary>Each held request is listed with its session, its call and what the person may choose, a standing allow left out.</summary>
    [Fact]
    public async Task What_the_sessions_hold_for_the_person_is_listed_with_its_choices()
    {
        var loop = Loop();
        var module = new DriverModule(Bus, loop);
        Assert.Empty((await AnswerAsync(module, "PERMISSION_WAITS")).GetProperty("waits").EnumerateArray());

        HeldPermissions.Of(loop.Events).For("d1").Hold("900", "c9", "npm start", Offered, "execute");

        var wait = Assert.Single((await AnswerAsync(module, "PERMISSION_WAITS")).GetProperty("waits").EnumerateArray());
        Assert.Equal(
            ("d1", "900", "c9", "npm start", "execute"),
            (wait.GetProperty("session").GetString(), wait.GetProperty("request").GetString(), wait.GetProperty("call").GetString(),
                wait.GetProperty("title").GetString(), wait.GetProperty("kind").GetString()));
        Assert.Equal(["yes", "no"], wait.GetProperty("choices").EnumerateArray().Select(choice => choice.GetProperty("id").GetString()));
        Assert.Equal(["allow_once", "reject_once"], wait.GetProperty("choices").EnumerateArray().Select(choice => choice.GetProperty("kind").GetString()));
        Assert.True(wait.TryGetProperty("since", out _));
    }

    /// <summary>PERMUNTIL1: each hold says when its bound releases it, the driver's bound from its start, so the page keeps no copy.</summary>
    [Fact]
    public async Task Each_hold_says_when_its_bound_releases_it()
    {
        var loop = Loop();
        var module = new DriverModule(Bus, loop);
        HeldPermissions.Of(loop.Events).For("d1").Hold("900", "c9", "npm start", Offered);

        var wait = Assert.Single((await AnswerAsync(module, "PERMISSION_WAITS")).GetProperty("waits").EnumerateArray());

        var since = DateTimeOffset.Parse(wait.GetProperty("since").GetString()!, CultureInfo.InvariantCulture);
        var until = DateTimeOffset.Parse(wait.GetProperty("until").GetString()!, CultureInfo.InvariantCulture);
        Assert.Equal(HeldPermissions.Bound, until - since);
    }

    /// <summary>The person's answer with an offered option ends the hold with it; the same answer again finds nothing held.</summary>
    [Fact]
    public async Task The_persons_answer_with_an_offered_option_ends_the_hold_with_it()
    {
        var loop = Loop();
        var module = new DriverModule(Bus, loop);
        var held = HeldPermissions.Of(loop.Events).For("d1").Hold("900", "c9", "npm start", Offered)!;

        var answer = await AnswerAsync(module, "SESSION_PERMISSION_ANSWER", new { session = "d1", request = "900", option = "yes" });

        Assert.True(answer.GetProperty("answered").GetBoolean());
        Assert.Equal(JsonValueKind.Null, answer.GetProperty("why").ValueKind);
        Assert.Equal("yes", (await held.WaitAsync(Poll.Patience)).Chosen?.Id);
        var again = await AnswerAsync(module, "SESSION_PERMISSION_ANSWER", new { session = "d1", request = "900", option = "yes" });
        Assert.Equal((false, "unheld"), (again.GetProperty("answered").GetBoolean(), again.GetProperty("why").GetString()));
    }

    /// <summary>A refusal names no option, and takes the offered rejection.</summary>
    [Fact]
    public async Task A_refusal_naming_no_option_takes_the_offered_rejection()
    {
        var loop = Loop();
        var held = HeldPermissions.Of(loop.Events).For("d1").Hold("900", "c9", "npm start", Offered)!;

        var answer = await AnswerAsync(new DriverModule(Bus, loop), "SESSION_PERMISSION_ANSWER", new { session = "d1", request = "900" });

        Assert.True(answer.GetProperty("answered").GetBoolean());
        Assert.Equal("no", (await held.WaitAsync(Poll.Patience)).Chosen?.Id);
    }

    /// <summary>
    /// 🔴 An option the agent did not offer, its standing allow, and a request not held are each refused by code with the sentence
    /// beside it, and what was held waits on as it was.
    /// </summary>
    [Theory]
    [InlineData("d1", "900", "maybe", "unoffered")]
    [InlineData("d1", "900", "always", "standing")]
    [InlineData("d1", "901", "yes", "unheld")]
    [InlineData("d2", "900", "yes", "unheld")]
    public async Task What_the_person_may_not_answer_is_refused_by_code_and_the_hold_waits_on(
        string session, string request, string option, string why)
    {
        var loop = Loop();
        var held = HeldPermissions.Of(loop.Events).For("d1").Hold("900", "c9", "npm start", Offered)!;

        var answer = await AnswerAsync(new DriverModule(Bus, loop), "SESSION_PERMISSION_ANSWER", new { session, request, option });

        Assert.False(answer.GetProperty("answered").GetBoolean());
        Assert.Equal(why, answer.GetProperty("why").GetString());
        Assert.EndsWith("Nothing was answered.", answer.GetProperty("message").GetString());
        Assert.False(held.IsCompleted);
        Assert.Single(HeldPermissions.Of(loop.Events).Held("d1"));
    }
}
