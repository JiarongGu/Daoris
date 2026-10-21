namespace Daoris.Driver;

/// <summary>
/// The codes a standing choice's refusal is said in (PUSH1f2a, D194 point 4, the alone-path design §1.3): the page's catalogue keys,
/// which the shell's <c>Refusals</c> declares as these, and the driver's verbatim refusal for every other. One table, read by the
/// route that answers the window and by <c>daoris-driver driver</c> acting alone, so a refusal is one line both ways.
/// </summary>
public static class ChoiceCodes
{
    /// <summary>*Try again* asked of a quest the verdicts neither parked nor held by the person's stop.</summary>
    public const string QuestNotHeld = "QUEST_NOT_HELD";

    /// <summary>*Try again* asked of a quest a pause holds: Resume is the press that moves it.</summary>
    public const string QuestPaused = "QUEST_PAUSED";

    /// <summary>A keep rule out of its kind's bounds, or a choice its kind does not take (KEEP1d, the cleanup design §2.2).</summary>
    public const string KeepOutOfRange = "KEEP_OUT_OF_RANGE";

    /// <summary>The shell's <c>DRIVER_REFUSED</c>: any other refusal, said in its own sentence.</summary>
    public const string Refused = "DRIVER_REFUSED";

    /// <summary>Each check an operation refuses by, and the code it is said in.</summary>
    public static IReadOnlyDictionary<string, string> Table { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [ChoiceRefusal.NotHeld] = QuestNotHeld,
        [ChoiceRefusal.Paused] = QuestPaused,
        [ChoiceRefusal.KeepOutOfRange] = KeepOutOfRange,
    };

    /// <summary>The code a refusal is said in: its check's, else the verbatim refusal.</summary>
    public static string Of(DriverException refused) =>
        refused is ChoiceRefusal coded && Table.TryGetValue(coded.Check, out var code) ? code : Refused;
}

/// <summary>
/// A standing choice's operation refusing by a named check (PUSH1f2a, as <see cref="ToolRefusal"/> carries a tool's): its sentence,
/// and the facts the window's catalogue says it with. <see cref="ChoiceCodes.Of"/> gives its code.
/// </summary>
public sealed class ChoiceRefusal(string check, string message, params (string Key, string Value)[] facts) : DriverException(message)
{
    /// <summary>The quest is neither parked nor held by the person's stop: nothing to try again.</summary>
    public const string NotHeld = "quest-not-held";

    /// <summary>The quest is paused, on its own or with its ask.</summary>
    public const string Paused = "quest-paused";

    /// <summary>A keep rule out of its kind's bounds (KEEP1d): its <c>kind</c>, <c>min</c> and <c>max</c> said with it.</summary>
    public const string KeepOutOfRange = "keep-out-of-range";

    public string Check { get; } = check;

    public IReadOnlyList<(string Key, string Value)> Facts { get; } = facts;
}

/// <summary>What *Try again* did to a quest (SESSUX1b): <c>marked</c> at its failures, or <c>released</c> from a stop's session.</summary>
public sealed record RetryDecision(string Quest, string Did, string? Session, Func<DriverConfig, DriverConfig> Change)
{
    public const string Marked = "marked";
    public const string Released = "released";
}

/// <summary>What a review's procedure look found: the repositories of a workspace whose checkout lacks it, and whether it was looked for.</summary>
public sealed record ReviewLooked(IReadOnlyList<string> Lacking, bool Unchecked);

/// <summary>
/// The standing choices' operations (PUSH1f2a, D194 point 4, the alone-path design §1.3): each the judgement a route made inside its
/// handler, taken out over the facts it judges by, so the window's route and <c>daoris-driver driver</c> acting alone call one
/// implementation. The rest of the choices are already <see cref="DriverConfig"/>'s <c>With*</c> and each rule's <c>Apply</c>; their
/// write is <see cref="Write"/>, which the route makes through its context with the loop's nudge.
/// </summary>
public static class StandingChoices
{
    /// <summary>A standing choice written: the file read, the change made, and saved under its lock (CONFIG1); the choices it wrote.</summary>
    /// <exception cref="DriverConfigUnreadableException">The file does not read, so nothing was written.</exception>
    public static DriverConfig Write(string path, Func<DriverConfig, DriverConfig> change)
    {
        var next = change(DriverConfig.Load(path));
        next.Save(path);
        return next;
    }

    /// <summary>
    /// A landing rule set, or cleared with null (D100): its plugin, where it names one and its shape holds, asked of the plugins
    /// <paramref name="catalog"/> holds before anything is written, as <c>daoris driver landing</c> asks it.
    /// </summary>
    /// <exception cref="DriverException">The rule's shape, or its plugin cannot land work here; nothing was written.</exception>
    public static DriverConfig Landing(DriverConfig config, string? repository, string? workspace, LandingRule? rule, PluginCatalog catalog)
    {
        if ((repository is null) == (workspace is null))
        {
            throw new DriverException("a landing rule is set for a `repository` or a `workspace` — name one of them.");
        }

        if (rule is { Plugin: { } plugin } && LandingRules.Problem(rule) is null && LandingRules.PluginProblem(plugin, catalog) is { } problem)
        {
            throw new DriverException($"{problem} Nothing was written.");
        }

        return workspace is not null ? config.WithWorkspaceLanding(workspace, rule) : config.WithLanding(repository!, rule);
    }

    /// <summary>
    /// *Try again* on <paramref name="quest"/> (RETRY1, SESSUX1b, D126 §3.4), judged from the planner's <paramref name="verdicts"/>: a
    /// quest parked on its failed sessions is marked at its failures (RETRY1b), a verdict carrying no count at the least count the
    /// planner parks at; one the person's stop holds is released from that session, with no mark, since a stop is not a strike.
    /// </summary>
    /// <exception cref="ChoiceRefusal">The quest is paused (PAUSE1b), or neither parked nor held: nothing is written.</exception>
    public static RetryDecision Retry(string quest, IReadOnlyList<Consideration> verdicts)
    {
        var verdict = verdicts.FirstOrDefault(c => string.Equals(c.Quest.Id, quest, StringComparison.OrdinalIgnoreCase));
        switch (verdict)
        {
            case { Verdict: StartVerdict.Exhausted } exhausted:
                // Marked where it stands rather than erased, so the records still read true and the next `strikes` failures park it
                // again. At its failures, not the limit (RETRY1b): a quest parked a second time has more failures than the limit.
                return new(quest, RetryDecision.Marked, null,
                    config => config.WithForgiven(quest, exhausted.Failures ?? config.ForgivenAt(quest) + config.Strikes));
            case { Verdict: StartVerdict.Stopped, HeldBy: { } stop }:
                return new(quest, RetryDecision.Released, stop.Session, config => config.WithReleased(quest, stop.Session));
            case { Verdict: StartVerdict.Paused, PausedBy: { } pause }:
                // A pause is the reason before a stop and the strikes (PAUSE1b, D132 §6.1): Resume moves it, and nothing is written.
                throw pause.Scope == WorkScope.Ask
                    ? new ChoiceRefusal(ChoiceRefusal.Paused, $"#{quest} is paused with ask #{pause.Id}; Resume carries it on.",
                        ("id", quest), ("pause", pause.Id))
                    : new ChoiceRefusal(ChoiceRefusal.Paused, $"#{quest} is paused on its own; Resume carries it on.",
                        ("id", quest), ("pause", pause.Id), ("context", "quest"));
            default:
                throw new ChoiceRefusal(ChoiceRefusal.NotHeld,
                    $"#{quest} is neither parked nor held by your stop on this machine, so there is nothing to try again.", ("id", quest));
        }
    }

    /// <summary>
    /// A review rule's environment put, its procedure looked for in each checkout <paramref name="registry"/> names that the edit
    /// reaches (REVIEWENV1a): a repository's checkout here that lacks it refuses the change; a workspace's names each one that lacks it.
    /// </summary>
    /// <exception cref="DriverException">The repository's checkout here does not hold the procedure; nothing is written.</exception>
    public static ReviewLooked Review(IReadOnlyList<(string Repository, string? Workspace, string? Root)> registry, ReviewEdit edit, string procedure)
    {
        var lacking = ReviewRules.Lacking(registry, edit, procedure);
        if (edit.Repository is { } repository)
        {
            if (lacking.Count > 0) throw new DriverException(ReviewRules.NotHeld(repository.Trim(), procedure));
            return new(lacking, !registry.Any(row => row.Root is { Length: > 0 }
                && string.Equals(row.Repository, repository.Trim(), StringComparison.OrdinalIgnoreCase)));
        }

        return new(lacking, !registry.Any(row => row.Root is { Length: > 0 }
            && string.Equals(RemoteTarget.Workspace(row.Workspace), RemoteTarget.Workspace(edit.Workspace), StringComparison.OrdinalIgnoreCase)));
    }
}
