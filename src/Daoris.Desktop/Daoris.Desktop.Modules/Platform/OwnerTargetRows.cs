namespace Daoris.Desktop;

/// <summary>
/// The owner's marks route by route (STEWARDROLE1a-ii, D209 §5.4): for every route the census makes the person's or a window's, the
/// payload's fields that name what the owner acts on, or <c>none</c> by a closed reason (<see cref="OwnerNone"/>). <c>HELP_APPLY</c>
/// marks by the plan it applies. <c>OwnerSetCensusTests</c> holds every such route to a row, each row's fields to its handler, and a
/// route that changes a session or the driver's, the sessions', the landing's or the lines' state to naming its target.
/// </summary>
/// <remarks>
/// A route a module classes whole (a window's own) is one row by its module's name. A route added later is marked in the change that
/// adds it (<c>bridge-presses.md</c>).
/// </remarks>
internal static class OwnerTargetRows
{
    public static Dictionary<string, OwnerTarget> All()
    {
        var rows = new Dictionary<string, OwnerTarget>(StringComparer.Ordinal);

        void Row(string route, params OwnerField[] fields) => rows.Add(route, OwnerTarget.Of(fields));
        void None(string why, params string[] routes)
        {
            foreach (var route in routes) rows.Add(route, OwnerTarget.Nothing(why));
        }

        OwnerField Session(string field = "id") => new(field, OwnerKinds.Session);
        OwnerField Repository(string field = "repository") => new(field, OwnerKinds.Repository);
        OwnerField Workspace(string field = "workspace") => new(field, OwnerKinds.Workspace);
        OwnerField Quest(string field = "quest") => new(field, OwnerKinds.Quest);
        OwnerField Ask(string field = "ask") => new(field, OwnerKinds.Ask);

        // A session's run, its words and its end: its quest, and every successor of it, which share the quest.
        foreach (var route in new[]
                 {
                     "STOP_SESSION", "CANCEL_TURN", "END_CHAT", "RESOLVE_SESSION", "SESSION_QUIET_CARRY_ON", "SESSION_QUIET_LEAVE", "SESSION_GO_ON_NEW",
                     "SESSION_START_FROM", "SESSION_GO_ON_BRIEF", "SESSION_HAND_OVER", "SESSION_STOP_WAITING", "SESSION_HELPER_ASK", "SESSION_PARTNER_ASK",
                     "SESSION_INPUT", "SESSION_DELETE", "SESSION_GO_AHEAD", "SET_SESSION_OPTION", "STOP_TASK", "SUPERVISOR_OVERRULE", "HELP_START_FROM",
                     "HELP_RENAME", "HELP_PIN", "LAND_SESSION_TREE", "HANDOFF", "OPINION_ANYWAY", "OPINION_MYSELF", "ASK_OPINION", "STOP_OPINION",
                     "DISCARD_SESSION_TREE",
                 })
        {
            Row(route, Session());
        }

        foreach (var route in new[]
                 {
                     "SESSION_QUEUE_EDIT", "SESSION_HELPER_APPROVE", "SESSION_HELPER_DECLINE", "SESSION_PERMISSION_ANSWER", "SESSION_HAND_OVER_APPROVE",
                     "SESSION_HAND_OVER_DECLINE", "SESSION_ANSWER_ALONE",
                 })
        {
            Row(route, Session("session"));
        }

        rows.Add("SESSION_ARCHIVE", OwnerTarget.Of(new OwnerField("ids", OwnerKinds.Session, Many: true)));

        // Quests, asks and the work they are.
        Row("RETRY_QUEST", Quest());
        Row("WORK_PAUSE", Ask(), Quest());
        Row("WORK_RESUME", Ask(), Quest());
        Row("WORK_ABANDON", Ask(), Quest());
        Row("WORK_HELD", new OwnerField("unit", OwnerKinds.Unit));
        Row("LAND_ASK", Ask());
        Row("LAND_ENTRY", new OwnerField("entry", OwnerKinds.Entry));
        Row("CHANNEL_POST", Ask());
        Row("CHANNEL_FORWARD", Ask());
        Row("HISTORY_CLEAR", Workspace(), Quest(), Ask());
        // A review shown again publishes the driver's state, so it marks its quest rather than pass as acting on nothing (§5.4).
        Row("SHOW_REVIEW_AGAIN", Quest());

        // A repository's lines and branches, and its standing choices, at its scope or its workspace's.
        Row("DISCARD_SESSION_BRANCH", Repository());
        Row("RECOVERY_DROP", Repository());
        Row("SWEEP", Workspace());
        Row("TREES_SYNC", Repository(), Workspace());
        Row("SETUP_PRESS", Repository());
        foreach (var route in new[] { "SET_DRIVABLE", "SET_HOLD", "SET_TREES", "SET_STANDING", "SET_SESSION_CHECKS", "SET_HEAVY_GATES", "SET_WRITE_ACROSS" })
        {
            Row(route, Repository());
        }

        foreach (var route in new[]
                 {
                     "SET_LINE", "SET_LANGUAGE", "SET_LANDING", "SET_REVIEW", "SET_OPINION", "SET_HELPERS", "SET_HAND_OVER", "SET_COMMITS", "SET_ACCESS",
                     "SET_READ_ACROSS", "WORKFLOW_LANDED",
                 })
        {
            Row(route, Repository(), Workspace());
        }

        // Ask Daoris's Apply marks by the plan it applies, each proposal door declaring its target (HelpProposals.Owned).
        rows.Add("HELP_APPLY", new OwnerTarget([], ByPlan: true));

        // The owner's own conversation, begun where it names: no quest.
        None(OwnerNone.OwnConversation, "START_CHAT", "START_HELP");

        // An appointment acts on the role, never on the repository it names, so appointing the supervisor on X never holds the steward on X.
        None(OwnerNone.Appointment, "SET_APPOINTMENT");

        // What acts on nothing a role acts on: a folder opened, a notice or a proposal dismissed, a channel or news marked, a card answered
        // (whose press is marked as it runs), a rule.
        None(OwnerNone.ActsOnNothing,
            "SESSION_OPEN_FOLDER", "PLUGIN_OPEN_FOLDER", "WORK_RECORDS_FOLDER", "NOTICES_READ", "NOTICE_DISMISS", "HELP_DISMISS", "CHANNEL_MARK",
            "PERSON_CONFIRM", "PERSON_CONFIRM_SHOWN", "RULE_ACTION", "RULE_PROPOSAL");

        // The machine's own settings and programs: its plugins, its agents' accounts and its tools.
        None(OwnerNone.MachineSetting,
            "SET_ACTOR", "SET_AGENT_SETTINGS", "SET_CAP", "SET_COOLOFF", "SET_HELP_MODE", "SET_HELP_GRANT", "SET_KEEP", "SET_NOTIFY", "SET_STRIKES",
            "TRUST_FOLDER", "ACCOUNT_USE", "HARNESS_ACTION", "HARNESSES_REFRESH", "TOOLS_DELETE", "TOOLS_DOWNLOAD", "TOOLS_LOCATION", "TOOLS_STOP",
            "TOOLS_USE", "TOOLS_GIT", "SYSTEM_DOWNLOADS_CLEAR", "SYSTEM_SPACE_FREE", "SET_STANDING_CONFIRMATION", "PLUGIN_ACTION", "PLUGIN_UPDATE",
            "PLUGIN_NEW", "PLUGIN_TRY", "PLUGIN_ADD", "PLUGIN_INSTALL",
            PipeTargets.Key("DAORIS.REMOTES", "SET"), PipeTargets.Key("DAORIS.REMOTES", "REMOVE"));

        // A window's own, naming nothing a role acts on.
        None(OwnerNone.NamesNothing,
            "WORKFLOW_NODE", "HARNESS_INPUT", "HARNESS_CANCEL", "TOOLS_PICK", "PERSON_CONFIRMATIONS", "PERSON_KEY", "READ_LEAVE", "SERVICE_CONFIRMATIONS",
            PipeTargets.Key("DAORIS.REGISTRY", "PICK_FOLDER"), PipeTargets.Key("DAORIS.REGISTRY", "WRITE_DECLARATION"),
            "DAORIS.BROWSER", "DAORIS.LOG", "DAORIS.TERMINAL", "DAORIS.UPDATE", "DAORIS.WINDOWS");
        return rows;
    }
}
