import { describe, expect, it } from 'vitest';
import { rooted } from '../test/sources';

/**
 * **A molecule imports no hook** — the rule that makes every state of the working
 * surface reachable by passing props, and therefore designable in Storybook and testable without a
 * bridge. A component that fetches its own data has states only a running system can produce, and
 * those are exactly the states nobody reviews.
 *
 * The rule is held here rather than by review because it breaks silently and one import at a time.
 * `src/work/` is covered by default and an organism is exempted by NAME: a list of what may reach
 * the data is a list someone must justify appending to, where a list of what may not is one someone
 * forgets to append to.
 */

/** Modules that reach the service or the shell — every other presentational file must not. */
const FORBIDDEN: { what: string; pattern: RegExp }[] = [
  { what: "the query layer ('./queries')", pattern: /\bfrom\s+'(?:\.\.?\/)+(?:data\/)?queries'/ },
  { what: "the shell bridge ('./shell')", pattern: /\bfrom\s+'(?:\.\.?\/)+shell'/ },
  // Its domains, which `./shell` only re-exports: without this row the rule is side-stepped
  // by importing the file the barrel names.
  { what: "a bridge domain ('./bridge/…')", pattern: /\bfrom\s+'(?:\.\.?\/)+bridge\// },
  { what: 'react-query directly', pattern: /\bfrom\s+'@tanstack\/react-query'/ },
  // The bridge ITSELF, not just this repository's wrapper around it. Without this row the
  // rule is trivially side-stepped by importing the library `./shell` is built on.
  { what: "the bridge library ('@shenora/react')", pattern: /\bfrom\s+'@shenora\/react'/ },
  // The window's chrome hook — the other shell-layer module, and the one that caught this gap out.
  // The app strip briefly imported it for two constants and every check here stayed green, because
  // **this check is per-file, not transitive**: it reads each source for a forbidden import and
  // knows nothing about what that import's own module reaches. So the list has to name every
  // shell-layer module by hand, and adding one is part of writing one.
  { what: "the window chrome ('./windowChrome')", pattern: /\bfrom\s+'(?:\.\.?\/)+(?:app\/)?windowChrome'/ },
];

/**
 * Organisms — allowed to hold a hook, because holding one for the molecules below them is their job.
 * Adding a name here is the deliberate act; it is meant to be noticed in review.
 */
const ORGANISMS = new Set<string>([
  './work/console/SessionConsole.tsx',
  // The asks' organism: it holds the queries so the row, the page and the composer do not.
  './asks/AsksPart.tsx',
  './work/attention/AttentionBand.tsx',
  // The conversation's organism: it holds the record and the scroll, so the view,
  // the tool card and the Markdown below it hold neither.
  './work/conversation/SessionConversation.tsx',
  // The earlier parts of a line: each is the conversation's organism in its earlier mode, so this holds none of its own.
  './work/conversation/EarlierParts.tsx',
  './work/console/DetachedSession.tsx',
  './work/review/DiffPane.tsx',
  './work/console/MonitorWindow.tsx',
  './work/rail/SessionRail.tsx',
  // The one owner of each act on a session: it holds the bridge's hooks, so the row, the list and
  // the page header, which each press it, hold none.
  './work/sessionActs.ts',
  // The one owner of a pause, a resume and an abandon: it holds the bridge's hooks, so the ask's page,
  // the quest's page and a session's header, which each press them, hold none.
  './work/pausing/workActs.ts',
  // The one owner of a clear: it holds the bridge's hook, so the quest's page, the ask's page and a
  // workspace's Details, which each press it, hold none.
  './work/history/historyActs.ts',
  // The one owner of a review's presses: it holds the service's and the bridge's hooks, so the
  // gate, the quest's page and What needs you, which each press them, hold none.
  './work/gates/reviewActs.ts',
  // The one owner of the second opinion's presses: it holds the bridge's hooks, so the gate and What
  // needs you, which each press them, hold none.
  './work/gates/opinionActs.ts',
  // The one owner of the answer to an agent's rule proposal: it holds the bridge's hook, so the
  // Agents page's card, What needs you and a session's conversation, which each press it, hold none.
  './work/waits/ruleAnswer.ts',
  './work/WorkFrame.tsx',
  // The Work frame's hooks: each holds one part of what the frame reads and presses, so the frame composes.
  './work/frame/useDockViews.ts',
  './work/head/useAttendedSession.ts',
  './work/head/useAttendedRecord.ts',
  './work/conversation/useAttendedBox.ts',
  './work/conversation/useWords.ts',
  './work/conversation/useBoxWords.ts',
  './work/start/useNewStart.ts',
  './work/start/StartPane.tsx',
  // The head's team facts: the helpers' hooks held once, so Co-workers, Bring in another agent and a member's thread hold none.
  './work/team/useTeam.ts',
  './work/head/useHeadAsks.ts',
  './work/review/useSessionLanding.ts',
  './work/waits/useAttendedWaits.ts',
  './work/head/useAttendedHead.tsx',
  // What the Work frame draws from those hooks: the regions' surfaces, the sessions list, the roster, the record,
  // the conversation and the boxes, each composed of the hooks' results.
  './work/frame/regionSurface.tsx',
  './work/rail/useSessionsList.tsx',
  './work/start/agentRoster.ts',
  './work/head/AttendedPane.tsx',
  // The terminal hold's container: it holds the bridge's hold and its let-go, so `TerminalHold` is handed them and holds neither.
  './work/head/SessionTerminalHold.tsx',
  // The supervisor's decision's container: it holds the bridge's overrule, so `SupervisorCourseCard` is handed the press and holds none.
  './work/head/SessionSupervisorCourse.tsx',
  './work/head/partRows.ts',
  './work/conversation/AttendedConversation.tsx',
  './work/conversation/AttendedBoxes.tsx',
  // The application's hooks: each holds one part of what the window reads and presses, so the shell composes.
  './app/shell/useShellState.ts',
  './app/shell/useFrameDoors.ts',
  './app/shell/useShellFeeds.ts',
  './app/shell/useShellFigures.ts',
  './app/shell/useOpener.ts',
  './app/shell/useSideViews.tsx',
  './app/shell/useCommands.ts',
  './app/shell/useShortcuts.ts',
  './app/shell/useDoors.ts',
  './app/shell/useWorkViews.ts',
  './app/shell/useAttentionDoors.ts',
  './app/shell/useContextDoors.ts',
  './app/shell/useViewLayouts.tsx',
  './app/shell/useScopeAndRefresh.ts',
  // Ask Daoris's organism: it reads the machine, so the panel and the starters do not.
  './help/AskDaoris.tsx',
  // Ask Daoris's conversation: the newest help session's record and its composer.
  './help/AskConversation.tsx',
  // Its parts: the place, the history, the sends, the mode, the proposals and the slot's words, each one hook.
  './help/ask/useHelpPlace.ts',
  './help/ask/useHelpHistory.ts',
  './help/ask/useHelpSend.ts',
  './help/ask/useHelpMode.tsx',
  './help/ask/useHelpFollow.tsx',
  './help/ask/useHelpSlotParts.tsx',
  // The machine as the queries answer it: one reading for the starters and the setup
  // guide, so the panel, the guide and the steps below them hold no query.
  './help/useMachine.ts',
  // The Plugins view's organism: it holds the catalogue and the plugin's acts, so the list, the
  // strip, the page and the offer's page below it hold none.
  './plugins/PluginsView.tsx',
  // The Agents place's organism: it holds the roster, the accounts and every act on an agent, so the
  // list, the strip, the page and an account's row below it hold none.
  './agents/AgentsView.tsx',
  // The Agents place's Roles mode: the role catalogue, the staffing and every press on them, so the card, the
  // section list and the strip below it hold none.
  './agents/roles/RolesView.tsx',
  // The Appointed roles section: the standing confirmation and every appointment press, so its list below holds none.
  './agents/roles/AppointedRoles.tsx',
  // And the one read behind Work, Accounts, By repository and an agent's *Used by*: a hook, held by the organisms.
  './agents/roles/structureRead.ts',
  // The Setup mode's list of places and its page: `PLACES` read once, so the list, the fold and the head below hold none.
  './agents/setup/SetupPlaces.tsx',
  // A place's Roles section: its chain's read and the role editor's presses, so its rows below hold none.
  './agents/setup/PlaceRoles.tsx',
  // A workspace's supervisor line among its place's Roles: the standing confirmation's read, so its door below holds none.
  './agents/setup/PlaceSupervisor.tsx',
  // A place's Sessions: the registry's row and the one reading of a repository's setup behind it, so the molecules below hold none.
  './agents/setup/PlaceSessions.tsx',
  // A place's Set up…: the plan's read and the person's Apply, so the page below it holds neither.
  './agents/setup/PlaceSetUpPanel.tsx',
  // A repository's driving for its page head: the hold and the ⋯'s drive and trees, so the page's molecule holds none.
  './agents/setup/PlaceDriving.tsx',
  // This machine's All roles and its notes: the Roles mode's read, so the cards below hold none.
  './agents/roles/AllRoles.tsx',
  './agents/setup/MachineNotes.tsx',
  // An adopted repository's note of the shared doctrine: the reconcile's plan and its press, so the note's molecule holds neither.
  './agents/setup/ReconcileNote.tsx',
  './projects/setup/useRepositorySetup.ts',
  // A repository's Workflow as its molecules take it: its Current, the acceptance and the step editor's bridge, held by the
  // Repositories view and a place's Sessions, so the Workflow tab and the place's Workflow below hold none.
  './projects/useWorkflowTab.ts',
  // A workspace's page's organism: it holds the driver's answers and every act on a workspace, so the
  // page, its Details and its Setup below it hold none.
  './projects/workspace/WorkspaceView.tsx',
  // A repository's Branches and History parts, and a workspace's branch groups: they hold Git's
  // reads, each drawn only while its tab shows, so the page, the tabs, the groups and the graph below them hold none.
  './projects/git/RepositoryGitView.tsx',
  // A repository's Integration tab: its look and acts, so the tab below it holds none.
  './projects/integration/RepositoryIntegration.tsx',
  './projects/files/RepositoryFiles.tsx',
  // The views a place's activity opens, organisms by standing at the sources' top above their folder's
  // molecules, named here by their folder: Search's and Convergence's queries, and
  // Knowledge's view over the two.
  './knowledge/SearchView.tsx',
  './knowledge/ConvergenceView.tsx',
  './knowledge/KnowledgeView.tsx',
  // The map's view: the workspace's wiring, so the drawing and the detail below it hold none.
  './map/MapView.tsx',
  // Quests' view: the queries, so the list, a quest's page and its composer hold none.
  './quests/QuestsView.tsx',
  // Repositories' view: the registry and the driver, so the list, a repository's page and its rows hold
  // none; and its drawers, which add, import and manage a repository with the service's and the shell's hooks.
  './projects/ProjectsView.tsx',
  './projects/ProjectManage.tsx',
  // Settings' frame: the driver's state and the harness runs, around whichever domain is chosen.
  './settings/SettingsView.tsx',
  // A tool's runs, an install, an update or a sign-in, and the sign-in on the row that asked for it: the shell's
  // hooks, so the Agents place's rows and What needs you, which draw them, hold none.
  './agents/harnessRuns.tsx',
  './agents/SignIn.tsx',
  // Settings → Get started: the organism over the guide, which reads the machine through `useMachine`.
  './help/setupGuide.tsx',
  // Settings → Git and → Updates: domains in folders of their own, past the name rule's reach, each holding the presses its cards do not.
  './settings/git/GitDomain.tsx',
  './settings/machine/UpdatesDomain.tsx',
]);

/**
 * An organism, imported by a molecule, is the data reached one step removed: the output panel
 * rendered the console organism itself, and every check above stayed green. A TYPE from an organism
 * is only a shape, and is allowed.
 */
const organismRules = [...ORGANISMS].map((path) => {
  const name = path.replace(/^.*\//, '').replace(/\.tsx?$/, '');
  return {
    what: `the organism ${name}`,
    pattern: new RegExp(`^import\\s+(?!type\\s)[^;]*?from\\s+'(?:\\.\\.?\\/)+(?:[\\w-]+\\/)*${name}'`, 'm'),
  };
}).concat([
  // Settings' domains, organisms by their name (below), and the list that imports every one of them.
  { what: 'a Settings domain', pattern: /^import\s+(?!type\s)[^;]*?from\s+'(?:\.\.?\/)+(?:[\w-]+\/)*\w+Domain'/m },
  { what: "the Settings domains' list", pattern: /^import\s+(?!type\s)[^;]*?from\s+'(?:\.\.?\/)+(?:settings\/)?domains'/m },
]);

/**
 * **A Settings domain is an organism by its name**: `settings/<Name>Domain.tsx` holds its
 * domain's queries, as SettingsView did before each domain had a file, so the molecules beside it hold
 * none; and `settings/domains.ts`, the list, imports every one. By name rather than listed here, so a new
 * domain is added in the list alone — and `settings/domains.test.ts` holds that every file so named IS a
 * registered domain, so the name cannot carry a molecule past this boundary.
 */
const SETTINGS_ORGANISM = /^\.\/settings\/(?:\w+Domain\.tsx|domains\.ts)$/;

export function offenders(files: [path: string, source: string][]): string[] {
  return files.flatMap(([path, source]) =>
    [...FORBIDDEN, ...organismRules]
      .filter((rule) => rule.pattern.test(source))
      .map((rule) => `${path} imports ${rule.what}`));
}

// `map/`: the map's drawing and detail are molecules, and MapView above them is the view.
// `asks/` and `compose/`: the ask's molecules, and the carry fields both composers share.
// `settings/`: Daoris's own AI, drawn from props — SettingsView above it holds the queries,
// and each domain's own `<Name>Domain.tsx` does.
// `projects/`: the driver's row, drawn from props — ProjectsView above it holds the driver; the list and a
// repository's page too.
// `help/`: Ask Daoris's panel and its starters, drawn from props — AskDaoris holds the machine.
// `links.tsx`: the one place a link opens, told where by a context the application provides.
// `plugins/`: the Plugins view's list, strip and pages, drawn from props — PluginsView holds the catalogue.
// `quests/`: Quests' list, a quest's page and its composer, drawn from props — QuestsView holds the queries.
// `knowledge/`: Search's and Convergence's lists and pages, drawn from props — SearchView and
// ConvergenceView hold the queries; Knowledge's head and strip too, and KnowledgeView holds the two.
// `update/`: the update's banner, drawn from props — the application holds the update's hooks.
// `menus/`: the right-click menu and its one handler, handed the frame's doors — App and the secondary
// window's root hold the bridge, so a surface that offers its acts reaches no data through it.
// `agents/`: the Agents place's list, strip, page and account rows, drawn from props — AgentsView holds the data.
// `workflow/`: the Workflow tab and its chart, drawn from props — the Repositories view and a workspace's
// page hold the answer.
const sources = rooted(import.meta.glob('/src/{design/ui.tsx,design/links.tsx,work/**/*.{ts,tsx},map/**/*.{ts,tsx},asks/**/*.{ts,tsx},compose/**/*.{ts,tsx},settings/**/*.{ts,tsx},projects/**/*.{ts,tsx},help/**/*.{ts,tsx},plugins/**/*.{ts,tsx},quests/**/*.{ts,tsx},knowledge/**/*.{ts,tsx},update/**/*.{ts,tsx},menus/**/*.{ts,tsx},agents/**/*.{ts,tsx},workflow/**/*.{ts,tsx}}', {
  eager: true, query: '?raw', import: 'default',
}) as Record<string, string>);

const presentational = Object.entries(sources)
  .filter(([path]) => !ORGANISMS.has(path) && !SETTINGS_ORGANISM.test(path) && !/\.(test|stories)\.tsx?$/.test(path));

describe('the presentational boundary', () => {
  it('catches an import it is meant to catch — the check itself, sabotaged', () => {
    expect(offenders([['./work/SessionRow.tsx', "import { useSessions } from '../queries';\n"]]))
      .toEqual(["./work/SessionRow.tsx imports the query layer ('./queries')"]);
    expect(offenders([['./work/SessionRow.tsx', "import { useSessions } from '../data/queries';\n"]]))
      .toEqual(["./work/SessionRow.tsx imports the query layer ('./queries')"]);
    expect(offenders([['./work/AppStrip.tsx', "import { STRIP_HEIGHT } from '../app/windowChrome';\n"]]))
      .toEqual(["./work/AppStrip.tsx imports the window chrome ('./windowChrome')"]);
    expect(offenders([['./work/SessionRow.tsx', "import { useDriver } from '../shell';\n"]]))
      .toHaveLength(1);
    // The road around the barrel: the domain file it re-exports.
    expect(offenders([['./work/SessionRow.tsx', "import { useDriver } from '../bridge/driver';\n"]]))
      .toEqual(["./work/SessionRow.tsx imports a bridge domain ('./bridge/…')"]);
    // The road around the wrapper: the library `./shell` is itself built on.
    expect(offenders([['./work/SessionRow.tsx', "import { useShenora } from '@shenora/react';\n"]]))
      .toEqual(["./work/SessionRow.tsx imports the bridge library ('@shenora/react')"]);
    expect(offenders([['./work/SessionRow.tsx', "import type { Session } from '../api';\n"]]))
      .toEqual([]);
    // One step removed: an organism holds the hook, and importing it reaches the data all the same.
    expect(offenders([['./work/frame.tsx', "import { SessionConsole } from '../SessionConsole';\n"]]))
      .toEqual(['./work/frame.tsx imports the organism SessionConsole']);
    expect(offenders([['./work/Row.tsx', "import type { AttentionDoors } from './AttentionBand';\n"]]))
      .toEqual([]);
    // A Settings domain is an organism by its name, and so is the list of them.
    expect(offenders([['./settings/Lines.tsx', "import { DriverDomain } from './DriverDomain';\n"]]))
      .toEqual(['./settings/Lines.tsx imports a Settings domain']);
    expect(offenders([['./settings/Lines.tsx', "import { SETTINGS_DOMAINS } from './domains';\n"]]))
      .toEqual(["./settings/Lines.tsx imports the Settings domains' list"]);
    expect(offenders([['./settings/Lines.tsx', "import type { SettingsDomainProps } from './domains';\n"]]))
      .toEqual([]);
    expect(SETTINGS_ORGANISM.test('./settings/DriverDomain.tsx')).toBe(true);
    expect(SETTINGS_ORGANISM.test('./settings/Lines.tsx')).toBe(false);
  });

  it('is looking at files at all — a vacuous boundary is a boundary that has stopped working', () => {
    expect(presentational.length).toBeGreaterThan(0);
  });

  // The Workflow tab and its chart are molecules, so every state is a story's props.
  it('holds the Workflow tab and its chart to it', () => {
    const paths = presentational.map(([path]) => path);
    expect(paths).toEqual(expect.arrayContaining(['./workflow/WorkflowTab.tsx', './workflow/WorkflowChart.tsx', './workflow/said.ts']));
  });

  // The run's view and its one line are molecules too, so every stage of a run is a story's props.
  it("holds a run's view and its one line to it", () => {
    const paths = presentational.map(([path]) => path);
    expect(paths).toEqual(expect.arrayContaining([
      './workflow/WorkflowRunView.tsx', './workflow/WorkflowLine.tsx', './workflow/runSaid.ts', './workflow/run.ts',
    ]));
  });

  it('holds: no presentational component reaches the service or the shell', () => {
    expect(offenders(presentational)).toEqual([]);
  });
});
