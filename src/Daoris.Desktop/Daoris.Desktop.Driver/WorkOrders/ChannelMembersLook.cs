namespace Daoris.Driver;

/// <summary>
/// The look's end of an outside agent's membership (CHANNELJOIN1c; the event-channel design §5.5, the co-working area's rule 15): one
/// whose channel's claims have expired, its ask with no quest open and no part running, or that neither read nor posted for a day, is
/// ended as <c>expired</c> by the person's key. Judged against the record at the look, never a timer, so a quest opened again, or a
/// part started again, keeps the member. A membership ended keeps its record; the service journals its end, so <i>At work</i> redraws.
/// </summary>
/// <remarks>At a whole look only: the list is one small local read, and nothing here is urgent.</remarks>
[LookPass("members", LookPhase.After, "landing")]
internal sealed class ChannelMembersLook(ILookWiring wiring) : ILookPass
{
    /// <summary>How long a member may neither read nor post before the look ends its membership.</summary>
    public static readonly TimeSpan Quiet = TimeSpan.FromHours(24);

    public async Task RunAsync(LookRound round, CancellationToken ct)
    {
        if (round.Part != LookPart.Look) return;
        await wiring.Stages.TimeAsync("members", () => EndAsync(wiring.Service, wiring.Harnesses.Clock(), ct)).ConfigureAwait(false);
    }

    /// <summary>Whether the look ends <paramref name="member"/> at <paramref name="now"/>: its claims are spent, or it was last seen over a day ago.</summary>
    public static bool Ends(ChannelMemberView member, DateTimeOffset now) =>
        member.Left is null && (member.Spent || now - (member.Seen ?? member.Joined ?? now) >= Quiet);

    /// <summary>Ends each member the host lists that <see cref="Ends"/> names, and answers how many it ended. A host that does not answer ends none.</summary>
    public static async Task<int> EndAsync(ServiceClient service, DateTimeOffset now, CancellationToken ct)
    {
        IReadOnlyList<ChannelMemberView> held;
        try
        {
            held = await service.ChannelMembersAsync(null, null, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException)
        {
            return 0; // The next look asks again.
        }

        var ended = 0;
        foreach (var member in held.Where(each => Ends(each, now)))
        {
            try
            {
                if ((await service.EndMemberAsync(member.Id, expired: true, ct).ConfigureAwait(false)).Member is not null) ended++;
            }
            catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException)
            {
                // Left for the next look.
            }
        }

        return ended;
    }
}
