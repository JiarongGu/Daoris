using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// What a session's ending says about the account it ran as: a limit its door refused a turn for, a credential or a model its provider
/// refused, a sign-in its agent refused. Each is read from the door's failure by the adapter's table and never from the transcript or
/// from words its agent could write, and each applied to the roster so the next start's walk passes the account.
/// </summary>
internal sealed class AccountEndings(string home, DriverConfig config, ServiceClient service, LoopContext context)
{
    /// <summary>
    /// 🔴 A turn the door refused for an account's limit: the account the session ran as cools until the reset the agent named, the
    /// record says <c>limit</c>, which is never a strike, and its note says until when, naming no account.
    /// </summary>
    /// <remarks>
    /// <para>Only a <c>failed</c> conclusion: a refused turn after the work reached its close or its wait ended as that, the person's
    /// stop is theirs, and a timeout carries no door failure. A failure no table recognises is a failure as today, and the strikes
    /// bound it.</para>
    /// <para>🔴 <b>The note carries the facts, never the agent's sentence</b>: the sentence names its reset in the zone of the machine
    /// it was said on, and in English, and the note travels to every machine. The cooling line carries the reset as a moment each
    /// reader words in its own zone and language; the sentence stays in the transcript and the conversation, which stay here.</para>
    /// </remarks>
    /// <param name="turnFailed">What the protocol door said refusing the turn, or null.</param>
    /// <param name="used">The context the door reported, for the log; null where it reported none.</param>
    /// <param name="byCode">A limit the reader read by its code with no sentence: cooled for an hour.</param>
    public (SessionConclusion Conclusion, bool Limit) Limited(
        SessionConclusion conclusion, ISessionAdapter adapter, HarnessSelection selection, string? turnFailed, string sessionId,
        AcpUsage? used, LimitSeen? byCode = null)
    {
        if (conclusion.State != "failed" || turnFailed is not { Length: > 0 }) return (conclusion, false);

        (CoolingEntry Entry, LimitSeen Seen)? limited;
        // The cool-offs that ended lately, read before this one replaces its account's: a reset that did not lift it.
        var now = context.Harnesses.Clock();
        IReadOnlyList<CoolingEntry> ended;
        try { ended = AccountCooling.Offered(home, now); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { ended = []; }

        try
        {
            limited = context.Harnesses.Limited(adapter.Name, selection.Profile, turnFailed, sessionId, config.CoolOff, byCode);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // The limit could not be read for a disk that would not answer: the record still says the agent's words. A cool-off the
            // file would not take is no such case: the roster holds it in memory, and the end is limited.
            return (conclusion, false);
        }

        if (limited is not { } read) return (conclusion, false);

        var (entry, seen) = read;
        service.AccountSaid(AccountLine.Limited(sessionId, adapter.Name, selection.Profile, seen, context.Events.TurnsEnded(sessionId) + 1, used?.Used));
        // Kept in memory only, said once in the machine log for this entry.
        if (AccountCooling.Unwritten(home, entry.Agent, entry.Account)) service.AccountSaid(AccountLine.CoolingUnkept(sessionId, adapter.Name, selection.Profile, seen));
        var noted = conclusion.Unsaid(turnFailed).Then(" ", CoolingWords.NoteOf(entry, context.Harnesses.Zone));

        // 🔴 An attempt refused with the same kind of limit within the margin after its account's cool-off ended met a reset that did
        // not lift it (an admin's spend limit can outlast the window the sentence names). Said once, on this end, and in the machine
        // log; it still waits for the next cool-off and never parks.
        if (LimitResume.NotLifted(ended, entry, now) is { } notLifted)
        {
            noted = noted.Then(" ", notLifted);
            service.AccountSaid(AccountLine.NotLifted(sessionId, adapter.Name, selection.Profile, seen));
        }

        return (noted, true);
    }

    /// <summary>
    /// What a session's door says about the windows of the account it runs as, kept by the roster as the door carries it, for the next
    /// start's walk. A reading not kept costs a start's ranking, never the session.
    /// </summary>
    /// <remarks>
    /// Asked once by every start that holds a session (a driven one, an intake, a carry-on, an opinion's or a helper's pass), where
    /// its account is chosen: so the session's turns are bound to that account in the usage history here.
    /// </remarks>
    public Action<JsonElement> Said(ISessionAdapter adapter, HarnessSelection selection, string sessionId)
    {
        UsageTurns.Bound(home, context.Events, sessionId, TurnBinding.Of(context.Harnesses.Adapters, adapter, selection.Profile));
        return info =>
        {
            try
            {
                context.Harnesses.Said(adapter.Name, selection.Profile, info, sessionId);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // Nothing kept is nothing said: the walk reads the account as unknown, as it did before the door spoke.
            }
        };
    }

    /// <summary>
    /// 🔴 A credential its provider refused is read from what the harness itself said of its ending, and the account is held so no
    /// further session sits through the same minutes of retries.
    /// </summary>
    /// <remarks>
    /// <para>Never from words its agent could write: an agent whose output ends quoting the phrase, reading a log or testing an error
    /// path, would hold an account its provider never refused. A door that carries only text gives nothing but its transcript, so there
    /// the exit, which only the harness sets, must say it failed (<see cref="HarnessEnding.Transcript"/>).</para>
    /// <para>And it reads signed out, kept as a reading is, so the hold outlives this process and the page says the account's state;
    /// its line is one no strike counts (<see cref="AccountNoteCodes.AccountsOwn"/>).</para>
    /// </remarks>
    /// <param name="ended">What the harness said of its ending, by its door.</param>
    public SessionConclusion Refused(SessionConclusion conclusion, ISessionAdapter adapter, HarnessSelection selection, HarnessEnding ended)
    {
        if (conclusion.State != "failed" || adapter.Toolchain is not { Refused: { Length: > 0 } refusedWords }
            || !Observation.Refused(ended, refusedWords))
        {
            return conclusion;
        }

        return conclusion.Then(" ", Refuse(adapter, selection, context.Harnesses.Adapters.OwnerOf(adapter.Name)));
    }

    /// <summary>
    /// The account a refused credential holds and reads signed out, named by <paramref name="owner"/>; its line for the record. What
    /// <see cref="Refused"/> applies, and a resume's open refused for access applies as the reader read it.
    /// </summary>
    public Noted Refuse(ISessionAdapter adapter, HarnessSelection selection, string owner)
    {
        // The hold a later start says names the account by the person's name, read now; the note, which travels and whose cleaner knows
        // only the record's own id, keeps the id.
        var named = selection.Profile is { } profile ? AccountNames.Said(AccountNames.Of(context.Harnesses.Home, owner), profile) : null;
        var hold = RefusedAccounts.RefusedHold(owner, selection.Profile, named);
        context.Harnesses.Refuse(adapter.Name, selection.Profile, hold.Note, hold);
        context.Harnesses.SignedOut(adapter.Name, selection.Profile);
        return RefusedAccounts.RefusedNote(owner, selection.Profile);
    }

    /// <summary>
    /// 🔴 The pipe door's refusal of the model its actor asked, read from measured words on a silent turn
    /// (<see cref="HarnessEnding.ModelRefused"/>): the one refusal after a prompt was handed that is no failure of the quest's. The
    /// account's <c>refused</c> reading for that model is kept, so the next start's walk passes the account without spawning, and the
    /// part ends with the account's line, which no strike counts (<see cref="AccountNoteCodes.AccountsOwn"/>), naming the agent and the model
    /// but no account, since the note travels.
    /// </summary>
    /// <param name="model">The model its actor's choice named; null keeps the conclusion, since a model the person's own command or
    /// environment named is no choice a reading passes.</param>
    public SessionConclusion ModelRefused(
        SessionConclusion conclusion, string agent, HarnessSelection selection, string? model, HarnessEnding ended, string sessionId)
    {
        if (conclusion.State != "failed" || !ended.ModelRefused || model is not { Length: > 0 }) return conclusion;

        if (AgentOffers.Refused(home, agent, selection.Version, selection.Profile, model, DateTimeOffset.UtcNow) is { } unkept)
        {
            context.Log?.Write("warn", "actor.offers-unkept", [("session", sessionId), ("why", unkept)]);
        }

        context.Log?.Write("info", "actor.refused", [("session", sessionId), ("agent", agent), ("model", model), ("code", AgentOffers.RefusedCode)]);
        return conclusion.Then(" ", Noted.Of(
            AccountNoteCodes.AccountRefusedModel,
            $"`{agent}` refused `{model}` on the account it ran on, so that account is passed for that model until the agent offers it again. "
            + "It is not counted as a failed session.",
            ("agent", agent), ("model", model)));
    }

    /// <summary>
    /// 🔴 A start its agent refused for its sign-in, read from the door's failure by the adapter's table (<see cref="SignInRefusals"/>)
    /// and never from the transcript: the account it ran as reads signed out, kept under the home with its time as a reading is, so the
    /// next start walks past it, and a start held on it names it and its sign-in. The note says why it ended, by a line no strike counts
    /// (<see cref="AccountNoteCodes.AccountsOwn"/>): the fault is the account's, never the quest's. The tool's own sign-in is never asked by a
    /// walk, so a reading alone would start it into the same refusal at every look: it is held as a refused credential is, until a
    /// person looks again.
    /// </summary>
    /// <param name="turnFailed">What the protocol door said refusing the call, or null.</param>
    /// <param name="ended">The run's ending: a refusal its door's own code reads as access is the account's sign-in, whatever its words.</param>
    public SessionConclusion SignedOut(
        SessionConclusion conclusion, ISessionAdapter adapter, HarnessSelection selection, string? turnFailed, HarnessEnding? ended = null) =>
        conclusion.State == "failed"
        && RefusedAccounts.SignInRefused(context.Harnesses, adapter, selection.Profile, turnFailed, AccessByCode(adapter, ended)) is { } line
            ? conclusion.Then(" ", line)
            : conclusion;

    /// <summary>
    /// Whether the refusal a run ended on reads access by a code or kind its door declares: the one reader's, its words read apart,
    /// since a credential its provider refused by its words is <see cref="Refused"/>'s.
    /// </summary>
    private bool AccessByCode(ISessionAdapter adapter, HarnessEnding? ended) =>
        ended?.Refusal is { } refused
        && AgentErrors.Read(context.Adapters, adapter, DoorFailure.OfPrompt(refused), context.Harnesses.Clock(), context.Harnesses.Zone)
            is { Kind: AgentErrorKind.Access } read
        && !read.By.Contains(":words:", StringComparison.Ordinal);
}
