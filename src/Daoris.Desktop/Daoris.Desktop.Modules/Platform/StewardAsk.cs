using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// A proved holder's act outside the acts the person granted its role: it asks the person as its route's own press, a card in <i>Notifications</i> like any terminal's, and confirmed it runs as the
/// person's. Within the acts the role's standing confirms it; an unproved process never reaches here, refused before its act is read.
/// </summary>
/// <remarks>
/// Only an act whose row names its person's route (<see cref="IRoleAct.PersonRoute"/>) and whose form is exactly that act's asks: the
/// form is the route's payload as its verb sends it, so the card shows what the person's own press would and nothing of the proof.
/// </remarks>
internal static class StewardAsk
{
    /// <summary>The machine log's event for a role's act that asked the person, by codes: the role, the act and the route.</summary>
    public const string LogEvent = "role.asked";

    /// <summary>The judgement of an act the role's acts do not name: one that asks where its row names a person's route, else <c>not-granted</c>.</summary>
    public static RoleJudgement Outside(IRoleAct row, RoleActForm act, string holder, IReadOnlyList<string> roles) =>
        row.PersonRoute is { } route && row.IsForm(act.Form)
            ? new RoleJudgement(false, RoleWhy.NotGranted, act.Role, Holder: holder, Act: act.Act, Roles: roles) { Asks = (route, act.Form.Clone()) }
            : RoleJudgement.Refused(RoleWhy.NotGranted);

    /// <summary>The person's press an act that asks is sent on as: the same id and module, its route and its form.</summary>
    public static IpcRequest Press(IpcRequest request, RoleJudgement judged) =>
        new() { Id = request.Id, Module = request.Module, Type = judged.Asks!.Value.Route, Payload = judged.Asks.Value.Form };
}
