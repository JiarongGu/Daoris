namespace Daoris.Driver;

/// <summary>
/// Which of <c>daoris-driver</c>'s verbs hold the home's lease while they act alone (HOMELEASE1, the terminal RPC design §5,
/// §7.1): a verb's mutation does, for its run; a read never does. Read off its words, as the host routes them.
/// </summary>
/// <remarks>
/// <para><b>An unclassed verb holds it.</b> A read that takes the lease only waits where another act holds the home; a write
/// that does not take it is the second writer this exists to stop. So the list below is the reads, and the verbs that are
/// not the home's writers at all:</para>
/// <list type="bullet">
/// <item><b>Reads</b>: <c>trace</c>, <c>git</c>, <c>logs</c>, <c>workflow run</c>, the lists (<c>sessions</c>, <c>history</c>,
/// <c>trees</c>, <c>trees list</c>, <c>trees state</c>, <c>work state</c>, <c>work held</c> naming no word), <c>plugins show</c> and <c>activity</c>, <c>sync status</c>,
/// <c>help list</c>, <c>opinion show</c>, an <c>update</c> that only asks where it stands, any <c>--plan</c>, and asking for
/// the usage.</item>
/// <item><b>The service's own doors</b>, whose one writer is the service already (§7.1): <c>quest delete</c>, <c>accept</c>,
/// <c>done</c>, <c>check</c> and <c>review</c>, <c>answer</c>, an ask's words, publish, close and delete, and <c>ask channel</c>.</item>
/// <item><b>A conversation in the terminal</b> (<c>chat</c>, <c>help resume</c>, <c>sessions start-from</c> and
/// <c>hand-over</c>): it runs for as long as the person talks, and holding the home that long would keep the desktop's loop
/// from starting; RPC1g moves its turns into the running instance.</item>
/// <item><b>The loop</b> (<c>drive</c>), which takes the lease as an instance, for its life.</item>
/// </list>
/// </remarks>
public static class VerbLeases
{
    private static readonly HashSet<string> Reading = new(StringComparer.Ordinal) { "trace", "git", "logs" };

    private static readonly HashSet<string> ServiceQuests = new(StringComparer.Ordinal)
    {
        // FABRIC-CLOSE1c3: the person's reopen and go-on are the service's doors alone, as the yes and the done are.
        "delete", "accept", "done", "check", "review", "reopen", "go-on",
    };

    private static readonly HashSet<string> AskWork = new(StringComparer.Ordinal) { "--pause", "--resume", "--abandon", "--clear" };

    /// <summary>Whether a verb's run, read off its words, holds the home's lease while it acts alone.</summary>
    public static bool Holds(IReadOnlyList<string> args)
    {
        if (args.Count == 0 || args.Any(arg => arg is "--plan" or "--help" or "-h")) return false;
        var verb = args[0];
        var next = args.Count > 1 ? args[1] : null;
        if (Reading.Contains(verb)) return false;

        return verb switch
        {
            "chat" or "answer" => false,
            "drive" or "--once" or "--until-idle" => false,
            "workflow" => next == "keep",
            "ask" => args.Skip(1).Any(AskWork.Contains),
            "quest" => next is not null && !ServiceQuests.Contains(next),
            "sessions" => next is not null && !next.StartsWith('-') && next is not ("start-from" or "hand-over"),
            "history" => next == "clear",
            "trees" => next is not (null or "list" or "state"),
            // `work state` is a look; `work recovery drop` deletes refs (RECOVERYDROP1), which only the lease's holder does.
            "work" => next == "recovery" || (next == "held" && args.Skip(2).Any(arg => HeldResolutions.All.Contains(arg))),
            "plugins" => next is "install" or "new" or "try",
            "sync" => next != "status",
            "help" => next is "rename" or "pin" or "unpin" or "delete",
            "opinion" => next is "ask" or "stop" or "anyway" or "myself",
            "update" => args.Skip(1).Any(arg => arg is "--when-idle" or "--now" or "--cancel"),
            _ => true,
        };
    }

    /// <summary>The words a lease names a verb by: its verb, and its sub-verb where it has one; never an argument.</summary>
    public static string Name(IReadOnlyList<string> args) =>
        args.Count > 1 && IsWord(args[1]) && SubVerbs.Contains(args[1])
            ? $"daoris-driver {args[0]} {args[1]}"
            : $"daoris-driver {(args.Count > 0 ? args[0] : "")}".TrimEnd();

    /// <summary>The second words that are a verb's own, which a lease may name; anything else may be a name or a path.</summary>
    private static readonly HashSet<string> SubVerbs = new(StringComparer.Ordinal)
    {
        "land", "remove", "clean", "sync", "hand", "stop", "finish", "decline", "say", "take-back", "reword", "archive",
        "unarchive", "delete", "go-on-new", "helper", "pause", "resume", "abandon", "clear", "rename", "pin", "unpin", "ask",
        "anyway", "myself", "install", "new", "try", "keep", "dismiss", "--pause", "--resume", "--abandon", "--clear",
        "--workspace", "--when-idle", "--now", "--cancel", "recovery", "held",
    };

    private static bool IsWord(string value) => value.Length > 0 && value.All(c => c is >= 'a' and <= 'z' or '-');
}
