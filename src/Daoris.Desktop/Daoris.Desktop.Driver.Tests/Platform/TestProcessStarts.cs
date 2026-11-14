using System.Text.RegularExpressions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>What a test's own start of a process is for: the purpose it names, or why no job of the one management holds it.</summary>
internal enum TestStartKind
{
    /// <summary>A program standing in for the thing under test (node, cmd, ping, powershell), started to be watched, held or ended by the test.</summary>
    StandIn,

    /// <summary>A Daoris program or the product's own command run as a process of its own, as a person's machine runs it: the test is of that process.</summary>
    Program,

    /// <summary>A server kept for a fixture's life (the service, git's http backend, an ssh or a TLS host) and stopped with it.</summary>
    Host,

    /// <summary>An agent's command as the provider lays it out, run bare: the test is of the command's words, not of the launcher.</summary>
    AgentCommand,

    /// <summary>A system command read to its end for a test's set-up (a junction, a version).</summary>
    Probe,

    /// <summary>The product's own start (git, or a stand-in agent binary) through a seam that counts it, then passes it on.</summary>
    Counted,

    /// <summary>Names a start in a sentence, a pattern or a planted source it scans, and starts nothing.</summary>
    Words,

    /// <summary>Builds a process object it never starts.</summary>
    Unstarted,
}

/// <summary>
/// Every direct start the desktop's tests make, named with its purpose or why no job holds it. A test starts a process the
/// product's gate cannot own: it stands in for the product's neighbour, runs a program whole, or builds a repository. Git through
/// <c>TestGit.Past</c> or <c>TestGit.Ceiling</c> is held by <c>GitCeilingCensusTests</c> and needs no entry here; every other file with a
/// direct start is listed under its kind, and a file that starts nothing is taken off. How many starts a listed file makes is not
/// pinned.
/// </summary>
internal static class TestProcessStarts
{
    private static readonly Regex Direct = new(@"\bProcess\.Start\b|\bnew\s+(?:System\.Diagnostics\.)?Process\s*[({]|\bCreateProcessW\s*\(");

    private static readonly Regex Ceiling = new(@"\bTestGit\.(?:Past|Ceiling)\(");

    private static readonly (TestStartKind Kind, string[] Files)[] Listed =
    [
        (TestStartKind.StandIn,
        [
            "Daoris.Desktop.Driver.Rehearsals/Agents/AgentSignInProcessTests.cs", "Daoris.Desktop.Driver.Tests/Agents/InAppBrowserServersTests.cs",
            "Daoris.Desktop.Driver.Tests/Agents/SessionProcessesProcessTests.cs", "Daoris.Desktop.Driver.Tests/Agents/LineageTokensTests.cs",
            "Daoris.Desktop.Driver.Tests/Conversations/ChatTurnTakingTests.cs", "Daoris.Desktop.Driver.Tests/Course/QuestPartStopProcessTests.cs",
            "Daoris.Desktop.Driver.Tests/Departments/FolderReturnProcessTests.cs", "Daoris.Desktop.Driver.Tests/Install/HeldFilesProcessTests.cs",
            "Daoris.Desktop.Driver.Tests/Loop/LineageExitProcessTests.cs", "Daoris.Desktop.Driver.Tests/Loop/LineageRaceProcessTests.cs",
            "Daoris.Desktop.Driver.Tests/Loop/OrphanedSessionTests.cs", "Daoris.Desktop.Driver.Tests/Platform/ProcessJobTests.cs",
            "Daoris.Desktop.Driver.Tests/Platform/OwnedProcessesProcessTests.cs", "Daoris.Desktop.Driver.Tests/Platform/GitPlaceExitTests.cs",
            "Daoris.Desktop.Driver.Tests/Review/HeavyRunProcessTests.cs",
            "Daoris.Desktop.Driver.Tests/Review/IntegrationVerdictProcessTests.cs", "Daoris.Desktop.Driver.Tests/Settings/DriverConfigFenceProcessTests.cs",
            "Daoris.Desktop.Driver.Tests/Store/KeepLockProcessTests.cs", "Daoris.Desktop.Driver.Tests/Store/RecordLockProcessTests.cs",
            "Daoris.Desktop.Driver.Tests/Workplace/GitLibraryFaultProcessTests.cs",
            "Daoris.Desktop.Driver.Tests/Workplace/GitRunnersEndTests.cs", "Daoris.Desktop.Driver.Rehearsals/Workplace/IntegrationPromoteGitTests.cs",
            "Daoris.Desktop.Driver.Tests/Workplace/TreeGuardTests.cs", "Daoris.Desktop.Driver.Rehearsals/Workplace/Pool/TreePoolStopsGitTests.cs",
            "Daoris.Desktop.Modules.Tests/Bridge/DriverModuleConversationProcessTests.cs", "Daoris.Desktop.Modules.Tests/Bridge/DriverModuleDriverTests.cs",
            "Daoris.Desktop.Modules.Tests/Bridge/DriverModuleSessionQuietTests.cs", "Daoris.Desktop.Modules.Tests/Bridge/ReleaseStandIn.cs",
            "Daoris.Desktop.Modules.Tests/Browser/ProcessBrowserChildTests.cs",
            "Daoris.Desktop.Modules.Tests/Loop/AtWorkProcessTests.cs", "Daoris.Desktop.Modules.Tests/Platform/StandingCallerTests.cs",
            "Daoris.Desktop.Modules.Tests/Update/AppWorkingFolderProcessTests.cs", "Daoris.Desktop.Modules.Tests/Update/HomePlacingProcessTests.cs",
        ]),
        (TestStartKind.Program,
        [
            "Daoris.Desktop.Driver.Tests/Agents/ServerKeeperProcessTests.cs", "Daoris.Desktop.Driver.Tests/Agents/ServerKeeperUtf8ProcessTests.cs",
            "Daoris.Desktop.Driver.Tests/Agents/ServerKeeperRestartProcessTests.cs", "Daoris.Desktop.Driver.Tests/Agents/ServerStartProcessTests.cs",
            "Daoris.Desktop.Driver.Tests/Agents/ServerKeeperToolsProcessTests.cs",
            "Daoris.Desktop.Driver.Tests/Course/MoveClaimProcessTests.cs", "Daoris.Desktop.Driver.Tests/DriverHostTests.cs",
            "Daoris.Desktop.Driver.Tests/DriverStartFailureTests.cs", "Daoris.Desktop.Driver.Tests/Instance/HeldChatProcessTests.cs",
            "Daoris.Desktop.Driver.Tests/Review/HeavyAppRunProcessTests.cs",
            "Daoris.Desktop.Driver.Tests/Install/ToolsChildProcessTests.cs", "Daoris.Desktop.Driver.Tests/Knowledge/KnowledgeConnectorStartTests.cs",
            "Daoris.Desktop.Driver.Tests/Participants/LineageHookProcessTests.cs", "Daoris.Desktop.Driver.Tests/Platform/DriverProgramWordsProcessTests.cs",
            "Daoris.Desktop.Driver.Tests/Platform/CommandLauncherProcessTests.cs", "Daoris.Desktop.Driver.Tests/Platform/HomeLeaseProcessTests.cs", "Daoris.Desktop.Driver.Tests/Platform/PieceLauncherProcessTests.cs",
            "Daoris.Desktop.Driver.Tests/Plugins/HookMarks.cs", "Daoris.Desktop.Driver.Tests/Plugins/PluginKitTests.cs",
            "Daoris.Desktop.Driver.Tests/Plugins/PluginToolsProcessTests.cs", "Daoris.Desktop.Driver.Tests/Plugins/PluginTrialShutdownTests.cs",
            "Daoris.Desktop.Driver.Tests/Workflows/Kinds/AgentStep/StepAgentProcessTests.cs",
            "Daoris.Desktop.Driver.Tests/Plugins/RelayHostProcessTests.cs", "Daoris.Desktop.Driver.Tests/Plugins/RelayOnceProcessTests.cs",
            "Daoris.Desktop.Driver.Tests/Setup/RepositoryCreateProcessTests.cs", "Daoris.Desktop.Driver.Tests/Workplace/GitDoorStartTests.cs", "Daoris.Desktop.Driver.Tests/Workplace/GitForWindowsProcessTests.cs",
            "Daoris.Desktop.Driver.Tests/Workplace/Pool/BuildAccountProcessTests.cs", "Daoris.Desktop.Driver.Tests/Workplace/Pool/TreeBaseBuildsProcessTests.cs",
            "Daoris.Desktop.Driver.Tests/Workplace/GitSshHoldsProcessTests.cs", "Daoris.Desktop.Driver.Tests/Workplace/SessionTreesTests.cs",
            "Daoris.Desktop.Modules.Tests/Browser/CodecPlaybackProcessTests.cs",
        ]),
        (TestStartKind.Host,
        [
            "Daoris.Desktop.Driver.Tests/Service/RealServiceHost.cs", "Daoris.Desktop.Driver.Tests/Service/ServiceHostEndProcessTests.cs",
            "Daoris.Desktop.Driver.Tests/Workplace/GitHttpBackendHost.cs",
            "Daoris.Desktop.Driver.Tests/Workplace/GitSshHost.cs", "Daoris.Desktop.Driver.Tests/Workplace/GitTlsHost.cs",
        ]),
        (TestStartKind.AgentCommand,
        [
            "Daoris.Desktop.Driver.Tests/Agents/NativePromptProcessTests.cs", "Daoris.Desktop.Driver.Tests/Agents/ProviderConformanceLive.cs",
            "Daoris.Desktop.Driver.Tests/Agents/StandInShimProcessTests.cs", "Daoris.Desktop.Driver.Tests/Participants/PacketOnlyReaderProcessTests.cs",
        ]),
        (TestStartKind.Probe,
        [
            "Daoris.Desktop.Driver.Tests/Knowledge/HelpCheckoutsGitTests.cs", "Daoris.Desktop.Driver.Tests/Plugins/RelayCopyTests.cs",
            "Daoris.Desktop.Driver.Tests/Workplace/FilePreviewLinkTests.cs", "Daoris.Desktop.Driver.Tests/Workplace/SpaceFloorTests.cs",
            "Daoris.Desktop.Driver.Tests/Workplace/Pool/BenchRuns.cs", "Daoris.Desktop.Driver.Tests/Workplace/Pool/BenchHolder.cs",
        ]),
        (TestStartKind.Counted,
        [
            "Daoris.Desktop.Driver.Tests/Agents/AgentSheetStartsTests.cs", "Daoris.Desktop.Modules.Tests/Bridge/AgentsOpenStartsTests.cs",
            "Daoris.Desktop.Driver.Tests/GitStartTally.cs", "Daoris.Desktop.Driver.Tests/Loop/StartTreeCloseGitTests.cs",
            "Daoris.Desktop.Driver.Rehearsals/Workplace/TidyGitCountTests.cs", "Daoris.Desktop.Driver.Rehearsals/Workplace/TidyEffectGitTests.cs",
            "Daoris.Desktop.Driver.Tests/Workplace/TidiedPartKeepingTests.cs", "Daoris.Desktop.Driver.Rehearsals/Workplace/TidyHomeBenchProcessTests.cs",
        ]),
        (TestStartKind.Words,
        [
            "Daoris.Desktop.Driver.Tests/StructureTests.cs",
            "Daoris.Desktop.Driver.Tests/Workplace/GitBranchesReadTests.cs", "Daoris.Desktop.Driver.Tests/Workplace/GitLaunchTests.cs",
            "Daoris.Desktop.Modules.Tests/Windows/TaskbarIdentityTests.cs",
        ]),
        (TestStartKind.Unstarted,
        [
            "Daoris.Desktop.Driver.Tests/Agents/DoorInputTests.cs", "Daoris.Desktop.Driver.Tests/Platform/ChildProcessTests.cs",
            "Daoris.Desktop.Modules.Tests/Update/UpdateRelaunchGateTests.cs",
        ]),
    ];

    /// <summary>The kind each listed file is named under.</summary>
    public static IReadOnlyDictionary<string, TestStartKind> Kinds { get; } =
        Listed.SelectMany(each => each.Files.Select(file => (file, each.Kind))).ToDictionary(each => each.file, each => each.Kind, StringComparer.Ordinal);

    /// <summary>A project's test sources by path under the desktop folder, line comments read out as the other starts censuses read them.</summary>
    public static List<(string File, string Source)> Sources(string project) =>
        [.. NoConsoleStarts.Read(NoConsoleStarts.Src(), "Daoris.Desktop/" + project)
            .Select(each => (each.File["Daoris.Desktop/".Length..], Regex.Replace(each.Source, @"//[^\n]*", "")))];

    /// <summary>The problems of <paramref name="project"/>'s sources: a direct start in a file that is neither git past the ceiling nor listed, and a listed file that starts none.</summary>
    public static List<string> Problems(IEnumerable<(string File, string Source)> sources, string project)
    {
        var problems = new List<string>();
        var starting = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (file, source) in sources)
        {
            if (!Direct.IsMatch(source)) continue;
            starting.Add(file);
            if (!Kinds.ContainsKey(file) && !Ceiling.IsMatch(source)) problems.Add($"{file}: a direct start of a process, not named");
        }

        problems.AddRange(Kinds.Keys.Where(file => file.StartsWith(project + "/", StringComparison.Ordinal) && !starting.Contains(file))
            .Select(file => $"{file} starts nothing directly now: take it off the list"));
        return problems;
    }
}
