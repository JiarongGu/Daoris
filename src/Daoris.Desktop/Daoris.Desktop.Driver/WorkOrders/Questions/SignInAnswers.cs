using Daoris.Ports.Browser;

namespace Daoris.Driver;

/// <summary>
/// The person's sign-in as the answer to a request waiting on it: each question on file that waits on a sign-in at a
/// site, a park's or a step-awaiting session's, that Daoris's browser shows done (<see cref="SignInSightings"/>) is said to its session in
/// Daoris's word under one key through the say door, so it is the asker's next turn with no one relaying it. Nothing where no question waits
/// on a sign-in or the browser is not running.
/// </summary>
public static class SignInAnswers
{
    /// <summary>One sentence per question the browser shows signed, saying whether its word is kept.</summary>
    /// <param name="say">The say door, as Daoris speaks: the session, the words and their key.</param>
    public static async Task<IReadOnlyList<string>> SayAsync(
        string home, IInAppBrowser browser, Func<string, string, string, CancellationToken, Task<SayAnswer>> say, CancellationToken ct)
    {
        var waiting = new QuestionFiles(home).SignIns();
        if (waiting.Count == 0) return [];
        var sightings = SignInSightings.Of(browser);
        if (await browser.PagesAsync(ct).ConfigureAwait(false) is not { } pages) return [];
        var events = new List<string>();
        foreach (var question in sightings.Signed(waiting, pages))
        {
            var said = await say(question.Session, SignInSightings.Word(question), SignInSightings.Key(question), ct).ConfigureAwait(false);
            events.Add(said.Kept
                ? $"session `{question.Session}`'s sign-in at {question.Site} was seen in Daoris's browser: it goes on."
                : $"session `{question.Session}`'s sign-in at {question.Site} was seen in Daoris's browser, and its word was not kept: {said.Message}");
        }

        return events;
    }
}
