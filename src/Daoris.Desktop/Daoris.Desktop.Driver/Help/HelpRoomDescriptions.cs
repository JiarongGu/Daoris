namespace Daoris.Driver;

internal static class HelpRoomDescriptions
{
    internal static HelpMachine Describe(IHelpRoomSection section, HelpMachine machine, HelpMachineSources sources) => section switch
    {
        HelpRoomAgents => Agents.Describe(machine, sources),
        HelpRoomAsks => Asks.Describe(machine, sources),
        HelpRoomIntro => Intro.Describe(machine, sources),
        HelpRoomLanding => Landing.Describe(machine, sources),
        HelpRoomMachineNow => MachineNow.Describe(machine, sources),
        HelpRoomMayDo => MayDo.Describe(machine, sources),
        HelpRoomOwnPlugins => OwnPlugins.Describe(machine, sources),
        HelpRoomWork => Work.Describe(machine, sources),
        HelpRoomWorkspaces => Workspaces.Describe(machine, sources),
        _ => machine,
    };

    private static class Agents
    {
        public static HelpMachine Describe(HelpMachine machine, HelpMachineSources sources)
        {
            static string Spell(LoginState login) => login.ToString().ToLowerInvariant();
            return machine with
            {
                Agents = [.. sources.Roster.Select(report => new HelpAgent(report.Adapter)
                {
                    Product = sources.Product(report.Adapter),
                    Present = report.Present,
                    Version = report.Version,
                    Accounts = [.. report.Profiles.Select(profile => new HelpAccount(profile.Name, Spell(profile.Login)))],
                    Strays = report.Strays,
                })],
            };
        }
    }

    private static class Asks
    {
        public static HelpMachine Describe(HelpMachine machine, HelpMachineSources sources) => machine with
        {
            OpenAsks = [.. sources.Standing
                .Where(ask => !string.Equals(ask.State, "Closed", StringComparison.OrdinalIgnoreCase))
                .Select(ask => new HelpAsk(ask.Id, ask.Sentence, ask.Workspace, ask.State, ask.Quests))],
        };
    }

    private static class Intro
    {
        public static HelpMachine Describe(HelpMachine machine, HelpMachineSources sources) => machine with { Mode = sources.Config.HelpMode };
    }

    private static class Landing
    {
        public static HelpMachine Describe(HelpMachine machine, HelpMachineSources sources) =>
            machine with { LandingPlugins = HelpRoomLanding.LandingPluginsOf(sources.Plugins) };
    }

    private static class MachineNow
    {
        public static HelpMachine Describe(HelpMachine machine, HelpMachineSources sources) => machine with
        {
            Adapter = sources.Config.Adapter,
            // MODELROLE1d (D205 §6.4): Intake's and Ask Daoris's first agent, as their actors read; an order that does not read is said by
            // the actors' own line, and its key stands here.
            Intake = First("intake", sources.Config) ?? sources.Config.IntakeAdapter,
            Helper = First("ask-daoris", sources.Config) ?? sources.Config.HelperAdapter,
            Cap = sources.Config.Cap,
            // ENTRY1f2 (D161's ENTRY1f note): by the Sessions list's own rule, so the count and the list name the same sessions,
            // less Ask Daoris's own conversation, which opens here; a teammate's never reached the snapshot.
            Waiting = [.. sources.Snapshot.Active
                .Where(session => KnowledgeWorkOrders.Current.WaitsOnYou(session.Id, session.State, session.Answer) && session.Repository != HelpRoom.Repository)
                .Select(session => new HelpWaitingSession(session.Id, session.Repository) { Ask = session.Ask })],
            Sessions = sources.Sessions is { } look ? Placed(look) : null,
            Asks = sources.Asks,
            Plugins = [.. sources.Plugins.Plugins.Select(entry =>
                new HelpPlugin(entry.Manifest.Id, entry.Enabled, entry.Manifest.Hooks?.Points ?? [])
                {
                    Problem = entry.Problem,
                    // Its kind only: the folder it came from is a path on this machine, which the room never names.
                    Source = ((IKnowledgePlugins)sources.Plugins).SourceKind(entry.Folder),
                })],
            Landed = [.. sources.Landed.Select(entry => new HelpLanded(entry.Repository, entry.Branch, entry.Session, entry.Pushed, entry.PullRequest)
            {
                State = entry.PullRequestState,
                AskFailed = entry.PullRequestAskFailed,
                // ACCESSWORDS1b: only where the rule's switch accepted it, as the review's note reads it: a press under the setup keeps
                // the level on its record too, and the setup accepted nothing there.
                Access = entry.AcceptedBy == AcceptedBy.Auto ? entry.Rule?.Access : null,
            })],
            Parked = sources.Parked,
            Held = sources.Held,
            // PAUSE1f-b: the pauses the pause and the resume write (design §2.5), asks first, each in its ids' order.
            Paused = [.. sources.Config.PausedAsks.Keys.Order(StringComparer.Ordinal).Select(ask => new PausedBy(WorkScope.Ask, ask)),
                .. sources.Config.PausedQuests.Keys.Order(StringComparer.Ordinal).Select(quest => new PausedBy(WorkScope.Quest, quest))],
            Browser = sources.Browser is { } files ? new HelpBrowser(files.Browser, files.Links, files.Extensions, files.Favorites) : null,
            History = sources.History,
            // PARALLEL1a: what the judges' facts had not read yet, which each section says where it says its part.
            NotReadYet = sources.NotReadYet,
        };
    
        private static string? First(string actor, DriverConfig config) => ActorKey.FirstAgent(actor, config);
    
        /// <summary>
        /// This machine's sessions as the Sessions list places them (SESSUX1h3): one rule, <see cref="SessionGroups.Read"/>, less Ask
        /// Daoris's own conversations and a teammate's, which this machine does not act on; archived ones are in the list's own
        /// group, so in no count here. The ended come newest first, as the list orders them.
        /// </summary>
        private static HelpSessions Placed(IKnowledgeSessionLook look) => KnowledgeWorkOrders.Current.Placed(look);
    }

    private static class MayDo
    {
        /// <summary>
        /// The checkouts it may read: every readable one here, from the driver's own setting, in every workspace in <c>default</c>'s room.
        /// A workspace's room (ASKSCOPE1c, D158's ASKSCOPE1 note point 5) reads its own workspace's alone, of those reading across already
        /// allows (D107): they are what its spawn is granted, so the room lists what its rules allow.
        /// </summary>
        public static HelpMachine Describe(HelpMachine machine, HelpMachineSources sources)
        {
            var registry = sources.Registry ?? sources.Snapshot.Repositories;
            var workspaceOf = registry
                .GroupBy(known => known.Repository, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(named => named.Key, named => named.First().Workspace, StringComparer.OrdinalIgnoreCase);
            var described = Restricted(machine, sources, registry, workspaceOf);
            return machine.Mode == HelpModes.Full ? Full(described, sources) : described;
        }
    
        /// <summary>
        /// HELPMODE1a1 (D170 as D183 amends it): in full access its spawn is handed every server the plugins here declare, for it or for
        /// sessions, approved or not (<c>ChatRunner.HandServersAsync</c>), each once by name; so none waits, and none is offered.
        /// </summary>
        private static HelpMachine Full(HelpMachine machine, HelpMachineSources sources)
        {
            var named = new HashSet<string>(StringComparer.Ordinal);
            return machine with
            {
                PluginServers = [.. sources.Plugins.Contributing.SelectMany(plugin => plugin.Manifest.Servers
                    .Where(server => named.Add(server.Name))
                    .Select(server => new HelpPluginServer(plugin.Manifest.Id, server.Name, server.Allow) { Writes = server.Writes }))],
                PluginServersWaiting = [],
                PluginServersOffered = [],
            };
        }
    
        private static HelpMachine Restricted(
            HelpMachine machine, HelpMachineSources sources, IReadOnlyList<RepoView> registry, Dictionary<string, string> workspaceOf)
        {
            // ASKSCOPE1c: the grants intersect, its workspace's checkouts and those reading across allows; the whole registry stays the
            // one each checkout's workspace is named from.
            var readable = sources.Workspace is { } circle
                ? registry.Where(known => string.Equals(RemoteTarget.Workspace(known.Workspace), circle, StringComparison.OrdinalIgnoreCase))
                : registry;
            return machine with
            {
                Reads = [.. AcrossReads.Readable(sources.Config, readable)
                    .Select(checkout => new HelpRead(checkout.Repository, sources.Workspace ?? workspaceOf[checkout.Repository], checkout.Path))],
                // HELPPLUG1a: the servers the person approved for it, as its spawn is handed them (HelpGrants), and those waiting.
                PluginServers = [.. (sources.Grants?.All ?? []).Where(grant => grant.State == HelpGrantStates.Approved)
                    .Select(grant => new HelpPluginServer(grant.Plugin, grant.Server.Name, grant.Server.Allow) { Writes = grant.Accepted })],
                // HELPPLUG1c: what each would bring, reads and writes apart, and one an update changed since its approval.
                PluginServersWaiting = [.. (sources.Grants?.All ?? []).Where(grant => grant.State == HelpGrantStates.Waits)
                    .Select(grant => new HelpPluginServer(grant.Plugin, grant.Server.Name, grant.Server.Allow)
                    {
                        Writes = grant.Server.Writes, Changed = grant.Changed,
                    })],
                PluginServersOffered = [.. Offered(sources)],
                // HELPPLUG1b: the skills its spawn is handed (SpawnSkills.Write), as the harness names them.
                PluginSkills = [.. sources.Plugins.Contributing.SelectMany(plugin => plugin.Manifest.Skills
                    .Where(skill => skill.HandedTo(PluginServerTo.Help) && SkillFiles.Judge(plugin.Folder, skill.Name) is null)
                    .Select(skill => $"{plugin.Manifest.Id}:{skill.Name}"))],
            };
        }
    
        /// <summary>
        /// HELPPLUG1c: each server an installed plugin's newer copy in the install would offer Ask Daoris and the installed copy does
        /// not, as an installed in-app browser 1.0.0 is beside the shipped 1.1.0: said with its update first, so it is not missing
        /// in silence.
        /// </summary>
        private static IEnumerable<HelpPluginServerOffered> Offered(HelpMachineSources sources)
        {
            foreach (var offer in sources.Offers.Where(offer => offer.Installed && offer.Problem is null))
            {
                if (sources.Plugins.Plugins.FirstOrDefault(entry => string.Equals(entry.Manifest.Id, offer.Id, StringComparison.OrdinalIgnoreCase))
                    is not { } installed)
                {
                    continue;
                }
    
                foreach (var server in offer.Manifest.Servers.Where(server => server.HandedTo(PluginServerTo.Help)
                    && !installed.Manifest.Servers.Any(each => each.Name == server.Name && each.HandedTo(PluginServerTo.Help))))
                {
                    yield return new HelpPluginServerOffered(installed.Manifest.Id, server.Name, installed.Manifest.Version, offer.Manifest.Version);
                }
            }
        }
    }

    private static class OwnPlugins
    {
        public static HelpMachine Describe(HelpMachine machine, HelpMachineSources sources) => machine with
        {
            Offers = [.. sources.Offers.Where(offer => !offer.Installed && offer.Problem is null).Select(offer =>
                new HelpOffer(offer.Id, offer.Manifest.Name, offer.Manifest.Version, offer.Manifest.Hooks?.Points ?? [], offer.Needs)
                {
                    Harnesses = [.. offer.Manifest.Harnesses.Select(harness => harness.Name)],
                    Servers = [.. offer.Manifest.Servers.Where(server => server.HandedTo(PluginServerTo.Sessions)).Select(server => server.Name)],
                    HelpServers = [.. offer.Manifest.Servers.Where(server => server.HandedTo(PluginServerTo.Help)).Select(server => server.Name)],
                })],
        };
    }

    private static class Work
    {
        public static HelpMachine Describe(HelpMachine machine, HelpMachineSources sources) =>
            machine with { Work = sources.Work, WorkUnread = [.. sources.WorkUnread.Select(unread => unread.Said)], WorkRecovery = sources.WorkRecovery };
    }

    private static class Workspaces
    {
        public static HelpMachine Describe(HelpMachine machine, HelpMachineSources sources)
        {
            var config = sources.Config;
            var lineOf = sources.Lines.ToDictionary(line => line.Repository, StringComparer.OrdinalIgnoreCase);
            bool Named(IReadOnlyList<string> list, string repository) => list.Contains(repository, StringComparer.OrdinalIgnoreCase);
    
            return machine with
            {
                Repositories = [.. sources.Snapshot.Repositories.Select(known => new HelpRepository(known.Repository, known.Workspace)
                {
                    Checkout = known.Root is { Length: > 0 },
                    Drivable = Named(config.Drivable, known.Repository),
                    Held = Named(config.Holds, known.Repository),
                    OwnTree = config.OpensOwnTree(known.Repository),
                    Line = lineOf.TryGetValue(known.Repository, out var line) ? new Line(line.Branch, line.Source) : new Line(null, LineSource.None),
                    // PARALLEL1a: a checkout whose line the facts have not judged yet, said so rather than as none git can name.
                    LineNotReadYet = Named(sources.NotReadYet.Repositories, known.Repository),
                    Landing = KnowledgeWorkplace.Current.ChooseLanding(config, known.Repository, known.Workspace),
                    // KNOWUSE1b: what the person says holds for every session there, in their words.
                    Standing = config.StandingFor(known.Repository)?.Says,
                    // REVIEWENV1a: where its work is shown to the person before it lands, and where that was set.
                    Review = KnowledgeReviews.Current.Review(config, known.Repository, known.Workspace),
                    // XAGENT1a: which other agent reads its work before it lands, and where that was set.
                    Opinion = KnowledgeReviews.Current.Opinion(config, known.Repository, known.Workspace),
                    // WORKFLOW1h-b (design §8.4): the workflow its new work follows, and what chose it.
                    Workflow = Followed(config, sources.Workflows, known.Repository, known.Workspace),
                })],
                WorkflowKinds =
                [
                    .. config.WorkspaceWorkflows.OrderBy(pair => pair.Key, StringComparer.Ordinal).SelectMany(pair => pair.Value.Kinds
                        .Select(kind => new HelpWorkflowKind(pair.Key, kind.Key, kind.Value.Label, kind.Value.Paths, kind.Value.Workflow))),
                ],
            };
        }
    
        /// <summary>
        /// The workflow a repository's work follows with no task's kind (design §4.1, rows 3, 5 and 6), Current where nothing chooses
        /// one, its newest version where it is saved here, and a repository's own kinds.
        /// </summary>
        private static HelpWorkflowFollowed Followed(DriverConfig config, IReadOnlyList<IKnowledgeWorkflowFile>? saved, string repository, string workspace)
        {
            var selected = KnowledgeWorkflows.Current.Select(config, repository, workspace);
            var own = config.Workflows.FirstOrDefault(pair => string.Equals(pair.Key, repository, StringComparison.OrdinalIgnoreCase)).Value;
            var followed = new HelpWorkflowFollowed(selected.Workflow, selected.Level)
            {
                Kinds = [.. own?.Kinds.Select(kind => (kind.Key, kind.Value.Workflow)) ?? []],
            };
            if (selected.Workflow == WorkflowLevels.Current || saved is null) return followed;
    
            var found = saved.FirstOrDefault(each => each.Id == selected.Workflow);
            return found switch
            {
                null => followed with { Problem = "not saved here" },
                { Problem: not null } => followed with { Problem = "which cannot be read here" },
                _ => followed with { Name = found.Name, Version = found.NewestVersion },
            };
        }
    }
}
