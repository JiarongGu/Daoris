/**
 * The lane table, as data: which gates can see a changed path. The merge tool's tests hold it to what each suite
 * reads and each rehearsal runs, so a gate a merge skips is one that could not see the change.
 */
import { fileURLToPath } from 'node:url';
import { reachRows } from './reach-rows.ts';
import { readWidePaths } from './verification.ts';

export type Rule = { paths?: readonly string[]; lane?: string; gates: '*' | readonly string[]; why: string };

// The declared wide paths, read apart from the rule's budget; a verification declared without them fails here, at load.
const WIDE_PATHS: readonly string[] = readWidePaths(fileURLToPath(new URL('../..', import.meta.url)));

// The .NET suites with a test that reads the page's catalogues or sources.
const PAGE_READERS = ['service', 'driver', 'modules', 'censuses'];

const DESKTOP_SUITES = ['driver', 'modules', 'censuses', 'driver-process', 'driver-process-git', 'driver-process-git-pinned', 'modules-process'];

// What the CLI's sources reach; a narrow rule for one of its files names these as well, since the first rule decides.
const CLI_READERS = ['driver', 'rehearse', 'rehearse-family', 'web', 'deployment'];

// What the modules lane's sources reach, named by its narrow rule for the C# sources as well.
const MODULES_READERS = ['modules', 'censuses', 'modules-process', 'deployment'];

// What the driver lane's sources reach, named by its narrow rule for the sources the page's twins parse as well.
const DRIVER_READERS = [...DESKTOP_SUITES, 'rehearse-family', 'deployment'];

// The packages' lock, which a port, a contract case or a package moves; each package's suite runs in its own development.
const PACKAGE_SUITES = ['packages-lock'];

/**
 * Which gates can see a changed path, beyond the baseline: the first rule that places the path decides.
 * A rule places paths by glob (`paths`, the lanes file's grammar) or by a lane of `daoris.lanes.json`
 * (`lane`). Narrow rules come before the lanes, and the laneless paths after. `'*'` is every gate. A
 * path no rule places runs every gate, and so does a gate no rule names. A gate named here that the
 * repository does not run names nothing.
 *
 * Held by `merge-branch.test.ts` to what each .NET suite reads of the repository, to what each rehearsal
 * imports and starts, and to every tracked file and lane. What a fast suite may read is given generously:
 * it costs minutes. The precision is in the Process halves and the rehearsals, which cost the hours.
 */
// test-map: strings name paths as data — this table and the gates' commands name the paths they place, which the tool does not
// read, so the census of test units follows this file's imports and not its strings.
export const REACH: readonly Rule[] = Object.freeze<Rule[]>([
  {
    paths: WIDE_PATHS,
    gates: '*',
    why: "it changes how every gate runs or reaches a path, a declared wide path (D349): the gates, lanes and test tools, the scripts, dependencies, lockfiles and build files, the shared contracts, line endings, what is tracked, or the file helpers every rehearsal imports and the git they start (GITSTART4)",
  },
  // Before the merge tool's own rule, which would place it there alone: the driver's twin reads the attribution contract too.
  {
    paths: ['tools/merge/attribution-contract.json'],
    gates: ['tool-tests', 'tool-e2e', 'driver'],
    why: "the merge tool's commit attribution and the driver's CommitAttributionContractTests read the one contract",
  },
  { paths: ['tools/workflows/**'], gates: ['driver'], why: "the driver's tests read this repository's own workflow files and apply them as the install does" },
  // The tests of the tools only the parent runs (the merge tool, the install's publisher, the family rehearsal's kit) are
  // the tool-tests gate, no longer part of verify. These rules come first and keep what each path reached before.
  // The merge tool's own files reach its end-to-end tier as well (`tool-e2e`): the gate declares the same files.
  {
    paths: [
      'tools/merge-branch.mjs', 'tools/merge-branch/**', 'tools/reached-checks.mjs', 'tools/merge-backlog.mjs', 'tools/merge-queue.mjs',
      'src/Daoris.Cli/tool-test/**', 'tools/merge/**', 'tools/vitest.config.ts',
    ],
    gates: ['tool-tests', 'tool-e2e'],
    why: "the merge tool, its modules, the merge queue, its reached checks and backlog driver, and the tools' tests: the unit tier `tool-tests` runs and the end-to-end tier `tool-e2e`, whose specs start the merge tool in scratch repositories (GATESLIM1, TOOLTIER1)",
  },
  { paths: ['tools/as-merged.mjs', 'tools/as-merged.test.mjs'], gates: ['tool-e2e'], why: "the merge's commit as a check reads it, and its tests merge in scratch repositories in the end-to-end tier (TOOLTIER1)" },
  {
    paths: [
      'tools/move-code.mjs', 'tools/doc-duplicates.mjs', 'tools/process-reach.mjs', 'tools/test-recall.mjs',
      'tools/worktree-setup.mjs', 'src/Daoris.Cli/test/_fixture.ts',
    ],
    gates: ['tool-tests'],
    why: "the merge tool's, the publisher's and the set-up kit's tests, which `tool-tests` runs and verify does not (GATESLIM1): the merge tool, its modules and what it imports (the Process census, PROCREACH1, among them), the mover that splits it, the census's failure reader and recall (CISCOPE1c; the census itself is a declared wide path), the merge queue and the admission record (CISCOPE1e), the tests, and the fixture they share",
  },
  // The machine's heavy-run admission, which the merge tool's gates take places in; its own tests run in verify.
  {
    paths: ['tools/machine-admission.mjs', 'tools/machine-admission.test.mjs'],
    gates: ['cli', 'tool-tests', 'driver', 'driver-process'],
    why: "the machine's heavy-run admission the merge tool's gates take places in (HEAVYQUEUE1): its table-driven tests run in verify, the merge tool's tests hold its use, and the driver's whole-take Process class (INTEGRATE1f part f2) starts the tool's admission beside the driver's",
  },
  {
    paths: ['src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/machine-admission.json'],
    gates: ['driver', 'driver-process', 'cli'],
    why: "the driver's MachineAdmissionTests, its whole take's Process class (INTEGRATE1f part f2) and the merge tool's admission tests (run in verify) read the admission's table (HEAVYQUEUE1)",
  },
  {
    paths: ['tools/rehearsal-kit.mjs'],
    gates: ['tool-tests', 'rehearse', 'rehearse-family', 'deployment'],
    why: "the rehearsals' kit, which the set-up kit's tool-tests import, and the port test it takes each run's ports with (REHEARSEPORT1, GATESLIM1)",
  },
  {
    paths: ['tools/cdp.mjs'],
    gates: ['rehearse', 'rehearse-family', 'deployment'],
    why: "the port test the rehearsals take each run's ports with (REHEARSEPORT1)",
  },
  { paths: ['tools/release-rehearsal.mjs', 'tools/release-prep.mjs'], gates: ['rehearse'], why: 'release tooling' },
  {
    paths: ['tools/service-publish.mjs'],
    gates: ['tool-tests', 'rehearse', 'deployment', 'rehearse-family'],
    why: "release tooling, and the hosts' one build recipe (CONNECTOR1), which desktop-publish imports: the deployment rehearsal drives it, the family rehearsal runs usage-report, which imports it, and the publish's tool-tests call it (GATESLIM1)",
  },
  { paths: ['tools/stage-package.mjs'], gates: ['rehearse', 'deployment'], why: "every pack runs it: the release rehearsal's, and the install's doctrine tool" },
  {
    paths: ['tools/family-rehearsal.mjs', 'tools/rehearsal-apart.mjs', 'tools/stub-two.mjs', 'tools/usage-report.mjs', 'tools/usage-report/**'],
    gates: ['rehearse-family'],
    why: 'the family rehearsal, its parts: among them the workflow verbs it sets a place\'s work apart with',
  },
  // The stand-ins' reader, which its own tool test and the family rehearsal import.
  { paths: ['tools/stand-ins.mjs'], gates: ['tool-tests', 'rehearse-family'], why: "the stand-ins' reader: its tool test (stand-ins.test.ts) and the family rehearsal, which finds the providers' stand-ins by it (AGENTPROVIDER1b)" },
  // The same row's files in each provider's folder: the driver's (its lane's readers, ProviderFilesTests among them), and the tool
  // test and the family rehearsal read them through `stand-ins.mjs`. A narrow rule, since the first rule decides.
  ...reachRows(DRIVER_READERS),
  { paths: ['tools/setup-kit.mjs'], gates: ['tool-tests', 'rehearse-family'], why: "the family rehearsal's set-up kit, and its tool-tests, which run it for real in a scratch repository (GATESLIM1)" },
  {
    paths: ['tools/desktop-publish.mjs', 'tools/desktop-publish/**', 'tools/ci-background.mjs', 'tools/test-failures.mjs'],
    gates: ['tool-tests', 'deployment', 'rehearse-family'],
    why: 'the publish the deployment rehearsal drives, and what it imports (the background run\'s records, CISCOPE1d, and the failure reader they import, CISCOPE1c, both of which the merge tool imports too); the family rehearsal runs usage-report, which imports it; and its tool-tests (GATESLIM1)',
  },
  {
    paths: ['tools/processes.mjs'],
    gates: ['tool-tests', 'deployment', 'rehearse-family', 'rehearse'],
    why: "what the publish imports (the rule above), and what the rehearsals' kit ends a timed-out command's leftovers with, so all three rehearsals (FAMILYFAIL1)",
  },
  { paths: ['tools/deployment-rehearsal.mjs', 'tools/desktop.mjs', 'tools/desktop/**'], gates: ['deployment'], why: 'the deployment rehearsal and what it imports' },
  { paths: ['src/Daoris.Devkit/**'], gates: ['devkit', 'layers'], why: 'its own suite, and the layers census, which counts its tests; it is also the universal gates and the code map, which every merge runs' },
  { paths: ['src/Daoris.Desktop/process.runsettings'], gates: ['driver', 'driver-process', 'modules-process'], why: "the Process halves' settings (MOD8), which the driver's CategoryTests read (PROCPAR1)" },
  { paths: ['src/Daoris.Desktop/git.process.runsettings'], gates: ['driver-process-git'], why: "the driver's git-only Process classes' settings, four at once (PROC1c)" },
  { paths: ['src/Daoris.Desktop/git.pinned.process.runsettings'], gates: ['driver-process-git-pinned'], why: "the settings of the git library's proofs run on the pinned MinGit (GITLIB1d)" },
  { paths: ['src/Daoris.Desktop/rehearsals.process.runsettings'], gates: ['driver-rehearsals'], why: "the driver's rehearsal tier's settings, every Process class four at once (LAYERS1 e)" },
  // Before the driver's tests, which would place it first: the page's twin reads it too.
  // A vitest's read reaches the web gate's unit run, which runs the vitests; the web gate builds and runs Playwright.
  {
    paths: ['src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/kept-names.json'],
    gates: ['driver', 'web-unit'],
    why: "the driver's KeptNamesTwinTests and the page's sessionActs vitest read the kept names' table (SESSDEL1c)",
  },
  {
    paths: ['src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/hand-over-order.json'],
    gates: ['driver', 'web-unit'],
    why: "the driver's HandOverOrderTests and the page's handOverOrder vitest read the person's order's table (MIXAGENT1c)",
  },
  {
    paths: ['src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/workflow-run-said.json'],
    gates: ['driver', 'web-unit', 'censuses'],
    why: "the driver's WorkflowRunSaidTwinTests, its WorkFrameCensusTests (each wait a progress makes said) and the page's runSaid vitest read a run's English table (WORKFLOW1c5)",
  },
  {
    paths: ['src/Daoris.Desktop/Directory.Build.props'],
    gates: [...DESKTOP_SUITES, 'rehearse-family', 'deployment'],
    why: 'every desktop project builds with it: the suites, the headless host the family rehearsal drives, and the deployed shell',
  },
  { paths: ['src/Daoris.Desktop/README.md'], gates: ['modules', 'service'], why: "the modules' route test reads its table, and the service's suite reads this repository's documents" },
  // BRAINSTORM1a2: the brainstorm answers' table sits among the driver's tests, and the service's twin validator reads it too.
  // BRAINSTORM1a3: so does the brainstorm files' table, which the service's brainstorm desk reads.
  {
    paths: [
      'src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/brainstorm-answers.json',
      'src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/brainstorm-files.json',
    ],
    gates: ['driver', 'service'],
    why: "the driver's BrainstormValidationTests and BrainstormFilesTwinTests and the service's McpBrainstormToolTests and BrainstormDeskTests read the brainstorm tables (BRAINSTORM1a2, BRAINSTORM1a3)",
  },
  // Before the driver's tests, which would place it first: the modules' test reads it too.
  {
    paths: ['src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/checkout-scope.json'],
    gates: ['driver', 'modules'],
    why: "the driver's TreesCommandTests and the modules' DriverModuleLinesTests read the checkout-scope table (BRSCOPE1b)",
  },
  // The backlog merges' table sits among the driver's tests, and the merge tool's twin reads it too.
  {
    paths: ['src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/backlog-merges.json'],
    gates: ['driver', 'tool-tests'],
    why: "the driver's BacklogMergeTests and the merge tool's tests read the backlog merges' table (LANDBACKLOG1)",
  },
  // The integration declarations' table sits among the driver's tests, and the merge tool's twin reads it too.
  {
    paths: ['src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/integration-declarations.json'],
    gates: ['driver', 'tool-tests'],
    why: "the driver's IntegrationDeclarationTests and the merge tool's tests read the integration declarations' table (INTEGRATE1d)",
  },
  // The agent family's shared tables moved to the driver's fixtures with their last CLI
  // readers gone; the page reads three of them, and the modules one. The vendor's files and the reads table are the driver's alone.
  { paths: ['src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/shell-words.json'], gates: ['driver', 'web-unit'], why: "the driver's ShellWord twin and the page's shellWord read the shell-word table (ACCTQUOTE1b), and the CLI's suite holds it against the shells" },
  { paths: ['src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/rotation-use.json'], gates: ['driver', 'web-unit'], why: "the driver's RotationUseTwinTests and the page's curve rules read the shared rotation-use table (SWITCH1a, SWITCH1d)" },
  { paths: ['src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/usage-curves.json'], gates: ['driver', 'modules', 'web-unit'], why: "the driver's UsageCurvesTwinTests, the modules' DriverModuleAccountsTests (the verdicts .NET pushes, PUSH1e) and the page's curve rules read the shared usage curve table (SWITCH1b-iii, SWITCH1d)" },
  { paths: ['src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/park-question.json'], gates: ['driver', 'web-unit'], why: "the driver's QuestionFilesTests and the page's parkQuestion.test.ts read the shared table of a park's question as each reader takes it (PARKVIEW1)" },
  {
    paths: ['src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/provider-census.json'],
    gates: ['driver', 'service', 'web-unit', 'tool-tests', 'censuses'],
    why: "the driver's ProviderCensusTests, the service's, the page's providerCensus.test.ts and the tools' provider-census.test.ts read the provider census's exceptions (AGENTPROVIDER1a)",
  },
  // Before the driver's tests, which would place it first: the modules' twin is its one reader.
  {
    paths: ['src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/landing-says.json'],
    gates: ['modules'],
    why: "the modules' LandingSaysTwinTests read the landing sentences' table, the terminal's words and the card's",
  },
  { paths: ['src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/**'], gates: ['driver', 'driver-process', 'driver-process-git', 'driver-process-git-pinned', 'censuses', 'driver-rehearsals', 'layers'], why: "the driver's tests, the fast half and the Process halves, the censuses among them, the rehearsals, which compile its git fixtures in, and the layers census, which counts its clock reads" },
  { paths: ['src/Daoris.Desktop/Daoris.Desktop.Modules.Tests/**'], gates: ['modules', 'modules-process', 'censuses', 'layers'], why: "the modules' tests, both halves, and the censuses among them, the layers census among them" },
  // Before the service's tests, which would place it first: the driver's twin reads it too.
  {
    paths: ['src/Daoris.Service/Daoris.Service.Tests/fixtures/history-words.json'],
    gates: ['service', 'driver'],
    why: "the service's and the driver's HistoryWordsTwinTests read the history words' table (HIST1m)",
  },
  // The connector's tool listing sits among the service's tests; the driver's containment test reads it too.
  {
    paths: ['src/Daoris.Service/Daoris.Service.Tests/fixtures/call-table.json'],
    gates: ['service', 'driver'],
    why: "McpRoleFilterTests and SuperviseContainmentTests read the connector's tool listing",
  },
  // The participant's work table sits among the service's tests, and the driver's twin and the page's identity read it too.
  {
    paths: ['src/Daoris.Service/Daoris.Service.Tests/fixtures/participant-work.json'],
    gates: ['service', 'driver', 'web-unit'],
    why: "the service's and the driver's ParticipantWorkTests and the page's identity tests read the participant's work table (FOUNDATION1c)",
  },
  {
    paths: ['src/Daoris.Service/Daoris.Service.Tests/fixtures/participant-work-readers.json'],
    gates: ['service', 'censuses'],
    why: "the service's, the driver's and the modules' ParticipantWorkReadersTests (the desktop's two are censuses) read the table of every reader of the marks that tells a work (FOUNDATION1c, census B)",
  },
  {
    paths: ['src/Daoris.Service/Daoris.Service.Tests/fixtures/helper-askers.json'],
    gates: ['service', 'driver', 'modules'],
    why: "the driver's HelperAskersTests, the modules' DriverModuleSessionHelpersTests and the service's HelperAskersTests ask the five doors of who may ask for a helper one table (FOUNDATION1q)",
  },
  {
    paths: ['src/Daoris.Service/Daoris.Service.Tests/fixtures/senders.json'],
    gates: ['service', 'driver', 'web-unit'],
    why: "COMMS1a's sender table, read by the service's, the driver's (SendersTests) and the page's tests alike",
  },
  { paths: ['src/Daoris.Service/Daoris.Service.Tests/**', 'src/Daoris.Service/Daoris.Service.Http.Tests/**'], gates: ['service', 'service-process', 'layers'], why: "both halves, and the layers census, which counts their clock reads" },
  { paths: ['src/Daoris.Cli/test/fixtures/name-case.json'], gates: ['driver'], why: "the driver's NameCaseTests reads the shared name-case table (CASEFOLD1d)" },
  {
    paths: ['src/Daoris.Cli/test/fixtures/home-files-keep.json'],
    gates: ['service', 'driver'],
    why: "the driver's HomeFilesKeepTests and BrowserSettingsTests read the shared table of the bytes an edit of each other home file a door edits writes (KEEPALL1, KEEPALL1c), and the service's RemoteConfigKeepTests its remotes.json rows' reads (KEEPALL1e)",
  },
  {
    paths: ['src/Daoris.Cli/test/fixtures/fence-cases.json'],
    gates: ['service', 'driver'],
    why: "the service's MarkdownFence and the driver's SelfDescription fence twins read the shared fence table (ORIENT2h6, ORIENT2h6b)",
  },
  { paths: ['tools/orient-index-fixtures/**'], gates: ['service', 'tool-tests'], why: "the service's DecisionNotes twin reads the digest's note table (ORIENT1h); the provider census scans every tool" },
  // By its name at any depth of the CLI's sources, which sit in module folders.
  {
    paths: ['src/Daoris.Cli/src/**/documents.ts'],
    gates: ['service', ...CLI_READERS],
    why: "the CLI's own, and the service's roles twin reads its ROLES from the file (ORIENT2e2)",
  },
  {
    paths: ['.claude/knowledge/adoption.md'],
    gates: ['service', 'driver'],
    why: "the driver's SetupBrief twin reads the playbook's steps (LAYOUT7, MOD9b), and the service's suite scans this repository's documents",
  },
  // The driver's scans enumerate every C# source of the modules and the app, to hold the next spawn site or ask
  // written there, so the files are not named one by one: today's would miss that next one.
  {
    paths: ['src/Daoris.Desktop/Daoris.Desktop.Modules/**/*.cs', 'src/Daoris.Desktop/Daoris.Desktop.App/**/*.cs'],
    gates: [...MODULES_READERS, 'driver'],
    why: "the modules' own, and the driver's NoConsoleWindow and PullRequestOccasion scans read every C# source of the modules and the app (MOD9c)",
  },
  // The page's twin vitests parse these sources' declarations, so they are named by file: the driver's other sources
  // reach no vitest. The vitests are the web gate's unit run, which builds nothing. Each by its name at any depth of the
  // driver's folder, since the driver's files move into module folders one module per branch.
  {
    paths: [
      'NoteCodes.cs', 'EndNoteCodes.cs', 'StartNoteCodes.cs', 'HeldNoteCodes.cs', 'ChatNoteCodes.cs', 'AccountNoteCodes.cs', 'PeerNoteCodes.cs', 'SessionEvents.cs', 'HandedCodes.cs', 'TraceCodes.cs', 'LandingCode.cs', 'WorkplaceFacts.cs', 'SweepKind.cs', 'BackgroundState.cs',
      // The trace page's twin reads which stores the chains leave unread; the landing page's twin reads the landed kinds' constants.
      'TraceChains.cs', 'LandedKind.cs',
      // MSG1g3: the page's say twin holds its refusal tables to these codes.
      'GoOnNew.cs', 'SessionSay.cs', 'Continuation.cs', 'ContinueReason.cs', 'ContinueCodes.cs', 'ChatRunner.StartFrom.cs',
      // The say twin holds the hand-over's refusal table to its codes; the codes it aliases are in
      // Events, so the twin reads their literals there.
      'HandOver.cs', 'HandOverAskWhy.cs',
      // `WordsNever.Continued` forwards the say door's answer's own word, which this record declares.
      'ServiceAnswers.cs',
      // The say twin holds *Go on with Daoris's brief*'s refusal table to its codes.
      'BriefedMove.cs',
      // The parts flow's twin holds its words to `PartBegan`.
      'SessionEntries.cs',
      // The landing flow's twin holds its codes to these.
      'LandedCleanups.cs', 'EndJudgement.cs', 'PullRequestStates.cs',
    ].map((name) => `src/Daoris.Desktop/Daoris.Desktop.Driver/**/${name}`),
    gates: [...DRIVER_READERS, 'web-unit'],
    why: "the driver's own, and the page's note, conversation, handed, trace and say vitests parse their declarations (MOD9e, MSG1g3)",
  },
  // The page's one drawing is tested over every shipped workflow, so a change to one reaches the page's vitests too.
  {
    paths: ['src/Daoris.Desktop/Daoris.Desktop.Driver/Defaults/workflows/**'],
    gates: [...DRIVER_READERS, 'web-unit'],
    why: "the driver's own, and the page's graphFlow vitest reads every shipped workflow to hold its fixtures to them (SETUPUX1n)",
  },
  // The service's McpToolRegistrationTests holds the brainstorm posture's list of the connector's tools to its
  // host's, reading the driver's file by its name; a path rule replaces its lane's, so the driver's readers stay. The
  // service's McpRoleFilterTests reads the partner's rules the same way (`PartnerPermissions.Offered`).
  {
    paths: [
      'src/Daoris.Desktop/Daoris.Desktop.Driver/**/BrainstormPermissions.cs',
      'src/Daoris.Desktop/Daoris.Desktop.Driver/**/PartnerPermissions.cs',
      'src/Daoris.Desktop/Daoris.Desktop.Driver/**/OpinionPermissions.cs',
    ],
    gates: [...DRIVER_READERS, 'service'],
    why: "the driver's own, and the service's McpToolRegistrationTests and McpRoleFilterTests read the brainstorm posture's and the partner's lists of the connector's tools (BRAINSTORM1a2, COAGENT1b)",
  },
  // Desktop-publish.test.ts (the tool-tests) imports the tarball reader from the CLI's sources; a path rule replaces the cli lane's.
  { paths: ['src/Daoris.Cli/src/**/tarball.ts'], gates: [...CLI_READERS, 'tool-tests'], why: "the CLI's own readers (the lane's), and the publish's tool-tests import it (GATESLIM1)" },
  // The driver's KeepLockProcessTests runs the CLI's keeping lock under node beside the driver's, so a change to
  // either side of the lock reaches the driver's Process half; a path rule replaces the cli lane's.
  {
    paths: ['src/Daoris.Cli/src/**/keeplock.ts'],
    gates: [...CLI_READERS, 'driver-process'],
    why: "the CLI's own readers (the lane's), and the driver's KeepLockProcessTests runs it under node against the driver's lock (CONFIG1)",
  },
  // The driver's DriverProgramWordsProcessTests runs a word outside the doctrine under node, the CLI's
  // dispatcher and the seam, beside a native program, so a change to those reaches the driver's Process half too.
  {
    paths: ['src/Daoris.Cli/src/cli.ts', 'src/Daoris.Cli/src/**/driverprogram.ts'],
    gates: [...CLI_READERS, 'driver-process'],
    why: "the CLI's own readers (the lane's), and the driver's DriverProgramWordsProcessTests runs these under node through the seam (PUSH1f2a)",
  },
  { paths: ['src/Daoris.Cli/test/**'], gates: [], why: "the CLI's tests, which verify runs at every merge" },
  // A fix to a browser test is re-gated by the web gate alone, so the stage voids no other verdict.
  { paths: ['src/Daoris.Web/e2e/**'], gates: ['web'], why: "the page's end-to-end specs, which only the web gate runs: no .NET suite reads them" },
  // The measured-box check's own specs, harness and config, which no other suite reads (tsc and the vitests read src/ alone).
  {
    paths: ['src/Daoris.Web/measured/**', 'src/Daoris.Web/playwright.measured.config.ts'],
    gates: ['web-measured'],
    why: "the measured-box check's specs, harness and config, which only its gate runs",
  },
  // What the measured-box check draws and measures, the flowchart and the setting rows; a path rule replaces the
  // web-shell lane's, so the lane's gates stay.
  {
    paths: ['flow*', 'Flow*', 'ui.tsx', 'ui.stories.tsx', 'storyWidths*'].map((name) => `src/Daoris.Web/src/design/${name}`),
    gates: ['web-measured', 'web', 'web-unit', ...PAGE_READERS],
    why: "the web shell's own, and the measured-box check draws the flowchart's and the setting rows' stories from them (FLOWCHART1e)",
  },
  { lane: 'web-shell', gates: ['web', 'web-unit', ...PAGE_READERS], why: "the page's own suites, and the .NET suites that read its catalogues and sources (MOD9's incident)" },
  { lane: 'web-settings', gates: ['web', 'web-unit', ...PAGE_READERS], why: "the page's own suites, and the .NET suites that read its catalogues and sources (MOD9's incident)" },
  {
    lane: 'driver',
    gates: [...DRIVER_READERS, 'driver-rehearsals'],
    why: 'the modules build on it, the family rehearsal drives its headless host, and the deployed shell runs its loop (D60): ServiceHostLocator, the staged build, a session',
  },
  // The driver's tests project builds the launcher (it compiles driver files in by link), so a launcher change
  // is compiled by the driver gate as well as by the publish. A path rule replaces its lane's, so the modules' readers stay.
  { paths: ['src/Daoris.Desktop/Daoris.Desktop.Launcher/**'], gates: [...MODULES_READERS, 'driver'], why: 'the modules lane\'s own, and the driver gate builds the launcher, which links driver files (LAUNCHGATE1)' },
  { lane: 'modules', gates: MODULES_READERS, why: 'the application, its launcher and the modules it hosts: the deployed shell (D60)' },
  // The assistant's lane is carved out of the three lanes above and its files stay where they were, so each reaches
  // what its tree's lane reaches: after those lanes' rules, a rule over a whole tree places only the files its lane no longer owns.
  {
    paths: ['src/Daoris.Desktop/Daoris.Desktop.Driver/**'],
    gates: [...DRIVER_READERS, 'driver-rehearsals'],
    why: "the assistant lane's files in the driver's tree, which reach what the driver lane's do until the assistant is its own package",
  },
  {
    paths: ['src/Daoris.Desktop/Daoris.Desktop.Modules/**'],
    gates: MODULES_READERS,
    why: "the assistant lane's files in the modules' tree, which reach what the modules lane's do until the assistant is its own package",
  },
  {
    paths: ['src/Daoris.Web/**'],
    gates: ['web', 'web-unit', ...PAGE_READERS],
    why: "the assistant lane's files in the page, which reach what the web shell's do until the assistant is its own package",
  },
  { lane: 'assistant', gates: '*', why: 'a file of the assistant lane outside the three trees above, which no rule has placed yet' },
  // PUSH1d2: the modules' SessionsStateSliceTests holds the sessions slice's fields to the service's `SessionResponse`, reading
  // the file by its path; a path rule replaces the service lane's, so its readers stay.
  {
    paths: ['src/Daoris.Service/Daoris.Service.Http/ApiContracts.cs'],
    gates: ['service', 'devkit', 'driver', 'rehearse-family', 'web', 'web-unit', 'deployment', 'modules'],
    why: "the service lane's own, and the modules' SessionsStateSliceTests holds the sessions slice's fields to its `SessionResponse` (PUSH1d2)",
  },
  // The modules' OwnerSetCensusTests holds every person's door to the record kind it declares, reading the file by
  // its path; a path rule replaces the service lane's, so its readers stay.
  {
    paths: ['src/Daoris.Service/Daoris.Service.Http/PersonDoors.cs'],
    gates: ['service', 'devkit', 'driver', 'rehearse-family', 'web', 'web-unit', 'deployment', 'modules'],
    why: "the service lane's own, and the modules' OwnerSetCensusTests holds each person's door to the record kind it declares (STEWARDROLE1a-ii)",
  },
  // The modules' ReconcileStatusProcessTests runs this checkout's CLI over a copy of the example engine; a path rule
  // replaces the cli lane's and the examples' (examples/engine/ is read by those two).
  {
    paths: ['examples/engine/**'],
    gates: ['service', 'rehearse-family', 'web', 'modules', 'modules-process'],
    why: "the example family the family rehearsal and the end-to-end suite run on, and the copy the modules' ReconcileStatusProcessTests runs the CLI over (CONTEXTONE1)",
  },
  {
    paths: ['src/Daoris.Cli/bin/**'],
    gates: [...CLI_READERS, 'modules', 'modules-process'],
    why: "the CLI's own readers (the lane's), and the modules' ReconcileStatusProcessTests runs it over a copy of the example engine (CONTEXTONE1)",
  },
  {
    lane: 'service',
    gates: ['service', 'service-process', 'devkit', 'driver', 'rehearse-family', 'web', 'web-unit', 'deployment'],
    why: "both halves; the devkit, driver and a page vitest read its sources; the family rehearsal, e2e suite and install run its host",
  },
  {
    lane: 'cli',
    gates: CLI_READERS,
    why: "the release rehearsal packs it, the family rehearsal and the page's end-to-end suite run it, the install carries it, and the driver's tests read its permissions",
  },
  {
    lane: 'layers',
    gates: ['layers', ...PACKAGE_SUITES, ...DRIVER_READERS, 'driver-rehearsals', 'service', 'service-process'],
    why: "the ports, the fakes, the adapters and the build settings the application's projects build with: the layers census and the fakes' own tests, the packages' lock (their suites run in their own development, not here), the desktop's suites and the service's tests that reference them, and the shell the family rehearsal and the install run",
  },
  { lane: 'tools', gates: [], why: "the rest of the tooling, which the CLI's suite tests" },
  { lane: 'records', gates: ['service'], why: "the service's suite scans this repository's own documents" },
  {
    paths: ['canon/core/knowledge/rooms-and-classes.md'],
    gates: ['service', 'driver', 'rehearse', 'rehearse-family'],
    why: "the driver's PlaceRoomTests holds the room classes it names to this canon document, beside what every canon file reaches",
  },
  { paths: ['canon/**'], gates: ['service', 'rehearse', 'rehearse-family'], why: 'the package ships it, the examples must be current with it, and the service scans the doctrine synced from it' },
  {
    paths: ['examples/plugins/**'],
    gates: ['service', 'driver', 'driver-process', 'rehearse-family', 'web', 'deployment'],
    why: "the example family the rehearsals and the end-to-end suite run on, the offers the install lays out, which the driver's PluginOffer test reads as they stand (D103, MOD9b), and the performed example the PluginKit Process tests run (FABRIC-PERFORM1e)",
  },
  { paths: ['examples/**'], gates: ['service', 'rehearse-family', 'web'], why: 'the example family the family rehearsal and the end-to-end suite run on' },
  { paths: ['README.md', 'LICENSE'], gates: ['service', 'rehearse', 'cli'], why: 'staged into the package the release rehearsal packs, and read by tools/doc-links.mjs, which verify runs' },
  { paths: ['llms.txt', 'context7.json'], gates: ['cli'], why: "tools/doc-links.mjs, which verify runs, holds their agent rules equal to the README's" },
  {
    paths: ['docs/code-map.json'],
    gates: ['service', 'web-unit'],
    why: "the service's scan, and the page's codeClusters vitest lays out this repository's own map (CANVAS1f)",
  },
  {
    paths: ['docs/model-roles.md'],
    gates: ['service', 'driver'],
    why: "the service's scan, and the driver's RoleDoorTests holds the Designer suggestion to its build-judgement row (FOUNDATION1y-i)",
  },
  {
    paths: ['docs/**', 'CHANGELOG.md', 'CLAUDE.md', 'ROADMAP.md', 'AGENTS.md', 'daoris.json', 'daoris.lock', '.claude/**'],
    gates: ['service'],
    why: "the service's suite scans this repository's own doctrine and decisions",
  },
  { paths: ['.mcp.json'], gates: [], why: "the harness's settings" },
]);
