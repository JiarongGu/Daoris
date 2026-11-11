using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>The lines domain's answers as the bridge carries them: each repository's line and the rules that resolve for it, and each rule's wire shape.</summary>
internal static class LinesWires
{
    /// <summary>
    /// What <c>LINES</c> answers for <paramref name="lines"/>: each repository's line and its policy, the language its
    /// sessions write in, where its work is reviewed, which other agent reads it, what a helper does, how its work is committed and
    /// the access setup its sessions start under, each as it resolves from <paramref name="config"/> and where it is set, in arrays keyed by repository and workspace. The lines
    /// slice pushes each repository's rows of it: one builder for both doors, so the pull and the push cannot drift.
    /// </summary>
    /// <param name="adapters">The loop's agents, the machine's plugins among them, which an opinion's <c>sameAgent</c> is judged by.</param>
    public static object Answer(IReadOnlyList<LineRow> lines, DriverConfig config, ProviderRegistry adapters)
    {
        var gitLevels = GitLevelsOf(config);
        return new
        {
            Lines = lines.Select(line => line with { Policy = IntegrateWire(config.LinePolicyOf(line.Repository, line.Workspace)) }).ToArray(),
            // What each one's sessions are asked to write to the person in, and where that is set: the driver's
            // resolution, read rather than recomputed by the page. None leaves the language and its source out.
            Languages = lines.Select(line => (line, language: SessionLanguages.Resolve(config, line.Repository, line.Workspace)))
                .Select(pair => new
                {
                    pair.line.Repository,
                    pair.line.Workspace,
                    Language = pair.language?.Code,
                    pair.language?.Name,
                    pair.language?.Source,
                })
                .ToArray(),
            // Where each one's work is reviewed before it lands, and where that is set: the driver's resolution,
            // read rather than recomputed by the page. Nothing set anywhere leaves the rule and its source out.
            Reviews = lines.Select(line => (line, review: ReviewRules.Resolve(config, line.Repository, line.Workspace)))
                .Select(pair => new
                {
                    pair.line.Repository,
                    pair.line.Workspace,
                    Rule = pair.review is null ? null : ReviewWire(pair.review.Rule),
                    pair.review?.Source,
                })
                .ToArray(),
            // Which other agent reads each one's work before it lands, and where that is set: the driver's
            // resolution, read rather than recomputed by the page. Nothing set anywhere leaves the rule and its source out.
            Opinions = lines.Select(line => (line, opinion: OpinionRules.Resolve(config, line.Repository, line.Workspace)))
                .Select(pair => new
                {
                    pair.line.Repository,
                    pair.line.Workspace,
                    Rule = pair.opinion is null ? null : OpinionWire(pair.opinion.Rule, ActorChoices.BuildAgent(config), adapters),
                    pair.opinion?.Source,
                })
                .ToArray(),
            // What a helper each one's agent asks for does, and how its work is committed, each with where it is set:
            // the driver's resolution, never null, `default` where nothing is set.
            Helpers = lines.Select(line => (line, helpers: HelperRules.Resolve(config, line.Repository, line.Workspace)))
                .Select(pair => new { pair.line.Repository, pair.line.Workspace, Rule = HelperWire(pair.helpers.Rule), pair.helpers.Source })
                .ToArray(),
            // What an agent's ask to hand its session over does, with where it is set, `default` where
            // nothing is: the driver's resolution, never null.
            HandOver = lines.Select(line => (line, handOver: HandOverRules.Resolve(config, line.Repository, line.Workspace)))
                .Select(pair => new { pair.line.Repository, pair.line.Workspace, Rule = new { pair.handOver.Rule.Mode }, pair.handOver.Source })
                .ToArray(),
            Growth = lines.Select(line => (line, growth: GrowthRules.Resolve(config, line.Repository, RemoteTarget.Workspace(line.Workspace))))
                .Select(pair => new { pair.line.Repository, pair.line.Workspace, pair.growth.Value, pair.growth.Source })
                .ToArray(),
            Commits = lines.Select(line => (line, commits: CommitRules.Resolve(config, line.Repository, line.Workspace)))
                .Select(pair => new { pair.line.Repository, pair.line.Workspace, Rule = CommitWire(pair.commits.Rule), pair.commits.Source })
                .ToArray(),
            // The access setup each one's sessions start under, every pause with its value and where it is set:
            // the driver's resolution, never null, `default` where nothing is set, so the page shows where each came from.
            Access = lines.Select(line => (line, access: AccessRules.Resolve(config, line.Repository, line.Workspace)))
                .Select(pair => new
                {
                    pair.line.Repository,
                    pair.line.Workspace,
                    pair.access.Full,
                    Pauses = pair.access.Pauses.Select(pause => new { pause.Pause, pause.Value, pause.Source }).ToArray(),
                })
                .ToArray(),
            // What a session's git may do on a remote, each class with its value and where it is set; the driver's
            // resolution, `default` where nothing is set (Write then `as-today`).
            GitAccess = lines.Select(line => (line, access: GitAccessKeys.Resolve(gitLevels, line.Repository, line.Workspace)))
                .Select(pair => new
                {
                    pair.line.Repository,
                    pair.line.Workspace,
                    Classes = pair.access.Classes.Select(each => new { each.Class, each.Value, each.Source }).ToArray(),
                })
                .ToArray(),
        };
    }

    /// <summary>A rule's <c>integrate</c> as the file writes it (and a press may send it), each key null where absent; null where the rule has none.</summary>
    public static object? IntegrateWire(LandingIntegrate? integrate) => integrate is null ? null : new
    {
        integrate.Request,
        integrate.Commit,
        integrate.Batch,
        integrate.Order,
        integrate.Gates,
        Unconfirmed = integrate.Unreached is null && integrate.Reached is null ? null : new { integrate.Unreached, integrate.Reached },
        integrate.Conflicts,
        integrate.Line,
    };

    /// <summary>
    /// A rule as the bridge carries it: whether it is none here, whether work waits for the person's look, and
    /// each environment, the first the default. Rows rather than an object's keys, as every map here: a key policy on the
    /// bridge would respell a name.
    /// </summary>
    public static object ReviewWire(ReviewRule rule) => new
    {
        None = rule.IsNone,
        rule.Required,
        Environments = rule.Environments.Select(each => new { each.Name, each.Kind, each.Procedure, each.Address, each.Run }).ToArray(),
    };

    /// <summary>
    /// A rule as the bridge carries it: whether it is none here, its occasions and reviewers in order, each switch,
    /// the bound of a pass (the default where none is written), and the reviewers of the working agent's own family, judged
    /// against <paramref name="working"/>, the agent this machine's work runs on, so the page says what the terminal says.
    /// </summary>
    /// <param name="adapters">
    /// The loop's agents, the machine's plugins among them, so a maker a plugin declares counts on the screen as it counts at
    /// the terminal and at Ask Daoris's door.
    /// </param>
    public static object OpinionWire(OpinionRule rule, string working, ProviderRegistry adapters) => new
    {
        None = rule.IsNone,
        rule.On,
        rule.Reviewers,
        rule.Required,
        rule.Verify,
        Minutes = rule.Bound,
        rule.Recheck,
        SameAgent = OpinionRules.SameAgent(rule, working, adapters),
    };

    /// <summary>The git access levels read from the file <paramref name="config"/>; none where it holds none.</summary>
    public static GitAccessLevels GitLevelsOf(DriverConfig config)
    {
        try
        {
            return config.Source is { Length: > 0 } text && System.Text.Json.Nodes.JsonNode.Parse(text) is System.Text.Json.Nodes.JsonObject root
                ? GitAccessKeys.Of(root)
                : GitAccessLevels.None;
        }
        catch (System.Text.Json.JsonException)
        {
            return GitAccessLevels.None;
        }
    }

    /// <summary>A level's git access as set, its classes in order, so the page says where a value came from.</summary>
    public static object[] GitClassesWire(IReadOnlyDictionary<string, string> level) =>
        [.. GitAccessKeys.Classes.Where(level.ContainsKey).Select(name => new { Class = name, Value = level[name] })];

    /// <summary>
    /// A level's access setup as the bridge carries it: whether it is full access, and the pauses it names in their order,
    /// each with its value, as rows. None set is no pause and not full, which is what setting nothing means.
    /// </summary>
    public static object AccessWire(AccessSetting? setting) => new
    {
        Full = setting?.Full ?? false,
        Pauses = setting is null || setting.Full
            ? []
            : AccessRules.Pauses.Where(pause => setting.Pauses.ContainsKey(pause.Name))
                .Select(pause => new { Pause = pause.Name, Value = setting.Pauses[pause.Name] }).ToArray(),
    };

    /// <summary>A helpers rule as the bridge carries it: its mode, and the agents in the order they are tried.</summary>
    public static object HelperWire(HelperRule rule) => new { rule.Mode, rule.Agents };

    /// <summary>A commit rule as the bridge carries it: its mode, and the person's convention, left out where none.</summary>
    public static object CommitWire(CommitRule rule) => new { rule.Mode, rule.Convention };
}
