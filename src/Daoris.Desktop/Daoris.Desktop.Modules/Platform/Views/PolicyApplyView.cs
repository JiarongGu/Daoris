using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// <c>daoris settings risk apply</c> as the card shows it (<c>POLICY_APPLY</c>): the waiting proposal the press names, its edits as before and
/// after and the waits they open or release, read from the home's proposals at the ask. Refused before any card where the proposal is none this
/// home holds, is applied already, or carries another digest than the press names, so the owner never confirms a change they were not shown.
/// </summary>
public sealed class PolicyApplyView : IPressView
{
    public string Module => DriverModule.Name;

    public string Route => "POLICY_APPLY";

    public Task<PressViewed> ViewAsync(IPipeGroundWorld world, JsonElement? payload, CancellationToken cancellationToken) => ViewOf(world, payload);

    private static async Task<PressViewed> ViewOf(IPipeGroundWorld world, JsonElement? payload)
    {
        var id = PressPayload.Text(payload, "proposal");
        var digest = PressPayload.Text(payload, "digest");
        var machine = await world.MachineAsync(CancellationToken.None).ConfigureAwait(false);
        var changes = new PolicyChanges(machine.Facts.Home!, () => DateTimeOffset.UtcNow, new OwnerTurns(_ => []));
        if (changes.Proposals().FirstOrDefault(each => string.Equals(each.Id, id, StringComparison.Ordinal)) is not { } proposal)
        {
            return PressViewed.Refused($"No change to the risk policy `{id}` is held here — `daoris settings risk apply <proposal> <digest>` applies one Ask Daoris proposed.");
        }

        if (proposal.State != PolicyProposal.Pending) return PressViewed.Refused($"`{id}` was applied already, so there is nothing to apply.");
        if (!string.Equals(proposal.Digest, digest, StringComparison.OrdinalIgnoreCase))
        {
            return PressViewed.Refused($"`{digest}` is not the digest of `{id}`: the proposal on its card is another than the one named.");
        }

        List<PressSaid> also = [];
        foreach (var edit in proposal.Edits)
        {
            also.Add(edit.Class is null
                ? new PressSaid("policy-apply-remove", ("place", edit.Place), ("act", edit.Act), ("mode", edit.Mode ?? "either"))
                : new PressSaid("policy-apply-set", ("place", edit.Place), ("act", edit.Act), ("mode", edit.Mode ?? "either"), ("class", edit.Class), ("by", edit.By ?? "default")));
        }

        if (proposal.Shifts.Count == 0) also.Add(new PressSaid("policy-apply-none"));
        foreach (var shift in proposal.Shifts)
        {
            also.Add(new PressSaid($"policy-apply-{shift.Effect}", ("place", shift.Place), ("act", shift.Act), ("mode", shift.Mode), ("from", shift.From), ("to", shift.To)));
        }

        return PressViewed.Shown(new PressView(
            "policy-apply",
            new Dictionary<string, string> { ["proposal"] = proposal.Id, ["digest"] = proposal.Digest, ["revision"] = proposal.BaseRevision, ["session"] = proposal.Session },
            $"daoris settings risk apply {proposal.Id} {proposal.Digest}", also));
    }
}
