using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>The driver's state as <c>STATE</c> and every standing choice answer it.</summary>
internal static class DriverStateAnswer
{
    /// <param name="loop">The loop whose configuration and memory the answer reads.</param>
    /// <param name="retried">What *Try again* did: the quest, <c>marked</c> or <c>released</c>, and the stop's
    /// session for a release. Null for every other answer, which the bridge leaves out.</param>
    /// <param name="reviewed">What a review's procedure look found, on the answer to <c>SET_REVIEW</c> alone.</param>
    /// <param name="shownAgain">What *Show it again* said, on the answer to <c>SHOW_REVIEW_AGAIN</c> alone.</param>
    /// <param name="parksAt">On a *Try again*'s mark, the failed sessions the quest's line's failures rule parks it at again; else the machine's rule is said.</param>
    /// <remarks>
    /// A read of the loop alone, so the domains whose standing choices answer with it (Across, Lines) reach it by name over
    /// their loop, holding no other domain's class.
    /// </remarks>
    public static object State(DriverLoop loop, object? retried = null, object? reviewed = null, object? shownAgain = null, int? parksAt = null)
    {
        var config = DriverConfig.Load(loop.ConfigPath);
        var access = ConfiguredAccess.Read(loop.Home);
        // The browser's grant is its plugin's approval, drawn here read only from that record.
        var browser = HelpGrants.Read(loop.Home, PluginCatalog.Load(loop.Home, ProviderRegistry.Built().Names)).Browser;
        // The checkouts agents outside may read, from the machine's facts as last judged, never a read of its own;
        // none before the facts are judged, and the loop's next look pushes them.
        string[] readable = loop.Facts.Current.Value<HelpMachineWhole>(HelpFacts.Machine, HelpFacts.Whole) is { } whole
            ? [.. AcrossRules.Readable(config, whole.Snapshot.Repositories).Select(checkout => checkout.Repository)]
            : [];
        return new
        {
            Retried = retried,
            Reviewed = reviewed,
            ShownAgain = shownAgain,
            // Whether the loop's service is up: until it is, every route that reads it refuses *still coming up*,
            // so the status bar says starting rather than ready, which this file alone would claim.
            Ready = loop.Service is not null,
            loop.ConfigPath,
            // Where this machine's Daoris lives, and what establishing it did this start — a
            // machine-local path, answered only over this bridge, like every path here.
            loop.Home,
            config.Drivable,
            config.Holds,
            // The person's standing confirmation as This Daoris's row and the line in
            // Notifications read it: offered, on, the presses granted and built, since, a record not honoured, its count this run, and
            // the holds a terminal made under it. None where this shell serves no pipe.
            StandingConfirmation = loop.Pipe?.StandingConfirmation.Now(),
            config.Trees,
            config.Cap,
            config.PollSeconds,
            config.Notify,
            // The failed sessions that park a quest, from the supervisor's rules alone: a retried quest's own line's, else the machine's.
            Strikes = parksAt ?? SupervisorRuleBook.Read(loop.Home).MachineFailures,
            // The cool-off a limit naming no time takes, in minutes: the file's, or the hour when it sets none.
            CoolOff = (int)config.CoolOff.TotalMinutes,
            // How long a session may be quiet before Daoris looks in, in minutes: the file's, or the default.
            Quiet = (int)QuietSetting.Of(config).TotalMinutes,
            config.Forgiven,
            // The stops the person released, the quest against the session, as the terminal lists them.
            config.Released,
            // 🔴 Reported so the page can show it, and modelled on the record so no toggle deletes it.
            // Off is "" on the wire, never null: the bridge leaves a null out, and the page tells a
            // shell older than the intake by this field's absence.
            IntakeAgent = ActorKey.FirstAgent("intake", config) ?? "",
            // Off is "" on the wire for the intake's reason: a shell older than Ask Daoris sends no field.
            HelperAgent = ActorKey.FirstAgent("ask-daoris", config) ?? "",
            // Ask Daoris's access mode as the person chose it: `restricted` or `full`, what its next start reads. A
            // conversation's own, the mode its start got, is its record's (`HELP_CONVERSATIONS`' `mode`).
            config.HelpMode,
            // Restricted mode's access as the person chose it: each grant by its id, with the operations chosen and those it acts on,
            // so the page offers no other, and whether a running conversation was handed another choice its next turn takes. The
            // browser's is read only from its plugin's approval, with no `built`, so the page offers nothing to choose.
            HelpGrants = new[]
            {
                Grant(loop, ConfiguredAccess.HousekeepingGrant, access, ConfiguredAccess.HousekeepingBuilt),
                Grant(loop, ConfiguredAccess.SettingsGrant, access, ConfiguredAccess.SettingsBuilt),
                new HelpGrantRow(
                    ConfiguredAccess.CheckoutsGrant, [.. readable.Where(access.Reads)], readable,
                    loop.Chat?.GrantPending(ConfiguredAccess.CheckoutsGrant, access) == true, false, null),
                new HelpGrantRow(
                    "browser",
                    browser is { State: HelpGrants.Approved } ? ["reads", .. browser.Accepted] : [],
                    null, false, browser?.Changed == true, browser?.Plugin),
            },
            // The lines as set, as rows rather than an object's keys: a key policy on the
            // bridge would respell a repository's name.
            Lines = config.Lines.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { Repository = p.Key, Branch = p.Value }).ToArray(),
            WorkspaceLines = config.WorkspaceLines.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { Workspace = p.Key, Branch = p.Value }).ToArray(),
            // Reading and writing across as set, as rows for the same reason.
            ReadAcross = config.ReadAcross.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { Repository = p.Key, Read = p.Value }).ToArray(),
            WorkspaceReadAcross = config.WorkspaceReadAcross.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { Workspace = p.Key, Read = p.Value }).ToArray(),
            WriteAcross = config.WriteAcross.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { Repository = p.Key, To = p.Value }).ToArray(),
            // The repositories whose checkout the person let Daoris change the files of (off where absent), as names.
            CheckoutWrites = config.CheckoutWrites.Where(p => p.Value).Select(p => p.Key).Order(StringComparer.Ordinal).ToArray(),
            // Each repository's standing answer, as rows for the same reason; `at` in UTC, or null where unknown.
            Standing = config.Standing.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { Repository = p.Key, p.Value.Says, p.Value.At }).ToArray(),
            // This machine's heavy gates for each repository that marks none, as rows for the same reason, each list in
            // the order its gates run; a gate's folder left out where it runs in the checkout itself.
            HeavyGates = config.HeavyGates.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new
                {
                    Repository = p.Key,
                    Gates = p.Value.Select(gate => new { gate.Name, gate.Run, Cwd = gate.WorkingDirectory }).ToArray(),
                }).ToArray(),
            // Which checks each repository's sessions run under Current, as rows for the same reason.
            SessionChecks = config.SessionChecks.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { Repository = p.Key, Checks = p.Value }).ToArray(),
            // The session languages as set, as rows for the same reason, and the closed table each screen names its
            // options from, so adding a language is a row in the driver's table and needs no window catalogue.
            Languages = config.Languages.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { Repository = p.Key, Language = p.Value }).ToArray(),
            WorkspaceLanguages = config.WorkspaceLanguages.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { Workspace = p.Key, Language = p.Value }).ToArray(),
            LanguageTable = SessionLanguages.Table.Select(row => new { row.Code, row.Name }).ToArray(),
            // The review rules as set, as rows for the same reason: a repository's may be none here.
            Reviews = config.Reviews.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { Repository = p.Key, Rule = LinesRoutes.ReviewWire(p.Value) }).ToArray(),
            WorkspaceReviews = config.WorkspaceReviews.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { Workspace = p.Key, Rule = LinesRoutes.ReviewWire(p.Value) }).ToArray(),
            // The second-opinion rules as set, as rows for the same reason, each naming the working agent's family
            // over the loop's agents, a plugin's among them.
            Opinions = config.Opinions.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { Repository = p.Key, Rule = LinesRoutes.OpinionWire(p.Value, ActorChoices.BuildAgent(config), loop.Harnesses.Adapters) }).ToArray(),
            WorkspaceOpinions = config.WorkspaceOpinions.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { Workspace = p.Key, Rule = LinesRoutes.OpinionWire(p.Value, ActorChoices.BuildAgent(config), loop.Harnesses.Adapters) }).ToArray(),
            // The helpers and commit rules as set, as rows for the same reason: a workspace's Defaults reads its own here.
            Helpers = config.Helpers.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { Repository = p.Key, Rule = LinesRoutes.HelperWire(p.Value) }).ToArray(),
            WorkspaceHelpers = config.WorkspaceHelpers.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { Workspace = p.Key, Rule = LinesRoutes.HelperWire(p.Value) }).ToArray(),
            // The hand-over rules as set, as rows for the same reason: a workspace's Defaults reads its own here.
            HandOver = config.HandOver.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { Repository = p.Key, Rule = new { p.Value.Mode } }).ToArray(),
            WorkspaceHandOver = config.WorkspaceHandOver.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { Workspace = p.Key, Rule = new { p.Value.Mode } }).ToArray(),
            Commits = config.Commits.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { Repository = p.Key, Rule = LinesRoutes.CommitWire(p.Value) }).ToArray(),
            WorkspaceCommits = config.WorkspaceCommits.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { Workspace = p.Key, Rule = LinesRoutes.CommitWire(p.Value) }).ToArray(),
            // The access setups as set, as rows for the same reason, and this machine's, none set being no pause: a
            // workspace's Defaults and Settings read their own here, a repository's setup reads what it resolves to off LINES.
            Access = config.Access.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { Repository = p.Key, Setting = LinesRoutes.AccessWire(p.Value) }).ToArray(),
            WorkspaceAccess = config.WorkspaceAccess.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { Workspace = p.Key, Setting = LinesRoutes.AccessWire(p.Value) }).ToArray(),
            MachineAccess = LinesRoutes.AccessWire(config.MachineAccess),
            // Git access as set, level by level: a workspace's Defaults and Settings read their own here.
            GitAccess = LinesRoutes.GitLevelsOf(config).Repository.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { Repository = p.Key, Classes = LinesRoutes.GitClassesWire(p.Value) }).ToArray(),
            WorkspaceGitAccess = LinesRoutes.GitLevelsOf(config).Workspace.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { Workspace = p.Key, Classes = LinesRoutes.GitClassesWire(p.Value) }).ToArray(),
            MachineGitAccess = LinesRoutes.GitClassesWire(LinesRoutes.GitLevelsOf(config).Machine),
            // What each session holds of git access now: its repository's, narrowed by what the person tightened.
            SessionGitAccess = SessionGitAccessRoutes.Wire(loop.Home),
            Running = loop.Processes.Running,
            // Who is driving Daoris's browser: the running sessions handed a server that drives it.
            DrivingBrowser = loop.Processes.DrivingBrowser,
            // The Ask Daoris conversations running under another access than the one chosen: each ends at its turn's end,
            // and the page keeps it in view so its next words resume it under the new access.
            HelpSwapping = loop.Chat?.HelpSwapping(config.HelpMode) ?? [],
            // The set-ups waiting for the person here, each with whether Daoris serves its tab now: the
            // strip's chip beside the browser's door. Left out where the loop carries no browser, which shows nothing.
            InReview = loop.Reviews?.Waiting
                .Select(row => new { row.Quest, row.Title, row.Environment, row.Look, row.Shows, row.Served })
                .ToArray(),
        };
    }

    /// <summary>A grant kept in the home's record, as <c>helpGrants</c> answers it: chosen, built, and pending a running conversation's next turn.</summary>
    private static HelpGrantRow Grant(DriverLoop loop, string grant, ConfiguredAccess access, IReadOnlyList<string> built) =>
        new(grant, access.Of(grant), built, loop.Chat?.GrantPending(grant, access) == true, false, null);
}
