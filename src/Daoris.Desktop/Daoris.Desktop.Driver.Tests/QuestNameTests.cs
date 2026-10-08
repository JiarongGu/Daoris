using System.Net;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// SESSUX1j (D126 §9): the service answers each quest's short title, its publisher's or the name it reads from the quest's
/// words, and the driver names a quest by it where a name is shown: the session listing, and a landing's <c>{slug}</c>
/// (LANDNAME1's naming half), so a branch is named for the work rather than cut from a note on the quest's first line.
/// A host from before the field answers none, and the title names the quest as before.
/// </summary>
public sealed class QuestNameTests
{
    private sealed class StandIn(Func<HttpRequestMessage, (HttpStatusCode Status, string Body)?> answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(answer(request) is { } said
                ? new HttpResponseMessage(said.Status) { Content = new StringContent(said.Body) }
                : new HttpResponseMessage(HttpStatusCode.NotFound));
    }

    private const string Refiled = "(Re-filed from ask #39c495, whose quest was taken outside the driver with no session.)";

    [Fact]
    public async Task The_clients_read_of_a_quest_carries_its_short_title_and_names_it_by_it()
    {
        var standIn = new StandIn(request => request.RequestUri!.AbsolutePath == "/api/quests"
            ? (HttpStatusCode.OK, $$"""
                [{"id":"q1","from":"ask #a1","to":"report","title":"{{Refiled}}","body":"b","status":"Open",
                  "short":"TK-2203 continue the prod half"},
                 {"id":"q2","from":"report","to":"backend","title":"Read the field names","body":"b","status":"Open"}]
                """)
            : null);
        using var service = new ServiceClient("http://stand-in", null, new HttpClient(standIn));

        var named = (await service.FindQuestAsync("q1"))!;
        var older = (await service.FindQuestAsync("q2"))!;

        Assert.Equal("TK-2203 continue the prod half", named.Short);
        Assert.Equal("TK-2203 continue the prod half", named.Name);
        Assert.Null(older.Short);
        Assert.Equal("Read the field names", older.Name);
    }

    /// <summary>
    /// LANDNAME1: the install's two landings came out <c>feature/re-filed-from-ask-39c495-whose-quest-was-…</c>. The slug is
    /// the quest's name in git's words, while the landing keeps the quest's whole title for its record and its plugin.
    /// </summary>
    [Fact]
    public async Task A_landing_is_named_for_the_quests_name_and_keeps_its_title()
    {
        var quest = new QuestView("q1", "ask #a1", "portal-ui", Refiled, "b", "Done") { Short = "TK-2203 continue the prod half" };
        Task<QuestView?> Find(string id) => Task.FromResult<QuestView?>(id == "q1" ? quest : null);

        var subject = await LandingRules.SubjectAsync("s1", "q1", Find, opening: null);

        Assert.Equal(Refiled, subject.Title);
        Assert.Equal("tk-2203-continue-the-prod-half", subject.Slug);
        Assert.Equal("feature/tk-2203-continue-the-prod-half-q1",
            LandingRules.Expand("feature/{slug}-{quest}", new LandingNames(subject.Quest!, subject.Slug, "portal-ui", "s1")));
    }

    /// <summary>A start's landing is told the same name, and a quest whose name is its title is named as it always was.</summary>
    [Fact]
    public async Task A_starts_landing_is_named_for_the_quests_name_too()
    {
        var named = new QuestView("q1", "ask #a1", "engine", Refiled, "b", "Open") { Short = "Frame cap" };
        var plain = new QuestView("q2", "game", "engine", "Fix the gap", "b", "Open") { Short = "Fix the gap" };

        var told = await Daoris.Driver.Driver.LandsAsAsync("s1", named, _ => throw new InvalidOperationException("never asked"), default);
        var same = await Daoris.Driver.Driver.LandsAsAsync("s2", plain, _ => throw new InvalidOperationException("never asked"), default);

        Assert.Equal("frame-cap", told.Slug);
        Assert.Equal(new LandingSubject("s2", "q2", "Fix the gap"), same);
        Assert.Equal("fix-the-gap", same.Slug);
    }
}
