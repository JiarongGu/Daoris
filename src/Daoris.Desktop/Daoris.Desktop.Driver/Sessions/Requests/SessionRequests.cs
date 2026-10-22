using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// The moves a request may carry (D126 §7.1): the person's three on a live or parked session, and their words to any session
/// of this machine's (MSG1e).
/// </summary>
public static class SessionMove
{
    /// <summary>End it now, as the person's stop: its quest is then held here until *Try again* (SESSUX1b).</summary>
    public const string Stop = "stop";

    /// <summary>Finish one that waits on the person, with their note.</summary>
    public const string Finish = "finish";

    /// <summary>Decline one that waits on the person, with their reason.</summary>
    public const string Decline = "decline";

    /// <summary>
    /// Words said to a session from a terminal (MSG1e, D137 §5.2): held at its door where it runs, or kept on its record for it
    /// to go on with. Answered beside the request (<see cref="SessionRequests.Answer"/>), since no state move says where they went.
    /// </summary>
    public const string Say = "say";

    /// <summary>
    /// A word waiting on a parked or ended record taken back, or with the request's text reworded, from a terminal (SAIDBACK1c,
    /// D137 §2.4): taken by the loop that holds the home, which keeps the withdrawn note in its own record of the conversation.
    /// Keyed and answered as a say is.
    /// </summary>
    public const string TakeBack = "take-back";

    /// <summary>
    /// Ask Daoris's housekeeping tool asking for one operation (HELPMODE1c2a, D183's note): written by the connector, the agent's
    /// and never the person's, taken by the loop running the conversation, judged there at the call, and answered beside the
    /// request in <see cref="HousekeepingAnswer"/>'s shape. Keyed as a say is.
    /// </summary>
    public const string Housekeep = "housekeep";

    /// <summary>
    /// Ask Daoris's chosen-settings tool asking to change one setting (HELPMODE1c3, D183's note): written by the connector as a
    /// housekeeping request is, taken by the loop running the conversation, judged and applied there at the call, and answered beside
    /// the request in <see cref="SettingAnswer"/>'s shape. Keyed as a say is.
    /// </summary>
    public const string Set = "set";

    /// <summary>
    /// Ask Daoris's checkout tool asking to read one checkout (HELPMODE1c4, D183's note): a file, a folder, <c>git status</c> or the
    /// branch list, by a repository and a path relative to its checkout; written by the connector as a setting request is, taken by
    /// the loop running the conversation, judged there at the call, and answered beside the request in <see cref="CheckoutAnswer"/>'s
    /// shape, what was read beside its words. Keyed as a say is.
    /// </summary>
    public const string Read = "read";

    public static bool Known(string move) => move is Stop or Finish or Decline or Say or TakeBack or Housekeep or Set or Read;
}

/// <summary>Who asked, as the machine log's <c>door</c> names a door: a word, never a sentence.</summary>
public static class RequestDoor
{
    /// <summary><c>daoris-driver sessions</c>.</summary>
    public const string Terminal = "terminal";

    /// <summary>A pause stopping what of its work another process runs (PAUSE1b, D132 §2.1): <see cref="WorkPausing"/>.</summary>
    public const string Pause = "pause";

    /// <summary>An abandon stopping or ending what of its work another process runs (PAUSE1d, D132 §3.4): <see cref="WorkAbandoning"/>.</summary>
    public const string Abandon = "abandon";

    /// <summary>Ask Daoris's housekeeping tool (HELPMODE1c2a): the agent's, through its connector, never the person's.</summary>
    public const string Help = "help";
}

/// <summary>One move asked of one session, for whichever loop on the home runs it.</summary>
/// <param name="Move">One of <see cref="SessionMove"/>.</param>
/// <param name="At">When it was asked: a request nobody took for <see cref="SessionRequests.Lifetime"/> is dropped.</param>
public sealed record SessionRequest(string Session, string Move, DateTimeOffset At)
{
    /// <summary>
    /// A finish's note or a decline's reason, the person's words; for a stop, none, or a pause's words for the record
    /// (PAUSE1b, design §4.1).
    /// </summary>
    public string? Note { get; init; }

    /// <summary>
    /// A pause's or an abandon's words for the record with their codes (LANG1a, D142 point 2), beside <see cref="Note"/>; null
    /// for the person's own words, and for a request an older door wrote, whose note is then carried whole.
    /// </summary>
    public IReadOnlyList<NotePart>? NoteParts { get; init; }

    /// <summary>
    /// That the record waited on the person when it was asked: its stop is then its card's, the record moved by the ledger
    /// once the process is let go, as <c>RESOLVE_SESSION</c>'s <c>stopped</c> moves it (D126 §3.3).
    /// </summary>
    public bool Parked { get; init; }

    /// <summary>Who asked, one of <see cref="RequestDoor"/>.</summary>
    public string By { get; init; } = RequestDoor.Terminal;

    /// <summary>A say's words, as the person said them (MSG1e); null for a move.</summary>
    public string? Text { get; init; }

    /// <summary>A say's files, by the names they are kept under for the session (<see cref="ChatFiles"/>), never a path.</summary>
    public IReadOnlyList<string> Files { get; init; } = [];

    /// <summary>
    /// A say's own name (MSG1e): <c>&lt;id&gt;.&lt;key&gt;.json</c>, so two said at once both wait and a stop of the same session is
    /// not replaced by them, and its answer is found beside it. Null for a move, which is one per session.
    /// </summary>
    public string? Key { get; init; }

    /// <summary>A say's *Send now* (STEER2c): what <c>SESSION_INPUT</c> sends as <c>now: true</c>. An older request has none, and is read as not now.</summary>
    public bool Now { get; init; }

    /// <summary>
    /// A say's urgency (COWORK1a, D188 point 1): <c>ordinary</c>, <c>urgent</c> or <c>now</c>, as <c>SESSION_INPUT</c> sends it
    /// as <c>level</c>. A field beside <see cref="Now"/>, which a say at <c>now</c> still writes for an older loop; a request
    /// with none reads as before, <c>now</c> by <see cref="Now"/> and urgent otherwise (<see cref="MessageLevels.Asked"/>).
    /// </summary>
    public string? Level { get; init; }

    /// <summary>The level the say asked for, read as <see cref="Level"/> says; a request from before by its <see cref="Now"/>.</summary>
    public MessageLevel Asked => MessageLevels.Asked(Level, Now);

    /// <summary>A take-back's word: the id it waits under on the record (SAIDBACK1c); null for any other move.</summary>
    public string? Word { get; init; }

    /// <summary>
    /// A housekeeping request's operation, one of <see cref="ConfiguredAccess.HousekeepingBuilt"/> (HELPMODE1c2a); a setting request's
    /// setting, one of <see cref="ConfiguredAccess.SettingsBuilt"/> (HELPMODE1c3); a checkout read's, one of
    /// <see cref="HelpCheckouts.Operations"/> (HELPMODE1c4); null for any other move.
    /// </summary>
    public string? Operation { get; init; }

    /// <summary>
    /// A housekeeping request's repository, by its registered name, null for a look at every one; a language's repository, for a
    /// setting request; the checkout's repository, for a checkout read; null for any other move.
    /// </summary>
    public string? Repository { get; init; }

    /// <summary>A housekeeping request's branches, by name, never a path; none for a look, and for any other move.</summary>
    public IReadOnlyList<string> Branches { get; init; } = [];

    /// <summary>A setting request's value, as the setting's door spells it (HELPMODE1c3); null for any other move.</summary>
    public string? Value { get; init; }

    /// <summary>A language's workspace, for a setting request (HELPMODE1c3); null for any other.</summary>
    public string? Workspace { get; init; }

    /// <summary>
    /// A checkout read's path, relative to the checkout of <see cref="Repository"/> as the agent wrote it (HELPMODE1c4); null for its
    /// top, and for any other move. Never trusted: <see cref="HelpCheckouts"/> judges it at the call.
    /// </summary>
    public string? Path { get; init; }
}

/// <summary>
/// What became of a checkout read (HELPMODE1c4), written beside it for the tool that asked, in the chosen settings' answer's fields and
/// one more: the operation, whether it was read, a code where it was not, the words the agent reads, and <see cref="Text"/>, what was
/// read. A twin: the service's <c>CheckoutBox</c> reads <c>said</c> and <c>text</c> by the same names.
/// </summary>
public sealed record CheckoutAnswer(string Operation, bool Done, string? Code, string Said)
{
    /// <summary>A file's text, at most <see cref="HelpCheckouts.Bound"/> of it; a folder's entries, one a line; git's answer. Null where nothing was read.</summary>
    public string? Text { get; init; }

    /// <summary>No service answered, so no record could be read.</summary>
    public const string NoService = "no-service";

    /// <summary>The session is not an Ask Daoris conversation of this machine's.</summary>
    public const string NotHelp = "not-help";

    /// <summary>The request named no operation of the three, no repository, or a path where its operation takes none.</summary>
    public const string Named = "named";

    /// <summary>No repository of that name is registered here.</summary>
    public const string Unknown = "unknown";

    /// <summary>The repository is registered in another workspace than the conversation's.</summary>
    public const string Outside = "outside";

    /// <summary>The repository has no checkout on this machine.</summary>
    public const string NoCheckout = "no-checkout";

    /// <summary>The repository's own reading by agents outside it is switched off (D107), so it is no checkout of the grant's.</summary>
    public const string NotReadable = "not-readable";

    /// <summary>The person turned the checkout off in configured access.</summary>
    public const string NotGranted = "not-granted";

    /// <summary>Not handed at this conversation's start: it holds from its next turn.</summary>
    public const string NotHanded = "not-handed";

    /// <summary>
    /// A path that is no path inside the checkout: <c>..</c>, an absolute path, a drive or a stream, a wildcard, git's own folder, or
    /// a link leading out of it.
    /// </summary>
    public const string Escapes = "escapes";

    /// <summary>Git ignores it, or a folder it lies in (<c>local/</c>, build output, secrets kept ignored).</summary>
    public const string Ignored = "ignored";

    /// <summary>No file or folder by that path in the checkout.</summary>
    public const string Missing = "missing";

    /// <summary>The read failed on its way: git could not say what it ignores, or the disk or git refused.</summary>
    public const string Failed = "failed";

    /// <summary>A refusal of <paramref name="operation"/> with its code, its words ending that nothing was read.</summary>
    public static CheckoutAnswer Refusal(string operation, string code, string said) => new(operation, false, code, $"{said} Nothing was read.");
}

/// <summary>
/// What became of a setting request (HELPMODE1c3), written beside it for the tool that asked, in housekeeping's answer's fields: the
/// setting, whether it was changed, a code where it was not, and the words the agent reads. A twin: the service's <c>SettingsBox</c>
/// reads <c>said</c> by the same name.
/// </summary>
public sealed record SettingAnswer(string Setting, bool Done, string? Code, string Said)
{
    /// <summary>No service answered, so no record could be read.</summary>
    public const string NoService = "no-service";

    /// <summary>The session is not an Ask Daoris conversation of this machine's.</summary>
    public const string NotHelp = "not-help";

    /// <summary>The person has not chosen the setting in configured access.</summary>
    public const string NotGranted = "not-granted";

    /// <summary>Chosen after this conversation's start, which was not handed it: it holds from its next turn.</summary>
    public const string NotHanded = "not-handed";

    /// <summary>A language for a repository or a workspace outside the conversation's.</summary>
    public const string Outside = "outside";

    /// <summary>The request named no setting of the five.</summary>
    public const string Named = "named";

    /// <summary>The setting's own door refused the value, in its words.</summary>
    public const string Refused = "refused";

    /// <summary>The change failed on its way: the driver's file or the service.</summary>
    public const string Failed = "failed";

    /// <summary>A refusal of <paramref name="setting"/> with its code, its words ending that nothing was changed.</summary>
    public static SettingAnswer Refusal(string setting, string code, string said) => new(setting, false, code, $"{said} Nothing was changed.");
}

/// <summary>
/// What became of a housekeeping request (HELPMODE1c2a), written beside it for the tool that asked: the operation, whether it was
/// carried out, a code where it was not, and the words the agent reads. A twin: the service's <c>HousekeepingBox</c> reads
/// <c>said</c> by the same name.
/// </summary>
public sealed record HousekeepingAnswer(string Operation, bool Done, string? Code, string Said)
{
    /// <summary>No service answered, so no record could be read.</summary>
    public const string NoService = "no-service";

    /// <summary>The session is not an Ask Daoris conversation of this machine's.</summary>
    public const string NotHelp = "not-help";

    /// <summary>The person has not chosen the operation in configured access.</summary>
    public const string NotGranted = "not-granted";

    /// <summary>Chosen after this conversation's start, which was not handed it: it holds from the next start.</summary>
    public const string NotHanded = "not-handed";

    /// <summary>The repository is registered in another workspace than the conversation's.</summary>
    public const string Outside = "outside";

    /// <summary>No repository of that name is registered here.</summary>
    public const string Unknown = "unknown";

    /// <summary>The repository has no checkout on this machine.</summary>
    public const string NoCheckout = "no-checkout";

    /// <summary>The request did not name what its operation acts on, or named it as no branch of Daoris's.</summary>
    public const string Named = "named";

    /// <summary>The discard's judge kept the branch.</summary>
    public const string Kept = "kept";

    /// <summary>The judgement failed on its way: git, the service or the disk.</summary>
    public const string Failed = "failed";

    /// <summary>A refusal of <paramref name="operation"/> with its code, its words ending that nothing was done.</summary>
    public static HousekeepingAnswer Refused(string operation, string code, string said) =>
        new(operation, false, code, $"{said} Nothing was done.");
}

/// <summary>
/// The requests every loop on the home watches (SESSUX1g, D126 §7.1): <c>&lt;home&gt;/sessions/requests/&lt;id&gt;.json</c>, a
/// move the person asked of a session another process on this machine runs, written atomically by the door that asked
/// and taken, once, by the loop whose own registry runs it.
/// </summary>
/// <remarks>
/// <para><b>Why a folder.</b> A terminal's verb holds no process; the session's process belongs to whichever loop started
/// it, the desktop's or a headless one, and only that loop can end it as the person's stop or let it go before the ledger
/// moves its record. A file under the home is what every Daoris process on this machine already shares, as the process
/// markers are (<see cref="SessionProcesses"/>), and it needs no port and no service.</para>
///
/// <para><b>Taken once.</b> A loop takes a request by renaming it, and reads and removes it only where it is still under the
/// name it was moved to: on Windows a rename another loop had already begun moves it on from under the first (REQTAKE1), so
/// the loop whose name it ends under has it. Two loops honouring at once act on it once, and never neither. The door that
/// asked withdraws it when nothing took it in time, so a late loop never acts on an
/// answer the person was already told did not come; it says withdrawn only once nothing else holds what it moved aside, since
/// a take's rename begun a moment before moves it on from there (REQTAKE2). A request nobody took for <see cref="Lifetime"/> is dropped by any
/// loop that looks, so one left by a door that died does not wait for ever.</para>
///
/// <para><b>Built for PAUSE1b too</b> (D132 §2.1, §6.1): a pause stops what of an ask's work another process runs through
/// the same request, a stop with <see cref="RequestDoor.Pause"/>, and records the stop as its own itself.</para>
///
/// <para><b>And for a terminal's words</b> (MSG1e, D137 §5.2): a say is named by its own key beside the session's id, and the
/// loop that takes it writes where the words stand beside it (<see cref="Answer"/>), since a say moves no record the asker could
/// watch. An answer nobody read is dropped with the requests nobody took.</para>
///
/// <para><b>And for Ask Daoris's housekeeping tools</b> (HELPMODE1c2a, D183's note): the connector, which reaches no judge of its own,
/// writes a keyed <see cref="SessionMove.Housekeep"/> for its conversation's session and waits for the answer beside it
/// (<see cref="HousekeepingAnswer"/>), withdrawing it where nobody took it in time. The connector is the twin writer.</para>
/// </remarks>
public sealed partial class SessionRequests(string home)
{
    /// <summary>How long a request nobody took stands: the asking door waits ten seconds (§7.1), and a minute is ample.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(1);

    private const string Taking = ".taking";

    /// <summary>
    /// How often a take reads what it moved aside before putting it back (REQTAKE1): what refuses it is another loop's rename,
    /// for the moment it moves it, so twenty tries ten milliseconds apart is ample.
    /// </summary>
    private const int ReadTries = 20;

    /// <summary>
    /// How often a withdrawal opens what it moved aside before putting it back (REQTAKE2): what holds it is a take's rename for
    /// the moment it moves it, or a reader, so a second of tries ten milliseconds apart is ample, and is spent only while held.
    /// </summary>
    private const int WithdrawTries = 100;

    /// <summary>A say's answer's ending, beside it: what the loop writes, so the folder's watch never hears it (FABRIC-EVENT2).</summary>
    internal const string Answered = ".answer.json";

    public string Folder => DaorisHome.Entry(home, HomeEntry.Requests);

    /// <summary>A fresh key for a say (MSG1e): enough of a random id that two said at once never share one.</summary>
    public static string NewKey() => Guid.NewGuid().ToString("N")[..12];

    /// <summary>Where a request is kept: a move under its session's id, a say under that and its own key.</summary>
    public string PathOf(SessionRequest request) => request.Key is null ? PathOf(request.Session) : Beside(request, ".json");

    /// <summary>Where a say's answer is kept, beside it, for the door that asked.</summary>
    private string AnswerPath(SessionRequest request) => Beside(request, Answered);

    private string Beside(SessionRequest request, string suffix)
    {
        PathOf(request.Session);
        if (request.Key is not { } key || !KeyShape().IsMatch(key))
        {
            throw new DriverException($"`{request.Key}` is not a request's key, so no request can be named by it.");
        }

        return Path.Combine(Folder, $"{request.Session}.{key}{suffix}");
    }

    [GeneratedRegex("^[0-9a-f]{6,32}$")]
    private static partial Regex KeyShape();

    /// <summary>Where a session's request is kept; refused for anything that is not a session id of this machine's.</summary>
    public string PathOf(string session)
    {
        if (!SessionEventText.IsLocalId(session))
        {
            throw new DriverException($"`{session}` is not a session id of this machine's, so no request can name it.");
        }

        return Path.Combine(Folder, $"{session}.json");
    }

    /// <summary>
    /// Ask: the request written beside, then renamed, replacing one for the same session that nobody took yet; a say,
    /// named by its own key, replaces nothing.
    /// </summary>
    public void Write(SessionRequest request)
    {
        var path = PathOf(request);
        if (!SessionMove.Known(request.Move)) throw new DriverException($"`{request.Move}` is not a move a request carries.");
        Directory.CreateDirectory(Folder);
        AtomicFile.WriteText(path, ToJson(request));
    }

    /// <summary>
    /// The requests waiting, oldest first. One older than <see cref="Lifetime"/>, or one that does not read and is as old,
    /// is dropped as this looks; so is a say's answer nobody read, which is never opened here, since its asker reads it.
    /// </summary>
    public IReadOnlyList<SessionRequest> Pending(DateTimeOffset now)
    {
        if (!Directory.Exists(Folder)) return [];
        var pending = new List<SessionRequest>();
        foreach (var path in Directory.EnumerateFiles(Folder, "*.json"))
        {
            // 🔴 A look every second that opened the answer its asker was reading made that read fail, or its removal (MSG1e).
            if (path.EndsWith(Answered, StringComparison.Ordinal))
            {
                if (File.Exists(path) && now - new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero) > Lifetime) Remove(path);
                continue;
            }

            var request = Read(path);
            var written = File.Exists(path) ? new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero) : now;
            if (now - (request?.At ?? written) > Lifetime)
            {
                Remove(path);
                continue;
            }

            if (request is not null) pending.Add(request);
        }

        return [.. pending.OrderBy(request => request.At)];
    }

    /// <summary>Whether a request for this session still waits to be taken.</summary>
    public bool IsPending(string session) => File.Exists(PathOf(session));

    /// <summary>
    /// Take a session's request, once: renamed aside, read and removed. Null when another loop took it first or its asker
    /// withdrew it, or when what was moved aside could not be read, which is put back for the next look (REQTAKE1).
    /// </summary>
    public SessionRequest? Take(string session) => TakeAt(PathOf(session));

    /// <summary>Take this request, once, by its own name: a say by its key. Null when another loop took it first or its asker withdrew it.</summary>
    public SessionRequest? Take(SessionRequest request) => TakeAt(PathOf(request));

    private static SessionRequest? TakeAt(string path)
    {
        var taking = path + "." + Guid.NewGuid().ToString("N")[..8] + Taking;
        try
        {
            File.Move(path, taking);
        }
        catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException or IOException or UnauthorizedAccessException)
        {
            return null;
        }

        // 🔴 REQTAKE1 (FIX-LOG 2026-10-16): on Windows another loop's rename opens the file, letting it be moved, a moment before
        // it moves it. This rename succeeds under that handle, the handle then moves the file on from this name, and while it is
        // open a read is refused. A take that removed what it could not read lost the request unread, and the other loop's move
        // then found it deleted. So a refused read is tried again, a file gone was moved on by the loop that has it, and only
        // what was read is removed.
        for (var attempt = 1; ; attempt++)
        {
            string text;
            try
            {
                text = File.ReadAllText(taking); // read-shares: refused while another loop's rename holds it, which must stay so (REQTAKE1)
            }
            catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException)
            {
                return null;
            }
            catch (Exception error) when (attempt < ReadTries && error is IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(10);
                continue;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                PutBack(taking, path);
                return null;
            }

            Remove(taking);
            return Parse(text);
        }
    }

    /// <summary>
    /// What a take moved aside and could not read, put back for the next look. Where a newer request for the session now stands in
    /// its place, that one replaces it, as a request nobody took is replaced.
    /// </summary>
    private static void PutBack(string taking, string path)
    {
        try
        {
            File.Move(taking, path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            if (File.Exists(path)) Remove(taking);
        }
    }

    /// <summary>
    /// Withdraw a session's request while nobody has taken it; false when it was taken, or never written, or held past every
    /// try, when it is put back for a loop to take (REQTAKE2).
    /// </summary>
    public bool Withdraw(string session) => WithdrawAt(PathOf(session));

    /// <summary>Withdraw this request by its own name while nobody has taken it: a say by its key.</summary>
    public bool Withdraw(SessionRequest request) => WithdrawAt(PathOf(request));

    /// <summary>
    /// Withdrawn by renaming it aside, as a loop takes it, then removed only once nothing else holds it, so of a withdrawal and a
    /// take only one wins and the answer says which (REQTAKE2); a look reading it that moment holds it for milliseconds, and is
    /// waited out rather than read as a take (MSG1e).
    /// </summary>
    private static bool WithdrawAt(string path)
    {
        var withdrawing = path + "." + Guid.NewGuid().ToString("N")[..8] + ".withdrawing";
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                File.Move(path, withdrawing);
                break;
            }
            catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException)
            {
                return false;
            }
            catch (Exception error) when (attempt < 20 && error is IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(10);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                return false;
            }
        }

        // 🔴 REQTAKE2 (FIX-LOG 2026-10-16): on Windows a take's rename opens the file, letting it be moved, a moment before it
        // moves it, so a rename begun before this one lands after it and moves the file on from the withdrawn name. Removing that
        // name found nothing there and said withdrawn while the loop acted on it. So the file is opened alone, which waits out
        // every handle opened before the rename above (none can be opened after it, the name being gone), and removed as that
        // handle closes; gone by then, the take that moved it on has it. On Linux a rename is by name and whole, so nothing moves
        // it on, and the open waits only for a reader.
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                using (new FileStream(withdrawing, FileMode.Open, FileAccess.Read, FileShare.None, 1, FileOptions.DeleteOnClose))
                {
                    return true;
                }
            }
            catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException)
            {
                return false;
            }
            catch (Exception error) when (attempt < WithdrawTries && error is IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(10);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // Held past every try: put back, it stands for a loop to take, and is not withdrawn.
                PutBack(withdrawing, path);
                return false;
            }
        }
    }

    /// <summary>
    /// Where a say's words stand, written beside it by the loop that took it (MSG1e), for the door that asked: atomically, so
    /// the asker reads the whole of it or nothing.
    /// </summary>
    public void Answer(SessionRequest request, WordsHeld held)
    {
        var path = AnswerPath(request);
        Directory.CreateDirectory(Folder);
        AtomicFile.WriteText(path, ToJson(held));
    }

    /// <summary>What became of a housekeeping request (HELPMODE1c2a), written beside it as a say's answer is, for the tool that asked.</summary>
    public void Answer(SessionRequest request, HousekeepingAnswer answer)
    {
        var path = AnswerPath(request);
        Directory.CreateDirectory(Folder);
        AtomicFile.WriteText(path, ToJson(answer.Operation, answer.Done, answer.Code, answer.Said));
    }

    /// <summary>What became of a setting request (HELPMODE1c3), written beside it in housekeeping's answer's fields.</summary>
    public void Answer(SessionRequest request, SettingAnswer answer)
    {
        var path = AnswerPath(request);
        Directory.CreateDirectory(Folder);
        AtomicFile.WriteText(path, ToJson(answer.Setting, answer.Done, answer.Code, answer.Said));
    }

    /// <summary>What became of a checkout read (HELPMODE1c4), written beside it in the same fields with what was read as <c>text</c>.</summary>
    public void Answer(SessionRequest request, CheckoutAnswer answer)
    {
        var path = AnswerPath(request);
        Directory.CreateDirectory(Folder);
        AtomicFile.WriteText(path, ToJson(answer.Operation, answer.Done, answer.Code, answer.Said, answer.Text, withText: true));
    }

    /// <summary>
    /// A say's answer, read once and removed; null while none is written. One held open that moment is read at the next ask,
    /// never dropped; one that does not read as an answer is removed.
    /// </summary>
    public WordsHeld? AnswerOf(SessionRequest request)
    {
        var path = AnswerPath(request);
        string text;
        try
        {
            if (!File.Exists(path)) return null;
            text = AtomicFile.ReadText(path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        for (var attempt = 0; attempt < 10 && !Remove(path); attempt++) Thread.Sleep(10);
        return ReadAnswer(text);
    }

    private static bool Remove(string path)
    {
        try
        {
            File.Delete(path);
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Held for a moment by whoever read it: the next look removes it.
            return false;
        }
    }

    /// <summary>One request, or null where the file does not read as one.</summary>
    private static SessionRequest? Read(string path)
    {
        try
        {
            return Parse(AtomicFile.ReadText(path));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>One request, or null where the text does not read as one.</summary>
    private static SessionRequest? Parse(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            string? Text(string name) =>
                root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

            if (Text("session") is not { Length: > 0 } session || Text("move") is not { } move || !SessionMove.Known(move)) return null;
            var at = Moments.Read(Text("at")) ?? DateTimeOffset.MinValue;
            return new SessionRequest(session, move, at)
            {
                Note = Text("note"),
                NoteParts = NotePart.Read(root),
                Parked = root.TryGetProperty("parked", out var parked) && parked.ValueKind == JsonValueKind.True,
                By = Text("by") ?? RequestDoor.Terminal,
                Text = Text("text"),
                Files = root.TryGetProperty("files", out var files) && files.ValueKind == JsonValueKind.Array
                    ? [.. files.EnumerateArray().Where(name => name.ValueKind == JsonValueKind.String).Select(name => name.GetString()!)]
                    : [],
                Key = Text("key"),
                Now = root.TryGetProperty("now", out var now) && now.ValueKind == JsonValueKind.True,
                Level = Text("level"),
                Word = Text("takeBackWord"),
                Operation = Text("operation"),
                Repository = Text("repository"),
                Branches = root.TryGetProperty("branches", out var branches) && branches.ValueKind == JsonValueKind.Array
                    ? [.. branches.EnumerateArray().Where(name => name.ValueKind == JsonValueKind.String).Select(name => name.GetString()!)]
                    : [],
                Value = Text("value"),
                Workspace = Text("workspace"),
                Path = Text("path"),
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>One answer, or null where the text does not read as one.</summary>
    private static WordsHeld? ReadAnswer(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("sent", out var sent)) return null;
            string? Text(string name) =>
                root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

            return new WordsHeld(sent.ValueKind == JsonValueKind.True, Text("reaches"), Text("why"))
            {
                Word = Text("word"),
                Message = Text("message"),
                Now = root.TryGetProperty("now", out var now) && now.ValueKind == JsonValueKind.True,
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Written by hand, as the driver's other files are, for the AOT reason <see cref="DriverConfig"/> gives.</summary>
    private static string ToJson(SessionRequest request) => JsonFile.Serialize(writer =>
    {
        writer.WriteStartObject();
        writer.WriteString("session", request.Session);
        writer.WriteString("move", request.Move);
        if (request.Note is not null) writer.WriteString("note", request.Note);
        if (request.Note is not null && request.NoteParts is { Count: > 0 } parts)
        {
            writer.WritePropertyName("noteParts");
            NotePart.Write(writer, parts);
        }

        writer.WriteBoolean("parked", request.Parked);
        writer.WriteString("by", request.By);
        writer.WriteString("at", Moments.Write(request.At));
        if (request.Text is not null) writer.WriteString("text", request.Text);
        if (request.Files.Count > 0)
        {
            writer.WriteStartArray("files");
            foreach (var name in request.Files) writer.WriteStringValue(name);
            writer.WriteEndArray();
        }

        if (request.Key is not null) writer.WriteString("key", request.Key);
        if (request.Now) writer.WriteBoolean("now", true);
        if (request.Level is not null) writer.WriteString("level", request.Level);
        if (request.Word is not null) writer.WriteString("takeBackWord", request.Word);
        if (request.Operation is not null && request.Move == SessionMove.Set)
        {
            // HELPMODE1c3: a setting's fields, in the connector's order.
            writer.WriteString("operation", request.Operation);
            if (request.Value is not null) writer.WriteString("value", request.Value);
            if (request.Repository is not null) writer.WriteString("repository", request.Repository);
            if (request.Workspace is not null) writer.WriteString("workspace", request.Workspace);
        }
        else if (request.Operation is not null && request.Move == SessionMove.Read)
        {
            // HELPMODE1c4: a checkout read's fields, in the connector's order.
            writer.WriteString("operation", request.Operation);
            if (request.Repository is not null) writer.WriteString("repository", request.Repository);
            if (request.Path is not null) writer.WriteString("path", request.Path);
        }
        else if (request.Operation is not null)
        {
            writer.WriteString("operation", request.Operation);
            if (request.Repository is not null) writer.WriteString("repository", request.Repository);
            writer.WriteStartArray("branches");
            foreach (var branch in request.Branches) writer.WriteStringValue(branch);
            writer.WriteEndArray();
        }

        writer.WriteEndObject();
    });

    // The twin's fields, in its order (HELPMODE1c2a, and HELPMODE1c3's settings alike): the service's boxes read `said`; a checkout
    // read's (HELPMODE1c4) carries `text` after them, null where nothing was read.
    private static string ToJson(string operation, bool done, string? code, string said, string? text = null, bool withText = false) =>
        JsonFile.Serialize(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("operation", operation);
            writer.WriteBoolean("done", done);
            if (code is null) writer.WriteNull("code");
            else writer.WriteString("code", code);
            writer.WriteString("said", said);
            if (withText && text is null) writer.WriteNull("text");
            else if (withText) writer.WriteString("text", text);
            writer.WriteEndObject();
        });

    private static string ToJson(WordsHeld held) => JsonFile.Serialize(writer =>
    {
        writer.WriteStartObject();
        writer.WriteBoolean("sent", held.Sent);
        if (held.Reaches is not null) writer.WriteString("reaches", held.Reaches);
        if (held.Why is not null) writer.WriteString("why", held.Why);
        if (held.Word is not null) writer.WriteString("word", held.Word);
        if (held.Message is not null) writer.WriteString("message", held.Message);
        if (held.Now) writer.WriteBoolean("now", true);
        writer.WriteEndObject();
    });
}
