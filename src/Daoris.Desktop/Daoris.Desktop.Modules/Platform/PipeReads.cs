using System.Text.Json;

namespace Daoris.Desktop;

/// <summary>
/// The read grounds a person's press names by <see cref="GroundReadAttribute"/>: the read each calls, the payload's fields it names,
/// and what its projection takes. A plan's projection takes the fields it names and none of the words, times and versions beside
/// them, so a press confirmed the next morning or after an update is refused only where what it acts on moved.
/// </summary>
/// <remarks>
/// Where the read answers no field the press names (the landings' commits are cut to eight characters, the sweep's and the sync's
/// rows carry no tip), the projection takes what the answer does carry: each commit's short id, a row's count of commits and its
/// kind; the session's tree head and a branch's tip are the <c>branch</c> grounds that see a commit itself. Where no read of the
/// bridge lists a wait by the press's id, the loop reads its own record (<see cref="LoopGround"/>).
/// </remarks>
internal static class PipeReads
{
    private static readonly TimeSpan Page = PipeGround.PageBound;

    /// <summary>The question a terminal's words answer, still open; only for a press naming one.</summary>
    public static PipeGround QuestionOpen() => Wait("SESSION_QUESTION", ["id", "question"], p => new { id = Text(p, "id") },
        (a, p) => Text(At(a, "question", "id")) == Text(p, "question") ? "open" : "").Only(p => Text(p, "question") is not null);

    /// <summary>A terminal's words with files bind the files' bytes, since the press reads them only once confirmed.</summary>
    public static PipeGround AttachedFiles() =>
        LoopGround.Plan("attachments", Page, "paths").Only(p => True(Field(p, "said")) && Each(Field(p, "paths")).Any());

    /// <summary>The word still held on its session.</summary>
    public static PipeGround QueuedWord() => Wait("SESSION_QUEUE", ["session", "id"], p => new { id = Text(p, "session") },
        (a, p) => Each(At(a, "queued")).Any(each => Text(At(each, "id")) == Text(p, "id")) ? "open" : "");

    public static PipeGround SessionOption() => Setting("SESSION_OPTIONS", ["id", "option"], p => new { id = Text(p, "id") },
        (a, p) => Text(At(Each(At(a, "options")).FirstOrDefault(each => Text(At(each, "id")) == Text(p, "option")), "current")) ?? "",
        p => Text(p, "value"));

    public static PipeGround LiveTask() => Wait("SESSION_STREAMS", ["id", "key"], p => new { id = Text(p, "id") },
        (a, p) => Each(At(a, "streams")).Any(each => Text(At(each, "key")) == Text(p, "key") && True(At(each, "live")) && True(At(each, "canStop")))
            ? "open" : "");

    public static PipeGround HelperWait() => Wait("HELPER_WAITS", ["ask", "session"], _ => null,
        (a, p) => Each(At(a, "waits")).Any(each => Text(At(each, "id")) == Text(p, "ask")) ? "open" : "");

    public static PipeGround PermissionWait() => Wait("PERMISSION_WAITS", ["session", "request"], _ => null,
        (a, p) => Each(At(a, "waits")).Any(each => Text(At(each, "session")) == Text(p, "session") && Text(At(each, "request")) == Text(p, "request"))
            ? "open" : "");

    /// <summary>Absent <c>ask</c>, the press answers the session's current one: the one read at the ask is the one held to.</summary>
    public static PipeGround HandOverAsk() => Wait("SESSION_HAND_OVER_ASK", ["session", "ask"], p => new { id = Text(p, "session") },
        (a, p) => At(a, "ask") is { } ask && (Text(p, "ask") is not { } named || Text(At(ask, "id")) == named)
            ? $"{Text(At(ask, "id"))}|{Text(At(ask, "state"))}|{Text(At(ask, "code"))}"
            : "");

    public static PipeGround AnswerAloneWait() => Wait("ANSWER_WAITS", ["session"], _ => null,
        (a, p) => Text(At(Each(At(a, "waits")).FirstOrDefault(each => Text(At(each, "session")) == Text(p, "session")), "since")) ?? "");

    public static PipeGround StewardLand() => Wait("STEWARD_LANDS", ["ask"], _ => null,
        (a, p) => Each(At(a, "lands")).Any(each => Text(At(each, "ask")) == Text(p, "ask")) ? "open" : "");

    public static PipeGround SupervisorDecision() => Wait("SESSION_SUPERVISOR", ["id", "decision"], p => new { id = Text(p, "id") },
        (a, p) => Text(At(Each(At(a, "decisions")).FirstOrDefault(each => Text(At(each, "id")) == Text(p, "decision")), "state")) ?? "");

    /// <summary>A pause is a file, never a state: a Pause binds the work it would stop.</summary>
    public static PipeGround WorkToPause() => Plan("WORK_PLAN", ["ask", "quest"], WorkOf,
        (a, _) => Fp(
            Rows(At(a, "quests"), "quest", "status", "pause"),
            Rows(At(a, "sessions"), "session", "state"),
            At(a, "paused") is null ? "unpaused" : "paused"));

    /// <summary>A Resume binds the pause it would remove.</summary>
    public static PipeGround PauseToResume() => Plan("WORK_PLAN", ["ask", "quest"], WorkOf,
        (a, _) => At(a, "paused") is { } paused
            ? Fp(Text(At(paused, "at")), Rows(At(paused, "stopped"), "quest", "session"))
            : "unpaused");

    public static PipeGround WorkToAbandon() => Plan("WORK_PLAN", ["ask", "quest", "pieces"], WorkOf,
        (a, _) => Fp(
            Rows(At(a, "quests"), "quest", "status", "joined", "key", "abandon"),
            Rows(At(a, "sessions"), "session", "state"),
            string.Join(",", Each(At(a, "abandon", "pieces")).Select(each => Text(each)).Order(StringComparer.Ordinal))));

    public static PipeGround HeldAttempt() => Wait("WORK_STATE", ["unit", "node", "attempt"], p => new { unit = Text(p, "unit") },
        (a, p) => Each(At(a, "held")).Any(each => Text(At(each, "unit")) == Text(p, "unit") && Text(At(each, "node")) == Text(p, "node")
            && Raw(At(each, "attempt")) == PipeGround.Raw(p, "attempt") && At(each, "resolution") is null) ? "open" : "");

    /// <summary>Each landing read binds each part's tree head and each target by its full tip: the short ids it answers do not.</summary>
    public static PipeGround AskLanding() => Plan("ASK_LANDING", ["ask"], p => new { ask = Text(p, "ask") },
        (a, _) => Fp([.. Each(At(a, "entries")).OrderBy(each => Text(At(each, "entry")), StringComparer.Ordinal).Select(Entry)]),
        tips: (a, _) => Each(At(a, "entries")).SelectMany(EntryTips));

    public static PipeGround EntryLanding() =>
        Plan("ENTRY_LANDING", ["entry"], p => new { entry = Text(p, "entry") }, (a, _) => Entry(a), tips: (a, _) => EntryTips(a));

    public static PipeGround OpinionGate() =>
        Wait("OPINION_GATE", ["id"], p => new { id = Text(p, "id") },
            (a, _) => At(a, "opinion") is { } opinion ? $"{Text(At(opinion, "state"))}|{Raw(At(opinion, "holds"))}|{Text(At(opinion, "opinion"))}" : "");

    /// <summary>Each recovery ref the page listed, by its tip.</summary>
    public static PipeGround RecoveryRefs() => new BranchGround(["repository", "refs"], payload =>
        PipeGround.Text(payload, "repository") is { Length: > 0 } repository
            ? [.. Each(Field(payload, "refs")).Select(each => Text(At(each, "ref"))).OfType<string>().Select(name => (repository, name))]
            : []);

    public static PipeGround SweepPlan() => Plan("SWEEP_PLAN", ["workspace", "only"], p => new { workspace = Text(p, "workspace") },
        (a, _) => Fp(
            Rows(At(a, "branches"), ["repository", "branch"], "kind", "commits", "removable", "discardable"),
            Rows(At(a, "landed"), ["repository", "branch"], "kind", "commits", "removable")),
        tips: (a, _) => Branches(At(a, "branches"), "branch").Concat(Branches(At(a, "landed"), "branch")));

    /// <summary>Each line bound by its tip here and at origin, each rebase and delete by its branch's tip.</summary>
    public static PipeGround SyncPlan() => Plan("TREES_SYNC_PLAN", ["repository", "workspace", "all", "also", "only"],
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
            .Concat(Branches(At(a, "deletes"), "branch")));

    public static PipeGround HistoryPlan() => Plan("HISTORY_PLAN", ["workspace", "quest", "ask", "units"],
        p => new { workspace = Text(p, "workspace"), quest = Text(p, "quest"), ask = Text(p, "ask"), failed = Field(p, "failed") },
        (a, _) => Fp(string.Join(",", Each(At(a, "units"))
            .Select(each => $"{Text(At(each, "kind"))}:{Text(At(each, "id"))}:{Text(At(each, "keep", "code"))}").Order(StringComparer.Ordinal))),
        TimeSpan.FromMinutes(4));

    public static PipeGround Downloads() => Plan("SYSTEM_DOWNLOADS", ["versions"], _ => null,
        (a, _) => Fp(Rows(At(a, "versions"), ["kind", "name", "version", "unfinished"])));

    /// <summary>Its own <c>plan: true</c> form, read whole: a unit the press names that left the plan moved it.</summary>
    public static PipeGround SpacePlan() => Plan("SYSTEM_SPACE_FREE", [], _ => new { plan = true },
        (a, _) => Fp(
            Rows(At(a, "now"), ["kind", "id"]),
            string.Join(",", Each(At(a, "early")).SelectMany(group => Each(At(group, "units")))
                .Select(each => $"{Text(At(each, "kind"))}:{Text(At(each, "id"))}").Order(StringComparer.Ordinal))));

    public static PipeGround RuleProposal() => Wait("RULES", ["id"], _ => null,
        (a, p) => Text(At(Each(At(a, "proposals")).FirstOrDefault(each => Text(At(each, "id")) == PipeGround.Id(Text(p, "id"))), "state")) ?? "");

    /// <summary>The plan the person read, by its digest, which pins each repository's changes and the acceptances offered.</summary>
    public static PipeGround PlacePlan() => Plan("PLACE_PLAN",
        ["repositories", "preset", "opinion", "look", "adoption", "builds", "drive", "digest", "accept"],
        p => new
        {
            repositories = Field(p, "repositories"), preset = Text(p, "preset"), opinion = Field(p, "opinion"), look = Field(p, "look"),
            adoption = Field(p, "adoption"), builds = Text(p, "builds"), drive = Field(p, "drive"),
        },
        (a, _) => Fp(Text(At(a, "digest"))),
        TimeSpan.FromMinutes(2));

    /// <summary>The doctrine tool's status for the checkout and whether it presses.</summary>
    public static PipeGround ReconcilePlan() => Plan("RECONCILE_PLAN", ["repository"], p => new { repository = Text(p, "repository") },
        (a, _) => Fp(Raw(At(a, "status")), Text(At(a, "pressable"))),
        TimeSpan.FromMinutes(2));

    public static PipeGround StandingAnswer() => State(["repository", "says"],
        (a, p) => Text(At(Named(At(a, "standing"), "repository", Text(p, "repository")), "says")) ?? "", p => Text(p, "says")?.Trim() ?? "");

    public static PipeGround SessionChecks() => State(["repository", "checks"],
        (a, p) => Text(At(Named(At(a, "sessionChecks"), "repository", Text(p, "repository")), "checks"))?.ToLowerInvariant() ?? "",
        p => Text(p, "checks")?.ToLowerInvariant() ?? "");

    /// <summary>Set whole, in order: each gate by its name, its run and its folder; none, or an empty list, as none.</summary>
    public static PipeGround HeavyGates() => State(["repository", "gates"],
        (a, p) => Gates(At(Named(At(a, "heavyGates"), "repository", Text(p, "repository")), "gates")), p => Gates(Field(p, "gates")));

    public static PipeGround WriteAcross() => State(["repository", "to", "allow"],
        (a, p) => Each(At(Named(At(a, "writeAcross"), "repository", Text(p, "repository")), "to")).Any(each => Same(Text(each), Text(p, "to")))
            ? "true" : "false",
        p => Raw(Field(p, "allow")));

    public static PipeGround LandedRoute() => Setting("WORKFLOW_CURRENT", ["repository", "workspace", "workflow", "gates"],
        p => Text(p, "repository") is { } repository ? new { repository } : new { workspace = Text(p, "workspace") },
        (a, _) => Fp(Pick(At(a, "start", "landed"), "workflow", "level")));

    /// <summary>A place's routes as they stand, so a choice or a kind changed between the ask and the Confirm is never overwritten unseen.</summary>
    public static PipeGround PlaceRoutes() => Setting("WORKFLOW_CURRENT", ["repository", "workspace", "workflow", "kind", "version"],
        p => Text(p, "repository") is { } repository ? new { repository } : new { workspace = Text(p, "workspace") },
        (a, _) => Fp(Rows(At(a, "start", "routes"), "kind", "workflow", "level", "label")));

    public static PipeGround WorkspaceKinds() => Setting("WORKFLOW_CURRENT", ["workspace", "kind", "label", "paths", "drop"],
        p => new { workspace = Text(p, "workspace") },
        (a, _) => Fp(Rows(At(a, "start", "routes"), "kind", "workflow", "label", "paths")));

    /// <summary>A verification asked under the landed route and the gates accepted at the tip.</summary>
    public static PipeGround VerifiedRoute() => Setting("WORKFLOW_CURRENT", ["repository", "branch", "commit"],
        p => new { repository = Text(p, "repository") },
        (a, _) => Fp(Pick(At(a, "start", "landed"), "workflow", "level")));

    /// <summary>The blob accepted and the one at the line's tip, so a declaration the line changed is never accepted unseen.</summary>
    public static PipeGround IntegrationDeclaration() => Setting("WORKFLOW_CURRENT", ["repository", "gates", "clear"],
        p => new { repository = Text(p, "repository") },
        (a, _) => Fp(Pick(At(a, "start", "integration"), "gates", "state"), Pick(At(a, "start", "integration", "tip"), "gates")));

    public static PipeGround Notify() => State(["notify"], (a, _) => Raw(At(a, "notify")) ?? "", p => Raw(Field(p, "notify")));

    public static PipeGround CoolOff() => State(["minutes"], (a, _) => Raw(At(a, "coolOff")) ?? "", p => Raw(Field(p, "minutes")));

    public static PipeGround Quiet() => State(["minutes"], (a, _) => Raw(At(a, "quiet")) ?? "", p => Raw(Field(p, "minutes")));

    public static PipeGround Manifest() => State(["files", "minutes", "copy"],
        (a, p) => Raw(At(a, Field(p, "files") is not null ? "manifestFiles" : Field(p, "copy") is not null ? "manifestCopy" : "manifestMinutes")) ?? "",
        p => Raw(Field(p, "files") ?? Field(p, "copy") ?? Field(p, "minutes")));

    public static PipeGround Cap() => State(["cap"], (a, _) => Raw(At(a, "cap")) ?? "", p => Raw(Field(p, "cap")));

    public static PipeGround HelpMode() => State(["mode"], (a, _) => Text(At(a, "helpMode")) ?? "", p => Text(p, "mode"));

    public static PipeGround Terminal() => State(["tier"], (a, _) => Text(At(a, "terminal")) ?? "", p => Text(p, "tier"));

    public static PipeGround HelpGrant() => State(["grant", "operation", "chosen"],
        (a, p) => Each(At(Named(At(a, "helpGrants"), "id", Text(p, "grant") ?? Daoris.Driver.ConfiguredAccess.ActsGrant), "chosen")).Any(each => Text(each) == Text(p, "operation"))
            ? "true" : "false",
        p => Raw(Field(p, "chosen")));

    public static PipeGround RoleRow() => Setting("ROLES", ["change", "id"], _ => null, (a, p) => Fp(Raw(Named(At(a, "roles"), "id", Text(p, "id")))));

    /// <summary>The rows of every role the batch names, in the order it names them.</summary>
    public static PipeGround RoleRows() => Setting("ROLES", ["changes"], _ => null,
        (a, p) => Fp([.. Each(Field(p, "changes")).Select(change => Raw(Named(At(a, "roles"), "id", Text(change, "id"))))]));

    public static PipeGround KeepRule() => Setting("SYSTEM_SPACE", ["kind"], _ => null,
        (a, p) => Fp(Raw(At(Named(At(a, "kinds"), "id", Text(p, "kind")), "rule")), Raw(At(a, "volume", "floor"))));

    public static PipeGround ProfileSettings() => Setting("HARNESSES", ["door", "profile"], _ => null,
        (a, p) => Fp([.. Each(At(a, "harnesses")).SelectMany(each => Each(At(each, "profiles")))
            .Where(each => Text(At(each, "name")) == Text(p, "profile")).Select(each => Raw(At(each, "settings")))]));

    public static PipeGround RuleLists() => Setting("RULES", ["scope", "name", "list", "rule", "id"], _ => null,
        (a, _) => Fp(Raw(At(a, "scopes")), Raw(At(a, "defaults"))));

    public static PipeGround AgentAccounts() => Setting("ACCOUNTS", ["door"], _ => null, (a, _) => Fp(Raw(At(a, "agents"))));

    public static PipeGround ToolChoice() => Setting("TOOLS_LIST", ["tool"], _ => null,
        (a, p) => Fp(Pick(Named(At(a, "tools"), "tool", Text(p, "tool")), "way", "version", "file")));

    /// <summary>A git setting's value is a command the person wrote, so the grounds hold its key alone.</summary>
    public static PipeGround GitSetting() => Setting("TOOLS_LIST", ["key"], _ => null,
        (a, p) => Fp(Text(At(At(a, "git"), Text(p, "key") ?? "")) ?? ""),
        p => Fp(Text(p, "value") ?? ""));

    /// <summary>Compared over the identities as the list answers them, which hold no secret.</summary>
    public static PipeGround GitIdentity() => Setting("TOOLS_LIST", ["identity"], _ => null,
        (a, p) => Fp(Raw(Named(At(At(a, "identities"), "identities"), "id", Text(p, "identity"))) ?? ""));

    public static PipeGround GitPointers() => Setting("TOOLS_LIST", ["scope", "repository", "workspace", "host", "remote"], _ => null,
        (a, _) => Fp(Raw(At(At(a, "identities"), "identities")) ?? ""));

    public static PipeGround ToolLocations() => Setting("TOOLS_LIST", ["address"], _ => null,
        (a, _) => Fp(string.Join(",", Each(At(a, "locations")).Select(each => Text(At(each, "address")) ?? Text(each)).Order(StringComparer.Ordinal))));

    /// <summary>Over the workspace's address alone, never its key, which the read redacts.</summary>
    public static PipeGround RemoteWiring() => Setting("STATE", ["workspace"], _ => null,
        (a, p) => Text(At(Named(At(a, "remotes"), "workspace", Text(p, "workspace")), "url")) ?? "").Of("DAORIS.REMOTES");

    public static PipeGround BrowserFiles() => Setting("STATE", [], _ => null,
        (a, _) => Fp(Pick(a, "browser", "links", "extensions", "settingsProblem", "favoritesProblem"), Rows(At(a, "favorites"), ["url"], "title")))
        .Of("DAORIS.BROWSER");

    public static PipeGround HarnessToAct() => Plan("HARNESSES", ["door", "profile"], _ => null, (a, p) => Harness(a, Text(p, "door")));

    public static PipeGround HarnessToRefresh() => Plan("HARNESSES", ["agent", "profile"], _ => null, (a, p) => Harness(a, Text(p, "agent")));

    public static PipeGround PluginState() => Plan("PLUGINS", ["id"], _ => null,
        (a, p) => Fp(Pick(Named(At(a, "plugins"), "id", Text(p, "id")), "id", "version", "enabled", "problem", "listening"),
            Rows(At(Named(At(a, "plugins"), "id", Text(p, "id")), "helpServers"), ["name"], "state", "accepted", "allow", "writes")));

    public static PipeGround ToolVersions() =>
        Plan("TOOLS_LIST", ["tool", "version"], _ => null, (a, p) =>
        {
            var tool = Named(At(a, "tools"), "tool", Text(p, "tool"));
            return Fp(Pick(tool, "tool", "way", "version"), Rows(At(tool, "downloaded"), ["version"]), Raw(At(tool, "running")));
        });

    public static PipeGround ToolRunning() => Wait("TOOLS_LIST", ["tool"], _ => null,
        (a, p) => Raw(At(Named(At(a, "tools"), "tool", Text(p, "tool")), "running")) ?? "");

    /// <summary>A repository named in one of <c>STATE</c>'s lists, set by the payload's boolean <paramref name="flag"/>.</summary>
    internal static ReadGround Listed(string list, string flag) =>
        State(["repository", flag],
            (a, p) => Each(At(a, list)).Any(each => Same(Text(each), Text(p, "repository"))) ? "true" : "false",
            p => Raw(Field(p, flag)));

    /// <summary>
    /// A standing choice at a repository's or a workspace's scope: its row in <c>STATE</c>'s list or its workspace twin. One whose edit
    /// merges into the rule names no value it sets, so it is never <c>already</c>; one that sets a single value does.
    /// </summary>
    internal static ReadGround Scoped(string list, string workspaces, string? sets)
    {
        return State(["repository", "workspace"],
            (a, p) => Text(p, "repository") is { } repository
                ? Value(Named(At(a, list), "repository", repository))
                : Value(Named(At(a, workspaces), "workspace", Text(p, "workspace"))),
            sets is null ? null : p => Text(p, sets) ?? "");

        // A row's value without its scope's name: one field (a branch, a language) is said as its text; a rule as its JSON.
        static string Value(JsonElement? row)
        {
            if (row is not { ValueKind: JsonValueKind.Object } found) return "";
            var values = found.EnumerateObject().Where(each => each.Name is not ("repository" or "workspace")).ToList();
            return values.Count == 1 && values[0].Value.ValueKind == JsonValueKind.String ? values[0].Value.GetString()! : Fp([.. values.Select(each => $"{each.Name}={each.Value.GetRawText()}")]);
        }
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

    /// <summary>Heavy gates as set: each gate's name, run and folder, in order; none or an empty list as none.</summary>
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

    /// <summary><c>HARNESSES</c>' row for <paramref name="harness"/> (every row where none is named): its install and its accounts' sign-ins.</summary>
    private static string Harness(JsonElement answer, string? harness) =>
        Fp([.. Each(At(answer, "harnesses"))
            .Where(each => harness is null || Same(Text(At(each, "door")), harness))
            .OrderBy(each => Text(At(each, "door")), StringComparer.Ordinal)
            .Select(each => Fp(Pick(each, "door", "present", "version", "problem", "managed", "pinned"), Rows(At(each, "profiles"), ["name"], "login")))]);

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
