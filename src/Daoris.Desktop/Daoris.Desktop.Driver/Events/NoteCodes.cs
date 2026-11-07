using System.Globalization;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The coded line of a note (<see cref="Noted"/>, whose shape is the service's): a note composed from one of the driver's
/// codes (<see cref="NoteCode"/>) is the events' own member of it, so every caller keeps <c>Noted.Of</c> as spelled.
/// </summary>
public static class NotedCodes
{
    extension(Noted)
    {
        /// <summary>One coded line, its English the text.</summary>
        public static Noted Of(NoteCode code, string text, params (string Name, object? Value)[] values) =>
            new(text, [code.Part(text, values)]);
    }
}

/// <summary>
/// A code a line of a session's note is worded by: the page's key, and the names of the facts its line carries, each a
/// placeholder in its entry in both catalogues.
/// </summary>
/// <param name="Code">The code, as the record and the page spell it.</param>
/// <param name="Values">The facts its line carries.</param>
/// <param name="Key">The page's key where it is not <c>note.&lt;code&gt;</c>: a sentence the page already words.</param>
public sealed record NoteCode(string Code, IReadOnlyList<string> Values, string? Key = null)
{
    /// <summary>Where <c>why</c> is a reason's code, the reasons it may name, each worded with its own values.</summary>
    public NoteReasons? Why { get; init; }

    /// <summary>The page's key for this code.</summary>
    public string CatalogueKey => Key ?? $"note.{Code}";

    /// <summary>
    /// The part, with its values: each named among this code's, or among its reasons' own; a null value is not written, and
    /// the page then shows the part's text.
    /// </summary>
    /// <exception cref="ArgumentException">A value this code does not declare: its entry could not say it.</exception>
    public NotePart Part(string text, params (string Name, object? Value)[] values)
    {
        var kept = new List<KeyValuePair<string, object>>();
        foreach (var (name, value) in values)
        {
            if (!Values.Contains(name) && Why?.Carries(name) != true)
            {
                throw new ArgumentException($"`{Code}` declares no value `{name}`, so no entry of it can say it", nameof(values));
            }

            if (value is not null) kept.Add(new(name, value));
        }

        return new NotePart { Code = Code, Values = kept, Text = text };
    }
}

/// <summary>One reason a <c>why</c> may name, its own values, and its entry's key.</summary>
public sealed record NoteReason(string Code, IReadOnlyList<string> Values, string Key);

/// <summary>A family of reasons a <c>why</c> names.</summary>
public sealed record NoteReasons(IReadOnlyList<NoteReason> All)
{
    /// <summary>Whether a value is one a reason here carries beside its code.</summary>
    public bool Carries(string value) => All.Any(reason => reason.Values.Contains(value));
}

/// <summary>
/// Every code the driver writes a line of a session's note by. A twin (<c>.claude/knowledge/twins.md</c>): the page's
/// <c>note.json</c> in both languages words each, the service's <c>LedgerNoteCodes</c> declares the ledger's own, and the
/// web's <c>locales/note.test.ts</c> parses these declarations, one per line as written here, to hold the catalogue to them.
/// </summary>
/// <remarks>
/// <b>A site writes a part only through its code here</b>, and <c>NoteCodesTests</c> reads both catalogues, so a new line
/// fails until both languages word it. A comment on a code says what it means to a reader of the note.
/// </remarks>
public static class NoteCodes
{
    /// <summary>Reason codes for why a conversation went on as it did (<see cref="ContinueCodes"/>), each with its own values, as the page words them (<c>work.say.why.*</c>).</summary>
    public static readonly NoteReasons Continue = new(
    [
        new(ContinueCodes.Account, [], "work.say.why.account"),
        new(ContinueCodes.Door, ["from", "to"], "work.say.why.door"),
        new(ContinueCodes.Unkept, [], "work.say.why.unkept"),
        new(ContinueCodes.Tree, [], "work.say.why.tree"),
        new(ContinueCodes.Unable, ["door"], "work.say.why.unable"),
        new(ContinueCodes.Offered, [], "work.say.why.offered"),
        new(ContinueCodes.Gone, [], "work.say.why.gone"),
        new(ContinueCodes.Refused, [], "work.say.why.refused"),
        new(ContinueCodes.Ended, [], "work.say.why.ended"),
        new(ContinueCodes.Elsewhere, ["agent"], "work.say.why.elsewhere"),
        new(ContinueCodes.Teammate, [], "work.say.why.teammate"),
        new(ContinueCodes.Intake, [], "work.say.why.intake"),
        new(ContinueCodes.StoodDown, [], "work.say.why.stoodDown"),
        new(ContinueCodes.Started, [], "work.say.why.started"),
        new(ContinueCodes.Exited, [], "work.say.why.exited"),
        new(ContinueCodes.Resumed, [], "work.say.why.resumed"),
    ]);

    /// <summary>Why a cool-off lasts until then, as the page words it (<c>harness.cooling.why.*</c>).</summary>
    public static readonly NoteReasons Cooling = new(
    [
        new(CoolingWhy.Stated, [], "harness.cooling.why.stated"),
        new(CoolingWhy.Assumed, [], "harness.cooling.why.assumed"),
        new(CoolingWhy.Default, [], "harness.cooling.why.default"),
        new(CoolingWhy.NotBelieved, [], "harness.cooling.why.notBelieved"),
    ]);

    /// <summary>
    /// Why a start ran on its account, a step of the walk as a conversation's first line says it
    /// (<see cref="RotationWords.Steps"/>): each with its own values, the other account it names always <c>over</c>, and a
    /// moment, a percent or a window as a fact, never a clause.
    /// </summary>
    public static readonly NoteReasons Opening = new(
    [
        new(OpeningWhy.Cooling, ["agent", "over", "until", "cooling"], "note.opening.why.cooling"),
        new(OpeningWhy.Refused, ["agent", "over"], "note.opening.why.refused"),
        new(OpeningWhy.SignedOut, ["agent", "over"], "note.opening.why.signed-out"),
        new(OpeningWhy.Locked, ["agent", "over"], "note.opening.why.locked"),
        new(OpeningWhy.KeptSelf, [], "note.opening.why.kept-self"),
        new(OpeningWhy.KeptLast, ["over"], "note.opening.why.kept-last"),
        new(OpeningWhy.Kept, ["over"], "note.opening.why.kept"),
        new(OpeningWhy.NearReached, ["over", "window"], "note.opening.why.near-reached"),
        new(OpeningWhy.NearWord, ["over", "window"], "note.opening.why.near-word"),
        new(OpeningWhy.NearCredits, ["over"], "note.opening.why.near-credits"),
        new(OpeningWhy.NearUsed, ["over", "used", "window", "near"], "note.opening.why.near-used"),
        new(OpeningWhy.OverCurve, ["over", "window", "used", "curve"], "note.opening.why.over-curve"),
        new(OpeningWhy.Near, ["over"], "note.opening.why.near"),
        new(OpeningWhy.Room, ["over", "used", "against"], "note.opening.why.room"),
        new(OpeningWhy.Fewest, [], "note.opening.why.fewest"),
        new(OpeningWhy.Lapsing, ["at"], "note.opening.why.lapsing"),
        new(OpeningWhy.Lapses, ["window", "unused", "at", "seen"], "note.opening.why.lapses"),
        new(OpeningWhy.MostRoom, ["room", "window"], "note.opening.why.most-room"),
        new(OpeningWhy.Behind, ["used", "gone"], "note.opening.why.behind"),
        new(OpeningWhy.Ahead, ["over", "used", "gone"], "note.opening.why.ahead"),
        new(OpeningWhy.NotStarted, [], "note.opening.why.not-started"),
        new(OpeningWhy.LeastRecent, [], "note.opening.why.least-recent"),
        new(OpeningWhy.Default, ["workspace"], "note.opening.why.default"),
        new(OpeningWhy.DefaultHere, [], "note.opening.why.default-here"),
        new(OpeningWhy.First, ["workspace"], "note.opening.why.first"),
        new(OpeningWhy.FirstHere, [], "note.opening.why.first-here"),
        new(OpeningWhy.FirstOrder, [], "note.opening.why.first-order"),
    ]);

    /// <summary>
    /// Who spoke the words a resumed conversation goes on with (<see cref="Continuations.Opening"/>): a reason per way the
    /// English says it, with no value of its own.
    /// </summary>
    public static readonly NoteReasons Resumed = new(
    [
        new(ResumedWhy.Answer, [], "note.resumed.why.answer"),
        new(ResumedWhy.Words, [], "note.resumed.why.words"),
        new(ResumedWhy.Findings, [], "note.resumed.why.findings"),
        new(ResumedWhy.AnswerAndFindings, [], "note.resumed.why.answer-and-findings"),
        new(ResumedWhy.WordsAndFindings, [], "note.resumed.why.words-and-findings"),
        new(ResumedWhy.Helper, [], "note.resumed.why.helper"),
        new(ResumedWhy.WordsAndHelper, [], "note.resumed.why.words-and-helper"),
        new(ResumedWhy.Daoris, [], "note.resumed.why.daoris"),
        new(ResumedWhy.Supervisor, [], "note.resumed.why.supervisor"),
    ]);

    /// <summary>Why the person's words never reached the session (<see cref="UnreachedWhy"/>): the page's <c>note.words.why.*</c>.</summary>
    public static readonly NoteReasons Unreached = new(
    [
        new(UnreachedWhy.EndedFirst, [], "note.words.why.ended-first"),
        new(UnreachedWhy.Unkept, [], "note.words.why.unkept"),
        new(UnreachedWhy.Unmarked, [], "note.words.why.unmarked"),
    ]);

    /// <summary>Why a conversation was handed to another agent (<see cref="HandOverWhy"/>): the page's <c>note.handed.why.*</c>.</summary>
    public static readonly NoteReasons Handed = new(
    [
        new(HandOverWhy.Press, [], "note.handed.why.press"),
        new(HandOverWhy.Near, [], "note.handed.why.near"),
        new(HandOverWhy.Limit, [], "note.handed.why.limit"),
        new(HandOverWhy.Agent, [], "note.handed.why.agent"),
        new(HandOverWhy.Curve, [], "note.handed.why.curve"),
        new(HandOverWhy.Resume, [], "note.handed.why.resume"),
        new(HandOverWhy.CarryOn, [], "note.handed.why.carry-on"),
        new(HandOverWhy.Retry, [], "note.handed.why.retry"),
        new(HandOverWhy.NotOffered, [], "note.handed.why.not-offered"),
        new(HandOverWhy.Transient, [], "note.handed.why.transient"),
        new(HandOverWhy.Failed, [], "note.handed.why.failed"),
        new(HandOverWhy.Quiet, [], "note.handed.why.quiet"),
        new(HandOverWhy.Budget, [], "note.handed.why.budget"),
    ]);

    /// <summary>Why no helper answered a question (<see cref="HelperWhy"/>): the page's <c>note.helper.why.*</c>.</summary>
    public static readonly NoteReasons Helper = new(
    [
        new(HelperWhy.Off, [], "note.helper.why.off"),
        new(HelperWhy.AskerGone, [], "note.helper.why.asker-gone"),
        new(HelperWhy.AskerUnfit, [], "note.helper.why.asker-unfit"),
        new(HelperWhy.TooMany, [], "note.helper.why.too-many"),
        new(HelperWhy.Unreadable, [], "note.helper.why.unreadable"),
        new(HelperWhy.NotStarted, [], "note.helper.why.not-started"),
        new(HelperWhy.NoAnswer, [], "note.helper.why.no-answer"),
        new(HelperWhy.Conversation, [], "note.helper.why.conversation"),
        new(HelperWhy.CannotTake, [], "note.helper.why.cannot-take"),
        new(HelperWhy.Declined, [], "note.helper.why.declined"),
        new(HelperWhy.CaptureMerge, [], "note.helper.why.capture-merge"),
        new(HelperWhy.CaptureLink, [], "note.helper.why.capture-link"),
        new(HelperWhy.CaptureSubmodule, [], "note.helper.why.capture-submodule"),
        new(HelperWhy.CaptureUnreadable, [], "note.helper.why.capture-unreadable"),
        new(HelperWhy.CaptureBound, [], "note.helper.why.capture-bound"),
        new(HelperWhy.CaptureMoved, [], "note.helper.why.capture-moved"),
        new(HelperWhy.HandUncertain, [], "note.helper.why.hand-uncertain"),
        new(HelperWhy.Limited, [], "note.helper.why.limited"),
        new(HelperWhy.Transient, [], "note.helper.why.transient"),
        new(HelperWhy.NotSetUp, [], "note.helper.why.not-set-up"),
        new(HelperWhy.NoneSetUp, [], "note.helper.why.none-set-up"),
    ]);

    /// <summary>Why a piece was refused, held or uncertain (<see cref="PartnerWhy"/>): the page's <c>note.partner.why.*</c>.</summary>
    public static readonly NoteReasons Partner = new(
    [
        new(PartnerWhy.AskerUnfit, [], "note.partner.why.asker-unfit"),
        new(PartnerWhy.AskerGone, [], "note.partner.why.asker-gone"),
        new(PartnerWhy.Blank, [], "note.partner.why.blank"),
        new(PartnerWhy.PathOutside, [], "note.partner.why.path-outside"),
        new(PartnerWhy.NoCommit, [], "note.partner.why.no-commit"),
        new(PartnerWhy.BaseMissing, [], "note.partner.why.base-missing"),
        new(PartnerWhy.TooMany, [], "note.partner.why.too-many"),
        new(PartnerWhy.NotListed, [], "note.partner.why.not-listed"),
        new(PartnerWhy.NoConnector, [], "note.partner.why.no-connector"),
        new(PartnerWhy.NotMeasured, [], "note.partner.why.not-measured"),
        new(PartnerWhy.CopyFailed, [], "note.partner.why.copy-failed"),
        new(PartnerWhy.NoJob, [], "note.partner.why.no-job"),
        new(PartnerWhy.StateUnreadable, [], "note.partner.why.state-unreadable"),
        new(PartnerWhy.JobUnknown, [], "note.partner.why.job-unknown"),
        new(PartnerWhy.RefMoved, [], "note.partner.why.ref-moved"),
        new(PartnerWhy.ImportRefused, [], "note.partner.why.import-refused"),
        new(PartnerWhy.NoWork, [], "note.partner.why.no-work"),
        new(PartnerWhy.HandCut, [], "note.partner.why.hand-cut"),
    ]);

    /// <summary>Why an agent's ask to hand its session over came to nothing (<see cref="HandOverAskWhy"/>): the page's <c>note.handover.why.*</c>.</summary>
    public static readonly NoteReasons HandOver = new(
    [
        new(HandOverAskWhy.Off, [], "note.handover.why.off"),
        new(HandOverAskWhy.Declined, [], "note.handover.why.declined"),
        new(HandOverAskWhy.JustHanded, [], "note.handover.why.just-handed"),
        new(HandOverAskWhy.NoAgent, [], "note.handover.why.no-agent"),
        new(HandOverAskWhy.AskerGone, [], "note.handover.why.asker-gone"),
        new(HandOverAskWhy.AskerUnfit, [], "note.handover.why.asker-unfit"),
        new(HandOverAskWhy.Cannot, [], "note.handover.why.cannot"),
        new(HandOverAskWhy.QuestClosed, [], "note.handover.why.quest-closed"),
        new(HandOverAskWhy.QuestElsewhere, [], "note.handover.why.quest-elsewhere"),
        new(HandOverAskWhy.QuestReopened, [], "note.handover.why.quest-reopened"),
        new(HandOverAskWhy.NotLastRun, [], "note.handover.why.not-last-run"),
        new(HandOverAskWhy.UnknownAgent, [], "note.handover.why.unknown-agent"),
        new(HandOverAskWhy.SameAgent, [], "note.handover.why.same-agent"),
        new(HandOverAskWhy.NoDoor, [], "note.handover.why.no-door"),
        new(HandOverAskWhy.Running, [], "note.handover.why.running"),
        new(HandOverAskWhy.TreeGone, [], "note.handover.why.tree-gone"),
        new(HandOverAskWhy.Waiting, [], "note.handover.why.waiting"),
    ]);

    /// <summary>The classes the codes are declared in, by domain: a code is declared in one of them, never here.</summary>
    public static IReadOnlyList<Type> Domains { get; } = [typeof(EndNoteCodes), typeof(StartNoteCodes), typeof(HeldNoteCodes), typeof(ChatNoteCodes), typeof(AccountNoteCodes), typeof(PeerNoteCodes)];

    /// <summary>
    /// Every code in <see cref="Domains"/>, read off the declarations so none escapes the catalogue test. Read on first use, since
    /// a domain's initializer reads this class's reasons and this class's initializer must not read the domains back.
    /// </summary>
    public static IReadOnlyList<NoteCode> All =>
        field ??= [.. Domains.SelectMany(domain => domain.GetFields()).Where(code => code.FieldType == typeof(NoteCode)).Select(code => (NoteCode)code.GetValue(null)!)];

    /// <summary>
    /// A reason's values for a <c>why</c>: its code, then its own, read from what it says; <c>elsewhere</c>'s agent is
    /// the adapter the record ran on, which the page may say by its product's name.
    /// </summary>
    /// <param name="ranOn">The adapter the record ran on, or null where it names none.</param>
    public static (string Name, object? Value)[] Reason(IContinueReason why, string? ranOn = null)
    {
        var values = new List<(string Name, object? Value)> { ("why", why.Code) };
        values.AddRange(why.Values.Select(value => (value.Name, (object?)value.Value)));
        if (why.Code == ContinueCodes.Elsewhere && why.Values.All(value => value.Name != "agent")) values.Add(("agent", ranOn));
        return [.. values];
    }

    /// <summary>A moment as a value: ISO 8601 in UTC, to the second, which the page formats in the reader's language and zone.</summary>
    public static string Moment(DateTimeOffset at) =>
        at.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
}
