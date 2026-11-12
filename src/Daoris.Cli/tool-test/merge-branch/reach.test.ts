import { test } from 'node:test';
import assert from 'node:assert/strict';
import {
  ALWAYS_REACHED, BASE, PAGE_PARSED, KEPT_NAMES, BACKLOG_MERGES, INTEGRATION_DECLARATIONS, ALL, reaches, runs, LONG, stalens, selected, repoPlan, tool,
  mergeRuns, BLOCKING, rootScripts, isPageParsedRule, PAGE_PARSED_NAMES, DRIVER_FOLDER, trackedFiles, repoLanes, type Rule,
} from '../support/merge-branch.ts';

// The census of test units reads this file's strings as the lane table's data, not as reads.
// test-map: strings name paths as data
// reaches: * — the lane table's tests hold it to every tracked file and lane, so any file added or moved can fail them

test('a merge runs the baseline and what each changed path can reach, but the long gates only in the full set', () => {
  // Each case is what the lane table says can see the change; a merge runs it all but the long gates.
  const cases: [string, string[], string[]][] = [
    // The service's suite scans this repository's own doctrine and decisions, so the docs reach it.
    ['docs only', ['docs/2026-09-30-parallel-development-design.md'], [...BASE, 'service']],
    ["the steward's records", ['TASKS.md', 'docs/task-archive.md'], [...BASE, 'service']],
    // The .NET suites read the page's catalogues and sources.
    ['the web shell', ['src/Daoris.Web/src/App.tsx'], [...BASE, 'web-types', 'web-rules', 'service', 'driver', 'modules', 'web', 'web-unit']],
    ["the web's settings", ['src/Daoris.Web/src/settings/DriverDomain.tsx'], [...BASE, 'web-types', 'web-rules', 'service', 'driver', 'modules', 'web', 'web-unit']],
    ['the driver', ['src/Daoris.Desktop/Daoris.Desktop.Driver/Driver.cs'],
      [...BASE, 'driver', 'modules', 'censuses', 'driver-process', 'driver-process-git', 'driver-process-git-pinned', 'modules-process', 'rehearse-family', 'deployment']],
    ['ServiceHostLocator', ['src/Daoris.Desktop/Daoris.Desktop.Driver/ServiceHostLocator.cs'],
      [...BASE, 'driver', 'modules', 'censuses', 'driver-process', 'driver-process-git', 'driver-process-git-pinned', 'modules-process', 'rehearse-family', 'deployment']],
    // The page's twin vitests parse these driver sources' declarations, so a change to one runs the web gate's unit
    // run as well, and not its build.
    ...PAGE_PARSED.map((path): [string, string[], string[]] => [`${path.split('/').at(-1)}, which a page twin parses`, [path],
      [...BASE, 'driver', 'modules', 'censuses', 'driver-process', 'driver-process-git', 'driver-process-git-pinned', 'modules-process', 'rehearse-family', 'web-unit', 'deployment']]),
    // The driver's Process classes run in two halves, the git-only ones four at once and the rest eight at once.
    ["the driver's tests", ['src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/LandingTests.cs'], [...BASE, 'driver', 'censuses', 'driver-process', 'driver-process-git', 'driver-process-git-pinned']],
    // A browser test is the web gate's alone; no .NET suite reads it (the test below holds that).
    ['a browser test', ['src/Daoris.Web/e2e/platform.spec.ts'], [...BASE, 'web']],
    // What the measured-box check draws reaches its gate beside the web shell's; its own specs and config reach it alone.
    ...['flowLayout.ts', 'Flow.tsx', 'Flow.stories.tsx', 'flowFrames.tsx', 'ui.tsx', 'ui.stories.tsx', 'storyWidths.tsx'].map((name): [string, string[], string[]] =>
      [`${name}, which the measured-box check draws`, [`src/Daoris.Web/src/design/${name}`], [...BASE, 'web-types', 'web-rules', 'service', 'driver', 'modules', 'web', 'web-unit', 'web-measured']]),
    ["the measured-box check's spec", ['src/Daoris.Web/measured/flow.spec.ts'], [...BASE, 'web-measured']],
    ["the measured-box check's config", ['src/Daoris.Web/playwright.measured.config.ts'], [...BASE, 'web-measured']],
    ['a design file it does not draw', ['src/Daoris.Web/src/design/theme.ts'], [...BASE, 'web-types', 'web-rules', 'service', 'driver', 'modules', 'web', 'web-unit']],
    // The driver's console and occasion scans read every C# source of the modules and the app, from its source root.
    ['the modules', ['src/Daoris.Desktop/Daoris.Desktop.Modules/Bridge/DriverModule.cs'], [...BASE, 'driver', 'modules', 'censuses', 'modules-process', 'deployment']],
    ["the modules' loop", ['src/Daoris.Desktop/Daoris.Desktop.Modules/Loop/DriverLoop.cs'], [...BASE, 'driver', 'modules', 'censuses', 'modules-process', 'deployment']],
    ['the desktop app', ['src/Daoris.Desktop/Daoris.Desktop.App/Program.cs'], [...BASE, 'driver', 'modules', 'censuses', 'modules-process', 'deployment']],
    // What those scans do not read stays the modules lane's alone: a project file, the icon, the modules' tests (the launcher is built by the driver gate).
    ["the modules' project", ['src/Daoris.Desktop/Daoris.Desktop.Modules/Daoris.Desktop.Modules.csproj'], [...BASE, 'modules', 'modules-process', 'deployment']],
    ["the app's icon", ['src/Daoris.Desktop/Daoris.Desktop.App/daoris.ico'], [...BASE, 'modules', 'modules-process', 'deployment']],
    ['the launcher', ['src/Daoris.Desktop/Daoris.Desktop.Launcher/Program.cs'], [...BASE, 'driver', 'modules', 'modules-process', 'deployment']],
    ["the modules' tests", ['src/Daoris.Desktop/Daoris.Desktop.Modules.Tests/DriverModuleRoutesTests.cs'], [...BASE, 'modules', 'censuses', 'modules-process']],
    // The tests of the tools only the parent runs are the tool-tests gate, reached by those tools and their folder.
    ['the publish script', ['tools/desktop-publish.mjs'], [...BASE, 'tool-tests', 'rehearse-family', 'deployment']],
    // The rehearsals' kit imports it too, to end what a timed-out command left running.
    ['the process queries the publish and the kit import', ['tools/processes.mjs'], [...BASE, 'tool-tests', 'rehearse', 'rehearse-family', 'deployment']],
    ['the service publisher the publish calls', ['tools/service-publish.mjs'], [...BASE, 'tool-tests', 'rehearse', 'rehearse-family', 'deployment']],
    ["the family rehearsal's set-up kit", ['tools/setup-kit.mjs'], [...BASE, 'tool-tests', 'rehearse-family']],
    ["the rehearsals' kit", ['tools/rehearsal-kit.mjs'], [...BASE, 'tool-tests', 'rehearse', 'rehearse-family', 'deployment']],
    ['a tool-test file', ['src/Daoris.Cli/tool-test/setup-kit.test.ts'], [...BASE, 'tool-tests']],
    ['the fixture the tool-tests share', ['src/Daoris.Cli/test/_fixture.ts'], [...BASE, 'tool-tests']],
    ['the tarball reader the publish imports', ['src/Daoris.Cli/src/platform/tarball.ts'], [...BASE, 'tool-tests', 'driver', 'rehearse', 'rehearse-family', 'web', 'deployment']],
    ["the CLI's keeping lock the driver's Process half runs", ['src/Daoris.Cli/src/platform/keeplock.ts'], [...BASE, 'driver', 'driver-process', 'rehearse', 'rehearse-family', 'web', 'deployment']],
    ['what the merge tool imports',['tools/doc-duplicates.mjs'], [...BASE, 'tool-tests']],
    ['the deployment rehearsal', ['tools/deployment-rehearsal.mjs'], [...BASE, 'deployment']],
    ['the canon', ['canon/core/rules/task-lifecycle.md'], [...BASE, 'service', 'rehearse', 'rehearse-family']],
    ['the examples', ['examples/engine/README.md'], [...BASE, 'service', 'rehearse-family', 'web', 'modules', 'modules-process']],
    // The driver reads the offers and the adoption playbook, as the service reads the digest's note table.
    ["the install's offers", ['examples/plugins/github-pull-request/land.mjs'], [...BASE, 'service', 'driver', 'driver-process', 'rehearse-family', 'web', 'deployment']],
    ['the adoption playbook', ['.claude/knowledge/adoption.md'], [...BASE, 'service', 'driver']],
    ["the digest's note table", ['tools/orient-index-fixtures/decision-notes.json'], [...BASE, 'service']],
    // The history words' table sits among the service's tests, and the driver's twin reads it too.
    ["the history words' table", ['src/Daoris.Service/Daoris.Service.Tests/fixtures/history-words.json'], [...BASE, 'service', 'driver']],
    // The participant's work table sits among the service's tests, and the driver's twin and the page's identity read it too.
    ["the participant's work table", ['src/Daoris.Service/Daoris.Service.Tests/fixtures/participant-work.json'], [...BASE, 'service', 'driver', 'web-unit']],
    ["the participant's work readers' table", ['src/Daoris.Service/Daoris.Service.Tests/fixtures/participant-work-readers.json'], [...BASE, 'service', 'driver', 'modules', 'censuses']],
    // The kept names' table sits among the driver's tests, and the page's twin reads it too.
    ["the kept names' table", [KEPT_NAMES], [...BASE, 'driver', 'web-unit']],
    // The backlog merges' table sits among the driver's tests, and the merge tool's twin reads it too.
    ["the backlog merges' table", [BACKLOG_MERGES], [...BASE, 'tool-tests', 'driver']],
    // The integration declarations' table sits among the driver's tests, and the merge tool's twin reads it too.
    ["the integration declarations' table", [INTEGRATION_DECLARATIONS], [...BASE, 'tool-tests', 'driver']],
    // The person's order's table sits among the driver's tests, and the page's twin reads it too.
    ["the hand-over order's table", ['src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/hand-over-order.json'], [...BASE, 'driver', 'web-unit']],
    // The brainstorm answers' table, which the service's twin validator reads too; and the posture's list of the
    // connector's tools, which the service's registration test holds to its host's.
    ["the brainstorm answers' table", ['src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/brainstorm-answers.json'], [...BASE, 'service', 'driver']],
    // The brainstorm files' table, which the service's brainstorm desk reads too.
    ["the brainstorm files' table", ['src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/brainstorm-files.json'], [...BASE, 'service', 'driver']],
    ["the brainstorm posture", ['src/Daoris.Desktop/Daoris.Desktop.Driver/WorkOrders/Domain/BrainstormPermissions.cs'],
      [...BASE, 'service', 'driver', 'modules', 'censuses', 'driver-process', 'driver-process-git', 'driver-process-git-pinned', 'modules-process', 'rehearse-family', 'deployment']],
    ["the service's tests", ['src/Daoris.Service/Daoris.Service.Tests/HistoryDeskTests.cs'], [...BASE, 'service', 'service-process']],
    // The fence table sits among the CLI's fixtures, and the driver's SelfDescription twin and the service's MarkdownFence twin read it too.
    ['the fence table', ['src/Daoris.Cli/test/fixtures/fence-cases.json'], [...BASE, 'service', 'driver']],
    // The rotation-use table, among the driver's fixtures, which its twin reads, and the
    // page's curve rules.
    ['the rotation-use table', ['src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/rotation-use.json'], [...BASE, 'driver', 'web-unit']],
    ["the landing sentences' table, which the modules' twin alone reads", ['src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/landing-says.json'], [...BASE, 'modules']],
    // The driver family's other shared tables sit in the driver's fixtures, its tests their one reader: which cycle governs a
    // unit, the bytes an edit of driver.json writes, a named graph as a version, a graph judged when saved, and the review and
    // second-opinion rules; the CLI's keep lock test still reads the lock rows, and verify runs it at every merge.
    ["the driver's tables the CLI's TypeScript left", [
      'src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/review-rules.json',
      'src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/opinion-rules.json',
      'src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/graph-homes.json',
      'src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/driver-config-keeps.json',
      'src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/workflow-graph-versions.json',
      'src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/graph-problems.json',
    ], [...BASE, 'driver', 'driver-process', 'driver-process-git', 'driver-process-git-pinned']],
    // The bytes an edit of each other home file a door edits writes sit among the CLI's fixtures, and the driver's
    // HomeFilesKeepTests reads them too, as the service's RemoteConfigKeepTests its remotes.json rows' reads and
    // the modules' BrowserSettingsTests browser/settings.json's.
    ['the home-files-keep table', ['src/Daoris.Cli/test/fixtures/home-files-keep.json'], [...BASE, 'service', 'driver']],
    // The usage curve's table, among the driver's fixtures, which its twin reads, and the
    // page's twin.
    ['the usage curve table', ['src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/usage-curves.json'], [...BASE, 'driver', 'modules', 'web-unit']],
    // A park's question as each reader takes it, the driver's QuestionFilesTests and the page's parkQuestion.test.ts.
    ['the park question table', ['src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/park-question.json'], [...BASE, 'driver', 'web-unit']],
    // The provider census's exceptions the four censuses share: the driver's ProviderCensusTests, the service's, the
    // page's providerCensus.test.ts and the tools' provider-census.test.ts.
    ['the provider census', ['src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/provider-census.json'], [...BASE, 'driver', 'service', 'web-unit', 'tool-tests']],
    // The stand-ins' reader is the tools' (its tool test and the family rehearsal
    // import it), and each provider's own files are the driver's, which the tool test and the rehearsal read too.
    ["the stand-ins' reader", ['tools/stand-ins.mjs'], [...BASE, 'tool-tests', 'rehearse-family']],
    ...['provider.json', 'pending.json', 'Program/agent.mjs'].map((file): [string, string[], string[]] => [
      `a provider's ${file}`, [`src/Daoris.Desktop/Daoris.Desktop.Driver/Agents/Providers/Stub/${file}`],
      [...BASE, 'driver', 'modules', 'driver-process', 'driver-process-git', 'driver-process-git-pinned', 'modules-process', 'rehearse-family', 'tool-tests', 'deployment'],
    ]),
    // The tools' provider census runs the contracts' generator, which reads each provider's own sources and the registry's reader.
    ['a provider\'s vocabulary source', ['src/Daoris.Desktop/Daoris.Desktop.Driver/Agents/Providers/Codex/CodexProvider.cs'],
      [...BASE, 'driver', 'modules', 'censuses', 'driver-process', 'driver-process-git', 'driver-process-git-pinned', 'modules-process', 'rehearse-family', 'tool-tests', 'deployment']],
    ['the provider vocabulary reader', ['src/Daoris.Desktop/Daoris.Desktop.Driver/Agents/Registry/ProviderVocabulary.cs'],
      [...BASE, 'driver', 'modules', 'censuses', 'driver-process', 'driver-process-git', 'driver-process-git-pinned', 'modules-process', 'rehearse-family', 'tool-tests', 'deployment']],
    ['release tooling', ['tools/release-rehearsal.mjs'], [...BASE, 'rehearse']],
    ['the CLI',['src/Daoris.Cli/src/platform/args.ts'], [...BASE, 'driver', 'rehearse', 'rehearse-family', 'web', 'deployment']],
    // The driver's DriverProgramWordsProcessTests runs the CLI's entry, dispatcher, driver row and seam under node.
    ["the CLI's seam", ['src/Daoris.Cli/src/cli.ts'], [...BASE, 'driver', 'driver-process', 'rehearse', 'rehearse-family', 'web', 'deployment']],
    ["the CLI's seam's module", ['src/Daoris.Cli/src/platform/driverprogram.ts'], [...BASE, 'driver', 'driver-process', 'rehearse', 'rehearse-family', 'web', 'deployment']],
    // The service's roles twin reads the CLI's ROLES from its file, so that file reaches the service too; by its
    // name in whichever module folder it sits.
    ["the CLI's roles", ['src/Daoris.Cli/src/layout/documents.ts'], [...BASE, 'service', 'driver', 'rehearse', 'rehearse-family', 'web', 'deployment']],
    ["the CLI's roles, at the sources' top", ['src/Daoris.Cli/src/documents.ts'], [...BASE, 'service', 'driver', 'rehearse', 'rehearse-family', 'web', 'deployment']],
    ['the merge tool and its tests', ['tools/merge-branch.mjs', 'src/Daoris.Cli/tool-test/merge-branch-ci.test.ts', 'tools/merge/flow.ts'], [...BASE, 'tool-tests']],
    // Its typed modules and their Vitest projects: the unit one runs in the CLI's verify, the end-to-end one in the tool-tests.
    ["the merge tool's typed module", ['tools/merge/flow.ts'], [...BASE, 'tool-tests']],
    ["the merge tool's Vitest configuration", ['tools/vitest.config.ts'], [...BASE, 'tool-tests']],
    ['the service', ['src/Daoris.Service/Daoris.Service.Core/Asks.cs'],
      [...BASE, 'service', 'service-process', 'devkit', 'driver', 'rehearse-family', 'web', 'web-unit', 'deployment']],
    ['the devkit', ['src/Daoris.Devkit/Daoris.Devkit.Core/Gates.cs'], [...BASE, 'devkit']],
    // The driver's fast CategoryTests read the file too, so a change there runs the driver's fast half.
    ["the Process halves' settings", ['src/Daoris.Desktop/process.runsettings'], [...BASE, 'driver', 'driver-process', 'modules-process']],
    // The second file runs only the driver's git-only classes, four at once.
    ["the git-only classes' settings", ['src/Daoris.Desktop/git.process.runsettings'], [...BASE, 'driver-process-git']],
    ['the declared gates', ['daoris.gates.json'], ALL],
    ['nothing at all', [], BASE],
  ];
  for (const [what, paths, listed] of cases) {
    // In the plan's order: the web's typecheck is a check, before the CLI's verify.
    const reachesUnits = paths.some((path) => /^(src|tools|examples|canon)\//.test(path) || ['daoris.gates.json', 'daoris.lanes.json', 'package.json', 'package-lock.json'].includes(path));
    const expected = ALL.filter((gate) => listed.includes(gate) || ALWAYS_REACHED.includes(gate) || (gate === 'reached' && reachesUnits));
    assert.deepEqual(reaches(paths), expected, `${what}: what the table says can see it`);
    assert.deepEqual(runs(paths), expected.filter((gate) => !LONG.includes(gate)), `${what}: what a merge runs`);
    // The stage reads the same table, so the gates a changed path makes stale are the ones it reaches.
    if (paths.length) assert.deepEqual(stalens(paths), expected, `${what}: the verdicts it makes stale`);
  }
  // Lanes add up: a branch crossing two runs what either reaches.
  assert.deepEqual(runs(['docs/x.md', 'tools/release-rehearsal.mjs', 'src/Daoris.Desktop/Daoris.Desktop.App/Program.cs']),
    [...BASE.slice(0, 3), 'reached', ...BASE.slice(3), ...ALWAYS_REACHED, 'service', 'driver', 'modules', 'censuses', 'rehearse']);

  // A merge touching the flowchart's layout lists the measured-box check, saying what reached it, and
  // --gated runs it.
  const flowLayout = ['src/Daoris.Web/src/design/flowLayout.ts'];
  const measured = selected(flowLayout, false, false).find((entry) => entry.gate.name === 'web-measured')!;
  assert.equal(measured.reached, true);
  assert.match(measured.why, /^in the CI beside; src\/Daoris\.Web\/src\/design\/flowLayout\.ts/);
  assert.ok(runs(flowLayout).includes('web-measured'));
  assert.equal(runs(['src/Daoris.Web/src/App.tsx']).includes('web-measured'), false, 'a page change it does not draw');

  // A branch that changes no tool the parent alone runs skips the tool-tests gate; --full runs it, as a light suite.
  assert.equal(runs(['src/Daoris.Service/Daoris.Service.Core/Asks.cs']).includes('tool-tests'), false);
  assert.equal(runs(['src/Daoris.Cli/src/cli.ts']).includes('tool-tests'), false);
  assert.equal(runs(['tools/merge-branch.mjs']).includes('tool-tests'), true);
  assert.equal(runs([], true).includes('tool-tests'), true);
  const slim = repoPlan.find((gate) => gate.name === 'tool-tests')!;
  assert.equal(slim.kind, 'suite');
  assert.equal(tool.isHeavyGate(slim), false);

  // A driver change runs eight gates, and says the desktop's long ones are in the full set before staging.
  const driver = selected(['src/Daoris.Desktop/Daoris.Desktop.Driver/Driver.cs']);
  assert.deepEqual(driver.filter((entry) => entry.run).map((entry) => entry.gate.name),
    ['universal', 'code-map', 'test-map', 'reached', 'cli', 'comments', 'driver', 'modules', 'censuses', 'rehearse-family']);
  for (const name of LONG.slice(1)) {
    const entry = driver.find((candidate) => candidate.gate.name === name)!;
    assert.equal(entry.run, false, name);
    assert.match(entry.why, /^in the full set before staging/, name);
    assert.match(entry.why, /Driver\.cs/, `${name} says what reached it`);
  }
  // By default a merge runs the blocking checks alone; every other gate it reaches is listed, skipped, "in the CI beside";
  // the long gates keep their own reason; --gated runs what the paths reach; --full runs every gate and a rerun names one.
  const service = ['src/Daoris.Service/Daoris.Service.Core/Asks.cs'];
  assert.deepEqual(mergeRuns(service), [...BLOCKING, 'reached', 'cli'], 'the reached units and the CLI\'s verify run at every merge that changes a source');
  assert.deepEqual(mergeRuns([]), [...BLOCKING, 'cli']);
  assert.deepEqual(runs(service), [...BASE.slice(0, 3), 'reached', ...BASE.slice(3), ...ALWAYS_REACHED, 'service', 'devkit', 'driver', 'rehearse-family', 'web', 'web-unit']);
  assert.deepEqual(mergeRuns(service, true), ALL);
  const byDefault = selected(service, false, false);
  assert.equal(byDefault.find((entry) => entry.gate.name === 'cli')!.run, true, 'the lists and records are held at every merge');
  for (const name of ['tool-tests', 'service', 'rehearse', 'rehearse-family', 'web']) {
    const entry = byDefault.find((candidate) => candidate.gate.name === name)!;
    assert.equal(entry.run, false, name);
    assert.match(entry.why, /^in the CI beside/, name);
  }
  assert.match(byDefault.find((entry) => entry.gate.name === 'service')!.why, /Asks\.cs/, 'it still says what reached the gate');
  assert.match(byDefault.find((entry) => entry.gate.name === 'deployment')!.why, /^in the full set before staging/);
  assert.match(byDefault.find((entry) => entry.gate.name === 'driver-process')!.why, /^in the full set before staging/);
  assert.equal(byDefault.find((entry) => entry.gate.name === 'cli')!.reached, true, 'the table still reaches the CLI at every merge');
  // --full still names every gate.
  assert.deepEqual(runs(['src/Daoris.Desktop/Daoris.Desktop.Driver/Driver.cs'], true), ALL);
});

test("a merge that changes the web's sources blocks on its typecheck, and one that does not lists it in the CI beside (MERGETSC1)", () => {
  const page = ['src/Daoris.Web/src/App.tsx'];
  const service = ['src/Daoris.Service/Daoris.Service.Core/Asks.cs'];
  const check = repoPlan.find((gate) => gate.name === 'web-types')!;
  assert.equal(check.run, 'npm run typecheck:web');
  assert.equal(check.kind, 'check', 'among the checks, fast first');
  assert.equal(tool.isHeavyGate(check), false);
  assert.equal(tool.isProcessLane(check), false);
  assert.deepEqual(tool.laneSchedule(repoPlan, { scripts: rootScripts() }).components['web-types'], [], 'it builds no .NET component');

  // The plan names it, and why, as it names the other blocking checks.
  assert.deepEqual(mergeRuns(page), [...BLOCKING, 'web-types', 'web-rules', 'reached', 'cli']);
  const entry = selected(page, false, false).find((candidate) => candidate.gate.name === 'web-types')!;
  assert.match(entry.why, /^blocks the merge: src\/Daoris\.Web\/src\/App\.tsx \(the web's typecheck/);
  assert.match(tool.selectionLines(selected(page, false, false)).join('\n'), /4\.\s+web-types\s+check\s+blocks the merge/);
  assert.deepEqual(mergeRuns(['src/Daoris.Web/tsconfig.json']), [...BLOCKING, 'web-types', 'reached', 'cli'], 'what configures its compiler');

  // A merge that changes nothing it reads skips it, even a path the lane table places nowhere.
  for (const paths of [service, ['src/Daoris.Web/e2e/rail.spec.ts'], ['src/Daoris.Desktop/Daoris.Desktop.Driver/Driver.cs'], ['TASKS.md']]) {
    // The desktop's sources reach the censuses' path check, and nothing else here.
    assert.deepEqual(mergeRuns(paths), [...BLOCKING, ...(paths[0]!.startsWith('src/') ? ['reached'] : []), 'cli', ...(paths[0]!.startsWith('src/Daoris.Desktop/') ? ['censuses'] : [])], paths[0]);
    assert.equal(runs(paths).includes('web-types'), false, `--gated: ${paths[0]}`);
    assert.equal(stalens(paths).includes('web-types'), false, `its verdict stands: ${paths[0]}`);
  }
  assert.equal(selected(service, false, false).find((candidate) => candidate.gate.name === 'web-types')!.why, 'in the CI beside');
  assert.ok(runs(page).includes('web-types'));
  assert.ok(stalens(page).includes('web-types'));
  assert.ok(mergeRuns(service, true).includes('web-types'), '--full runs it');
});

test("a merge that changes the web's sources blocks on its design and structure checks, and one that does not lists them in the CI beside (MERGEWEBRULES1)", () => {
  const story = ['src/Daoris.Web/src/design/ui.stories.tsx'];
  const service = ['src/Daoris.Service/Daoris.Service.Core/Asks.cs'];
  const check = repoPlan.find((gate) => gate.name === 'web-rules')!;
  assert.equal(check.run, 'npm run check:web-rules');
  assert.equal(rootScripts()['check:web-rules'],
    'npm --prefix src/Daoris.Web run test -- src/design/tokens.test.ts src/test/structure.test.ts src/test/providerCensus.test.ts src/design/presentational.test.ts src/design/pageMeasure.test.tsx',
    'the five files, not the whole unit run');
  assert.equal(check.kind, 'check', 'among the checks, fast first');
  assert.equal(tool.isHeavyGate(check), false);
  assert.equal(tool.isProcessLane(check), false);
  assert.deepEqual(tool.laneSchedule(repoPlan, { scripts: rootScripts() }).components['web-rules'], [], 'it builds no .NET component');

  // The plan names it, and why, beside the web's typecheck.
  assert.deepEqual(mergeRuns(story), [...BLOCKING, 'web-types', 'web-rules', 'reached', 'cli']);
  const entry = selected(story, false, false).find((candidate) => candidate.gate.name === 'web-rules')!;
  assert.match(entry.why, /^blocks the merge: src\/Daoris\.Web\/src\/design\/ui\.stories\.tsx \(the web's design and structure checks/);
  assert.match(tool.selectionLines(selected(story, false, false)).join('\n'), /5\.\s+web-rules\s+check\s+blocks the merge/);
  assert.deepEqual(mergeRuns(['src/Daoris.Web/vite.config.ts']), [...BLOCKING, 'web-types', 'web-rules', 'reached', 'cli'], 'what configures its tests');
  assert.deepEqual(mergeRuns(['src/Daoris.Web/tsconfig.json']), [...BLOCKING, 'web-types', 'reached', 'cli'], 'the compiler alone is the typecheck\'s');

  // A merge that changes nothing it reads skips it, even a path the lane table places nowhere.
  for (const paths of [service, ['src/Daoris.Web/e2e/rail.spec.ts'], ['src/Daoris.Desktop/Daoris.Desktop.Driver/Driver.cs'], ['TASKS.md']]) {
    // The desktop's sources reach the censuses' path check, and nothing else here.
    assert.deepEqual(mergeRuns(paths), [...BLOCKING, ...(paths[0]!.startsWith('src/') ? ['reached'] : []), 'cli', ...(paths[0]!.startsWith('src/Daoris.Desktop/') ? ['censuses'] : [])], paths[0]);
    assert.equal(runs(paths).includes('web-rules'), false, `--gated: ${paths[0]}`);
    assert.equal(stalens(paths).includes('web-rules'), false, `its verdict stands: ${paths[0]}`);
  }
  assert.equal(selected(service, false, false).find((candidate) => candidate.gate.name === 'web-rules')!.why, 'in the CI beside');
  assert.ok(runs(story).includes('web-rules'));
  assert.ok(stalens(story).includes('web-rules'));
  assert.ok(mergeRuns(service, true).includes('web-rules'), '--full runs it');
});

/**
 * The driver's files move into module folders one module per branch, so the rule naming the sources the page's
 * twins parse names each by its file's name at any depth of the driver's folder. A move edits no rule, a file of the same
 * name in another project is not one of them, and each name is one tracked source, so a rename fails here rather than
 * leaving a glob that matches nothing.
 */
test("the driver's sources the page's twins parse reach the web gate by their names, wherever the driver keeps them (STRUCT1)", () => {
  const rule = tool.REACH.find(isPageParsedRule);
  assert.ok(rule, "MOD9e's rule names each source by its name under the driver's folder");
  for (const name of PAGE_PARSED_NAMES) {
    for (const path of [`${DRIVER_FOLDER}${name}`, `${DRIVER_FOLDER}Platform/${name}`, `${DRIVER_FOLDER}Trees/Domain/${name}`]) {
      assert.ok(runs([path]).includes('web-unit'), `${path} reaches the web gate's unit run`);
    }
  }
  assert.equal(runs(['src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/Landing.cs']).includes('web-unit'), false, "the driver's tests' file of that name");
  assert.equal(runs(['src/Daoris.Desktop/Daoris.Desktop.Driver/Help/Landing.cs.bak']).includes('web-unit'), false, 'a name that only begins so');
  const files = trackedFiles();
  for (const glob of rule.paths!) {
    assert.ok(glob.startsWith(`${DRIVER_FOLDER}**/`), `${glob} names its file at any depth of the driver's folder`);
    assert.equal(files.filter(tool.laneMatcher([glob])).length, 1, `${glob} is one tracked source`);
  }
});

test('a path no rule places runs every gate but the long ones, and says which path; --full runs every gate and says so', () => {
  const unplaced = tool.selectGates(repoPlan, ['docs/x.md', 'src/Daoris.Somewhere/Program.cs'], { lanes: repoLanes, gated: true });
  assert.deepEqual(unplaced.unplaced, ['src/Daoris.Somewhere/Program.cs']);
  // But a path check, which its own paths reach.
  const typechecks = tool.PATH_CHECKS.map((check) => check.gate).filter((gate) => gate !== 'reached');
  assert.deepEqual(unplaced.gates.filter((entry) => !entry.reached).map((entry) => entry.gate.name), typechecks, 'when unsure, a path reaches everything');
  assert.deepEqual(unplaced.gates.filter((entry) => entry.run).map((entry) => entry.gate.name), ALL.filter((gate) => !LONG.includes(gate) && !typechecks.includes(gate)));
  const driverProcess = unplaced.gates.find((entry) => entry.gate.name === 'driver-process')!;
  assert.match(driverProcess.why, /^in the full set before staging/);
  assert.match(driverProcess.why, /src\/Daoris\.Somewhere\/Program\.cs/);
  assert.match(unplaced.gates.find((entry) => entry.gate.name === 'devkit')!.why, /src\/Daoris\.Somewhere\/Program\.cs/);

  const full = tool.selectGates(repoPlan, ['docs/x.md'], { lanes: repoLanes, full: true });
  assert.deepEqual(full.gates.filter((entry) => entry.run).map((entry) => entry.gate.name), ALL);
  assert.ok(full.gates.filter((entry) => !BASE.includes(entry.gate.name)).every((entry) => /--full/.test(entry.why)));

  // Every gate says why it runs or why it is skipped.
  const docs = tool.selectGates(repoPlan, ['docs/x.md'], { lanes: repoLanes, gated: true });
  const why = Object.fromEntries(docs.gates.map((entry) => [entry.gate.name, entry.why]));
  assert.match(why['cli']!, /every merge/);
  assert.match(why['service']!, /docs\/x\.md/);
  assert.match(why['devkit']!, /no changed path reaches it/);
  // A long gate skipped says where it runs, reached or not.
  assert.match(why['driver-process']!, /^in the full set before staging/);
});

test('a gate the lane table never names runs at every merge, and a rule naming a gate not in the plan names nothing', () => {
  const plan = tool.gatePlan([
    { name: 'cli', run: 'npm run verify' },
    { name: 'mystery', run: 'node mystery.mjs' },
    { name: 'web', run: 'npm run test:web' },
  ]);
  const reach: Rule[] = [{ paths: ['docs/**'], gates: ['nowhere'], why: 'a gate this repository does not run' }];
  const selected = tool.selectGates(plan, ['docs/x.md'], { reach, gated: true });
  assert.deepEqual(selected.gates.map((entry) => [entry.gate.name, entry.run]), [['cli', true], ['mystery', true], ['web', true]]);
  assert.match(selected.gates.find((entry) => entry.gate.name === 'mystery')!.why, /the lane table does not name it/);
  // Once a rule names a gate, a merge whose paths do not reach it skips it.
  const named = tool.selectGates(plan, ['docs/x.md'], { reach: [...reach, { paths: ['src/**'], gates: ['web', 'mystery'], why: 'the page' }], gated: true });
  assert.deepEqual(named.gates.map((entry) => [entry.gate.name, entry.run]), [['cli', true], ['mystery', false], ['web', false]]);
});
