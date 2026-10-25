using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>
/// A verb's own reading of its lease (FOUNDATION1 mod-vi, the design §14.3): what <see cref="VerbLeases"/> cannot spell, since the verb's
/// command is a higher subdomain's and the lower one names none of its types. The subdomain that owns the command answers it, once, when the
/// driver's assembly loads (<see cref="VerbLeases.Answer"/>), before any host reads a lease.
/// </summary>
/// <param name="Holds">Whether the words after the verb change a home, so the verb holds its lease from the top.</param>
/// <param name="Verbs">The second words that are the verb's own, which a lease may name (<see cref="VerbLeases.Name"/>); null where it has none.</param>
/// <param name="Isolated">
/// Whether the words after the verb act on a file a terminal's variable names, which is no instance's, so they hold nothing
/// (<see cref="VerbLeases.HoldsHere"/>); null where the verb never does.
/// </param>
public sealed record VerbLeaseReading(
    Func<IReadOnlyList<string>, bool> Holds,
    IReadOnlyList<string>? Verbs = null,
    Func<IReadOnlyList<string>, Func<string, string?>, bool>? Isolated = null);

/// <summary>
/// Which of <c>daoris-driver</c>'s verbs hold the home's lease while they act alone (HOMELEASE1, the terminal RPC design §5,
/// §7.1): a verb's mutation does, for its run; a read never does. Read off its words, as the host routes them. The host takes it in
/// the home the loop drives and holds, the folder the driver's config is in (<see cref="DriverConfig.FindHome(Func{string, string?})"/>,
/// LEASEHOME2).
/// </summary>
/// <remarks>
/// <para><b>An unclassed verb holds it.</b> A read that takes the lease only waits where another act holds the home; a write
/// that does not take it is the second writer this exists to stop. So the list below is the reads, and the verbs that are
/// not the home's writers at all:</para>
/// <list type="bullet">
/// <item><b>Reads</b>: <c>trace</c>, <c>git</c>, <c>logs</c>, <c>workflow run</c>, the lists (<c>sessions</c>, <c>history</c>,
/// <c>trees</c>, <c>trees list</c>, <c>trees state</c>, <c>work state</c>, <c>work held</c> naming no word), <c>plugins show</c> and <c>activity</c>, <c>sync status</c>,
/// <c>help list</c>, <c>help mode</c> naming no mode, <c>access</c> and <c>heavy</c> naming no change, <c>opinion show</c>, an <c>update</c> that only asks where it stands, any <c>--plan</c>, and asking for
/// the usage; the driver's standing choices' <c>list</c> (PUSH1f2a), and any of them on a file <c>DAORIS_DRIVER_CONFIG</c> names
/// (<see cref="HoldsHere"/>); the tools' <c>list</c>, <c>path</c> and a bare <c>locations</c> (PUSH1f2c); the agents' <c>rules</c> and
/// <c>rules proposals</c>, a <c>settings</c> naming no change, a <c>trust</c> asked or dry-run, and an agent's settings or trust on a file
/// <c>DAORIS_HARNESS_CONFIG</c> names (PUSH1f2d).</item>
/// <item><b>A long verb</b> (PUSH1f2c, the alone-path design §1.4): <c>tool download</c>, <c>update</c> and <c>use … managed</c> hold
/// nothing while they fetch, and take the lease themselves to publish, waited for; <c>agent key</c> holds nothing while the key is
/// typed and takes it to write the key's account, and <c>agent install</c> none at all, its installer writing the machine (PUSH1f2e5); <c>agent login</c> holds nothing while the
/// agent's sign-in runs and takes it to publish the sign-in's end (PUSH1f2e6); <c>agent pin</c> and a pinned <c>update</c> hold nothing while a
/// release downloads or npm installs and take it to publish the pin, and an unpinned <c>update</c> none at all (PUSH1f2e).</item>
/// <item><b>The service's own doors</b>, whose one writer is the service already (§7.1): <c>quest delete</c>, <c>accept</c>,
/// <c>done</c>, <c>check</c> and <c>review</c>, <c>answer</c>, an ask's words, publish, close and delete, and <c>ask channel</c>.</item>
/// <item><b>A conversation in the terminal</b> (<c>chat</c>, <c>help resume</c>, <c>sessions start-from</c> and
/// <c>hand-over</c>): it runs for as long as the person talks, and holding the home that long would keep the desktop's loop
/// from starting; RPC1g moves its turns into the running instance.</item>
/// <item><b>The loop</b> (<c>drive</c>), which takes the lease as an instance, for its life.</item>
/// <item><b>A repository's heavy gates</b> (<c>run-heavy</c>, HEAVYRUN1a): they run for minutes, and holding the home that long would
/// keep every other verb and the desktop's start waiting on a build; the run writes the home only in its own slot's files.</item>
/// </list>
/// </remarks>
public static class VerbLeases
{
    private static readonly HashSet<string> Reading = new(StringComparer.Ordinal) { "trace", "logs" };

    private static readonly HashSet<string> ServiceQuests = new(StringComparer.Ordinal)
    {
        // FABRIC-CLOSE1c3: the person's reopen and go-on are the service's doors alone, as the yes and the done are.
        "delete", "accept", "done", "check", "review", "reopen", "go-on",
    };

    /// <summary>The verbs whose own command reads their lease, each answered by the subdomain that owns the command (<see cref="Answer"/>).</summary>
    private static readonly ConcurrentDictionary<string, VerbLeaseReading> Readings = new(StringComparer.Ordinal);

    private static readonly HashSet<string> AskWork = new(StringComparer.Ordinal) { "--pause", "--resume", "--abandon", "--clear" };

    /// <summary>
    /// Answers how a verb's own words read its lease, for a verb whose command a lower subdomain cannot name (FOUNDATION1 mod-vi). A verb
    /// nobody answers is unclassed, and an unclassed verb holds the lease.
    /// </summary>
    internal static void Answer(string verb, VerbLeaseReading reading) => Readings[verb] = reading;

    /// <summary>The verbs answered, each with the reading its command's subdomain gave.</summary>
    internal static IReadOnlyDictionary<string, VerbLeaseReading> Answered => Readings;

    /// <summary>Whether a verb's run, read off its words, holds the home's lease while it acts alone.</summary>
    public static bool Holds(IReadOnlyList<string> args)
    {
        if (args.Count == 0 || args.Any(arg => arg is "--plan" or "--help" or "-h")) return false;
        var verb = args[0];
        var next = args.Count > 1 ? args[1] : null;
        if (Reading.Contains(verb)) return false;
        if (Readings.TryGetValue(verb, out var reading)) return reading.Holds([.. args.Skip(1)]);

        return verb switch
        {
            "chat" or "answer" => false,
            "drive" or "--once" or "--until-idle" => false,
            "run-heavy" => false,
            "ask" => args.Skip(1).Any(AskWork.Contains),
            "quest" => next is not null && !ServiceQuests.Contains(next),
            "sessions" => next is not null && !next.StartsWith('-') && next is not ("start-from" or "hand-over"),
            "history" => next == "clear",
            "trees" => next is not (null or "list" or "state"),
            "plugins" => next is "install" or "new" or "try",
            "sync" => next != "status",
            // `help mode` alone says the mode; naming one writes the driver's file (HELPMODE1a1).
            "help" => next is "rename" or "pin" or "unpin" or "delete" || (next == "mode" && args.Count > 2),
            "opinion" => next is "ask" or "stop" or "anyway" or "myself",
            // The driver's standing choices `daoris driver …` hands on (PUSH1f2a, D194 point 2): each change is a short act, held from the
            // top; `list`, the usage and a verb this program does not have hold none. Read through the command's own reading.
            "driver" => DriverChoicesCommand.Changes([.. args.Skip(1)]),
            // The tools `daoris tool …` hands on (PUSH1f2c): a way set to the system's or a file, a delete, a location's change and a
            // look hold it from the top; a read and the long verbs none, a download taking it itself to publish (the alone-path design §1.4).
            "tool" => ToolsCommand.Changes([.. args.Skip(1)]),
            "update" => args.Skip(1).Any(arg => arg is "--when-idle" or "--now" or "--cancel"),
            // `git branches`, `git identity list` and `public-key` are reads; an identity's change, a default and a pointer write the
            // home's identities or the driver's file (GITAUTH1c).
            "git" => GitIdentityCommand.Changes([.. args.Skip(1)]),
            _ => true,
        };
    }

    /// <summary>
    /// <see cref="Holds"/> on this terminal's variables: a file <c>DAORIS_DRIVER_CONFIG</c> names is no instance's (D194 point 2, the
    /// alone-path design §1.2), so the driver family acting on it holds nothing, as it sends nothing; and a file
    /// <c>DAORIS_HARNESS_CONFIG</c> names is no instance's either, so an agent's settings and trust on its home hold nothing, while the
    /// rules, <c>DAORIS_HOME</c>'s whatever it names, hold the home's lease (PUSH1f2d). A verb that holds takes the lease in the home the
    /// loop drives, <see cref="DriverConfig.FindHome(Func{string, string?})"/>, where the loop holds it (LEASEHOME2).
    /// </summary>
    public static bool HoldsHere(IReadOnlyList<string> args, Func<string, string?> environment) =>
        Holds(args) && !(args[0] == "driver" && DriverConfig.Overridden(environment))
        && !(Readings.TryGetValue(args[0], out var reading) && reading.Isolated?.Invoke([.. args.Skip(1)], environment) == true);

    /// <summary>The words a lease names a verb by: its verb, and its sub-verb where it has one; never an argument.</summary>
    public static string Name(IReadOnlyList<string> args) =>
        args.Count > 1 && IsWord(args[1]) && (SubVerbs.Contains(args[1]) || (args[0] == "driver" && DriverChoicesCommand.Verbs.Contains(args[1]))
            || (args[0] == "tool" && ToolsCommand.Verbs.Contains(args[1]))
            || (Readings.TryGetValue(args[0], out var reading) && reading.Verbs?.Contains(args[1]) == true))
            ? $"daoris-driver {args[0]} {args[1]}"
            : $"daoris-driver {(args.Count > 0 ? args[0] : "")}".TrimEnd();

    /// <summary>The second words that are a verb's own, which a lease may name; anything else may be a name or a path.</summary>
    private static readonly HashSet<string> SubVerbs = new(StringComparer.Ordinal)
    {
        "land", "remove", "clean", "sync", "hand", "stop", "finish", "decline", "say", "take-back", "reword", "archive",
        "unarchive", "delete", "go-on-new", "helper", "partner", "pause", "resume", "abandon", "clear", "rename", "pin", "unpin", "ask",
        "anyway", "myself", "install", "new", "try", "keep", "dismiss", "forward","--pause", "--resume", "--abandon", "--clear",
        "--workspace", "--when-idle", "--now", "--cancel", "recovery", "held", "mode", "skip",
    };

    private static bool IsWord(string value) => value.Length > 0 && value.All(c => c is >= 'a' and <= 'z' or '-');
}
