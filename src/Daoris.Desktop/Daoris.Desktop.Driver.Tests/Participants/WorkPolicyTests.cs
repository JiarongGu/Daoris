using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1q (D212; the foundation design §2.3): one policy shape, read from the keys <c>driver.json</c> has now, so no key moves. Each
/// work's resolved policy carries what every work has (its consent, the agents it permits, its concurrency, its minutes) and a typed
/// requirement of its own, each default written once in the reader's table: helpers <c>on</c>, hand-over <c>ask</c>, opinions none, a
/// partner's no rule yet and never <c>on</c>, the brainstorm's mode listed with no reader.
/// </summary>
public sealed class WorkPolicyTests
{
    private const string Set = """
        {
          "adapter": "claude-code",
          "intakeAdapter": "codex-acp",
          "helperAdapter": "claude-code",
          "helpers": { "web-app": { "mode": "ask", "agents": ["codex-acp"] } },
          "workspaceHelpers": { "work": { "mode": "off" } },
          "handOver": { "web-app": { "mode": "on" } },
          "workspaceHandOver": { "work": { "mode": "off" } },
          "opinions": { "web-app": { "reviewers": ["codex-acp"], "required": true, "verify": true, "minutes": 30, "recheck": false } },
          "workspaceOpinions": { "work": { "reviewers": ["gemini"] } },
          "brainstorms": { "mode": "ask", "agents": ["codex-acp", "claude-code"] },
          "workspaceBrainstorms": { "work": { "mode": "off" } }
        }
        """;

    private static readonly DriverConfig Nothing = DriverConfig.Parse("""{ "adapter": "claude-code" }""");
    private static readonly DriverConfig Written = DriverConfig.Parse(Set);

    /// <summary>Where nothing is set, each work's default is the reader's table's, and every policy names its work.</summary>
    [Theory]
    [InlineData(ParticipantWork.Help, WorkConsent.On, HelperJudge.PerAsker, HelperInstruction.DefaultMinutes)]
    [InlineData(ParticipantWork.Partner, WorkConsent.NotARule, PartnerAdmission.OpenBound, PartnerInstruction.DefaultMinutes)]
    [InlineData(ParticipantWork.Review, WorkConsent.Off, null, null)]
    [InlineData(ParticipantWork.Brainstorm, WorkConsent.Ask, BrainstormKey.MostAgents, 30)]
    [InlineData(ParticipantWork.Intake, WorkConsent.Off, 1, null)]
    [InlineData(ParticipantWork.AskDaoris, WorkConsent.Off, 1, null)]
    [InlineData(ParticipantWork.Chat, WorkConsent.On, null, null)]
    public void Nothing_set_reads_each_works_default(string work, string consent, int? concurrency, int? minutes)
    {
        var policy = WorkPolicies.Of(Nothing, work, "web-app", "work");
        Assert.Equal((work, consent, concurrency, minutes, WorkPolicies.DefaultSource), (policy.Work, policy.Consent, policy.Concurrency, policy.Minutes, policy.Source));
        Assert.Equal(consent is WorkConsent.On or WorkConsent.Ask, policy.Enabled);
    }

    /// <summary>A helper's default asks Daoris's own set-up agents alone (HELPERDEFAULT1); a rule the person set is read as written.</summary>
    [Fact]
    public void A_helpers_policy_is_the_helpers_rule_standing_for_the_repository()
    {
        Assert.Equal(new HelpRequirement(SetUp: true), WorkPolicies.Of(Nothing, ParticipantWork.Help, "web-app", null).Requirement);

        var own = WorkPolicies.Of(Written, ParticipantWork.Help, "web-app", "work");
        Assert.Equal((WorkConsent.Ask, "codex-acp", "repository"), (own.Consent, string.Join(",", own.Agents), own.Source));
        Assert.Equal(new HelpRequirement(SetUp: false), own.Requirement);
        var shared = WorkPolicies.Of(Written, ParticipantWork.Help, "reports", "work");
        Assert.Equal((WorkConsent.Off, false, "workspace"), (shared.Consent, shared.Enabled, shared.Source));
    }

    /// <summary>A partner's policy is no rule yet whatever is set (no `partners` key, COAGENT1d), admitted only where measured.</summary>
    [Fact]
    public void A_partners_policy_is_no_rule_yet_and_never_on()
    {
        var policy = WorkPolicies.Of(Written, ParticipantWork.Partner, "web-app", "work");
        Assert.Equal((WorkConsent.NotARule, false, new PartnerRequirement(Measured: true)), (policy.Consent, policy.Enabled, policy.Requirement));
    }

    /// <summary>An opinion's rule as written, its typed requirement beside it; a repository with no rule reads its workspace's.</summary>
    [Fact]
    public void A_reviews_policy_is_the_opinions_rule_with_its_requirement()
    {
        var own = WorkPolicies.Of(Written, ParticipantWork.Review, "web-app", "work");
        Assert.Equal((WorkConsent.On, "codex-acp", 30, "repository"), (own.Consent, string.Join(",", own.Agents), own.Minutes, own.Source));
        Assert.Equal(new ReviewRequirement(Required: true, Verify: true, Recheck: false, MakerPasses: true), own.Requirement);

        var shared = WorkPolicies.Of(Written, ParticipantWork.Review, "reports", "work");
        Assert.Equal((WorkConsent.On, "gemini", OpinionKey.DefaultMinutes, "workspace"), (shared.Consent, string.Join(",", shared.Agents), shared.Minutes, shared.Source));
    }

    /// <summary>The brainstorm's rule, the machine's or its workspace's; its mode is listed, and no start reads it yet.</summary>
    [Fact]
    public void A_brainstorms_policy_lists_its_mode_with_no_reader()
    {
        var machine = WorkPolicies.Of(Written, ParticipantWork.Brainstorm, null, "default");
        Assert.Equal((WorkConsent.Ask, "codex-acp,claude-code", "machine"), (machine.Consent, string.Join(",", machine.Agents), machine.Source));
        Assert.Equal(new BrainstormRequirement(FanOut: BrainstormKey.MostAgents, ModeRead: false), machine.Requirement);
        Assert.Equal((WorkConsent.Off, "workspace"), (WorkPolicies.Of(Written, ParticipantWork.Brainstorm, null, "work").Consent, WorkPolicies.Of(Written, ParticipantWork.Brainstorm, null, "work").Source));
    }

    /// <summary>A working session's policy carries the hand-over's consent, `ask` where nothing is set; driven work counts against the cap.</summary>
    [Theory]
    [InlineData(ParticipantWork.Build)]
    [InlineData(ParticipantWork.Design)]
    [InlineData(ParticipantWork.Chat)]
    public void A_working_sessions_policy_carries_the_hand_overs_consent(string work)
    {
        Assert.Equal(new HandOverRequirement(WorkConsent.Ask, WorkPolicies.DefaultSource), WorkPolicies.Of(Nothing, work, "web-app", "work").Requirement);
        Assert.Equal(new HandOverRequirement(WorkConsent.On, "repository"), WorkPolicies.Of(Written, work, "web-app", "work").Requirement);
        Assert.Equal(new HandOverRequirement(WorkConsent.Off, "workspace"), WorkPolicies.Of(Written, work, "reports", "work").Requirement);
        Assert.Equal(work == ParticipantWork.Chat ? null : Nothing.Cap, WorkPolicies.Of(Nothing, work, "web-app", "work").Concurrency);
    }

    /// <summary>Intake and Ask Daoris are on where their keys name an agent, the keys today's gates read.</summary>
    [Fact]
    public void Intake_and_ask_daoris_are_on_where_their_keys_name_an_agent()
    {
        Assert.Equal(WorkConsent.On, WorkPolicies.Of(Written, ParticipantWork.Intake, "web-app", "work").Consent);
        Assert.Equal(WorkConsent.On, WorkPolicies.Of(Written, ParticipantWork.AskDaoris, null, "work").Consent);
    }

    /// <summary>The three rules that resolve elsewhere resolve through this one reader, so no second reading of a key stands.</summary>
    [Fact]
    public void The_rules_resolve_through_the_one_reader()
    {
        Assert.Equal(WorkPolicies.Opinion(Written, "web-app", "work"), OpinionRules.Resolve(Written, "web-app", "work"));
        Assert.Equal(WorkPolicies.HandOver(Written, "reports", "work"), (HandOverRules.Resolve(Written, "reports", "work").Rule, HandOverRules.Resolve(Written, "reports", "work").Source));
        Assert.Equal(WorkPolicies.Brainstorms(Written, "work"), (BrainstormRules.Resolve(Written, "work").Rule, BrainstormRules.Resolve(Written, "work").Source));
    }
}
