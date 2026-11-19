namespace Daoris.Driver;

/// <summary>
/// The requests waiting on the person's sign-in at a site, read against Daoris's browser (<see cref="SignInAnswers"/>): each it shows
/// done is told so in Daoris's word, under one key, at its line's head, for the next look to take the session up. Nothing where no shell
/// carries a browser, no request waits on a sign-in, or the browser is not running.
/// </summary>
[LookPass("browser-sign-ins", LookPhase.After, "sign-outs")]
internal sealed class BrowserSignInsLook(ILookStagesWiring wiring) : ILookPass
{
    public async Task RunAsync(LookRound round, CancellationToken ct)
    {
        if (wiring.Context.Browser is not { } browser) return;
        round.Events.AddRange(await SignInAnswers.SayAsync(
                wiring.Home, browser,
                async (session, text, key, token) => (await WordsHead.FollowAsync(
                        session, at => wiring.Service.SayAsync(at, text, ct: token, from: DaorisWords.From, key: key))
                    .ConfigureAwait(false)).Said,
                ct)
            .ConfigureAwait(false));
    }
}
