namespace Daoris.Driver;

/// <summary>
/// The parks waiting on the person's sign-in at a site, read against Daoris's browser (<see cref="SignInSightings"/>): each it shows
/// done is told so in Daoris's word, under one key, for the next look to take the park up. Nothing where no shell carries a browser,
/// no park waits on a sign-in, or the browser is not running.
/// </summary>
[LookPass("browser-sign-ins", LookPhase.After, "sign-outs")]
internal sealed class BrowserSignInsLook(ILookStagesWiring wiring) : ILookPass
{
    public async Task RunAsync(LookRound round, CancellationToken ct)
    {
        if (wiring.Context.Browser is not { } browser) return;
        var waiting = new QuestionFiles(wiring.Home).SignIns();
        var sightings = SignInSightings.Of(browser);
        var pages = waiting.Count == 0 ? [] : await browser.PagesAsync(ct).ConfigureAwait(false);
        if (pages is null) return;
        foreach (var question in sightings.Signed(waiting, pages))
        {
            var (said, _) = await WordsHead.FollowAsync(
                    question.Session,
                    at => wiring.Service.SayAsync(at, SignInSightings.Word(question), ct: ct, from: DaorisWords.From, key: SignInSightings.Key(question)))
                .ConfigureAwait(false);
            round.Events.Add(said.Kept
                ? $"session `{question.Session}`'s sign-in at {question.Site} was seen in Daoris's browser: it goes on."
                : $"session `{question.Session}`'s sign-in at {question.Site} was seen in Daoris's browser, and its word was not kept: {said.Message}");
        }
    }
}
