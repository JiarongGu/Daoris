using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The person's *Send now*: the words steered into the running turn by the adapter's <c>_session/steering</c>, which delivers them at
/// priority <c>now</c>, cutting the step in flight. A steer passes the run's door and its words' mark as a prompt does
/// (<see cref="AcpPrompting"/>), and reaches the session through <see cref="ITurnPromptWiring"/>. Where the words landed is said:
/// <c>injected</c> in the running turn, <c>startedNewTurn</c> as the next (<see cref="AcpStartedTurn"/>).
/// </summary>
internal sealed class AcpSteering(ITurnPromptWiring wiring, AcpPrompting prompting, AcpStartedTurn started)
{
    /// <summary>
    /// The words steered into the running turn. The request asks the adapter never to start a turn of its own
    /// (<c>idleBehavior: promptRequired</c>), since a turn it started would answer nobody here. Answered <c>injected</c>, the words are
    /// taken there, told through the listener on the reader's thread before anything after the answer is read, and the calls still open
    /// are marked as cut.
    /// </summary>
    /// <returns>The adapter's outcome: <c>injected</c>, or <c>promptRequired</c> where no turn is running after all.</returns>
    /// <exception cref="AcpRefusal">The agent refused the steer, in its words.</exception>
    /// <exception cref="DriverException">The agent never said it steers, or the session is not open: nothing is sent.</exception>
    public async Task<string> SteerAsync(IAgentWord words, CancellationToken ct)
    {
        if (!wiring.Steers)
        {
            throw new DriverException(
                "this agent did not say it steers words into a running turn — they reach it at its next step or its turn's end.");
        }

        if (wiring.SessionId is null) throw new DriverException("this ACP session is not open — nothing can be steered into it.");

        // A kept word is marked attempted on its record before the wire, in the order asked for, and written only once the record named it
        // marked: else its steer says it never reached, and it waits on its record. Through the run's door, so a run that ends meanwhile
        // hands it back.
        var passage = wiring.Open.Enter([words]);
        var marked = await prompting.Attempted(words).ConfigureAwait(false);
        wiring.Open.Marked(passage, marked);

        // The adapter's own parameters (claude-agent-acp 0.84.0, `parseSteerRequest`, acp-agent.js:215-238): the session, the
        // prompt's content blocks as `session/prompt` carries them, and the steering `_meta`. The priority is the adapter's.
        var answer = await wiring.RequestAsync(
            "_session/steering",
            new { sessionId = wiring.SessionId, prompt = AcpPrompting.Blocks([words.Prompt], words.Files), _meta = new { steering = new { idleBehavior = "promptRequired" } } },
            ct,
            prompting.Wrote(null, words),
            answered: frame =>
            {
                switch (Outcome(frame))
                {
                    case "injected":
                        wiring.Calls.CutOpen();
                        prompting.Asked(words);
                        Landed(Noted.Of(ChatNoteCodes.WordsSentNowInTurn, "Sent now: it went into the turn it was on."));
                        break;
                    case "startedNewTurn":
                        // Codex reads no idleBehavior: the turn just ended, and the words began the next, which no prompt owns.
                        prompting.Asked(words);
                        Landed(Noted.Of(
                            ChatNoteCodes.WordsSentNowNextTurn, "Sent now: its turn ended before it could take this, so this began the next."));
                        break;
                }
            },
            admit: () => wiring.Open.Admit(passage)).ConfigureAwait(false);
        return Outcome(answer) ?? "unknown";
    }

    /// <summary>
    /// A driven session's steer, answered: where the agent refused it, answered anything but <c>injected</c>, or never had it, it costs
    /// those words, said by its code with their id so the page settles them, never the run.
    /// </summary>
    /// <param name="who">What the English calls the one that refused: a conversation's runner says "conversation".</param>
    /// <returns>The adapter's outcome, or <c>refused</c> or <c>unreached</c>: never a stop reason, and never thrown.</returns>
    /// <param name="handBack">
    /// Where the steer is answered <c>promptRequired</c> (the turn ended between the press and the steer) the adapter leaves the words to
    /// the client: this takes them back to be prompted next, and says whether it did. Said once, taken once where they are finally taken,
    /// so nothing is noted when it did.
    /// </param>
    public async Task<string> SteeredAsync(
        IAgentWord words, CancellationToken ct, string who = "session", Func<IAgentWord, bool>? handBack = null)
    {
        try
        {
            var outcome = await SteerAsync(words, ct).ConfigureAwait(false);
            if (outcome == "promptRequired" && handBack?.Invoke(words) == true) return outcome;
            if (outcome == "startedNewTurn")
            {
                await started.EndedAsync(ct).ConfigureAwait(false);
                return outcome;
            }

            if (outcome != "injected") NoteWords(WordsNotes.NeverReached(NotSteered(outcome)), words);
            return outcome;
        }
        catch (AcpTurnedAway)
        {
            // The run ended before the steer is written, and its words go back with the run's: nothing more is said here.
            return "unreached";
        }
        catch (AcpRefusal refused)
        {
            NoteWords(WordsNotes.Refused(refused.Words, who), words);
            return "refused";
        }
        catch (Exception error) when (error is DriverException or IOException or ObjectDisposedException or InvalidOperationException)
        {
            NoteWords(WordsNotes.NeverReached(error.Message), words);
            return "unreached";
        }
    }

    /// <summary>The program's sentence for a steer the agent answered with anything but <c>injected</c>.</summary>
    public static string NotSteered(string outcome) =>
        $"the agent answered the steer `{outcome}`, so no running turn took them.";

    /// <summary>The driver's sentence about the person's words, by its code and with their id, so the page settles the words.</summary>
    private void NoteWords(Noted noted, IAgentWord words)
    {
        wiring.Console.End();
        wiring.Say(noted.Note);
        wiring.Emit(new SessionEvent
        {
            Kind = SessionEventKind.Note, Text = noted.Note, Parts = noted.Parts.Count > 0 ? noted.Parts : null,
            Words = words.Id is { } id ? [id] : null,
        });
    }

    /// <summary>Where the words sent now landed, said once they are taken there, on the reader's thread before what follows.</summary>
    private void Landed(Noted noted)
    {
        wiring.Say(noted.Note);
        wiring.Emit(new SessionEvent { Kind = SessionEventKind.Note, Text = noted.Note, Parts = noted.Parts });
    }

    /// <summary>A steer's <c>outcome</c>, from its answer frame or its result; null where it said none.</summary>
    private static string? Outcome(JsonElement answer)
    {
        var result = answer.ValueKind == JsonValueKind.Object && answer.TryGetProperty("result", out var inner) ? inner : answer;
        return result.ValueKind == JsonValueKind.Object && result.TryGetProperty("outcome", out var outcome)
            && outcome.ValueKind == JsonValueKind.String
            ? outcome.GetString()
            : null;
    }
}
