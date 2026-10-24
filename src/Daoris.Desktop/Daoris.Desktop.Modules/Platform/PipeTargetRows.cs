using System.Text.Json;

namespace Daoris.Desktop;

/// <summary>
/// The rows of <see cref="PipeTargets"/>, route by route, as the confirm-wait design's §5.1 table names them (CONFIRMWAIT1a-i): the
/// payload's field each ground reads, the read each plan, setting and wait ground calls, and what its projection takes. A plan's
/// projection takes the fields its row names and none of the words, times and versions beside them, so a press confirmed the next
/// morning or after an update is refused only where what it acts on moved.
/// </summary>
/// <remarks>
/// Where the read answers no field the design's row names (the landings' commits are cut to eight characters, the sweep's and the
/// sync's rows carry no tip), the projection takes what the answer does carry beside them: each commit's short id, a row's count of
/// commits and its kind; the session's tree head and a branch's tip are the <c>branch</c> grounds that see a commit itself. Where no
/// read of the bridge lists a wait by the press's id (a second opinion being read, a run's node, a go-ahead, a trust hold, a help
/// proposal, whose read settles and draws what it lists), the loop reads its own record (<see cref="LoopGround"/>).
/// </remarks>
internal static class PipeTargetRows
{
    private static readonly TimeSpan Page = PipeGround.PageBound;

    /// <summary>
    /// The routes whose <c>setting</c> ground names no value it sets, so they are never <c>already</c>, each with why (the confirm-wait
    /// design §15.3 item 12): an edit merged into a rule, an act beside the value, a list edited in place, or a key.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> NotAlready = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["SET_LANDING"] = "merges into the repository's landing rule",
        ["SET_REVIEW"] = "merges into the repository's review rule",
        ["SET_OPINION"] = "merges into the repository's opinion rule",
        ["SET_HELPERS"] = "merges into the repository's helpers rule",
        ["SET_HAND_OVER"] = "merges into the repository's hand-over rule",
        ["SET_COMMITS"] = "merges into the repository's commits rule",
        ["SET_ACCESS"] = "merges into the repository's access rule",
        ["SET_READ_ACROSS"] = "merges into the repository's reading across",
        ["SET_WRITE_ACROSS"] = "adds or removes one repository of the list it writes to",
        ["RULE_ACTION"] = "edits a rule list in place",
        ["SET_KEEP"] = "merges into a kind's keeping rule",
        ["SET_AGENT_SETTINGS"] = "merges into a profile's settings",
        ["SET_ACTOR"] = "edits an actor's row in place",
        ["WORKFLOW_LANDED"] = "reads the gates at the line's tip and accepts them by their blob",
        ["TOOLS_USE"] = "checks a file's version or downloads a managed one",
        ["TOOLS_LOCATION"] = "edits the list of locations in place",
        ["ACCOUNT_USE"] = "signs in or out of an account",
        ["DAORIS.REMOTES.SET"] = "carries a key the read redacts",
        ["DAORIS.REMOTES.REMOVE"] = "removes a workspace's wiring and its key",
    };

    public static Dictionary<string, PipeGroundRow> All()
    {
        var rows = new Dictionary<string, PipeGroundRow>(StringComparer.Ordinal);

        void Row(string route, params PipeGround[] grounds) => rows.Add(route, new PipeGroundRow(grounds));
        void Timeless(string route, string why) => rows.Add(route, new PipeGroundRow([], why));

        // Sessions: what runs or ends, and words as the person's.
        Row("SESSION_ARCHIVE", new SessionGround("ids", many: true));
        Row("SESSION_DELETE", new SessionGround("id"));
        foreach (var route in new[] { "STOP_SESSION", "CANCEL_TURN", "END_CHAT", "RESOLVE_SESSION", "SESSION_QUIET_CARRY_ON", "SESSION_QUIET_LEAVE",
                     "SESSION_GO_ON_NEW", "SESSION_START_FROM", "SESSION_GO_ON_BRIEF", "SESSION_HAND_OVER", "SESSION_STOP_WAITING",
                     "SESSION_HELPER_ASK", "SESSION_PARTNER_ASK" })
        {
            Row(route, new SessionGround("id"));
        }

        // A terminal's words with files (`said`, `paths`) bind the files' bytes, since the press reads them only once confirmed (§15.1 item 5).
        Row("SESSION_INPUT", new SessionGround("id"), Wait("SESSION_QUESTION", ["id", "question"], p => new { id = Text(p, "id") },
                (a, p) => Text(At(a, "question", "id")) == Text(p, "question") ? "open" : "").Only(p => Text(p, "question") is not null),
            LoopGround.Plan("attachments", Page, "paths").Only(p => True(Field(p, "said")) && Each(Field(p, "paths")).Any()));
        Row("SESSION_QUEUE_EDIT", Wait("SESSION_QUEUE", ["session", "id"], p => new { id = Text(p, "session") },
            (a, p) => Each(At(a, "queued")).Any(each => Text(At(each, "id")) == Text(p, "id")) ? "open" : ""));
        Row("SET_SESSION_OPTION", new SessionGround("id"), Setting("SESSION_OPTIONS", ["id", "option"], p => new { id = Text(p, "id") },
            (a, p) => Text(At(Each(At(a, "options")).FirstOrDefault(each => Text(At(each, "id")) == Text(p, "option")), "current")) ?? "",
            p => Text(p, "value")));
        Row("STOP_TASK", new SessionGround("id"), Wait("SESSION_STREAMS", ["id", "key"], p => new { id = Text(p, "id") },
            (a, p) => Each(At(a, "streams")).Any(each => Text(At(each, "key")) == Text(p, "key") && True(At(each, "live")) && True(At(each, "canStop")))
                ? "open" : ""));
        Row("SESSION_GO_AHEAD", LoopGround.Wait("go-ahead", "ask", "number"));
        foreach (var route in new[] { "SESSION_HELPER_APPROVE", "SESSION_HELPER_DECLINE" })
        {
            Row(route, Wait("HELPER_WAITS", ["ask", "session"], _ => null,
                (a, p) => Each(At(a, "waits")).Any(each => Text(At(each, "id")) == Text(p, "ask")) ? "open" : ""));
        }

        Row("SESSION_PERMISSION_ANSWER", Wait("PERMISSION_WAITS", ["session", "request"], _ => null,
            (a, p) => Each(At(a, "waits")).Any(each => Text(At(each, "session")) == Text(p, "session") && Text(At(each, "request")) == Text(p, "request"))
                ? "open" : ""));
        foreach (var route in new[] { "SESSION_HAND_OVER_APPROVE", "SESSION_HAND_OVER_DECLINE" })
        {
            // Absent `ask`, the press answers the session's current one: the one read at the ask is the one held to.
            Row(route, Wait("SESSION_HAND_OVER_ASK", ["session", "ask"], p => new { id = Text(p, "session") },
                (a, p) => At(a, "ask") is { } ask && (Text(p, "ask") is not { } named || Text(At(ask, "id")) == named)
                    ? $"{Text(At(ask, "id"))}|{Text(At(ask, "state"))}|{Text(At(ask, "code"))}"
                    : ""));
        }

        Row("SESSION_ANSWER_ALONE", Wait("ANSWER_WAITS", ["session"], _ => null,
            (a, p) => Text(At(Each(At(a, "waits")).FirstOrDefault(each => Text(At(each, "session")) == Text(p, "session")), "since")) ?? ""));
        Row("SUPERVISOR_OVERRULE", Wait("SESSION_SUPERVISOR", ["id", "decision"], p => new { id = Text(p, "id") },
            (a, p) => Text(At(Each(At(a, "decisions")).FirstOrDefault(each => Text(At(each, "id")) == Text(p, "decision")), "state")) ?? ""));

        // Work, quests and asks.
        Row("RETRY_QUEST", new QuestGround("quest"));
        // A pause is a file, never a state (§15.1 item 3): a Pause binds the work it would stop, and a Resume the pause it would remove.
        Row("WORK_PAUSE", new AskGround("ask"), new QuestGround("quest"), Plan("WORK_PLAN", ["ask", "quest"], WorkOf,
            (a, _) => Fp(
                Rows(At(a, "quests"), "quest", "status", "pause"),
                Rows(At(a, "sessions"), "session", "state"),
                At(a, "paused") is null ? "unpaused" : "paused")));
        Row("WORK_RESUME", new AskGround("ask"), new QuestGround("quest"), Plan("WORK_PLAN", ["ask", "quest"], WorkOf,
            (a, _) => At(a, "paused") is { } paused
                ? Fp(Text(At(paused, "at")), Rows(At(paused, "stopped"), "quest", "session"))
                : "unpaused"));
        Row("WORK_ABANDON", new AskGround("ask"), new QuestGround("quest"), Plan("WORK_PLAN", ["ask", "quest", "pieces"], WorkOf,
            (a, _) => Fp(
                Rows(At(a, "quests"), "quest", "status", "joined", "key", "abandon"),
                Rows(At(a, "sessions"), "session", "state"),
                string.Join(",", Each(At(a, "abandon", "pieces")).Select(each => Text(each)).Order(StringComparer.Ordinal)))));
        Row("WORK_HELD", Wait("WORK_STATE", ["unit", "node", "attempt"], p => new { unit = Text(p, "unit") },
            (a, p) => Each(At(a, "held")).Any(each => Text(At(each, "unit")) == Text(p, "unit") && Text(At(each, "node")) == Text(p, "node")
                && Raw(At(each, "attempt")) == PipeGround.Raw(p, "attempt") && At(each, "resolution") is null) ? "open" : ""));
        Row("WORKFLOW_NODE", LoopGround.Wait("workflow-node", "run", "node"));
        Row("CHANNEL_POST", new AskGround("ask"));
        Row("CHANNEL_FORWARD", new AskGround("ask"), new RepositoryGround("repository"));
        // Each landing read binds each part's tree head and each target by its full tip (§15.1 item 1): the short ids it answers do not.
        Row("LAND_ASK", new AskGround("ask"), Plan("ASK_LANDING", ["ask"], p => new { ask = Text(p, "ask") },
            (a, _) => Fp([.. Each(At(a, "entries")).OrderBy(each => Text(At(each, "entry")), StringComparer.Ordinal).Select(Entry)]),
            tips: (a, _) => Each(At(a, "entries")).SelectMany(EntryTips)));
        Row("LAND_ENTRY", Plan("ENTRY_LANDING", ["entry"], p => new { entry = Text(p, "entry") }, (a, _) => Entry(a), tips: (a, _) => EntryTips(a)));

        // Lands, hand-offs, reviews and trees.
        Row("LAND_SESSION_TREE", new SessionGround("id"), BranchGround.HeadOf("id"), Plan("LANDING", ["id", "branch"],
            p => new { id = Text(p, "id"), branch = Text(p, "branch") }, (a, _) => Landing(a),
            tips: (a, p) => Text(p, "id") is { Length: > 0 } session
                ? [PipeRef.Of(session, "HEAD"), .. Text(At(a, "target")) is { Length: > 0 } target ? [PipeRef.Of(session, target)] : Array.Empty<PipeRef>()]
                : []));
        // An id-only hand-off names no branch: the branch its plan resolved is bound by its tip.
        Row("HANDOFF", new SessionGround("id"), new RepositoryGround("repository"), BranchGround.Named("repository", "branch"),
            Plan("HANDOFF_PLAN", ["id", "branch", "repository", "plugin"],
                p => new { id = Text(p, "id"), branch = Text(p, "branch"), repository = Text(p, "repository"), plugin = Text(p, "plugin") },
                (a, _) => Fp(Pick(a, "session", "branch", "repository", "plugin", "problem", "commits")),
                tips: (a, _) => Text(At(a, "repository")) is { Length: > 0 } repository && Text(At(a, "branch")) is { Length: > 0 } branch
                    ? [PipeRef.In(repository, branch)]
                    : []));
        Row("OPINION_ANYWAY", new SessionGround("id"), OpinionGate());
        Row("OPINION_MYSELF", new SessionGround("id"), OpinionGate());
        Row("ASK_OPINION", new SessionGround("id"), BranchGround.HeadOf("id"));
        // CHATFLOW1d2: Accept without the look… on a chat run, bound to the tree's head its press captures and lands.
        Row("LAND_WITHOUT_LOOK", new SessionGround("id"), BranchGround.HeadOf("id"));
        Row("STOP_OPINION", LoopGround.Wait("opinion", "opinion"));
        // The tree it removes, by `id` or by `path`: its head and index, and with `force` its uncommitted bytes (§15.1 item 2).
        Row("DISCARD_SESSION_TREE", new SessionGround("id"), BranchGround.HeadOf("id"), LoopGround.Plan("tree", Page, "id", "path", "force"));
        Row("DISCARD_SESSION_BRANCH", new RepositoryGround("repository"), BranchGround.Named("repository", "branch", "named"));
        Row("RECOVERY_DROP", new RepositoryGround("repository"), new BranchGround(["repository", "refs"], payload =>
            PipeGround.Text(payload, "repository") is { Length: > 0 } repository
                ? [.. Each(Field(payload, "refs")).Select(each => Text(At(each, "ref"))).OfType<string>().Select(name => (repository, name))]
                : []));
        Row("SWEEP", Plan("SWEEP_PLAN", ["workspace", "only"], p => new { workspace = Text(p, "workspace") },
            (a, _) => Fp(
                Rows(At(a, "branches"), ["repository", "branch"], "kind", "commits", "removable", "discardable"),
                Rows(At(a, "landed"), ["repository", "branch"], "kind", "commits", "removable")),
            tips: (a, _) => Branches(At(a, "branches"), "branch").Concat(Branches(At(a, "landed"), "branch"))));
        // Each line bound by its tip here and at origin, each rebase and delete by its branch's tip.
        Row("TREES_SYNC", Plan("TREES_SYNC_PLAN", ["repository", "workspace", "all", "also", "only"],
            p => new { repository = Text(p, "repository"), workspace = Text(p, "workspace"), all = Field(p, "all"), also = Field(p, "also") },
            (a, _) => Fp(
                Rows(At(a, "lines"), ["repository", "line"], "kind", "from", "to", "commits", "moves"),
                Rows(At(a, "rebases"), ["repository", "branch"], "kind", "commits", "replays"),
                Rows(At(a, "deletes"), ["repository", "branch"], "kind", "commits", "removable")),
            // `lookBound` of a count the press does not say (`bridge/repositories/lines.ts`'s 32): eighteen minutes.
            TimeSpan.FromMinutes(18),
            tips: (a, _) => Branches(At(a, "lines"), "line")
                .Concat(Branches(At(a, "lines"), "line").Select(each => each with { Branch = $"origin/{each.Branch}" }))
                .Concat(Branches(At(a, "rebases"), "branch"))
                .Concat(Branches(At(a, "deletes"), "branch"))));
        Row("HISTORY_CLEAR", Plan("HISTORY_PLAN", ["workspace", "quest", "ask", "units"],
            p => new { workspace = Text(p, "workspace"), quest = Text(p, "quest"), ask = Text(p, "ask"), failed = Field(p, "failed") },
            (a, _) => Fp(string.Join(",", Each(At(a, "units"))
                .Select(each => $"{Text(At(each, "kind"))}:{Text(At(each, "id"))}:{Text(At(each, "keep", "code"))}").Order(StringComparer.Ordinal))),
            TimeSpan.FromMinutes(4)));
        Row("SYSTEM_DOWNLOADS_CLEAR", Plan("SYSTEM_DOWNLOADS", ["versions"], _ => null,
            (a, _) => Fp(Rows(At(a, "versions"), ["kind", "name", "version", "unfinished"]))));
        // Its own `plan: true` form, read whole: the units it names are among those, and a unit it names that left the plan moved it.
        Row("SYSTEM_SPACE_FREE", Plan("SYSTEM_SPACE_FREE", [], _ => new { plan = true },
            (a, _) => Fp(
                Rows(At(a, "now"), ["kind", "id"]),
                string.Join(",", Each(At(a, "early")).SelectMany(group => Each(At(group, "units")))
                    .Select(each => $"{Text(At(each, "kind"))}:{Text(At(each, "id"))}").Order(StringComparer.Ordinal)))));
        Row("NOTICE_DISMISS", LoopGround.Plan("notices", PipeGround.StateBound, "id", "all", "workspace"));

        // Conversations begun on an account.
        Row("START_CHAT", new RepositoryGround("repository"));
        Row("START_HELP", new RepositoryGround("workspace"));
        Row("HELP_START_FROM", new SessionGround("id"));
        Row("HELP_APPLY", LoopGround.Wait("help", "id", "revision"));
        Row("HELP_DISMISS", LoopGround.Wait("help", "id"));
        Row("RULE_PROPOSAL", Wait("RULES", ["id"], _ => null,
            (a, p) => Text(At(Each(At(a, "proposals")).FirstOrDefault(each => Text(At(each, "id")) == PipeGround.Id(Text(p, "id"))), "state")) ?? ""));
        Row("TRUST_FOLDER", LoopGround.Wait("trust", "folder", "trustFile"));

        // The set-up, and a repository's standing choices.
        Row("SETUP_PRESS", new RepositoryGround("repository"), Plan("SETUP_PLAN", ["repository"], p => new { repository = Text(p, "repository") },
            (a, _) => Fp(
                Pick(a, "repository", "workspace", "line", "layout", "adopted", "declares", "asks", "agent", "lands", "landsWith", "pressable"),
                string.Join(",", Each(At(a, "rules")).Select(each => Text(each)).Order(StringComparer.Ordinal)),
                string.Join(",", Each(At(a, "refusals")).Select(each => Text(At(each, "code"))).Order(StringComparer.Ordinal))),
            TimeSpan.FromMinutes(2)));
        Row("SET_DRIVABLE", new RepositoryGround("repository"), Listed("drivable", "drivable"));
        Row("SET_HOLD", new RepositoryGround("repository"), Listed("holds", "held"));
        Row("SET_TREES", new RepositoryGround("repository"), Listed("trees", "ownTree"));
        Row("SET_STANDING", new RepositoryGround("repository"), State(["repository", "says"],
            (a, p) => Text(At(Named(At(a, "standing"), "repository", Text(p, "repository")), "says")) ?? "", p => Text(p, "says")?.Trim() ?? ""));
        Row("SET_SESSION_CHECKS", new RepositoryGround("repository"), State(["repository", "checks"],
            (a, p) => Text(At(Named(At(a, "sessionChecks"), "repository", Text(p, "repository")), "checks"))?.ToLowerInvariant() ?? "",
            p => Text(p, "checks")?.ToLowerInvariant() ?? ""));
        // Set whole, in order (§15.3 item 12): each gate by its name, its run and its folder; none, or an empty list, as none.
        Row("SET_HEAVY_GATES", new RepositoryGround("repository"), State(["repository", "gates"],
            (a, p) => Gates(At(Named(At(a, "heavyGates"), "repository", Text(p, "repository")), "gates")), p => Gates(Field(p, "gates"))));
        Scoped(rows, "SET_LINE", "lines", "workspaceLines", sets: "branch");
        Scoped(rows, "SET_LANGUAGE", "languages", "workspaceLanguages", sets: "language");
        Scoped(rows, "SET_LANDING", "landings", "workspaceLandings");
        Scoped(rows, "SET_REVIEW", "reviews", "workspaceReviews");
        Scoped(rows, "SET_OPINION", "opinions", "workspaceOpinions");
        Scoped(rows, "SET_HELPERS", "helpers", "workspaceHelpers");
        Scoped(rows, "SET_HAND_OVER", "handOver", "workspaceHandOver");
        Scoped(rows, "SET_COMMITS", "commits", "workspaceCommits");
        Scoped(rows, "SET_ACCESS", "access", "workspaceAccess");
        Scoped(rows, "SET_READ_ACROSS", "readAcross", "workspaceReadAcross");
        Row("SET_WRITE_ACROSS", new RepositoryGround("repository"), State(["repository", "to", "allow"],
            (a, p) => Each(At(Named(At(a, "writeAcross"), "repository", Text(p, "repository")), "to")).Any(each => Same(Text(each), Text(p, "to")))
                ? "true" : "false",
            p => Raw(Field(p, "allow"))));
        Row("WORKFLOW_LANDED", new RepositoryGround("repository", "workspace"), Setting("WORKFLOW_CURRENT", ["repository", "workspace", "workflow"],
            p => Text(p, "repository") is { } repository ? new { repository } : new { workspace = Text(p, "workspace") },
            (a, _) => Fp(Pick(At(a, "start", "landed"), "workflow", "level"))));

        // The machine's standing choices.
        Row("SET_NOTIFY", State(["notify"], (a, _) => Raw(At(a, "notify")) ?? "", p => Raw(Field(p, "notify"))));
        Row("SET_STRIKES", State(["strikes"], (a, _) => Raw(At(a, "strikes")) ?? "", p => Raw(Field(p, "strikes"))));
        Row("SET_COOLOFF", State(["minutes"], (a, _) => Raw(At(a, "coolOff")) ?? "", p => Raw(Field(p, "minutes"))));
        Row("SET_CAP", State(["cap"], (a, _) => Raw(At(a, "cap")) ?? "", p => Raw(Field(p, "cap"))));
        Row("SET_HELP_MODE", State(["mode"], (a, _) => Text(At(a, "helpMode")) ?? "", p => Text(p, "mode")));
        Row("SET_HELP_GRANT", State(["grant", "operation", "chosen"],
            (a, p) => Each(At(Named(At(a, "helpGrants"), "id", Text(p, "grant") ?? "housekeeping"), "chosen")).Any(each => Text(each) == Text(p, "operation"))
                ? "true" : "false",
            p => Raw(Field(p, "chosen"))));
        Row("SET_ACTOR", Setting("ACTORS", ["actor"], _ => null,
            (a, p) => Fp(Raw(Named(At(a, "actors"), "id", Text(p, "actor"))))));
        Row("SET_KEEP", Setting("SYSTEM_SPACE", ["kind"], _ => null,
            (a, p) => Fp(Raw(At(Named(At(a, "kinds"), "id", Text(p, "kind")), "rule")), Raw(At(a, "volume", "floor")))));
        Row("SET_AGENT_SETTINGS", Setting("HARNESSES", ["harness", "profile"], _ => null,
            (a, p) => Fp([.. Each(At(a, "harnesses")).SelectMany(each => Each(At(each, "profiles")))
                .Where(each => Text(At(each, "name")) == Text(p, "profile")).Select(each => Raw(At(each, "settings")))])));
        Row("RULE_ACTION", Setting("RULES", ["scope", "name", "list", "rule", "id"], _ => null,
            (a, _) => Fp(Raw(At(a, "scopes")), Raw(At(a, "defaults")))));
        Row("ACCOUNT_USE", Setting("ACCOUNTS", ["harness"], _ => null, (a, _) => Fp(Raw(At(a, "agents")))));
        Row("TOOLS_USE", Setting("TOOLS_LIST", ["tool"], _ => null,
            (a, p) => Fp(Pick(Named(At(a, "tools"), "tool", Text(p, "tool")), "way", "version", "file"))));
        Row("TOOLS_LOCATION", Setting("TOOLS_LIST", ["address"], _ => null,
            (a, _) => Fp(string.Join(",", Each(At(a, "locations")).Select(each => Text(At(each, "address")) ?? Text(each)).Order(StringComparer.Ordinal)))));
        foreach (var route in new[] { "SET", "REMOVE" })
        {
            // Over the workspace's address alone, never its key, which the read redacts.
            rows.Add(PipeTargets.Key("DAORIS.REMOTES", route), new PipeGroundRow([Setting("STATE", ["workspace"], _ => null,
                (a, p) => Text(At(Named(At(a, "remotes"), "workspace", Text(p, "workspace")), "url")) ?? "").Of("DAORIS.REMOTES")]));
        }

        // Code that runs in the driver, and the programs it runs with.
        Row("HARNESS_ACTION", Plan("HARNESSES", ["harness", "profile"], _ => null, (a, p) => Harness(a, Text(p, "harness"))));
        Row("HARNESSES_REFRESH", Plan("HARNESSES", ["agent", "profile"], _ => null, (a, p) => Harness(a, Text(p, "agent"))));
        Row("PLUGIN_ACTION", Plan("PLUGINS", ["id"], _ => null,
            (a, p) => Fp(Pick(Named(At(a, "plugins"), "id", Text(p, "id")), "id", "version", "enabled", "problem", "listening"),
                Rows(At(Named(At(a, "plugins"), "id", Text(p, "id")), "helpServers"), ["name"], "state", "accepted", "allow", "writes"))));
        Row("PLUGIN_ADD", LoopGround.Plan("plugin-folder", Page, "folder"));
        Row("PLUGIN_NEW", LoopGround.Plan("plugin-new", Page, "folder", "id"));
        Row("PLUGIN_TRY", LoopGround.Plan("plugin-try", Page, "id", "folder"));
        Row("PLUGIN_INSTALL", LoopGround.Plan("plugin-offer", Page, "offer"));
        Row("PLUGIN_UPDATE", LoopGround.Plan("plugin-update", Page, "id", "shipped"));
        Row("TOOLS_DELETE", Tool());
        Row("TOOLS_DOWNLOAD", Tool());
        Row("TOOLS_STOP", Wait("TOOLS_LIST", ["tool"], _ => null,
            (a, p) => Raw(At(Named(At(a, "tools"), "tool", Text(p, "tool")), "running")) ?? ""));
        Row("TOOLS_GIT", LoopGround.Plan("file", PipeGround.StateBound, "file"));
        rows.Add(PipeTargets.Key("DAORIS.REGISTRY", "WRITE_DECLARATION"),
            new PipeGroundRow([LoopGround.Plan("declaration", PipeGround.StateBound, "path")]));

        Timeless("CHANNEL_MARK", "a read mark, which never moves back");
        Timeless("NOTICES_READ", "marks news read");
        Timeless("HELP_RENAME", "a conversation's name");
        Timeless("HELP_PIN", "a conversation's pin");
        return rows;
    }

    private static ReadGround Plan(
        string route, string[] fields, Func<JsonElement?, object?> ask, Func<JsonElement, JsonElement?, string> take, TimeSpan? bound = null,
        Func<JsonElement, JsonElement?, IEnumerable<PipeRef>>? tips = null) =>
        new(PipeGroundKind.Plan, route, fields, ask, take, bound ?? Page) { Tips = tips };

    /// <summary><c>WORK_PLAN</c>'s payload for the ask or the quest a work press names.</summary>
    private static object WorkOf(JsonElement? payload) => Text(payload, "ask") is { } ask ? new { ask } : new { quest = Text(payload, "quest") };

    /// <summary>An entry's branches as its landing answers them: its target in its repository, and each part's tree head.</summary>
    private static IEnumerable<PipeRef> EntryTips(JsonElement entry)
    {
        if (Text(At(entry, "repository")) is { Length: > 0 } repository && Text(At(entry, "target")) is { Length: > 0 } target)
        {
            yield return PipeRef.In(repository, target);
        }

        foreach (var part in Each(At(entry, "parts")))
        {
            if (Text(At(part, "session")) is { Length: > 0 } session) yield return PipeRef.Of(session, "HEAD");
        }
    }

    /// <summary>Each row's branch, by its <paramref name="field"/>, in its repository.</summary>
    private static IEnumerable<PipeRef> Branches(JsonElement? rows, string field) =>
        Each(rows).Select(each => (Repository: Text(At(each, "repository")), Branch: Text(At(each, field))))
            .Where(each => each.Repository is { Length: > 0 } && each.Branch is { Length: > 0 })
            .Select(each => PipeRef.In(each.Repository!, each.Branch!));

    /// <summary>Heavy gates as set (§15.3 item 12): each gate's name, run and folder, in order; none or an empty list as none.</summary>
    private static string Gates(JsonElement? gates) =>
        Each(gates).Any() ? Fp([.. Each(gates).Select(each => $"{Text(At(each, "name"))}|{Text(At(each, "run"))}|{Text(At(each, "cwd"))}")]) : "";

    private static ReadGround Wait(string route, string[] fields, Func<JsonElement?, object?> ask, Func<JsonElement, JsonElement?, string> take) =>
        new(PipeGroundKind.Wait, route, fields, ask, take, Page);

    private static ReadGround Setting(
        string route, string[] fields, Func<JsonElement?, object?> ask, Func<JsonElement, JsonElement?, string> take, Func<JsonElement?, string?>? sets = null) =>
        new(PipeGroundKind.Setting, route, fields, ask, take, Page) { Sets = sets };

    /// <summary>A standing choice <c>STATE</c> says, compared at the ask with what the press sets where <paramref name="sets"/> says it.</summary>
    private static ReadGround State(string[] fields, Func<JsonElement, JsonElement?, string> take, Func<JsonElement?, string?>? sets = null) =>
        Setting("STATE", fields, _ => null, take, sets);

    /// <summary>A repository named in one of <c>STATE</c>'s lists, set by the payload's boolean <paramref name="flag"/>.</summary>
    private static ReadGround Listed(string list, string flag) =>
        State(["repository", flag],
            (a, p) => Each(At(a, list)).Any(each => Same(Text(each), Text(p, "repository"))) ? "true" : "false",
            p => Raw(Field(p, flag)));

    /// <summary>
    /// A standing choice at a repository's or a workspace's scope (the lines' routes): its row in <c>STATE</c>'s list or its workspace
    /// twin. One whose edit merges into the rule names no value it sets, so it is never <c>already</c>; one that sets a single value does.
    /// </summary>
    private static void Scoped(Dictionary<string, PipeGroundRow> rows, string route, string list, string workspaces, string? sets = null)
    {
        var ground = State(["repository", "workspace"],
            (a, p) => Text(p, "repository") is { } repository
                ? Value(Named(At(a, list), "repository", repository))
                : Value(Named(At(a, workspaces), "workspace", Text(p, "workspace"))),
            sets is null ? null : p => Text(p, sets) ?? "");
        rows.Add(route, new PipeGroundRow([new RepositoryGround("repository", "workspace"), ground]));

        // A row's value without its scope's name: one field (a branch, a language) is said as its text; a rule as its JSON.
        string Value(JsonElement? row)
        {
            if (row is not { ValueKind: JsonValueKind.Object } found) return "";
            var values = found.EnumerateObject().Where(each => each.Name is not ("repository" or "workspace")).ToList();
            return values.Count == 1 && values[0].Value.ValueKind == JsonValueKind.String ? values[0].Value.GetString()! : Fp([.. values.Select(each => $"{each.Name}={each.Value.GetRawText()}")]);
        }
    }

    private static ReadGround OpinionGate() =>
        Wait("OPINION_GATE", ["id"], p => new { id = Text(p, "id") },
            (a, _) => At(a, "opinion") is { } opinion ? $"{Text(At(opinion, "state"))}|{Raw(At(opinion, "holds"))}|{Text(At(opinion, "opinion"))}" : "");

    private static ReadGround Tool() =>
        Plan("TOOLS_LIST", ["tool", "version"], _ => null, (a, p) =>
        {
            var tool = Named(At(a, "tools"), "tool", Text(p, "tool"));
            return Fp(Pick(tool, "tool", "way", "version"), Rows(At(tool, "downloaded"), ["version"]), Raw(At(tool, "running")));
        });

    /// <summary><c>HARNESSES</c>' row for <paramref name="harness"/> (every row where none is named): its install and its accounts' sign-ins.</summary>
    private static string Harness(JsonElement answer, string? harness) =>
        Fp([.. Each(At(answer, "harnesses"))
            .Where(each => harness is null || Same(Text(At(each, "harness")), harness))
            .OrderBy(each => Text(At(each, "harness")), StringComparer.Ordinal)
            .Select(each => Fp(Pick(each, "harness", "present", "version", "problem", "managed", "pinned"), Rows(At(each, "profiles"), ["name"], "login")))]);

    /// <summary>A landing's plan as <c>LANDING</c> answers it: what lands and how, never a subject's words or a gate's time.</summary>
    private static string Landing(JsonElement plan) => Fp(
        Pick(plan, "form", "target", "source", "plugin", "problem", "own", "stands", "uncommitted"),
        string.Join(",", Each(At(plan, "lands")).Select(each => Text(At(each, "commit")))),
        string.Join(",", Each(At(plan, "leftOut")).Select(each => Text(At(each, "commit"))).Order(StringComparer.Ordinal)),
        Text(At(plan, "conflict", "says")),
        Text(At(plan, "workflow", "state")), Text(At(plan, "review", "state")), Text(At(plan, "opinion", "state")));

    /// <summary>An entry's landing as <c>ENTRY_LANDING</c> answers it: each part as a landing is taken, with what superseded it.</summary>
    private static string Entry(JsonElement entry) => Fp([
        Text(At(entry, "entry")), Text(At(entry, "repository")), Text(At(entry, "form")), Text(At(entry, "target")),
        .. Each(At(entry, "parts")).OrderBy(each => Text(At(each, "session")), StringComparer.Ordinal).Select(part => Fp(
            Pick(part, "session", "form", "target", "own", "plugin", "problem", "held", "sameTreeAs"),
            string.Join(",", Each(At(part, "lands")).Select(each => Text(At(each, "commit")))),
            string.Join(",", Each(At(part, "leftOut")).Select(each => Text(At(each, "commit"))).Order(StringComparer.Ordinal)),
            Text(At(part, "conflict", "says")), Text(At(part, "superseded", "by")))),
    ]);

    private static string Fp(params string?[] parts) => PipeGround.Fingerprint(parts);

    /// <summary>The payload's field <paramref name="name"/>, or null.</summary>
    private static JsonElement? Field(JsonElement? payload, string name) =>
        payload is { ValueKind: JsonValueKind.Object } body && body.TryGetProperty(name, out var value) ? value : null;

    private static string? Text(JsonElement? payload, string name) => PipeGround.Text(payload, name);

    /// <summary>A value as text: a string as it is, anything else as its JSON; null where absent or null.</summary>
    private static string? Text(JsonElement? value) => value switch
    {
        { ValueKind: JsonValueKind.String } text => text.GetString(),
        null or { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined } => null,
        { } other => other.GetRawText(),
    };

    private static string? Raw(JsonElement? value) => value is { ValueKind: not JsonValueKind.Undefined } found ? found.GetRawText() : null;

    private static bool True(JsonElement? value) => value is { ValueKind: JsonValueKind.True };

    private static bool Same(string? one, string? other) => one is not null && string.Equals(one, other, StringComparison.OrdinalIgnoreCase);

    /// <summary>The value at <paramref name="path"/> under <paramref name="element"/>, by property names; null where any is absent.</summary>
    private static JsonElement? At(JsonElement? element, params string[] path)
    {
        var at = element;
        foreach (var name in path)
        {
            if (at is not { ValueKind: JsonValueKind.Object } here || !here.TryGetProperty(name, out var next)) return null;
            at = next;
        }

        return at is { ValueKind: JsonValueKind.Null } ? null : at;
    }

    private static IEnumerable<JsonElement> Each(JsonElement? array) =>
        array is { ValueKind: JsonValueKind.Array } items ? items.EnumerateArray() : [];

    /// <summary>The first item of <paramref name="array"/> whose <paramref name="field"/> is <paramref name="value"/>, ignoring case.</summary>
    private static JsonElement? Named(JsonElement? array, string field, string? value) =>
        value is null ? null : Each(array).Where(each => Same(Text(At(each, field)), value)).Select(each => (JsonElement?)each).FirstOrDefault();

    /// <summary>The named fields of <paramref name="row"/>, each <c>name=value</c>, in the order named.</summary>
    private static string Pick(JsonElement? row, params string[] fields) =>
        string.Join("|", fields.Select(field => $"{field}={Text(At(row, field))}"));

    /// <summary>An array's items, each by <paramref name="id"/>'s fields and <paramref name="take"/>'s, sorted by its id.</summary>
    private static string Rows(JsonElement? array, string[] id, params string[] take) =>
        string.Join("\n", Each(array)
            .Select(each => (Id: Pick(each, id), Taken: Pick(each, take)))
            .OrderBy(each => each.Id, StringComparer.Ordinal)
            .Select(each => $"{each.Id}#{each.Taken}"));

    private static string Rows(JsonElement? array, string id, params string[] take) => Rows(array, [id], take);
}
