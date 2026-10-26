namespace Daoris.Driver;

/// <summary>Actor opening composition above the account shape, under its existing caller syntax (FOUNDATION1 mod-ix).</summary>
public static class ActorOpenings
{
    extension(RotatedOpening)
    {
        /// <summary>
        /// The opening note's actor line (D205 §4.6 item 1, MODELROLE1c2), where a later choice than the first ran: kept in the local
        /// conversation's record beside <i>Opened on</i>, which never syncs, so it may name accounts; never on the record's note. Nothing
        /// where the first choice ran, or nothing was passed.
        /// </summary>
        internal static void SayActor(ISessionEvents events, string sessionId, string actor, ActorEffective ran, IReadOnlyList<BuildPass> passed)
        {
            if (RotatedOpening.ActorLine(actor, ran, passed) is not { } line) return;
            events.Keep(sessionId, new SessionEvent { Kind = SessionEventKind.Note, Text = line.Note, Parts = line.Parts }, say: null);
        }

        /// <summary>
        /// <c>Ran as Build's choice 2, claude-code-acp · sonnet: choice 1, claude-code-acp · opus: …</c> (§4.6, §7.1), by code; null where the
        /// entry that ran is no later choice of an order, or nothing was passed.
        /// </summary>
        internal static Noted? ActorLine(string actor, ActorEffective ran, IReadOnlyList<BuildPass> passed)
        {
            if (!int.TryParse(ran.From, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var n) || n < 2
                || passed.Count == 0)
            {
                return null;
            }

            var entry = new BuildPass(ran, "", null).Named;
            var said = string.Join("; ", passed.Select(pass => pass.Said));
            return Noted.Of(
                NoteCodes.OpeningActor, $"Ran as {ActorWords.Name(actor)}'s choice {n}, {entry}: {said}.",
                ("actor", actor), ("n", n), ("entry", entry), ("passed", said));
        }
    }
}
