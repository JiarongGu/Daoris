using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>A steward's act is read against its grant again at its step, so an act taken back while it queued behind its gates never runs.</summary>
public sealed class RoleActGrantTests : Bridge
{
    /// <summary>An act un-ticked in the role's authority while it queued behind its gates is refused <c>not-granted</c> at its step and never runs.</summary>
    [Fact]
    public void ROLESREVIEW1c_an_act_taken_from_the_authority_between_admission_and_its_step_is_refused_not_granted()
    {
        var judge = new StandingConfirmation(Home, null, new StandingConfirmationTests.Placed(StandingPlace.Outside),
            walk: new StandingConfirmationTests.Walked()).Probing(new RoleKit.Probe());
        var (_, key) = RoleKit.Start(judge, RoleKit.Stewarding(share: null));
        var nonce = SupervisorProof.Nonce();
        var admitted = RoleKit.JudgeAct(judge, RoleKit.Act(key!, nonce, RoleKit.GenerationOf(judge, AppointedRoles.Steward)), nonce);
        Assert.True(admitted.Covered, admitted.Why);

        judge.Staff(RoleKit.Steward([]));

        Assert.Equal(RoleWhy.NotGranted, judge.RunAct(admitted.ActId!));
        Assert.Equal(RoleActStates.Cancelled, judge.ActState(admitted.ActId!));
    }

    /// <summary>A catalogue that cannot be read at an act's step withdraws no grant: the act says <c>role-changed</c> as admission does and runs once it reads.</summary>
    [Fact]
    public void ROLESREVIEW2p_an_unreadable_catalogue_at_the_step_is_role_changed_and_withdraws_nothing()
    {
        var judge = new StandingConfirmation(Home, null, new StandingConfirmationTests.Placed(StandingPlace.Outside),
            walk: new StandingConfirmationTests.Walked()).Probing(new RoleKit.Probe());
        var (_, key) = RoleKit.Start(judge, RoleKit.Stewarding(share: null));
        var nonce = SupervisorProof.Nonce();
        var admitted = RoleKit.JudgeAct(judge, RoleKit.Act(key!, nonce, RoleKit.GenerationOf(judge, AppointedRoles.Steward)), nonce);
        var readable = judge.Slots;

        judge.Slots = () => null;
        Assert.Equal(SupervisorWhy.RoleChanged, judge.RunAct(admitted.ActId!));
        Assert.Equal(RoleActStates.Admitted, judge.ActState(admitted.ActId!));

        judge.Slots = readable;
        Assert.Null(judge.RunAct(admitted.ActId!));
    }
}
