using System.Net;
using System.Text;

namespace Daoris.Driver.Tests;

/// <summary>
/// The look ends an outside agent's membership with its line, once its channel's
/// claims are spent, and after a quiet day judged against the member's own record, never a timer; a member still within both bounds
/// stays, and the end is the person's key's, said as <c>expired</c>.
/// </summary>
public sealed class ChannelMembersLookTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-06T09:00:00Z");

    private static string Member(string id, string seen, bool spent) => $$"""
        {"id":"{{id}}","origin":"ask:a1","entry":"ask:a1/outside:{{id}}","agent":"codex","repository":"checker","joined":"2026-11-05T09:00:00Z","seen":"{{seen}}","spent":{{(spent ? "true" : "false")}}}
        """;

    [Fact]
    public void A_member_is_ended_when_its_claims_are_spent_or_it_was_quiet_a_day_and_not_before()
    {
        ChannelMemberView View(bool spent, DateTimeOffset? seen, DateTimeOffset? left = null) =>
            new("o1a2b3c4", "ask:a1", "codex", "checker", left, null, Now.AddDays(-3), seen, spent);

        Assert.True(ChannelMembersLook.Ends(View(spent: true, seen: Now), Now));
        Assert.False(ChannelMembersLook.Ends(View(spent: false, seen: Now.AddHours(-23)), Now));
        Assert.True(ChannelMembersLook.Ends(View(spent: false, seen: Now.AddHours(-24)), Now));
        Assert.True(ChannelMembersLook.Ends(View(spent: false, seen: null), Now));
        Assert.False(ChannelMembersLook.Ends(View(spent: true, seen: Now, left: Now), Now));
    }

    [Fact]
    public async Task The_look_ends_each_spent_or_quiet_member_as_expired_by_the_keys_leave_and_keeps_the_rest()
    {
        var requests = new List<string>();
        var bodies = new List<string>();
        var standIn = new StandIn(request =>
        {
            requests.Add($"{request.Method} {request.RequestUri!.AbsolutePath}");
            if (request.Method == HttpMethod.Get)
            {
                return (HttpStatusCode.OK, "{\"members\":[" + string.Join(',',
                    Member("o0000001", "2026-11-06T08:59:00Z", spent: true),
                    Member("o0000002", "2026-11-05T08:00:00Z", spent: false),
                    Member("o0000003", "2026-11-06T08:30:00Z", spent: false)) + "]}");
            }

            var id = request.RequestUri!.AbsolutePath.Split('/')[^2];
            return (HttpStatusCode.OK, $$"""{"id":"{{id}}","origin":"ask:a1","entry":"e","agent":"codex","joined":"2026-11-05T09:00:00Z","left":"2026-11-06T09:00:00Z","why":"expired"}""");
        }, bodies);
        using var service = new ServiceClient("http://stand-in", "key", new HttpClient(standIn));

        var ended = await ChannelMembersLook.EndAsync(service, Now, CancellationToken.None);

        Assert.Equal(2, ended);
        Assert.Equal(["GET /api/channel/members", "POST /api/channel/members/o0000001/leave", "POST /api/channel/members/o0000002/leave"], requests);
        Assert.All(bodies.Skip(1), body => Assert.Contains("\"why\":\"expired\"", body, StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_host_that_does_not_answer_ends_none_and_raises_nothing()
    {
        using var service = new ServiceClient("http://stand-in", "key", new HttpClient(new StandIn(_ => (HttpStatusCode.NotFound, "{}"), [])));

        Assert.Equal(0, await ChannelMembersLook.EndAsync(service, Now, CancellationToken.None));
    }

    private sealed class StandIn(Func<HttpRequestMessage, (HttpStatusCode Status, string Body)> answer, List<string> bodies) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            bodies.Add(request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct));
            var (status, body) = answer(request);
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }
}
