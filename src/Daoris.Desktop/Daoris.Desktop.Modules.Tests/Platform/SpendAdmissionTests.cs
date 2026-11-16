using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The census in the common admission: every supervisor kind's act is admitted only where its grants cover both the act and what it
/// spends, so an act whose spend no grant covers (a share's or an approval's destination) stays the owner role's whatever the act's grant.
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

    private StandingConfirmation Standing(params string[] acts)
    {
        var role = RoleKit.Supervisor(acts: acts, holder: RoleHolders.Agent);
        return new StandingConfirmation(_home, null, new StandingConfirmationTests.Placed(StandingPlace.Outside), walk: new StandingConfirmationTests.Walked())
        {
            Wider = () => RuleWiderPlaces.None,
            Slots = () => [RoleKit.Slot(role) with { Revision = 1 }],
            Risk = () => PlaceRiskBook.Unwritten.Apply(new PlaceRiskEdit { Workspace = "work", Risk = PlaceRisks.Low }),
        };
    }

    /// <summary>🔴 Granted the act alone, an act that spends a destination no grant covers is refused in the admission, naming the spend.</summary>
    [Fact]
    public void An_act_whose_spend_no_grant_covers_is_refused_in_the_common_admission()
    {
        var judge = Standing("landing.approve", "files.allow-share", "files.allow-run");

        var approve = judge.Admit(SupervisorKinds.Landing, Place, "approve");
        var share = judge.Admit(SupervisorKinds.Files, Place, "allow-share");

        Assert.Equal((SupervisorWhy.NotGranted, ActSpending.Destination), (approve.Why, approve.Missing));
        Assert.Equal((SupervisorWhy.NotGranted, ActSpending.Destination), (share.Why, share.Missing));
        // An act that spends nothing is admitted on its grant, and one not granted names the act.
        Assert.Null(judge.Admit(SupervisorKinds.Files, Place, "allow-run").Why);
        Assert.Equal("files.allow-copy", judge.Admit(SupervisorKinds.Files, Place, "allow-copy").Missing);
    }
}
