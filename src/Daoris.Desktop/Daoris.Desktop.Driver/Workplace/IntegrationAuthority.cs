namespace Daoris.Driver;

/// <summary>
/// Whether a steward act's appointment still stands (the integration design §8, D209), asked at promotion: its generation live and the owner
/// having acted on none of the act's repository, quest or session since (D209's owner-set). The registry that knows an appointment lives in the
/// modules, above the driver, so the driver reads it through this seam, which the modules install when the role is active (STEWARDROLE1d).
/// </summary>
internal interface IIntegrationAppointments
{
    /// <summary>Null where the appointment of <paramref name="request"/>'s steward act stands; else the words that say why it does not.</summary>
    Task<string?> LapsedAsync(IntegrationRequest request, CancellationToken ct);
}

/// <summary>
/// The seam's default: nothing in this build reads an appointment's liveness yet, since no part has activated the steward's role, so a
/// steward's request cannot be shown to stand and is held at promotion. An authority that cannot be read is no authority (§8).
/// </summary>
internal sealed class IntegrationAppointments : IIntegrationAppointments
{
    public static IntegrationAppointments Unread { get; } = new();

    public Task<string?> LapsedAsync(IntegrationRequest request, CancellationToken ct) =>
        Task.FromResult<string?>(
            $"nothing in this build reads whether the appointment behind its act `{request.Requester.Act}` (generation {request.Requester.Generation}) still stands, "
            + "so the act cannot be shown to stand");
}

/// <summary>
/// The requester's authority, judged again at promotion (INTEGRATE1e part e4b, D215; the integration design §5.2 step 8, §8): the door judged it
/// at admission, and the line moves only after it is read again, since the steward's appointment may have been revoked, the owner may have acted,
/// or the rule may have changed since the request was kept. A request that fails it is held and the candidate formed again without it.
/// </summary>
/// <remarks>
/// <para>A <b>person's</b> request (the owner's card, or an Accept) is the owner's own authority and is not withdrawn by any revoke, so it stands.
/// <b>Daoris's sweep</b> stands while the line still carries a policy and the place's workflow's <c>integrate</c> (<see cref="PlaceIntegrate"/>)
/// still says <c>request: done</c>. A <b>steward's</b> request stands while the line still carries a policy and the step says
/// <c>request: steward</c>, its act names its appointment's generation, and the seam (<see cref="IIntegrationAppointments"/>) says the
/// appointment and generation are live and the owner has acted on nothing of its repository, quest or session since.</para>
/// </remarks>
internal static class IntegrationAuthority
{
    /// <summary>Null where <paramref name="request"/>'s requester still has the authority it asked with; else the words that say why it does not.</summary>
    /// <param name="home">The Daoris home whose saved workflows' place's <c>integrate</c> step says who requests now.</param>
    public static async Task<string?> LapsedAsync(
        IntegrationRequest request, DriverConfig config, string home, IIntegrationAppointments appointments, CancellationToken ct)
    {
        var requester = request.Requester;
        if (requester.Kind == IntegrationRequester.Person) return null;

        // An automatic land's merge stands as a press does: its delivery judged the standing authority again just before its first effect.
        if (requester.Door == IntegrationDoors.Landing) return null;

        // INTEGRATE1g: a node's request is its landing's acceptance (§17.4). The person's Accept stands as their card does; a version's own step,
        // its gate proof naming the version, stands whatever the place follows now, since a bound version is never edited; an automatic landing
        // with no version behind it stands while the place's workflow says `request: done`, as the sweep's does.
        if (requester.Door == IntegrationDoors.Node && request.Proof is { } proof
            && (proof.AcceptedBy == AcceptedBy.Person || proof.Workflow is not null))
        {
            return null;
        }

        if (config.LinePolicyOf(request.Repository, request.Workspace) is null)
        {
            return $"`{request.Repository}`'s line no longer carries a policy, so a request made under it is no longer admitted";
        }

        var asks = WorkplaceWorkflows.Current.IntegrateOf(home, config,request.Repository, request.Workspace).Request;
        switch (requester.Kind)
        {
            case IntegrationRequester.Daoris:
                return asks == LandingIntegrate.Done
                    ? null
                    : $"the place's workflow's `integrate` now says `request: {asks}`, and the come-up's sweep requests only where it says `request: done`";
            case IntegrationRequester.Steward:
                if (asks != LandingIntegrate.Steward)
                {
                    return $"the place's workflow's `integrate` now says `request: {asks}`, and the steward requests only where it says `request: steward`";
                }

                if (string.IsNullOrWhiteSpace(requester.Act) || requester.Generation is null)
                {
                    return "the request names no act and appointment generation to read again";
                }

                return await appointments.LapsedAsync(request, ct).ConfigureAwait(false);
            default:
                return $"`{requester.Kind}` is no requester this build knows";
        }
    }
}
