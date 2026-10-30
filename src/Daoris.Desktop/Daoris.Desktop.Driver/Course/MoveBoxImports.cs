using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;

namespace Daoris.Driver;

/// <summary>What one pass over a home's boxes of one kind came to (<see cref="IMoveBoxImport.Import"/>).</summary>
/// <param name="Imported">The moves written from a box this pass, each box set aside.</param>
/// <param name="Waiting">
/// The records whose box is still to be imported because the pass was held, the start's confirmation not settled or the store not taking
/// it: the course reads each one's old part as <c>Never</c>, and no take-up, go-on or sweep acts on it meanwhile (§13.5).
/// </param>
/// <param name="Behind">The records whose box waits behind the record's standing move, imported once that move ends; read as <c>Never</c> too.</param>
/// <param name="Unread">The boxes that do not read as a move: left where they are, as evidence, holding nothing.</param>
/// <param name="Held">Why the pass was held, or null.</param>
public sealed record MoveBoxPass(IReadOnlyList<MoveRecord> Imported, IReadOnlyList<string> Waiting, IReadOnlyList<string> Behind, int Unread, string? Held)
{
    /// <summary>Nothing to import: no box in the home.</summary>
    public static readonly MoveBoxPass None = new([], [], [], 0, null);

    /// <summary>The records whose old part the course reads as <c>Never</c> until their box is imported.</summary>
    public IEnumerable<string> Never => Waiting.Concat(Behind);
}

/// <summary>
/// The import of one kind of box an older build writes, into the move record (FOUNDATION1f-i part 2; §3.3, §13.5). Called by the take-up
/// (<see cref="MoveTakeUp"/>) before anything else at each come-up and look; the limit box's is handed it by the conversations' holder since
/// FOUNDATION1f-vi part a, and the hand-over's, a conversation's box, since FOUNDATION1f-v part a.
/// </summary>
public interface IMoveBoxImport
{
    /// <summary>The box's name: <c>limit-move</c> or <c>hand-over</c>.</summary>
    string Box { get; }

    /// <summary>One pass over the home's boxes of this kind, once the start's confirmation is settled.</summary>
    MoveBoxPass Import();

    /// <summary>The records with a box of this kind in place, in the order of their ids.</summary>
    IReadOnlyList<string> Pending();
}

/// <summary>
/// The import of a box an older build writes (FOUNDATION1f-i part 2; §3.3, §13.5): each read once, its bytes taken whole and its move read from
/// those bytes, written as the store's own row with the import's receipt in one transaction, and only then set aside as
/// <c>&lt;box&gt;.imported</c>, never deleted, and only where the file is still the bytes read; a box rewritten meanwhile is a later write, which
/// the next pass reads.
/// </summary>
/// <remarks>
/// <para><b>Set aside, never left as a backup.</b> A box is a move an older build <i>acts on</i>, so it is imported only once the start's
/// confirmation is settled, and its old part is read as <c>Never</c> until it is; a build rolled back before it confirmed meets every box as it
/// was (§13.5).</para>
/// <para><b>Once.</b> A box whose very bytes an import already holds was imported, and a crash stopped short of setting it aside: only its
/// set-aside is left to do. A box whose record has a standing move waits behind it, left in place, and is imported once that move ends; there
/// are never two records of one move (§3.3).</para>
/// <para><b>Called by one production site per box, in the row that retires its reader</b>: importing a box sets it aside from today's readers,
/// so the limit box is imported since FOUNDATION1f-vi part a and a conversation's hand-over box since FOUNDATION1f-v part a, each move read
/// from the store; a quest session's hand-over box waits for part c (<c>MoveStoreCensusTests</c>).</para>
/// </remarks>
public abstract class MoveBoxImport(string home, MoveStore store) : IMoveBoxImport
{
    /// <summary>What an imported box's name gains when it is set aside.</summary>
    public const string SetAsideSuffix = ".imported";

    private static readonly ConcurrentDictionary<string, byte> Said = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public abstract string Box { get; }

    /// <summary>What follows a session's id in its box's name.</summary>
    protected abstract string Suffix { get; }

    /// <summary>Called between a box's import and its set-aside: a test's way to write the box again meanwhile.</summary>
    internal Action<string>? BeforeSetAside { get; set; }

    /// <summary>Where a session's box is kept.</summary>
    public string PathOf(string session) => DaorisHome.Entry(home, HomeEntry.Sessions, session + Suffix);

    /// <summary>Where it is set aside once imported.</summary>
    public string SetAsideOf(string session) => PathOf(session) + SetAsideSuffix;

    /// <summary>The move a box's text keeps, or null where it does not read as one.</summary>
    protected abstract MoveFromBox? Read(string session, string text);

    /// <summary>
    /// Whether this importer owns the session's box. A kind of box another reader owns, the quest session's hand-over box (the driver's look
    /// takes it), is left where it is, imported by nobody and read as nobody's <c>Never</c>.
    /// </summary>
    protected virtual bool Owns(string session) => true;

    /// <inheritdoc />
    public IReadOnlyList<string> Pending() =>
        [.. HandOverFiles.Leaves(DaorisHome.Entry(home, HomeEntry.Sessions), "*" + Suffix)
            .Select(leaf => leaf[..^Suffix.Length])
            .Where(SessionEventText.IsLocalId)
            .Where(Owns)
            .Order(StringComparer.Ordinal)];

    /// <inheritdoc />
    public MoveBoxPass Import()
    {
        var found = Pending();
        if (found.Count == 0) return MoveBoxPass.None;
        if (store.Settled() is { Is: false } waiting) return Held(found, waiting.Said ?? "the start's confirmation is not settled");
        var imported = new List<MoveRecord>();
        var behind = new List<string>();
        var unread = 0;
        for (var at = 0; at < found.Count; at++)
        {
            var session = found[at];
            try
            {
                switch (ImportOne(session, out var move, out var why))
                {
                    case Outcome.Done:
                        if (move is not null) imported.Add(move);
                        break;
                    case Outcome.Unread:
                        unread++;
                        break;
                    case Outcome.Behind:
                        behind.Add(session);
                        break;
                    default:
                        return Held(found.Skip(at).ToList(), why!) with { Imported = imported, Behind = behind, Unread = unread };
                }
            }
            catch (Exception error) when (error is DesktopStoreException or SqliteException)
            {
                return Held(found.Skip(at).ToList(), error.Message) with { Imported = imported, Behind = behind, Unread = unread };
            }
        }

        return new MoveBoxPass(imported, [], behind, unread, null);
    }

    private enum Outcome
    {
        Done,
        Unread,
        Behind,
        Held,
    }

    private Outcome ImportOne(string session, out MoveRecord? move, out string? why)
    {
        move = null;
        why = null;
        var path = PathOf(session);
        byte[] bytes;
        try
        {
            bytes = AtomicFile.ReadBytes(path);
        }
        catch (FileNotFoundException)
        {
            // Taken away since it was listed, by today's reader or another importer: nothing to do.
            return Outcome.Done;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            why = $"{Box} box of {session} could not be read: {error.Message}";
            return Outcome.Held;
        }

        if (Read(session, Text(bytes)) is not { } box) return Outcome.Unread;
        var sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var receipt = Path.GetRelativePath(home, path).Replace('\\', '/');
        if (store.LastImport(receipt) is not { } last || last.Import.Sha256 != sha)
        {
            var outcome = store.Import(new StoreImport(MoveStore.Family, receipt, bytes.Length, sha, 1 + box.Words.Count, ImportClass.SetAside), box);
            if (outcome.Standing is not null) return Outcome.Behind;
            if (outcome.Move is null)
            {
                why = outcome.Held ?? "the store would not take it";
                return Outcome.Held;
            }

            move = outcome.Move;
        }

        BeforeSetAside?.Invoke(path);
        SetAside(path, sha);
        return Outcome.Done;
    }

    /// <summary>The box moved to its set-aside name, unless it changed since it was read: that one is a later write, read at the next pass.</summary>
    private static void SetAside(string path, string sha)
    {
        try
        {
            if (Convert.ToHexString(SHA256.HashData(AtomicFile.ReadBytes(path))).ToLowerInvariant() != sha) return;
            File.Move(path, path + SetAsideSuffix, overwrite: true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Left where it is: its receipt says it was imported, and the next pass sets it aside.
        }
    }

    /// <summary>A file's bytes as text, a byte-order mark dropped, as the home's files are read.</summary>
    private static string Text(byte[] bytes)
    {
        var preamble = Encoding.UTF8.Preamble;
        return bytes.AsSpan().StartsWith(preamble) ? Encoding.UTF8.GetString(bytes, preamble.Length, bytes.Length - preamble.Length) : Encoding.UTF8.GetString(bytes);
    }

    /// <summary>A held pass, its reason logged once for the process and home, since the look asks again until it is settled.</summary>
    private MoveBoxPass Held(IReadOnlyList<string> waiting, string why)
    {
        if (Said.TryAdd(home + "\n" + Box + "\n" + why, 0))
        {
            using var log = new MachineLog(home, "driver");
            log.Warn("moves.import-held", ("box", Box), ("why", why));
        }

        return new MoveBoxPass([], waiting, [], 0, why);
    }
}

/// <summary>
/// The ended limit move's box, <c>&lt;s&gt;.limit-move.json</c> (LIMITRESUME1c, <see cref="LimitBoxes"/>), imported as the move it keeps
/// (FOUNDATION1f-i part 2): a conversation's brief from an ended record, at <c>old-ended</c> before its new part opened and at <c>opened</c>
/// once the box names it, with the words it said under the box's own key; its recipe's bound and its refused turns' bound the box's, and its
/// why the one its message is composed by.
/// </summary>
/// <param name="home">The home whose boxes these are.</param>
/// <param name="store">The move record the boxes are imported into, under its run.</param>
/// <param name="limited">
/// Whether a limit held the record within its first <c>n</c> events, as <c>LimitHeld</c> reads them: the move's why is <c>limit</c> where one
/// did, else <c>retry</c> for a move to another account of its own agent (the box's <c>account</c>), else <c>resume</c>, its own conversation
/// proven lost or the restart's (the ended limit move's own composition, <c>LimitMove.ComposedWhy</c>).
/// </param>
public sealed class LimitBoxImport(string home, MoveStore store, Func<string, int, bool> limited) : MoveBoxImport(home, store)
{
    /// <inheritdoc />
    public override string Box => "limit-move";

    /// <inheritdoc />
    protected override string Suffix => LimitBoxes.Suffix;

    /// <summary>The key the box's words go under, which today's take-up says its message again by.</summary>
    public static string KeyOf(string session) => $"limit-move/{session}";

    /// <inheritdoc />
    protected override MoveFromBox? Read(string session, string text)
    {
        if (LimitBoxes.Parse(text) is not { } box) return null;
        var why = limited(session, box.Through) ? HandOverWhy.Limit : box.Why == ContinueWhy.Account ? HandOverWhy.Retry : HandOverWhy.Resume;
        var decision = new MoveDecision(session, MoveExecutors.Conversation, MoveHows.Brief, why, HandOverBy.Daoris)
        {
            FromEnded = true,
            RecipeWhy = why,
            RecipeBound = box.Before,
            RefusedBound = box.Through,
        };
        if (box.To is not { } to) return new MoveFromBox(decision, MoveRecordPhase.OldEnded);

        // Only a box whose open moved the words (`moved`) names them on its destination. One an older host's open wrote moved none, its words
        // still on the old record: the move is opened with no word, as the live move records such an open, since naming them as the
        // destination's would read them gone from it, left (FOUNDATION1f-vi part a).
        return new MoveFromBox(decision, MoveRecordPhase.Opened)
        {
            Destination = to,
            Words = box.Moved
                ? [.. box.Said.Distinct(StringComparer.Ordinal).Select((word, at) => new MoveRecordWord(word, null, $"{KeyOf(session)}/{word}", at))]
                : [],
        };
    }
}

/// <summary>
/// The conversation's hand-over box, <c>&lt;s&gt;.hand-over.json</c> (MIXAGENT1a-i, <see cref="HandOverBox"/>), imported as the move it keeps
/// (FOUNDATION1f-i part 2): a hand-over decided and not yet taken, to the agent and account it names, by its why and its asker, at the moment
/// it was asked.
/// </summary>
/// <param name="home">The home whose boxes these are.</param>
/// <param name="store">The move record the boxes are imported into, under its run.</param>
/// <param name="conversation">
/// Whether a session is a conversation, read from its record's kind (FOUNDATION1f-v part a): the same file name holds a quest session's box,
/// which the driver's look takes until part c, so only a conversation's is imported. False for a record the ledger could not be asked about.
/// </param>
public sealed class HandOverBoxImport(string home, MoveStore store, Func<string, bool> conversation) : MoveBoxImport(home, store)
{
    /// <inheritdoc />
    public override string Box => "hand-over";

    /// <inheritdoc />
    protected override string Suffix => HandOverBox.Suffix;

    /// <inheritdoc />
    protected override bool Owns(string session) => conversation(session);

    /// <inheritdoc />
    protected override MoveFromBox? Read(string session, string text) =>
        HandOverFiles.ParseBox(text) is { } ask
            ? new MoveFromBox(
                new MoveDecision(session, MoveExecutors.Conversation, MoveHows.Brief, ask.Why, ask.AskedBy) { Agent = ask.Agent, Account = ask.Account },
                MoveRecordPhase.Decided)
            {
                DecidedAt = ask.At.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            }
            : null;
}
