using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// The admission side of a steward's act an allow supervisor allowed once: which allow supervisor answers an act's place, never one whose
/// holder is the steward's own, and the act itself admitted as the steward's, once, with the owner's marks read first as for a granted act.
/// </summary>
internal sealed class StewardAllowedAct(StandingConfirmation owner, StandingState state)
{
    /// <summary>The act that allows a steward's act, as the allow kind's grants name it.</summary>
    public const string Act = "steward";

    /// <summary>
    /// The allow supervisor's hold for the act's place, refused <c>not-granted</c> where its terminal's holder is the steward's holder,
    /// else the admission as <see cref="StandingConfirmation.Admit"/> answers it.
    /// </summary>
    public DecisionAdmitted Admit(RolePlace place, string? stewardHolder)
    {
        var admitted = owner.Admit(SupervisorKinds.Allow, place, Act);
        if (admitted.Why is not null || stewardHolder is null) return admitted;
        if (owner.Supervising(SupervisorKinds.Allow, place).Role is not { Hold: { Terminal: true } } effective) return admitted;
        lock (state.Gate)
        {
            return state.HoldOf(effective.Id)?.Holder == stewardHolder ? DecisionAdmitted.Refused(SupervisorWhy.NotGranted) : admitted;
        }
    }

    /// <summary>
    /// The act admitted as the steward's own: its hold and holder still live, its form still its row's, the owner not having acted on its
    /// target this run (<c>owner-set</c>), and its id drawn here. Refused with why, nothing admitted.
    /// </summary>
    public RoleJudgement Allowed(RoleJudgement asked, JsonElement? payload, string decision)
    {
        if (RoleActForm.Read(payload) is not { } form || asked.Generation is not { } generation || asked.Holder is not { } holder || asked.Role is not { } role)
        {
            return RoleJudgement.Refused(SupervisorWhy.Form);
        }

        RoleJudgement judged;
        lock (state.Gate)
        {
            if (!state.Holds.Any(each => each.Generation == generation && each.Kind == role) || !state.Holders.ContainsKey(holder))
            {
                return RoleJudgement.Refused(SupervisorWhy.RoleChanged);
            }

            if (!owner.TableOf(role).TryGetValue(form.Act, out var row)) return RoleJudgement.Refused(RoleWhy.NotRole);
            if (!row.IsForm(form.Form)) return RoleJudgement.Refused(SupervisorWhy.Form);
            var target = new RoleScope(row.TargetKind, row.Target(form.Form));
            if (state.OwnerHolds(target)) return RoleJudgement.Refused(RoleWhy.OwnerSet);
            var admitted = state.Acts.Admit(role, form.Act, generation, holder, target.Name, target.Kind);
            state.Answers[generation] = state.Answers.GetValueOrDefault(generation) + 1;
            judged = new RoleJudgement(true, null, role, null, generation, holder, form.Act, admitted, asked.Roles);
        }

        owner.Log.Info("role.act", ("role", role), ("act", form.Act), ("outcome", "admitted"), ("by", "steward"), ("decision", decision));
        return judged;
    }
}
