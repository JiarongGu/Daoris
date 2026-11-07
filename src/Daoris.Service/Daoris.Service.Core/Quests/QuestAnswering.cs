namespace Daoris.Knowledge;

/// <summary>A done answers each requirement, and a departure holds what follows for the person's yes.</summary>
internal sealed class QuestAnswering(AskStore? asks, QuestReviews reviews)
{
    /// <summary>
    /// The answers a done gives, judged against the quest's requirements: one for each, by its
    /// number, each <c>met</c> with how its check is met, or <c>departed</c> with the reason and the person's words it
    /// turns on, which must be theirs. The answers as kept, trimmed at their ends, in the requirements' order — or why
    /// not, naming each requirement or answer at fault.
    /// </summary>
    /// <remarks>
    /// A departure's words are matched as a requirement's are (<see cref="QuestRequirement.QuotedIn"/>), against the person's
    /// words on the ask where this host holds it, and always against the quest's own requirements, which were checked where the
    /// ask is and travel with the quest. A quest with none closes as it always did, and takes no answers.
    /// </remarks>
    public async Task<(IReadOnlyList<QuestAnswer> Answers, QuestRespondRefusal? Refusal, string? Message)> JudgeAnswersAsync(
        Quest quest, IReadOnlyList<QuestAnswer> given, CancellationToken ct)
    {
        (IReadOnlyList<QuestAnswer>, QuestRespondRefusal?, string?) Bad(string why) =>
            ([], QuestRespondRefusal.BadAnswer, why + " Nothing was closed.");

        var required = quest.Requirements;
        if (required.Count == 0)
        {
            return given.Count == 0
                ? ([], null, null)
                : Bad($"Quest `#{quest.Id}` carries no requirements, so a `done` answers none: close it with a note.");
        }

        var kept = new Dictionary<int, QuestAnswer>();
        foreach (var (answer, index) in given.Select((answer, index) => (answer, index + 1)))
        {
            var which = $"Answer {index}";
            if (answer.Requirement < 1 || answer.Requirement > required.Count)
            {
                return Bad(answer.Requirement < 1
                    ? $"{which} names no requirement: answer each by its number, from 1, as the quest lists them."
                    : $"{which} names requirement {answer.Requirement}, and quest `#{quest.Id}` carries {required.Count}.");
            }

            which = $"{which} (requirement {answer.Requirement})";
            if (kept.ContainsKey(answer.Requirement))
            {
                return Bad($"Requirement {answer.Requirement} is answered twice: answer each once.");
            }

            var met = QuestWords.Blank(answer.Met);
            var departed = QuestWords.Blank(answer.Departed);
            var quote = QuestWords.Blank(answer.Quote);
            if (met is not null && departed is not null)
            {
                return Bad($"{which} says `met` and `departed` at once: say which.");
            }

            if (met is null && departed is null)
            {
                // A departure's reason left blank is a departure without its reason, not an answer that says nothing.
                return Bad(answer.Departed is not null
                    ? $"{which} departs without its reason: say why the work departed from it."
                    : $"{which} says neither `met` nor `departed`: say how its check was met, or why the work departed from it.");
            }

            if (met is not null && answer.Quote is not null)
            {
                return Bad($"{which} is met, and a met answer quotes nothing: its requirement already quotes the person.");
            }

            if (departed is not null && quote is null)
            {
                return Bad($"{which} departs without the person's own words it turns on (`quote`), copied exactly from what "
                    + "they said: a reading of their words is the reason, never their words.");
            }

            var tooLong = new[] { ("how", met), ("reason", departed), ("quote", quote) }
                .FirstOrDefault(half => half.Item2 is { Length: > QuestExchange.MaxRequirementLength });
            if (tooLong.Item2 is not null)
            {
                return Bad($"{which}'s {tooLong.Item1} is longer than {QuestExchange.MaxRequirementLength} characters: say it in a few lines.");
            }

            kept[answer.Requirement] = new QuestAnswer(answer.Requirement, met, departed, quote);
        }

        var unanswered = Enumerable.Range(1, required.Count).Where(number => !kept.ContainsKey(number)).ToList();
        if (unanswered.Count > 0)
        {
            return ([], QuestRespondRefusal.Unanswered,
                $"Quest `#{quest.Id}` carries {QuestWords.Count(required.Count, "requirement")}, each the person's own words, and this "
                + $"`done` leaves {(unanswered.Count == 1 ? "one" : unanswered.Count.ToString(System.Globalization.CultureInfo.InvariantCulture))} unanswered:\n\n"
                + string.Join("\n", unanswered.Select(number =>
                    $"- requirement {number}: \"{Clip(required[number - 1].Quote)}\" — check: {Clip(required[number - 1].Check)}"))
                + "\n\nAnswer each by its number: `met`, with how its check was met, or `departed`, with the reason and the "
                + "person's own words it turns on (`quote`), copied exactly. A departure is shown to the person, and what "
                + $"follows this quest waits for their yes. Nothing was closed; the quest stays {quest.Status}.");
        }

        var ordered = kept.OrderBy(pair => pair.Key).Select(pair => pair.Value).ToList();
        if (await UnquotedAsync(quest, ordered, ct).ConfigureAwait(false) is { } unquoted)
        {
            return ([], QuestRespondRefusal.NotQuoted, unquoted);
        }

        return (ordered, null, null);
    }

    /// <summary>
    /// Why a departure's words are not the person's, naming each — or null when every departure quotes them:
    /// their words on the ask where this host holds it, and the quest's requirements wherever it is.
    /// </summary>
    private async Task<string?> UnquotedAsync(Quest quest, IReadOnlyList<QuestAnswer> answers, CancellationToken ct)
    {
        var departures = answers.Where(answer => answer.IsDeparture).ToList();
        if (departures.Count == 0) return null;

        var askId = AskDesk.AskOf(quest.From);
        var held = askId is not null && asks is not null ? await asks.FindAsync(askId, ct).ConfigureAwait(false) : null;
        var words = (held?.Words.Select(word => word.Text) ?? []).Concat(quest.Requirements.Select(requirement => requirement.Quote)).ToList();

        var unsaid = departures.Where(answer => !words.Any(text => QuestRequirement.QuotedIn(answer.Quote!, text))).ToList();
        if (unsaid.Count == 0) return null;

        var where = held is not null
            ? $"on ask `#{held.Id}`"
            : $"in quest `#{quest.Id}`'s requirements — this host does not hold ask `#{askId}`, so here a departure may "
              + "quote only the requirements' own words";
        return (unsaid.Count == 1 ? "A departure quotes" : $"{unsaid.Count} departures quote")
            + $" words the person never said {where}:\n\n"
            + string.Join("\n", unsaid.Select(answer => $"- requirement {answer.Requirement}: \"{Clip(answer.Quote!)}\""))
            + "\n\nA departure quotes the person's own words it turns on, verbatim (whitespace and case aside): from the ask, "
            + "what they said on it since, or the requirement itself. A reading of their words attributed to them is what "
            + "this refuses: say it in the reason. Nothing was closed.";
    }

    /// <summary>
    /// What a done said of its requirements, for whoever closed it: each met, or which departed and what that
    /// holds — the next step, a quest waiting on it, or the ask — until the person accepts it. Nothing for a quest with none.
    /// </summary>
    public async Task<string> AnsweredAsync(Quest closed, CancellationToken ct) =>
        await RequirementsAnsweredAsync(closed, ct).ConfigureAwait(false) + await reviews.ReviewWaitsAsync(closed, ct).ConfigureAwait(false);

    private async Task<string> RequirementsAnsweredAsync(Quest closed, CancellationToken ct)
    {
        if (closed.Answers.Count == 0) return "";
        var departed = closed.Answers.Count(answer => answer.IsDeparture);
        var met = closed.Answers.Count == 1 ? " Its requirement is met." : $" Each of its {closed.Answers.Count} requirements is met.";
        if (closed.Hold is null or QuestHold.Unreviewed) return met;

        // What a met answer's evidence is, for the session that can still commit it: Daoris reads the
        // work's last commit when the session ends, and a met answer without it holds the quest for the person.
        var wanted = closed.EvidenceWanted;
        var read = $"Daoris reads {string.Join(", ", wanted.Select(each => $"`{each.Item.Named}`").Distinct())} in this work's "
                   + $"last commit when this session ends, so commit {(wanted.Count == 1 ? "it" : "them")} first if you have not";
        if (departed == 0)
        {
            return $"{met} It is held until Daoris reads its evidence: {read}. Until it is found, "
                   + $"{string.Join("; and ", await reviews.HoldsAsync(closed, "it is found", ct).ConfigureAwait(false))}.";
        }

        return $" It departed from {departed} of its {QuestWords.Count(closed.Answers.Count, "requirement")}, so it is held for the "
               + $"person's yes: {string.Join("; and ", await reviews.HoldsAsync(closed, "they accept it", ct).ConfigureAwait(false))}. "
               + $"The departure is kept on the quest, and the person accepts it with `daoris quest accept {closed.Id}`."
               + (closed.AwaitsEvidence ? $" Its evidence is read too: {read}." : "");
    }
}
