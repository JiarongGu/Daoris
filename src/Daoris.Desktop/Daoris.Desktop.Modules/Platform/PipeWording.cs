using System.Collections.Frozen;

namespace Daoris.Desktop;

/// <summary>
/// The part a value plays in the sentence a terminal's press is shown by (NOTIFY2b, the notification design §3.2). A wire's word,
/// told in lower case as <see cref="PipeNamed.Role"/>: added to, never renamed.
/// </summary>
public enum PipeRole
{
    /// <summary>A session, named as Sessions names it with its repository and short id beside, and its workspace where a name is in two.</summary>
    Session,

    /// <summary>Several sessions, each named as <see cref="Session"/> is.</summary>
    Sessions,

    /// <summary>Words the press carries, quoted as written, apart from Daoris's sentence.</summary>
    Words,

    /// <summary>D188's level, in the composer's words: when the words reach a working session.</summary>
    Level,

    /// <summary>Files, each by its path as sent, whole: two files share a name.</summary>
    Files,

    /// <summary>A quest, by its name and id.</summary>
    Quest,

    /// <summary>An ask, by its name and id.</summary>
    Ask,

    /// <summary>A repository, by its name.</summary>
    Repository,

    /// <summary>An agent, by its name.</summary>
    Agent,

    /// <summary>A value that chooses how the act runs, which the sentence says in words.</summary>
    Form,
}

/// <summary>One field of a press's payload its sentence names, by the top-level name its handler reads, and the part it plays.</summary>
public sealed record PipeWord(string Field, PipeRole Role);

/// <summary>
/// One value a press's sentence names (NOTIFY2b): its role in lower case, its JSON pointer as <see cref="PipeConfirmationField.Name"/>
/// gives it, and its value as the card shows it. Every value stays in <c>fields</c> besides, so none leaves the card's view.
/// </summary>
public sealed record PipeNamed(string Role, string Name, string Value);

/// <summary>
/// Which values a terminal's press of each person's route names in the sentence the window shows it by (NOTIFY2b, D196 point 4, the
/// notification design §3.2): a row beside the pipe's census for every route it classes <see cref="PipeClass.Person"/>, in any form.
/// </summary>
/// <remarks>
/// <para><b>Judged here, worded by the page</b> (D182): the pipe's telling carries the named values by role as <c>named</c>, a field
/// added beside <c>fields</c>, never in place of it; the page words the act by <see cref="Key"/> in the reader's language and lists
/// every value no role names under the sentence, so none leaves the card's view. There is no quiet role: a value that looked quiet
/// (<c>SESSION_INPUT</c>'s <c>said</c>) chooses how the act runs, and is a <see cref="PipeRole.Form"/> the sentence says.</para>
///
/// <para><b>Every person's route has a row</b>, and its act's words under <see cref="Key"/> in both catalogues: <c>PipeWordingTests</c>
/// fails on a route the census makes the person's with no row or no words, and on a row naming a route that is not, so a route's
/// constant is never the sentence. A row naming no field says the act alone, its values listed under it.</para>
/// </remarks>
public static class PipeWording
{
    private static readonly PipeWord[] None = [];

    /// <summary>Each person's route by its module and route, with the fields its sentence names.</summary>
    public static FrozenDictionary<(string Module, string Route), IReadOnlyList<PipeWord>> Rows { get; } = Table();

    /// <summary>The fields <paramref name="route"/> of <paramref name="module"/> names by role; null where it has no row.</summary>
    public static IReadOnlyList<PipeWord>? For(string module, string route) =>
        Rows.TryGetValue((Canonical(module), route), out var row) ? row : null;

    /// <summary>
    /// The catalogue key of the act's words: <c>person.route.ROUTE</c> for the driver's, and <c>person.route.MODULE.ROUTE</c> for
    /// another module's, its name past <c>DAORIS.</c>, since two modules may name a route alike (<c>REMOTES</c>' <c>SET</c>). The
    /// page's twin is <c>routeKey</c> in <c>person/Confirmations.tsx</c>.
    /// </summary>
    public static string Key(string module, string route) =>
        string.Equals(module, DriverModule.Name, StringComparison.OrdinalIgnoreCase)
            ? $"person.route.{route}"
            : $"person.route.{(module.StartsWith("DAORIS.", StringComparison.OrdinalIgnoreCase) ? module["DAORIS.".Length..] : module).ToUpperInvariant()}.{route}";

    /// <summary>
    /// The values of <paramref name="fields"/> the sentence of <paramref name="route"/> names, in its row's order and each field's in
    /// the payload's: a field's own pointer, or one under it (<c>/paths/0</c> under <c>paths</c>). None where the route has no row.
    /// </summary>
    public static IReadOnlyList<PipeNamed> Named(string module, string route, IReadOnlyList<PipeConfirmationField> fields)
    {
        if (For(module, route) is not { Count: > 0 } row) return [];
        var named = new List<PipeNamed>();
        foreach (var word in row)
        {
            var at = $"/{word.Field}";
            foreach (var field in fields)
            {
                if (field.Name == at || field.Name.StartsWith(at + "/", StringComparison.Ordinal))
                {
                    named.Add(new PipeNamed(word.Role.ToString().ToLowerInvariant(), field.Name, field.Value));
                }
            }
        }

        return named;
    }

    private static string Canonical(string module) => module.ToUpperInvariant();

    private static FrozenDictionary<(string Module, string Route), IReadOnlyList<PipeWord>> Table()
    {
        var table = new Dictionary<(string, string), IReadOnlyList<PipeWord>>();
        var driver = Canonical(DriverModule.Name);
        void Row(string route, params PipeWord[] words) => table.Add((driver, route), words);
        void Act(params string[] routes)
        {
            foreach (var route in routes) table.Add((driver, route), None);
        }

        static PipeWord W(string field, PipeRole role) => new(field, role);

        // A session's own presses, which name it (and the words, the level and the files said to it) where the handler reads them.
        Row("SESSION_INPUT", W("id", PipeRole.Session), W("text", PipeRole.Words), W("level", PipeRole.Level), W("paths", PipeRole.Files),
            W("said", PipeRole.Form));
        Row("SESSION_QUEUE_EDIT", W("session", PipeRole.Session), W("text", PipeRole.Words), W("level", PipeRole.Level));
        Row("SESSION_ARCHIVE", W("ids", PipeRole.Sessions));
        Row("SESSION_DELETE", W("id", PipeRole.Session), W("ids", PipeRole.Sessions));
        Row("STOP_SESSION", W("id", PipeRole.Session));
        Row("STOP_TASK", W("id", PipeRole.Session));
        Row("CANCEL_TURN", W("id", PipeRole.Session));
        Row("END_CHAT", W("id", PipeRole.Session));
        Row("RESOLVE_SESSION", W("id", PipeRole.Session), W("note", PipeRole.Words));
        Row("LAND_SESSION_TREE", W("id", PipeRole.Session));
        Row("LAND_WITHOUT_LOOK", W("id", PipeRole.Session), W("words", PipeRole.Words));
        Row("DISCARD_SESSION_TREE", W("id", PipeRole.Session));
        Row("SESSION_GO_ON_NEW", W("id", PipeRole.Session));
        Row("SESSION_START_FROM", W("id", PipeRole.Session));
        Row("SESSION_GO_ON_BRIEF", W("id", PipeRole.Session));
        Row("SESSION_STOP_WAITING", W("id", PipeRole.Session));
        Row("SESSION_HAND_OVER", W("id", PipeRole.Session), W("agent", PipeRole.Agent));
        Row("SESSION_HELPER_ASK", W("id", PipeRole.Session), W("agent", PipeRole.Agent), W("question", PipeRole.Words));
        Row("SESSION_PARTNER_ASK", W("id", PipeRole.Session), W("agent", PipeRole.Agent), W("piece", PipeRole.Words));
        Row("SESSION_HELPER_APPROVE", W("session", PipeRole.Session));
        Row("SESSION_HELPER_DECLINE", W("session", PipeRole.Session));
        Row("SESSION_HAND_OVER_APPROVE", W("session", PipeRole.Session));
        Row("SESSION_HAND_OVER_DECLINE", W("session", PipeRole.Session));
        Row("SESSION_QUIET_CARRY_ON", W("id", PipeRole.Session));
        Row("SESSION_QUIET_LEAVE", W("id", PipeRole.Session));
        Row("SESSION_PERMISSION_ANSWER", W("session", PipeRole.Session));
        Row("SESSION_ANSWER_ALONE", W("session", PipeRole.Session));
        Row("SUPERVISOR_OVERRULE", W("id", PipeRole.Session));
        Row("SESSION_GO_AHEAD", W("id", PipeRole.Session), W("ask", PipeRole.Ask), W("words", PipeRole.Words));
        Row("SET_SESSION_OPTION", W("id", PipeRole.Session));
        Row("STOP_OPINION", W("id", PipeRole.Session), W("words", PipeRole.Words));

        // A quest's, an ask's and a channel's.
        Row("RETRY_QUEST", W("quest", PipeRole.Quest));
        // FABRIC-CUTOVER1f: a run is named by its chain's first quest here; its node and act are listed under the sentence, the words quoted.
        Row("WORKFLOW_NODE", W("run", PipeRole.Quest), W("act", PipeRole.Form), W("words", PipeRole.Words));
        Row("WORK_RESUME", W("quest", PipeRole.Quest), W("ask", PipeRole.Ask));
        Row("LAND_ASK", W("ask", PipeRole.Ask));
        Row("CHANNEL_POST", W("text", PipeRole.Words));
        Row("CHANNEL_FORWARD", W("repository", PipeRole.Repository));
        Row("START_CHAT", W("repository", PipeRole.Repository), W("adapter", PipeRole.Agent), W("text", PipeRole.Words));

        // A repository's: its lines and branches, and the person's standing choices for it.
        Row("HANDOFF", W("repository", PipeRole.Repository));
        Row("DISCARD_SESSION_BRANCH", W("repository", PipeRole.Repository));
        Row("RECOVERY_DROP", W("repository", PipeRole.Repository));
        Row("TREES_SYNC", W("repository", PipeRole.Repository));
        Row("SETUP_PRESS", W("repository", PipeRole.Repository));
        Row("WORKFLOW_LANDED", W("repository", PipeRole.Repository));
        Row("SET_DRIVABLE", W("repository", PipeRole.Repository));
        Row("SET_HEAVY_GATES", W("repository", PipeRole.Repository));
        Row("SET_HOLD", W("repository", PipeRole.Repository));
        Row("SET_LANDING", W("repository", PipeRole.Repository));
        Row("SET_LANGUAGE", W("repository", PipeRole.Repository));
        Row("SET_LINE", W("repository", PipeRole.Repository));
        Row("SET_READ_ACROSS", W("repository", PipeRole.Repository));
        Row("SET_SESSION_CHECKS", W("repository", PipeRole.Repository));
        Row("SET_STANDING", W("repository", PipeRole.Repository));
        Row("SET_TREES", W("repository", PipeRole.Repository));
        Row("SET_WRITE_ACROSS", W("repository", PipeRole.Repository));

        // An agent's: its settings, its accounts, its actions, and the agents the driver starts.
        Row("SET_AGENT_SETTINGS", W("harness", PipeRole.Agent));
        Row("HARNESS_ACTION", W("harness", PipeRole.Agent));
        Row("ACCOUNT_USE", W("harness", PipeRole.Agent));
        Row("HARNESSES_REFRESH", W("agent", PipeRole.Agent));

        // The rest say their act alone, every value listed under it.
        Act(
            "LAND_ENTRY", "OPINION_ANYWAY", "OPINION_MYSELF", "SWEEP", "HISTORY_CLEAR", "SYSTEM_DOWNLOADS_CLEAR", "WORK_ABANDON", "WORK_PAUSE",
            "WORK_HELD", "HELP_DISMISS", "CHANNEL_MARK", "NOTICES_READ", "NOTICE_DISMISS", "HELP_APPLY", "START_HELP", "HELP_START_FROM",
            "HELP_RENAME", "HELP_PIN",
            "SET_ACCESS", "SET_ACTOR", "SET_CAP", "SET_COMMITS", "SET_COOLOFF", "SET_HAND_OVER", "SET_HELPERS", "SET_HELP_MODE", "SET_HELP_GRANT", "SET_KEEP",
            "SET_NOTIFY", "SET_OPINION", "SET_REVIEW", "SET_STRIKES", "TRUST_FOLDER", "RULE_ACTION", "RULE_PROPOSAL", "PLUGIN_ACTION",
            "PLUGIN_ADD", "PLUGIN_INSTALL", "PLUGIN_NEW", "PLUGIN_TRY", "TOOLS_DELETE", "TOOLS_DOWNLOAD", "TOOLS_LOCATION", "TOOLS_STOP",
            "TOOLS_USE", "ASK_OPINION",
            // A look in one form and the person's press in another (a `PipeRule`): worded for the press.
            "TOOLS_GIT", "PLUGIN_UPDATE", "SYSTEM_SPACE_FREE");

        // The other modules classed route by route: a declaration written into a repository, and the machine's wiring.
        table.Add((Canonical("DAORIS.REGISTRY"), "WRITE_DECLARATION"), None);
        table.Add((Canonical("DAORIS.REMOTES"), "SET"), None);
        table.Add((Canonical("DAORIS.REMOTES"), "REMOVE"), None);
        return table.ToFrozenDictionary();
    }
}
