namespace Daoris.Driver;

/// <summary>A work's consent (FOUNDATION1q, D212; the foundation design §2.3): its rule's mode, or no rule yet.</summary>
public static class WorkConsent
{
    public const string On = "on";
    public const string Ask = "ask";
    public const string Off = "off";

    /// <summary>A partner's, until COAGENT1d gives it a key: never <see cref="On"/>.</summary>
    public const string NotARule = "not-a-rule";
}

/// <summary>What one work's policy requires beyond what every work has: a type of its own per work that has any.</summary>
public abstract record WorkRequirement;

/// <summary>A helper's: the default asks Daoris's own set-up agents alone (HELPERDEFAULT1, <see cref="HelperRule.SetUp"/>).</summary>
public sealed record HelpRequirement(bool SetUp) : WorkRequirement;

/// <summary>A second opinion's (D155): whether work waits with no reviewer, verifies and rechecks; its reviewers walked by maker (point 4).</summary>
public sealed record ReviewRequirement(bool Required, bool Verify, bool Recheck, bool MakerPasses) : WorkRequirement;

/// <summary>A partner's: admitted only where its confinement was measured (D199 point 4).</summary>
public sealed record PartnerRequirement(bool Measured) : WorkRequirement;

/// <summary>A brainstorm's fan-out; its mode is listed and read by no start yet (<paramref name="ModeRead"/>).</summary>
public sealed record BrainstormRequirement(int FanOut, bool ModeRead) : WorkRequirement;

/// <summary>A working session's hand-over: the consent an agent's ask to hand over waits on, and where it was set.</summary>
public sealed record HandOverRequirement(string Consent, string Source) : WorkRequirement;

/// <summary>
/// One work's resolved policy (FOUNDATION1q, D212; the foundation design §2.3): what every work has and its typed requirement.
/// </summary>
/// <param name="Agents">The agents its rule permits, in order; none where its rule names none, the role's staffing deciding.</param>
/// <param name="Concurrency">How many at once, in the unit its counter counts (an asker's helpers, a chain's pieces, the cap's slots); null for none.</param>
/// <param name="Minutes">One turn's bound; null where it is watched and never timed out.</param>
/// <param name="Source">Where its rule was set: <c>repository</c>, <c>workspace</c>, <c>machine</c> or <see cref="WorkPolicies.DefaultSource"/>.</param>
public sealed record WorkPolicy(
    string Work, string Consent, IReadOnlyList<string> Agents, int? Concurrency, int? Minutes, string Source, WorkRequirement? Requirement = null)
{
    public bool Enabled => Consent is WorkConsent.On or WorkConsent.Ask;
}

/// <summary>
/// The one reader of each work's policy (FOUNDATION1q, D212; the foundation design §2.3), from the keys <c>driver.json</c> has now, so no
/// key moves and an older build reads its keys as before (D205 point 6). Each default is written once, in <see cref="Of"/>'s table.
/// </summary>
/// <remarks>
/// Ask Daoris is on where <c>helperAdapter</c> names an agent and Intake where its actor's first agent is one: the keys their gates read
/// today, which the actor's write-through keeps aligned (survey §1.3 row 10). The rules that resolve in a subdomain above this one
/// (<see cref="OpinionRules"/>, <see cref="BrainstormRules"/>, <see cref="HandOverRules"/>) resolve through this reader.
/// </remarks>
public static class WorkPolicies
{
    public const string DefaultSource = "default";
    public const string MachineSource = "machine";

    /// <summary>
    /// The policy standing for <paramref name="work"/> in a repository and its workspace (a brainstorm's, its workspace alone); with no
    /// repository, the workspace's own (COMPANYVIEW1c), and with neither, the default.
    /// </summary>
    /// <exception cref="ActorUnreadableException">Intake's actor does not read: its start holds with that sentence, as today.</exception>
    public static WorkPolicy Of(DriverConfig config, string work, string? repository, string? workspace)
    {
        switch (work)
        {
            case ParticipantWork.Help:
                var helpers = repository is { Length: > 0 } || workspace is { Length: > 0 }
                    ? HelperRules.Resolve(config, repository ?? "", workspace)
                    : new(HelperRule.Default, HelperRules.DefaultSource);
                return new(work, helpers.Rule.Mode, helpers.Rule.Agents, HelperJudge.PerAsker, HelperInstruction.DefaultMinutes, helpers.Source,
                    new HelpRequirement(helpers.Rule.SetUp));
            case ParticipantWork.Partner:
                return new(work, WorkConsent.NotARule, [], PartnerAdmission.OpenBound, PartnerInstruction.DefaultMinutes, DefaultSource, new PartnerRequirement(Measured: true));
            case ParticipantWork.Review:
                return Opinion(config, repository ?? "", workspace) switch
                {
                    { Rule: { IsNone: false } rule } opinion => new(work, WorkConsent.On, rule.Reviewers, null, rule.Bound, opinion.Source,
                        new ReviewRequirement(rule.Required, rule.Verify, rule.Recheck, MakerPasses: true)),
                    { } none => new(work, WorkConsent.Off, [], null, null, none.Source),
                    null => new(work, WorkConsent.Off, [], null, null, DefaultSource),
                };
            case ParticipantWork.Brainstorm:
                var (voices, from) = Brainstorms(config, workspace);
                return new(work, voices.Mode, voices.Agents, BrainstormKey.MostAgents, voices.Minutes.Request, from,
                    new BrainstormRequirement(BrainstormKey.MostAgents, ModeRead: false));
            case ParticipantWork.Intake:
                return Keyed(work, ActorChoices.FirstAgent("intake", config));
            case ParticipantWork.AskDaoris:
                return Keyed(work, config.HelperAdapter);
            default:
                var (handOver, source) = HandOver(config, repository ?? "", workspace);
                return new(work, WorkConsent.On, [], work == ParticipantWork.Chat ? null : config.Cap, null, source, new HandOverRequirement(handOver.Mode, source));
        }
    }

    /// <summary>The second-opinion rule standing for a repository: its own (a rule or none), else its workspace's, else null.</summary>
    public static ResolvedOpinion? Opinion(DriverConfig config, string repository, string? workspace) =>
        config.Opinions.TryGetValue(repository.Trim(), out var own)
            ? new ResolvedOpinion(own, CommitRules.RepositorySource)
            : config.WorkspaceOpinions.TryGetValue(RemoteTarget.Workspace(workspace), out var shared)
                ? new ResolvedOpinion(shared, CommitRules.WorkspaceSource)
                : null;

    /// <summary>The hand-over rule standing for a repository: its own, else its workspace's, else <see cref="HandOverRule.Default"/>.</summary>
    public static (HandOverRule Rule, string Source) HandOver(DriverConfig config, string repository, string? workspace) =>
        config.HandOver.TryGetValue(repository.Trim(), out var own)
            ? (own, CommitRules.RepositorySource)
            : config.WorkspaceHandOver.TryGetValue(RemoteTarget.Workspace(workspace), out var shared)
                ? (shared, CommitRules.WorkspaceSource)
                : (HandOverRule.Default, DefaultSource);

    /// <summary>The brainstorm rule standing for a question: its workspace's whole, else this machine's, else <see cref="BrainstormRule.Default"/>.</summary>
    public static (BrainstormRule Rule, string Source) Brainstorms(DriverConfig config, string? workspace) =>
        workspace?.Trim() is { Length: > 0 } named && config.WorkspaceBrainstorms.TryGetValue(named, out var shared)
            ? (shared, CommitRules.WorkspaceSource)
            : config.Brainstorms is { } machine
                ? (machine, MachineSource)
                : (BrainstormRule.Default, DefaultSource);

    /// <summary>A work one per place, on where its key names an agent; which agents run it is its role's staffing, never the policy's.</summary>
    private static WorkPolicy Keyed(string work, string? agent) =>
        agent is { Length: > 0 }
            ? new(work, WorkConsent.On, [], 1, null, MachineSource)
            : new(work, WorkConsent.Off, [], 1, null, DefaultSource);
}
