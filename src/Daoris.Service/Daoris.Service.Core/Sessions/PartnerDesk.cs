namespace Daoris.Knowledge;

/// <summary>What the driver names for a partner's record: it is the driver's to say which piece, where it writes, as which agent and account.</summary>
/// <param name="Lead">The session that asked for the piece.</param>
/// <param name="Piece">The ask's id under the home's <c>partners</c> box.</param>
/// <param name="Adapter">The agent that writes it.</param>
/// <param name="Tree">Its clone with no remote, never the lead's tree.</param>
public sealed record PartnerOpen(string Lead, string Piece, string Adapter, string Tree)
{
    public string? Account { get; init; }

    public string? HarnessVersion { get; init; }

    /// <summary>The ask's exact base commit, which the clone stands at.</summary>
    public string? BaseCommit { get; init; }
}

/// <summary>
/// The judgement over a partner's record (COAGENT1b, D199 points 1 and 2; the co-agent design §2.3 step 5, §7.3): one implementation for
/// the local host's door the driver calls, as <see cref="HelperDesk"/> is for a helper's. It opens a partner's record, a chat on no quest
/// that serves the lead and carries the piece, with an entry of its own, in its clone. The open is idempotent by the piece's id: a record
/// already open for the piece is answered as this one, so a restart between the open and the state that names it opens nothing twice.
/// Whether the rule allows the piece, the choice of agent, the reservation and the copy are the driver's.
/// </summary>
/// <param name="box">The partners' box the ask is read from, or null for the home's: an open names an ask on file for its lead.</param>
/// <param name="local">Whether this is a local host's desk; a shared host's refuses.</param>
public sealed class PartnerDesk(SessionStore sessions, PartnerBox? box = null, bool local = true)
{
    private const string Shared = "A partner writes a clone this machine made, and is opened only on this machine.";

    public async Task<HelperOutcome> OpenAsync(PartnerOpen open, DateTimeOffset now, CancellationToken ct = default)
    {
        if (!local) return new(HelperRefusal.Shared, Shared);
        if (string.IsNullOrWhiteSpace(open.Lead) || string.IsNullOrWhiteSpace(open.Piece) || string.IsNullOrWhiteSpace(open.Adapter)
            || Trees.Normalize(open.Tree) is not { } tree)
        {
            return Refused(HelperRefusal.BadShape, "A partner's record needs its lead, its piece, its agent and its own clone");
        }

        var piece = open.Piece.Trim();
        var ask = (box ?? PartnerBox.FromEnvironment()).ReadAsk(piece);
        if (ask is null) return Refused(HelperRefusal.NotFound, $"There is no piece `{piece}` on this machine");
        if (!string.Equals(ask.Asker, open.Lead.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return Refused(HelperRefusal.BadShape, $"Piece `{piece}` was asked by session `{ask.Asker}`, not `{open.Lead.Trim()}`");
        }

        return await sessions.ExclusiveAsync(async inside =>
        {
            // Idempotent by the piece (the design §7.2's opening row): the record already open for it is this one.
            if (await sessions.ForPieceAsync(piece, inside).ConfigureAwait(false) is { } already)
            {
                return new HelperOutcome(HelperRefusal.None, $"Session `{already.Id}` writes piece `{piece}` already, for session `{already.Serves}`.")
                {
                    Session = already,
                };
            }

            var lead = await sessions.FindAsync(open.Lead.Trim(), inside).ConfigureAwait(false);
            if (lead is not { Origin: null })
            {
                return Refused(HelperRefusal.NotFound, $"There is no session `{open.Lead.Trim()}` of this machine's to work for");
            }

            string? wrong =
                lead.Serves is { } serves ? $"Session `{lead.Id}` serves session `{serves}` itself, and cannot lead a piece"
                : lead.Opinion is { } reads ? $"Session `{lead.Id}` reads another session's work for second opinion `{reads}`, and cannot lead a piece"
                : lead.Ask is { } intake ? $"Session `{lead.Id}` is the intake for ask `#{intake}`, and cannot lead a piece"
                : string.Equals(Trees.Normalize(lead.Tree), tree, StringComparison.Ordinal)
                    ? $"`{tree}` is session `{lead.Id}`'s own tree: a partner writes a clone of its own"
                : null;
            if (wrong is not null) return Refused(HelperRefusal.BadShape, wrong);

            if (await sessions.ActiveForAsync(lead.Repository, tree, inside).ConfigureAwait(false) is { } holder)
            {
                return Refused(HelperRefusal.Busy, $"`{lead.Repository}`'s tree `{tree}` already has an active session, `{holder.Id}`: a partner's clone is its own");
            }

            var partner = await sessions.CreateAsync(
                    quest: null, lead.Repository, open.Adapter.Trim(), now, lead.Workspace, SessionKind.Chat,
                    harnessVersion: open.HarnessVersion, profile: open.Account, tree: tree, baseCommit: open.BaseCommit?.ToLowerInvariant(),
                    ct: inside, serves: lead.Id, ownEntry: true, piece: piece)
                .ConfigureAwait(false);
            return new HelperOutcome(
                HelperRefusal.None,
                $"Session `{partner.Id}` writes piece `{piece}` of `{lead.Repository}` in a clone of its own, for session `{lead.Id}`.")
            {
                Session = partner,
            };
        }, ct).ConfigureAwait(false);
    }

    private static HelperOutcome Refused(HelperRefusal refusal, string why) => new(refusal, $"{why}. Nothing was kept.");
}
