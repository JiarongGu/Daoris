namespace Daoris.Driver;

/// <summary>What <c>daoris-driver quest review</c> was asked: the quest, the verdict as the service spells it, and the person's words.</summary>
/// <param name="Said">
/// <c>reviewed</c>, <c>not-yet</c> or <c>skipped</c>, which <c>off</c> is (REVIEWENV1j, design §3.6); or
/// <see cref="QuestReviewCommand.SetUpStep"/>, a set-up step asked in <see cref="SetUpIn"/>.
/// </param>
public sealed record QuestReviewAsk(string Quest, string Said, string? Words)
{
    /// <summary>
    /// The environment the chain's choice sets the work up in (REVIEWENV1j, design §3.6): <c>on</c>, the repository's default, or an
    /// environment's name; null for a verdict.
    /// </summary>
    public string? SetUpIn { get; init; }
}

/// <summary>What the verdict's door reaches beyond the service: the step's session, for a <i>not yet</i>, and the machine log.</summary>
/// <param name="Service">The local host, whose review door keeps the verdict.</param>
public sealed record QuestReviewWorld(ServiceClient Service)
{
    /// <summary>
    /// This machine's driver config, whose review rule a set-up's choice is judged against (REVIEWENV1j, design §1.4): the
    /// environment named, or <c>on</c>'s default, must be one the quest's repository declares. Empty declares none.
    /// </summary>
    public DriverConfig Config { get; init; } = DriverConfig.Empty;

    /// <summary>
    /// The person's <i>not yet</i> said to the set-up step's session as its next turn (D137): the terminal's <c>sessions say</c>,
    /// which holds it at the door of a loop that runs the session or keeps it on its record. Its exit code; null says nothing.
    /// </summary>
    public Func<string, string, CancellationToken, Task<int>>? Say { get; init; }

    /// <summary>Where the verdict's <c>review.verdict</c> line goes (D94); null where nothing keeps one.</summary>
    public Action<LandingLine>? Log { get; init; }
}

/// <summary>
/// The terminal's door to the person's verdict on a review (REVIEWENV1c, D154 point 8; the review environment design §3.3, D50):
/// <c>daoris-driver quest review &lt;id&gt; reviewed|not-yet|skip ["…"]</c>. It prints what the set-up step showed and how to show it
/// again, sends the verdict with the set-up it printed, by its <c>machine</c> and <c>sequence</c> (REVIEWENV1b3: what the person
/// looked at is what the verdict answers), and says what the gate now says. A <i>not yet</i> needs words, and they go to the step's
/// session as its next turn (design §3.4).
/// </summary>
/// <remarks>
/// <para><b>The person's alone</b>: no connector tool and no Ask Daoris card gives a verdict (design §3.3). This door is a
/// terminal's, beside the quest's page and the strip's chip that REVIEWENV1g draws.</para>
///
/// <para><b>The chain's choice at the gate</b> (REVIEWENV1j, design §3.6): <c>quest review &lt;id&gt; off ["…"]</c> is the skip,
/// and <c>on|&lt;environment&gt;</c> is <i>Set it up</i>, the same act at another time. Each calls the door the gate's press calls
/// (<c>/review</c>, <c>/set-up-step</c>), never a second path. The environment is judged against the rule standing for the quest's
/// repository first, since the service holds no rule (design §1.4: refused at the door, naming the repository).</para>
///
/// <para><b>Exit codes</b>: 0 kept, 1 refused (no such quest, nothing shown yet, an environment not declared, a service refusal),
/// 2 the usage.</para>
/// </remarks>
public static class QuestReviewCommand
{
    public const string Usage =
        "usage: daoris-driver quest review <id> reviewed|not-yet|skip [\"…\"]\n"
        + "       say what you saw of a set-up step's showing: reviewed lets the work it holds land; not-yet, with your words, sends\n"
        + "       them to the step's session as its next turn; skip lets this work land without a review, on a set-up step or the\n"
        + "       chain's work quest. It prints what was shown and how to show it again, then what the gate says.\n"
        + "       daoris-driver quest review <id> off [\"…\"]  ·  daoris-driver quest review <id> on|<environment>\n"
        + "       choose whether a done quest's work is reviewed, as its gate does: off is the skip; on (the repository's default\n"
        + "       environment) or an environment it declares publishes a set-up step to show the work there.";

    /// <summary>What <see cref="QuestReviewAsk.Said"/> is for a set-up step the chain's choice asks for (REVIEWENV1j).</summary>
    public const string SetUpStep = "set-up-step";

    /// <summary>Whether a <c>quest</c> line asks for a review's verdict, or the chain's choice.</summary>
    public static bool Asks(IReadOnlyList<string> args) => args is ["review", ..];

    /// <summary>
    /// The line read, or null with why not. Every word after a verdict, or after <c>off</c>, is the person's, joined as said; <c>on</c>
    /// and an environment's name take none, since a set-up step keeps no words of the person's.
    /// </summary>
    public static QuestReviewAsk? Read(IReadOnlyList<string> args, out string? problem)
    {
        problem = null;
        if (args is not ["review", var id, var verdict, ..] || !Id(id))
        {
            problem = "`quest review` takes one quest's id, then `reviewed`, `not-yet` or `skip`, then your words; "
                + "or `off`, `on` or an environment's name.";
            return null;
        }

        var words = string.Join(" ", args.Skip(3)).Trim();
        var said = verdict switch
        {
            "reviewed" => ReviewVerdicts.Reviewed,
            "not-yet" => ReviewVerdicts.NotYet,
            // REVIEWENV1j: the chain's `off` at the gate is the skip (design §3.6).
            "skip" or "skipped" or "off" => ReviewVerdicts.Skipped,
            _ => null,
        };
        if (said is null)
        {
            // REVIEWENV1j: `on` or an environment's name is *Set it up*, judged as a rule judges a name.
            if (verdict != "on" && ReviewRules.NameProblem(verdict) is { } unnamed)
            {
                problem = $"`{verdict}` is not a verdict or a review choice: `reviewed`, `not-yet` or `skip`; or `off`, `on` or an "
                    + $"environment's name, and {unnamed}";
                return null;
            }

            if (words.Length > 0)
            {
                problem = $"`{verdict}` sets the work up for review in a set-up step, which keeps no words of yours: "
                    + "`off \"…\"` keeps them with a skip.";
                return null;
            }

            return new QuestReviewAsk(id.TrimStart('#'), SetUpStep, null) { SetUpIn = verdict };
        }

        if (said == ReviewVerdicts.NotYet && words.Length == 0)
        {
            problem = "`not-yet` needs your words: what is not right yet is what the step's session acts on.";
            return null;
        }

        return new QuestReviewAsk(id.TrimStart('#'), said, words.Length == 0 ? null : words);
    }

    /// <summary>
    /// Print what was shown, give the verdict, send a <i>not yet</i>'s words on, and say what the gate now says; or, for the chain's
    /// choice of an environment, publish the set-up step there (REVIEWENV1j).
    /// </summary>
    public static async Task<int> RunAsync(QuestReviewAsk ask, QuestReviewWorld world, TextWriter output, CancellationToken ct = default)
    {
        var quest = await world.Service.FindQuestAsync(ask.Quest, ct).ConfigureAwait(false);
        if (quest is null)
        {
            output.WriteLine($"daoris-driver: no quest `#{ask.Quest}` here. Ids come from the quest's page or `daoris-driver sessions`.");
            return 1;
        }

        if (ask.SetUpIn is { } chosen) return await SetUpAsync(quest, chosen, world, output, ct).ConfigureAwait(false);

        QuestSetUpView? shown = null;
        if (ask.Said != ReviewVerdicts.Skipped)
        {
            if (quest.SetUpIn is null)
            {
                output.WriteLine($"daoris-driver: quest `#{quest.Id}` is no set-up step, so nothing of it was shown to review: "
                    + $"`{ask.Said}` answers a set-up. `daoris-driver quest review {quest.Id} skip` lets its work land without a review.");
                return 1;
            }

            shown = quest.SetUps.LastOrDefault();
            if (shown is not { Machine: not null, Sequence: not null })
            {
                output.WriteLine($"daoris-driver: set-up step `#{quest.Id}` has shown nothing yet in `{quest.SetUpIn}`: its session says "
                    + "what it showed, and Daoris posts it when the session ends.");
                return 1;
            }

            foreach (var line in Shown(quest, shown)) output.WriteLine(line);
        }

        var (ok, message) = await world.Service.ReviewAsync(quest.Id, ask.Said, ask.Words, shown?.Machine, shown?.Sequence, ct)
            .ConfigureAwait(false);
        output.WriteLine($"daoris-driver: {message}");
        if (!ok) return 1;
        world.Log?.Invoke(ReviewLines.Verdict(quest.Id, ask.Said, ReviewDoors.Terminal));

        // Not yet goes back to the step's session as its next turn (design §3.4, D137): the person's own set-up has no session.
        if (ask.Said == ReviewVerdicts.NotYet)
        {
            if (shown!.Session is not { } session)
            {
                output.WriteLine("daoris-driver: that set-up was your own, so no session hears your words: they are kept on the quest and its ask.");
            }
            else if (world.Say is { } say)
            {
                await say(session, ask.Words!, ct).ConfigureAwait(false);
            }
        }

        output.WriteLine($"daoris-driver: the gate: {Gate(quest, ask.Said, shown)}");
        return 0;
    }

    /// <summary>
    /// <i>Set it up in <c>environment</c></i> from a terminal (REVIEWENV1j, design §3.6): the environment judged against the rule standing
    /// for the quest's repository, <c>on</c> read as its default, then the set-up step's door the gate's press calls, whose sentence is
    /// printed whole, a refusal included.
    /// </summary>
    private static async Task<int> SetUpAsync(QuestView quest, string chosen, QuestReviewWorld world, TextWriter output, CancellationToken ct)
    {
        if (ReviewRules.Resolve(world.Config, quest.To, quest.Workspace) is not { Rule.IsNone: false } declared)
        {
            output.WriteLine($"daoris-driver: `{quest.To}` declares no review environment, so nothing of its work can be shown for review: "
                + $"`daoris driver review {quest.To} <environment> --kind local|deployed --procedure <path>` declares one. Nothing was published.");
            return 1;
        }

        var names = declared.Rule.Environments.Select(environment => environment.Name).ToList();
        var environment = chosen == "on" ? names[0] : chosen;
        if (!names.Contains(environment))
        {
            output.WriteLine($"daoris-driver: `{quest.To}` declares no review environment `{environment}`: it declares {ReviewRules.Either(names)}. "
                + "Nothing was published.");
            return 1;
        }

        var (ok, message, step) = await world.Service.PublishSetUpStepAsync(quest.Id, environment, ct).ConfigureAwait(false);
        output.WriteLine($"daoris-driver: {message}");
        if (!ok) return 1;

        output.WriteLine($"daoris-driver: the gate: `{quest.To}`'s work waits for set-up step `#{step ?? "…"}` to show it in `{environment}`, "
            + $"then for your look: `daoris-driver quest review {step ?? "<step>"} reviewed` once you have looked.");
        return 0;
    }

    /// <summary>What the set-up step showed, where to look and how to show it again, as the person is about to answer it.</summary>
    internal static IReadOnlyList<string> Shown(QuestView step, QuestSetUpView setUp)
    {
        var lines = new List<string>
        {
            $"daoris-driver: set-up step `#{step.Id}` showed it in `{step.SetUpIn}` at `{Short(setUp.Commit)}`"
            + (setUp.Session is { } session ? $", said by session {session}" : ", as your own set-up")
            + (setUp.Shows is { Length: > 0 } shows ? $": {shows}" : "."),
        };
        lines.Add(setUp.Look is { } look
            ? $"  look: {look}"
            : setUp.Local ? $"  look: on {setUp.Machine ?? "the machine that showed it"}, where its tab is" : "  look: not said");
        if (setUp.Again is { Length: > 0 } again) lines.Add($"  again: {again.ReplaceLineEndings(" ")}");
        return lines;
    }

    /// <summary>What the gate says once the verdict is kept (design §3.2), from the verdict and the set-up it answers.</summary>
    private static string Gate(QuestView quest, string said, QuestSetUpView? shown) => said switch
    {
        ReviewVerdicts.Reviewed =>
            $"work up to `{Short(shown!.Commit)}` in `{quest.To}` may land: the review's Accept, `daoris-driver trees land <session>`, or "
            + "the next look where it is accepted automatically. Work added after it waits for its own review.",
        ReviewVerdicts.NotYet =>
            $"it waits for the set-up `#{quest.Id}` shows next, in `{quest.SetUpIn}`.",
        _ => $"`{quest.To}`'s work in this chain lands without a review.",
    };

    private static bool Id(string word) => word.TrimStart('#').Length > 0 && !word.StartsWith('-');

    private static string Short(string commit) => commit.Length > 8 ? commit[..8] : commit;
}
