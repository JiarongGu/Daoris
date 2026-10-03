using System.Globalization;

namespace Daoris.Driver;

/// <summary>
/// The composer's account of the pieces it joined (CONTEXT1, D143 point 1): each section's size as counted while joining, and,
/// from the target the instruction was composed for, where it came from and what its bound left out. The bounds are counted by
/// the same rules the instruction quotes by (<see cref="TargetPrompt.RequirementsShown"/>, <see cref="AskWordsText.Bounded"/>,
/// <see cref="GoAheadsText.Shown"/>), so the account cannot disagree with what was handed.
/// </summary>
/// <remarks>
/// The English here is the driver's sentence for a terminal (<c>daoris-driver trace</c>); the page words each section by its
/// codes. A section that could have been handed and was not is said, with why (D143 point 3): the ones a person looks for
/// when a session seems not to know something.
/// </remarks>
internal static class InstructionAccounting
{
    /// <summary>The sections whose absence is said, in the instruction's order.</summary>
    private static readonly string[] SaidWhenAbsent =
    [
        HandedSections.Requirements, HandedSections.Words, HandedSections.GoAheads, HandedSections.Standing,
        HandedSections.Language, HandedSections.Map, HandedSections.Indexes,
    ];

    /// <summary>The instruction joined from its pieces, and the account of it: each section once, in the order it first appears.</summary>
    internal static ComposedInstruction Account(SessionTarget target, IReadOnlyList<TargetPrompt.Piece> pieces)
    {
        var sizes = new Dictionary<string, int>(StringComparer.Ordinal);
        var order = new List<string>();
        foreach (var piece in pieces)
        {
            if (piece.Text.Length == 0) continue;
            if (!sizes.TryAdd(piece.Section, piece.Text.Length))
            {
                sizes[piece.Section] += piece.Text.Length;
                continue;
            }

            order.Add(piece.Section);
        }

        var text = string.Concat(pieces.Select(piece => piece.Text));
        IReadOnlyList<HandedSection> sections =
        [
            .. order.Select(name => Handed(name, sizes[name], target)),
            .. SaidWhenAbsent.Where(name => !sizes.ContainsKey(name)).Select(name => Absent(name, target)),
        ];
        return new ComposedInstruction(text, new InstructionAccount(text.Length, sections));
    }

    /// <summary>An intake's instruction (<see cref="IntakePrompt"/>), its own composer's: one section, its parts not counted apart.</summary>
    internal static ComposedInstruction Intake(SessionTarget target, string prompt) => new(
        prompt,
        new InstructionAccount(prompt.Length,
        [
            new HandedSection(
                HandedSections.Intake, HandedSources.Ask,
                $"the intake's instruction: {N(prompt.Length)} characters, composed for ask #{target.Ask}; its parts are not counted apart")
            {
                Chars = prompt.Length,
                From = target.Ask,
            },
        ]));

    private static HandedSection Handed(string name, int chars, SessionTarget target)
    {
        var size = $"{N(chars)} characters";
        HandedSection Own(string said) => new(name, HandedSources.Driver, $"{said}: {size}, the driver's own words") { Chars = chars };

        return name switch
        {
            HandedSections.Quest => new HandedSection(
                name, HandedSources.Quest,
                $"the quest: {size}, quest #{target.QuestId} asked by `{target.Asker}`, as the service answered it")
            {
                Chars = chars,
                From = target.QuestId,
            },
            HandedSections.Carried => Carried(target, chars, size),
            HandedSections.Requirements => Requirements(target, chars, size),
            HandedSections.Words => Words(target, chars, size),
            HandedSections.GoAheads => GoAheads(target, chars, size),
            HandedSections.Standing => Standing(target, chars, size),
            HandedSections.WrittenTo => new HandedSection(
                name, HandedSources.Written, $"the person's words to an earlier session on this quest: {size}, quoted whole")
            {
                Chars = chars,
            },
            HandedSections.Answered => new HandedSection(
                name, HandedSources.Quest,
                $"the question it waited on: {size}, quest #{target.Answered!.Id} to `{target.Answered.To}` and its answer")
            {
                Chars = chars,
                From = target.Answered.Id,
            },
            HandedSections.CarryOn => CarryOn(target, chars, size),
            HandedSections.Close => Own(target.Answered is null && target.CutOff is null
                ? "how to take it and close it"
                : "how to carry it on and close it"),
            HandedSections.Language => new HandedSection(
                name,
                target.Language!.Source == LanguageSource.Repository ? HandedSources.Repository : HandedSources.Workspace,
                $"the session language: {size}, `{target.Language.Code}`, set for its {target.Language.Source} on this machine")
            {
                Chars = chars,
                From = target.Language.Code,
            },
            HandedSections.Map => new HandedSection(name, HandedSources.Tree, $"the code map: {size}, naming `{target.CodeMap}` in its tree")
            {
                Chars = chars,
                From = target.CodeMap,
            },
            HandedSections.Landing => new HandedSection(
                name, HandedSources.Landing,
                $"how it lands: {size}, its branch put on `{target.LandsOn!.Target}`"
                + (target.LandsOn.Plugin is null ? " for the person to push" : " and pushed by a plugin once the person accepts it"))
            {
                Chars = chars,
                From = target.LandsOn.Target,
            },
            HandedSections.Reading => Reading(target, chars, size),
            HandedSections.Look => Own("where to look before asking"),
            HandedSections.Indexes => Indexes(target, chars, size),
            HandedSections.Attributed => Own("the person's words met second-hand"),
            HandedSections.Asking => Own(AskWords.AskOf(target.Asker) is null
                ? "asking another repository, and stopping for the person"
                : "asking another repository, and stopping for the person, a go-ahead asked once on the ask"),
            HandedSections.Closing => Own("the closing note's two lists, each item naming its source"),
            HandedSections.Proposing => Own("proposing a rule"),
            HandedSections.Boundary => target.WritesAcross.Count == 0
                ? Own("the boundary")
                : new HandedSection(
                    name, HandedSources.Checkouts,
                    $"the boundary: {size}, with {Plural(target.WritesAcross.Count, "checkout")} the person declared it may change")
                {
                    Chars = chars,
                    Shown = target.WritesAcross.Count,
                },
            _ => throw new InvalidOperationException($"no account for the section `{name}`"),
        };
    }

    private static HandedSection Carried(SessionTarget target, int chars, string size)
    {
        var parts = new List<string>();
        if (target.Links.Count > 0) parts.Add(Plural(target.Links.Count, "link"));
        if (target.Attachments.Count > 0) parts.Add(Plural(target.Attachments.Count, "file"));
        if (target.Parent is { } parent) parts.Add($"the quest it follows, #{parent}");
        if (target.Then.Count > 0) parts.Add(target.Then.Count == 1 ? $"its next step, to `{target.Then[0].To}`" : $"its next {target.Then.Count} steps");

        var elsewhere = target.Attachments.Count(file => file.Path is null);
        return new HandedSection(
            HandedSections.Carried, HandedSources.Quest, $"what the quest carries: {size} from quest #{target.QuestId}: {Joined(parts)}")
        {
            Chars = chars,
            From = target.QuestId,
            Cuts = elsewhere == 0
                ? null
                : [new HandedCut(HandedCuts.FilesElsewhere, $"{Plural(elsewhere, "file")} not on this machine, named without a path") { Count = elsewhere }],
        };
    }

    private static HandedSection Requirements(SessionTarget target, int chars, string size)
    {
        var of = target.Requirements.Count;
        var shown = TargetPrompt.RequirementsShown(target.Requirements);
        return new HandedSection(
            HandedSections.Requirements, HandedSources.Quest, $"the requirements: {size}, {shown} of {of} from quest #{target.QuestId}")
        {
            Chars = chars,
            From = target.QuestId,
            Shown = shown,
            Of = of,
            Cuts = shown == of
                ? null
                :
                [
                    new HandedCut(
                        HandedCuts.RequirementsBound,
                        $"requirements {shown + 1} to {of} left out past its bound of {N(TargetPrompt.RequirementsLimit)} characters; "
                        + "`quest_list` shows each whole")
                    {
                        Count = of - shown,
                        Limit = TargetPrompt.RequirementsLimit,
                    },
                ],
        };
    }

    private static HandedSection Words(SessionTarget target, int chars, string size)
    {
        var words = target.Words!;
        if (words.Said is not { } said)
        {
            return new HandedSection(
                HandedSections.Words, HandedSources.Ask, $"the person's words: {size} from ask #{words.Ask}, saying they could not be read")
            {
                Chars = chars,
                From = words.Ask,
                Cuts = [new HandedCut(HandedCuts.WordsUnread, "they could not be read for this start, so none after the ask itself were handed")],
            };
        }

        var (shown, left) = AskWordsText.Bounded(said);
        var cuts = new List<HandedCut>();
        if (left.Count > 0)
        {
            cuts.Add(new HandedCut(
                HandedCuts.WordsOlder,
                $"{Plural(left.Count, "older word")}, said from {When(left[0].At)} to {When(left[^1].At)}, left out past its bound of "
                + $"{N(AskWordsText.WordsLimit)} characters; the ask's record keeps every one")
            {
                Count = left.Count,
                Limit = AskWordsText.WordsLimit,
                From = left[0].At,
                To = left[^1].At,
            });
        }

        var cutLong = shown.Count(word => word.Kind != AskWordView.Asked && word.Text.Length > AskWordsText.WordLimit);
        if (cutLong > 0)
        {
            cuts.Add(new HandedCut(
                HandedCuts.WordsLong,
                cutLong == 1
                    ? $"1 word cut at {N(AskWordsText.WordLimit)} characters"
                    : $"{N(cutLong)} words each cut at {N(AskWordsText.WordLimit)} characters")
            {
                Count = cutLong,
                Limit = AskWordsText.WordLimit,
            });
        }

        if (words.KeptFrom is { } from)
        {
            cuts.Add(new HandedCut(HandedCuts.WordsNotKept, $"words said before {When(from)} were not kept on the ask") { From = from });
        }

        return new HandedSection(
            HandedSections.Words, HandedSources.Ask, $"the person's words: {size}, {shown.Count} of {said.Count} from ask #{words.Ask}")
        {
            Chars = chars,
            From = words.Ask,
            Shown = shown.Count,
            Of = said.Count,
            Cuts = cuts.Count == 0 ? null : cuts,
        };
    }

    private static HandedSection GoAheads(SessionTarget target, int chars, string size)
    {
        var words = target.Words!;
        var held = words.GoAheads!;
        var shown = GoAheadsText.Shown(held);
        var cuts = new List<HandedCut>();
        if (shown < held.Count)
        {
            cuts.Add(new HandedCut(
                HandedCuts.GoAheadsBound,
                $"go-aheads {held[shown].Number} to {held[^1].Number} left out past its bound of {N(GoAheadsText.Limit)} characters; "
                + "the ask's page shows each")
            {
                Count = held.Count - shown,
                Limit = GoAheadsText.Limit,
            });
        }

        var cutLong = held.Take(shown).Count(goAhead => goAhead.Act.Length > GoAheadsText.ActLimit);
        if (cutLong > 0)
        {
            cuts.Add(new HandedCut(
                HandedCuts.ActsLong,
                cutLong == 1
                    ? $"1 act cut at {N(GoAheadsText.ActLimit)} characters"
                    : $"{N(cutLong)} acts each cut at {N(GoAheadsText.ActLimit)} characters")
            {
                Count = cutLong,
                Limit = GoAheadsText.ActLimit,
            });
        }

        return new HandedSection(
            HandedSections.GoAheads, HandedSources.Ask, $"the go-aheads: {size}, {shown} of {held.Count} from ask #{words.Ask}")
        {
            Chars = chars,
            From = words.Ask,
            Shown = shown,
            Of = held.Count,
            Cuts = cuts.Count == 0 ? null : cuts,
        };
    }

    private static HandedSection Standing(SessionTarget target, int chars, string size)
    {
        var standing = target.Standing!;
        var over = standing.Says.Length - StandingText.Limit;
        return new HandedSection(
            HandedSections.Standing, HandedSources.Repository,
            $"the standing answer: {size}, the person's for `{target.Repository}` on this machine"
            + (standing.At is { } at ? $", set {When(at)}" : ""))
        {
            Chars = chars,
            From = target.Repository,
            At = standing.At,
            Cuts = over <= 0
                ? null
                :
                [
                    new HandedCut(HandedCuts.StandingLong, $"{N(over)} characters of it left out past its bound of {N(StandingText.Limit)}")
                    {
                        Count = over,
                        Limit = StandingText.Limit,
                    },
                ],
        };
    }

    private static HandedSection CarryOn(SessionTarget target, int chars, string size)
    {
        var facts = new List<string> { "its record's end" };
        if (target.PersonSaid is not null) facts.Add("the person's answer");
        else if (target.Released) facts.Add("that the person released it");
        facts.Add(target.InFlight.Count == 0 ? "nothing uncommitted" : Plural(target.InFlight.Count, "uncommitted change"));
        var plan = target.LastPlan.Count;
        if (plan > 0) facts.Add($"its plan of {Plural(plan, "step")}");
        if (target.LastWords is { Length: > 0 }) facts.Add("its last words");
        if (target.AccountChanged) facts.Add("that it ran on another account");

        return new HandedSection(
            HandedSections.CarryOn, HandedSources.Record, $"what the session before left: {size}: {Joined(facts)}")
        {
            Chars = chars,
            Shown = plan > 0 ? Math.Min(plan, TargetPrompt.PlanLimit) : null,
            Of = plan > 0 ? plan : null,
            Cuts = plan <= TargetPrompt.PlanLimit
                ? null
                :
                [
                    new HandedCut(
                        HandedCuts.PlanBound,
                        $"{Plural(plan - TargetPrompt.PlanLimit, "plan step")} left out past its bound of {TargetPrompt.PlanLimit}")
                    {
                        Count = plan - TargetPrompt.PlanLimit,
                        Limit = TargetPrompt.PlanLimit,
                    },
                ],
        };
    }

    private static HandedSection Reading(SessionTarget target, int chars, string size)
    {
        var readOnly = target.ReadsAcross.Count(read => !target.WritesAcross.Any(write =>
            string.Equals(write.Repository, read.Repository, StringComparison.OrdinalIgnoreCase)));
        return new HandedSection(
            HandedSections.Reading, HandedSources.Checkouts,
            $"the checkouts it may read: {size}, {Plural(readOnly, "checkout")} the person declared it may read and not change")
        {
            Chars = chars,
            Shown = readOnly,
        };
    }

    private static HandedSection Indexes(SessionTarget target, int chars, string size)
    {
        var of = target.Indexes.Count;
        var shown = Math.Min(of, TargetPrompt.IndexLimit);
        return new HandedSection(
            HandedSections.Indexes, HandedSources.Tree, $"the repository's indexes: {size}, naming {shown} of the {of} its tree keeps")
        {
            Chars = chars,
            Shown = shown,
            Of = of,
            Cuts = of <= TargetPrompt.IndexLimit
                ? null
                :
                [
                    new HandedCut(HandedCuts.IndexesBound, $"{N(of - TargetPrompt.IndexLimit)} more counted, not named, past the {TargetPrompt.IndexLimit} it names")
                    {
                        Count = of - TargetPrompt.IndexLimit,
                        Limit = TargetPrompt.IndexLimit,
                    },
                ],
        };
    }

    /// <summary>A section that could have been handed and was not, saying why (D143 point 3).</summary>
    private static HandedSection Absent(string name, SessionTarget target)
    {
        var ask = target.Words?.Ask;
        return name switch
        {
            HandedSections.Requirements => new HandedSection(
                name, HandedSources.Quest, "the requirements: none, since the quest names none")
            {
                Chars = 0, From = target.QuestId, None = HandedNones.Empty,
            },
            HandedSections.Words => new HandedSection(
                name, HandedSources.Ask, "the person's words: none, since no ask asked this quest")
            {
                Chars = 0, None = HandedNones.NoAsk,
            },
            HandedSections.GoAheads => target.Words is null
                ? new HandedSection(name, HandedSources.Ask, "the go-aheads: none, since no ask asked this quest") { Chars = 0, None = HandedNones.NoAsk }
                : target.Words.GoAheads is null
                    ? new HandedSection(name, HandedSources.Ask, $"the go-aheads: none, since the service answered none for ask #{ask}")
                    {
                        Chars = 0, From = ask, None = HandedNones.NotAnswered,
                    }
                    : new HandedSection(name, HandedSources.Ask, $"the go-aheads: none asked on ask #{ask}")
                    {
                        Chars = 0, From = ask, None = HandedNones.Empty,
                    },
            HandedSections.Standing => new HandedSection(
                name, HandedSources.Repository, $"the standing answer: none set for `{target.Repository}` on this machine")
            {
                Chars = 0, From = target.Repository, None = HandedNones.NotSet,
            },
            HandedSections.Language => new HandedSection(
                name, HandedSources.Repository, "the session language: none set for its repository or its workspace on this machine")
            {
                Chars = 0, None = HandedNones.NotSet,
            },
            HandedSections.Map => new HandedSection(name, HandedSources.Tree, "the code map: none, since its tree keeps none")
            {
                Chars = 0, None = HandedNones.Empty,
            },
            HandedSections.Indexes => new HandedSection(name, HandedSources.Tree, "the repository's indexes: none, since its tree names none")
            {
                Chars = 0, None = HandedNones.Empty,
            },
            _ => throw new InvalidOperationException($"no absence is said for the section `{name}`"),
        };
    }

    /// <summary>A count as the terminal writes one: digits grouped the invariant way, the same on every machine.</summary>
    internal static string N(int count) => count.ToString("N0", CultureInfo.InvariantCulture);

    internal static string Plural(int count, string noun) => count == 1 ? $"1 {noun}" : $"{N(count)} {noun}s";

    /// <summary>Items as a sentence lists them: a comma between, and an <c>and</c> before the last.</summary>
    internal static string Joined(IReadOnlyList<string> items) => items.Count switch
    {
        0 => "",
        1 => items[0],
        _ => $"{string.Join(", ", items.Take(items.Count - 1))} and {items[^1]}",
    };

    /// <summary>A moment as the instructions say it: to the minute, in UTC, the same on every machine.</summary>
    private static string When(DateTimeOffset at) => at.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);
}
