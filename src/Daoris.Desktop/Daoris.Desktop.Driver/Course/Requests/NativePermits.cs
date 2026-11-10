namespace Daoris.Driver;

/// <summary>What became of a native session's permission ask: the tool, whether it is allowed, why the hold ended, and its words.</summary>
/// <param name="Why">One of <see cref="PermissionWhy"/>'s, or <c>held</c> where the same ask was held already.</param>
public sealed record PermitAnswer(string Tool, bool Allowed, string Why, string Said);

/// <summary>
/// A native session's permission ask (<see cref="SessionMove.Permit"/>) held for the person as the protocol door holds a request: listed
/// in the registry the page's card and the allow supervisor read (<see cref="HeldPermissions"/>), offered as allow once or refuse, and
/// answered beside the request when the hold ends, whoever ends it. The script waiting on the answer is the harness's call held.
/// </summary>
public static class NativePermits
{
    /// <summary>The ask's two offers: this call's allow, and a refusal; never a standing allow, which is a rule's to make.</summary>
    public static readonly IReadOnlyList<PermissionChoice> Offered =
    [
        new("allow", "Allow once", PermissionChoice.AllowOnce),
        new("reject", "Refuse", PermissionChoice.RejectOnce),
    ];

    /// <summary>Hold <paramref name="request"/> for the person, then answer it beside itself; returns once it is answered.</summary>
    public static async Task HoldAsync(SessionRequests requests, HeldPermissions registry, SessionRequest request)
    {
        var tool = request.Operation ?? "";
        var held = registry.For(request.Session).Hold(request.Key ?? "", call: null, request.Text ?? tool, Offered, Kind(tool));
        if (held is null)
        {
            requests.Answer(request, new PermitAnswer(tool, false, "held", "The same ask is held already; this one was refused."));
            return;
        }

        var end = await held.ConfigureAwait(false);
        var allowed = end.Chosen?.Allows == true;
        requests.Answer(request, new PermitAnswer(tool, allowed, end.Why, end.Why switch
        {
            PermissionWhy.Answered => allowed ? "Allowed once." : "Refused.",
            PermissionWhy.Bound => $"Nobody answered within {(int)HeldPermissions.Bound.TotalMinutes} minutes, so Daoris refused it.",
            _ => "The session ended before anyone answered, so Daoris refused it.",
        }));
    }

    /// <summary>The call's kind as the protocol door's wire names one, so the card words it alike.</summary>
    private static string? Kind(string tool) => tool switch
    {
        "Bash" or "PowerShell" => "execute",
        "Edit" or "Write" or "MultiEdit" or "NotebookEdit" => "edit",
        "WebFetch" => "fetch",
        _ => null,
    };
}
