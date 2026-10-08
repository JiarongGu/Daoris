using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// A set-up step as the driver drives it (REVIEWENV1c, D154 points 4, 5 and 9; the review environment design §2.1, §2.2, §2.6):
/// the environment its rule declares, why the planner sits one it cannot honestly start, the procedure its tree must hold, and
/// the commit Daoris reads from its tree and posts, never the session's word.
/// </summary>
public static class ReviewSetUps
{
    /// <summary>
    /// The environment a set-up step shows its chain's work in, as the rule standing for its repository here declares it (design
    /// §2.2: handed from the rule as it stands at the start); null for a quest that is no set-up step, and where no rule names it.
    /// </summary>
    public static ReviewEnvironment? Environment(DriverConfig config, QuestView quest, string? workspace) =>
        quest.SetUpIn is { } named && ReviewRules.Resolve(config, quest.To, workspace) is { Rule.IsNone: false } rule
            ? rule.Rule.Environments.FirstOrDefault(environment => environment.Name == named)
            : null;

    /// <summary>
    /// Why the planner sits a set-up step it cannot honestly start (design §2.1), or null where it may start: no rule here names its
    /// environment; or, for a local one, no address to show it at, or no window here to show it in (a headless loop, where Daoris's
    /// browser is not handed). Whether its tree holds the procedure is read at the spawn, once the tree exists
    /// (<see cref="ProcedureMissing"/>).
    /// </summary>
    /// <param name="window">Whether this loop carries Daoris's browser: the desktop's does, a headless one does not.</param>
    public static string? Sits(DriverConfig config, QuestView quest, string? workspace, bool window)
    {
        if (quest.SetUpIn is not { } named) return null;
        if (Environment(config, quest, workspace) is not { } environment)
        {
            return $"set-up step `#{quest.Id}` shows its chain's work in `{named}`, and no review rule here names `{named}` for "
                + $"`{quest.To}`: `daoris driver review {quest.To} {named} --kind local|deployed --procedure <path>` declares it.";
        }

        if (environment.Kind != "local") return null;
        if (environment.Address is null)
        {
            return $"set-up step `#{quest.Id}` shows its chain's work in `{named}`, which declares no address to show it at: "
                + $"`daoris driver review {quest.To} {named} --address <url>` declares where the app normally runs.";
        }

        return window
            ? null
            : $"set-up step `#{quest.Id}` shows its chain's work in Daoris's browser at `{environment.Address}`, and this loop has no "
              + "window to show it in: the desktop's loop starts it.";
    }

    /// <summary>
    /// Why a set-up step's start is held at its spawn (design §1.1, §2.1), or null where it may start: its tree holds no file at
    /// the procedure's path, which Daoris checks exists and never reads for meaning.
    /// </summary>
    public static string? ProcedureMissing(ReviewEnvironment environment, QuestView quest, string tree) =>
        ReviewRules.Holds(tree, environment.Procedure)
            ? null
            : $"set-up step `#{quest.Id}` follows `{environment.Procedure}` to reach `{environment.Name}`, and this tree holds no "
              + $"`{environment.Procedure}`: a procedure is `{quest.To}`'s own document, and writing it is its own work.";

    /// <summary>
    /// The set-ups a set-up step's session said, posted with the commit Daoris read from its tree (design §2.6): at the end of its
    /// run, and of every later run words went on with. The tree's <c>HEAD</c> is read here, never reported; each said set-up is
    /// kept once by the service, so posting again is no move. The conversation is told what was posted, and the machine log gets
    /// <c>review.shown</c>. Never throws: the record has concluded, and the next end posts what this one could not.
    /// </summary>
    /// <param name="quest">The quest as the conclusion read it; nothing is posted for one that is no set-up step.</param>
    /// <param name="started">When the run opened, for the log's seconds until shown.</param>
    /// <returns>What was said, or null where nothing was posted.</returns>
    public static async Task<string?> PostAsync(
        ServiceClient service, DriverConfig config, SessionEvents events, QuestView? quest, string session, string tree, string? workspace,
        DateTimeOffset started, CancellationToken ct)
    {
        if (quest?.SetUpIn is not { } named) return null;
        string said;
        try
        {
            if (Environment(config, quest, workspace) is not { } environment)
            {
                said = $"its set-up was not posted: no review rule here names `{named}` for `{quest.To}` any more, so its kind is not "
                    + "known. Declare it, and its next end posts it.";
            }
            else if (await WorkingTree.HeadAsync(tree, ct).ConfigureAwait(false) is not { } commit || !EvidenceCodes.IsObjectId(commit))
            {
                said = "its set-up was not posted: its tree's commit could not be read.";
            }
            else
            {
                var (ok, message) = await service.PostSetUpAsync(quest.Id, session, commit, environment.Kind, ct).ConfigureAwait(false);
                said = ok
                    ? $"Daoris read `{commit[..8]}` from its tree and posted what it showed in `{named}`: {message}"
                    : $"its set-up was not posted: {message}";
                if (ok)
                {
                    var after = await service.FindQuestAsync(quest.Id, ct).ConfigureAwait(false);
                    var newest = after?.SetUps.LastOrDefault(setUp => string.Equals(setUp.Session, session, StringComparison.OrdinalIgnoreCase));
                    service.LandingSaid(ReviewLines.Shown(
                        quest.Id, session, environment.Kind, served: newest?.Served is not null, run: newest?.Run is not null,
                        seconds: newest?.At is { } at ? (long)Math.Max(0, (at - started).TotalSeconds) : null));
                }
            }
        }
        catch (Exception error) when (error is HttpRequestException or DriverException or JsonException or IOException
                                          || (error is OperationCanceledException && !ct.IsCancellationRequested))
        {
            said = $"its set-up was not posted: {error.Message.TrimEnd().TrimEnd('.')}. Its next end posts it.";
        }

        events.Keep(session, new SessionEvent { Kind = SessionEventKind.Note, Text = $"— {said}" }, say: null);
        return said;
    }
}

/// <summary>
/// The machine log's review lines (REVIEWENV1c, design §3.5, D94): <c>review.shown</c>, <c>review.verdict</c> and
/// <c>review.held</c>, codes, ids and counts only, never a sentence, an address or anyone's words. They ride the landing line's
/// channel (<see cref="ServiceClient.LandingSaid"/>), which writes the event each names.
/// </summary>
public static class ReviewLines
{
    /// <summary>A set-up posted: its kind, whether a folder was served or a run ran, and the seconds from the run's open.</summary>
    public static LandingLine Shown(string quest, string session, string kind, bool served, bool run, long? seconds) =>
        new("review.shown", [("quest", quest), ("session", session), ("kind", kind), ("served", served), ("run", run), ("seconds", seconds)]);

    /// <summary>The person's verdict, and the door it was given at.</summary>
    public static LandingLine Verdict(string quest, string said, string door) =>
        new("review.verdict", [("quest", quest), ("said", said), ("door", door)]);

    /// <summary>A landing the gate held: whose, where, the gate's state, the level that decided, and the door it was tried at.</summary>
    public static LandingLine Held(string session, string? repository, string? workspace, ReviewGateState gate, string door) =>
        new("review.held",
        [
            ("session", session), ("repository", repository), ("workspace", workspace), ("state", gate.State),
            ("level", gate.Decision.Level), ("door", door),
        ]);
}

/// <summary>The doors a landing or a verdict is given at, as the review's log lines name them.</summary>
public static class ReviewDoors
{
    public const string Screen = "screen";
    public const string Terminal = "terminal";
    public const string Look = "look";
}
