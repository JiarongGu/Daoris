using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The census in the common admission: every supervisor kind's act is admitted only where its grants cover the act, and what it spends
/// is an act of the risk policy judged at its place: a share's destination <c>publish.outside</c>, an approval's <c>release.start</c>. Where
/// the policy passes the spend the act's grant admits it; where it classes it the owner's it stays the owner role's whatever the grant.
/// </summary>
public sealed class SpendAdmissionTests : IDisposable
{
    private static readonly RolePlace Place = new("work", "engine");

    private readonly string _home = Path.Combine(AppContext.BaseDirectory, "spend-admission", Guid.NewGuid().ToString("N")[..8]);

    public SpendAdmissionTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private StandingConfirmation Standing(string config, params string[] acts) => StandingAs(config, AppointedRoles.Supervisor, acts);

    private StandingConfirmation StandingAs(string config, string id, params string[] acts)
    {
        var role = RoleKit.Supervisor(id, acts: acts, holder: RoleHolders.Agent);
        return new StandingConfirmation(_home, null, new StandingConfirmationTests.Placed(StandingPlace.Outside), walk: new StandingConfirmationTests.Walked())
        {
            Wider = () => RuleWiderPlaces.None,
            Slots = () => [RoleKit.Slot(role) with { Revision = 1 }],
            Risk = () => PlaceRiskBook.Unwritten.Apply(new PlaceRiskEdit { Workspace = "work", Risk = PlaceRisks.Low }),
            Config = () => DriverConfig.Parse(config),
        };
    }

    /// <summary>🔴 Granted the act alone, an act whose spend the policy classes the owner's is refused in the admission, naming the spend.</summary>
    [Fact]
    public void An_act_whose_spend_the_policy_classes_the_owners_is_refused_in_the_common_admission()
    {
        const string owners = """{ "risk": { "workspace/work": [ { "kind": "release.start", "class": "owner" }, { "kind": "publish.outside", "class": "owner" } ] } }""";
        var judge = Standing(owners, "landing.approve", "files.allow-share", "files.allow-run");

        var approve = judge.Admit(SupervisorKinds.Landing, Place, "approve");
        var share = judge.Admit(SupervisorKinds.Files, Place, "allow-share");

        Assert.Equal((SupervisorWhy.NotGranted, ActSpending.Destination), (approve.Why, approve.Missing));
        Assert.Equal((SupervisorWhy.NotGranted, ActSpending.Destination), (share.Why, share.Missing));
        // An act that spends nothing is admitted on its grant, and one not granted names the act.
        Assert.Null(judge.Admit(SupervisorKinds.Files, Place, "allow-run").Why);
        Assert.Equal("files.allow-copy", judge.Admit(SupervisorKinds.Files, Place, "allow-copy").Missing);
    }

    [Fact]
    public void With_nothing_configured_the_seed_passes_a_destination_and_the_acts_grant_admits_it()
    {
        var judge = Standing("{}", "landing.approve", "files.allow-share");

        Assert.Null(judge.Admit(SupervisorKinds.Landing, Place, "approve").Why);
        Assert.Null(judge.Admit(SupervisorKinds.Files, Place, "allow-share").Why);
    }

    /// <summary>🔴 A row gating the spend on a role it names holds it for that role alone: another role granted the act does not take it.</summary>
    [Fact]
    public void A_row_naming_its_answering_role_admits_that_role_and_refuses_another_granted_the_act()
    {
        const string named = """{ "risk": { "workspace/work": [ { "kind": "release.start", "class": "role", "by": "release-manager" } ] } }""";

        var other = StandingAs(named, "reports-supervisor", "landing.approve").Admit(SupervisorKinds.Landing, Place, "approve");
        var its = StandingAs(named, "release-manager", "landing.approve").Admit(SupervisorKinds.Landing, Place, "approve");

        Assert.Equal((SupervisorWhy.NotGranted, ActSpending.Destination), (other.Why, other.Missing));
        Assert.Null(its.Why);
        // With no role named, the gate's default role is the place's supervisor, which any supervisor deciding the kind holds.
        Assert.Null(StandingAs("""{ "risk": { "workspace/work": [ { "kind": "release.start", "class": "role" } ] } }""", "reports-supervisor", "landing.approve")
            .Admit(SupervisorKinds.Landing, Place, "approve").Why);
    }

    /// <summary>🔴 A driver.json that does not read is the owner's: a spend and a go-ahead are refused, and nothing throws.</summary>
    [Fact]
    public void An_unreadable_policy_reads_every_spend_and_go_ahead_as_the_owners_and_never_throws()
    {
        var judge = Standing("{}", "landing.approve");
        judge.Config = () => throw new DriverConfigUnreadableException(DaorisHome.Entry(_home, HomeEntry.DriverConfig), "it does not parse");

        Assert.Equal(ActSpending.Destination, judge.Admit(SupervisorKinds.Landing, Place, "approve").Missing);
        Assert.Equal(RiskVerdict.OwnerOutcome, judge.GoAheadOutcome(Place, "write", "staging", "supervisor"));
        Assert.Equal(SupervisorWhy.PolicyOwner, GoAheadAuthority.OwnersOwn("write", judge.GoAheadOutcome(Place, "write", "staging", "supervisor")));
    }
}
