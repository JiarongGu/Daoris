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
        /// A start opened on the account count <paramref name="number"/> (STAFFPOLICY1c): its row of <c>role_starts</c> written by the ledger, and,
        /// where the role's policy chose it by anything but the order, the opening note says why, in the local record only. Nothing is said for a
        /// role on <i>First that can run</i>, which says what it said before.
        /// </summary>
        internal static void SayStaff(ISessionEvents events, StaffLedger? ledger, string sessionId, long number, string actor)
        {
            if (ledger?.Opened(number, sessionId) is not { Reservation: not null } bound || (bound.Rule == StaffRules.Order && bound.By == StaffBy.First)) return;
            var line = Noted.Of(NoteCodes.OpeningChosen, $"{ActorWords.Name(actor)}'s policy chose this: {bound.Said}.", ("actor", ActorWords.Name(actor)), ("said", bound.Said));
            events.Keep(sessionId, new SessionEvent { Kind = SessionEventKind.Note, Text = line.Note, Parts = line.Parts }, say: null);
        }

        /// <summary>
        /// Where a quest marked design started (FOUNDATION1x, MODELROLE1l), whose order it ran on: the role taking <c>design</c> by its name,
        /// or Build while none does; kept in the local conversation's record beside the opening, like the actor line.
        /// </summary>
        internal static void SayDesign(ISessionEvents events, string sessionId, string role)
        {
            var line = RotatedOpening.DesignLine(role);
            events.Keep(sessionId, new SessionEvent { Kind = SessionEventKind.Note, Text = line.Note, Parts = line.Parts }, say: null);
        }

        /// <summary>
        /// Where a start moved to this account because the one before refused it for its sign-in (LIMITPICK2): kept beside <i>Opened on</i> in the
        /// local record, which never syncs, so it may name the account.
        /// </summary>
        internal static void SayMovedOff(ISessionEvents events, string sessionId, string account)
        {
            var line = Noted.Of(NoteCodes.OpeningMovedOff, $"The same start moved here: `{account}` refused it for its sign-in.", ("account", account));
            events.Keep(sessionId, new SessionEvent { Kind = SessionEventKind.Note, Text = line.Note, Parts = line.Parts }, say: null);
        }

        /// <summary>
        /// Where a start moved to its walk's next answer because its agent refused the model its choice named (MODELREFUSE1): kept beside
        /// <i>Opened on</i> in the local record, naming the agent and the model but no account, since the account was not at fault.
        /// </summary>
        internal static void SayMovedOffModel(ISessionEvents events, string sessionId, string agent, string model)
        {
            var line = Noted.Of(
                NoteCodes.OpeningMovedOffModel, $"The same start moved here: `{agent}` refused `{model}` on the account it ran on.",
                ("agent", agent), ("model", model));
            events.Keep(sessionId, new SessionEvent { Kind = SessionEventKind.Note, Text = line.Note, Parts = line.Parts }, say: null);
        }

        internal static Noted DesignLine(string role) => Noted.Of(NoteCodes.OpeningDesign, $"Design work: it ran on {role}'s order.", ("role", role));

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
