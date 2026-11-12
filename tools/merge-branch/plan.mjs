/**
 * The gates a run chooses from: every declared gate and each script only the release workflow runs, ordered by
 * kind, and the lanes and builds that schedule them.
 */
import { readFileSync, existsSync } from 'node:fs';
import { join } from 'node:path';
import { Refusal } from './args.mjs';

// test-map: strings name paths as data — the projects and commands named here are what the plan reads gates by, not reads

const KINDS = ['check', 'suite', 'rehearsal'];

/**
 * The gates that run the `Process` test category: every test in one starts a real process or runs a
 * real tick, so a failure there may be the load and is re-run alone once. Named by the settings file those
 * gates run with, not by a list of classes: a list goes stale the day a new real-process class is written.
 */
export const isProcessGate = (gate) => /process\.runsettings/.test(gate.run);

/**
 * The long gates: the real-process halves and the deployment rehearsal, which took 30 to 70 minutes
 * of a merge between them. A merge never runs one; the full set does, and a stage requires it. Named by
 * what they run, as `isProcessGate` is, so a renamed gate or a new Process half is still one.
 */
export const isLongGate = (gate) => isProcessGate(gate) || /\brehearse:deploy\b/.test(gate.run);

/**
 * The Process lane: the gates that start real processes, run one at a time beside the light lane. The
 * real-process halves, and every `npm run rehearse*` script. Read from what each runs, as `isProcessGate` is: the
 * workflow's own rehearsals are in no declaration, and the devkit reads `daoris.gates.json` too.
 */
export const isProcessLane = (gate) => isProcessGate(gate) || /^npm run rehearse/.test(gate.run.trim());

/** The driver's git half: a Process half run with `git.process.runsettings`, its git-only classes four at once. */
export const isGitHalf = (gate) => /git\.process\.runsettings/.test(gate.run);

/**
 * The rehearsals that may run beside a git half, by the root script each runs. Each builds the desktop's projects,
 * which the git half builds into and runs from, only into an output of its own (`ownBuild` in `tools/rehearsal-kit.mjs`,
 * and `desktop-publish --artifacts-path` for the deployment rehearsal's publishes), or builds no .NET project at all, and
 * shuts no build server down: a test reads each one's tool for both. They run one after the other, in this order, never
 * beside each other; the merge tool's header says why the release and deployment rehearsals may run here and not beside the
 * family or each other.
 */
export const BESIDE_GIT_HALF = Object.freeze(['rehearse', 'rehearse:family', 'rehearse:deploy']);

/** Whether a gate runs a rehearsal of `BESIDE_GIT_HALF`, read from its command as the lanes are. */
export const runsBesideGitHalf = (gate) => BESIDE_GIT_HALF.includes(scriptNamed(gate.run) ?? '');

/**
 * The rehearsals a CI run owes a stage rather than runs, by the root script each runs: the family's and the
 * deployment's, each about a quarter of a CI run. A plain `--ci` leaves them owed,
 * neither failed nor passed; `--ci --stage` runs them on the commit to stage, and the stage still asks their passes.
 */
export const STAGE_REHEARSALS = Object.freeze(['rehearse:family', 'rehearse:deploy']);

/** Whether a gate runs a rehearsal of `STAGE_REHEARSALS`, read from its command as the lanes are. */
export const isStageRehearsal = (gate) => STAGE_REHEARSALS.includes(scriptNamed(gate.run) ?? '');

/** The gates a stage requires: all, or with `light` all but the stage's rehearsals, which the CI runs on the staged commit after the install is built. */
export const stagePlan = (plan, { light = false } = {}) => (light ? plan.filter((gate) => !isStageRehearsal(gate)) : plan);

/**
 * The heavy light gates: the suites that load the CPU for minutes (the CLI's verify, the .NET test suites, the web's
 * build and end-to-end run, and its unit run). A Process half started beside them failed timing tests that passed alone. A Process gate starts after these, not only after its own component's.
 * Named by what a gate runs, as `isProcessLane` is. The cheap light gates (universal, code-map, orient-index) are not.
 */
export const isHeavyGate = (gate) => !isProcessLane(gate)
  && (/^npm run (verify|test:web)\b/.test(gate.run.trim()) || /^dotnet test\b/.test(gate.run.trim()));

/**
 * The component a `dotnet test|build|run|publish` or `tree-base.mjs build` command builds: the first two folders of the
 * project it names, or null when it names none. A project references only its component's, so two components' gates
 * never build one project.
 */
export function dotnetComponent(run) {
  const command = run.trim().replace(/^node tools\/orient-index\.mjs\s+&&\s+/, '').replace(/^node tools\/as-merged\.mjs\s+/, '');
  const verb = /^(?:dotnet|node tools\/tree-base\.mjs)\s+(?:test|build|run|publish)(?=\s|$)(.*)$/.exec(command);
  if (!verb) return null;
  const args = verb[1].trim().split(/\s+/).filter(Boolean);
  const at = args.indexOf('--project');
  const path = at >= 0 ? args[at + 1] : args[0];
  if (!path || path.startsWith('-')) return null;
  const folders = path.replace(/^["']|["']$/g, '').split(/[\\/]+/).filter((part) => part && part !== '.');
  return folders.length ? folders.slice(0, 2).join('/') : null;
}

/**
 * How many light gates run at once. This repository's light gates build three components between them
 * (the devkit's, with the CLI's verify, which ends in its universal gates; the service's, with the web gate, which builds
 * the HTTP host its own build writes into; the desktop's) and one builds none (orient-index, 2 to 3 s). So three places
 * let each component's gates run in their own while a fourth would only ever hold orient-index. Measured from merges'
 * logs: the seven light gates of a merge took 394 to 434 s in turn; two places would take the longer of the devkit's
 * 175 s and the service's and the desktop's together, 257 s; three take the devkit's 175 s. A full set
 * that runs the web gate ends no sooner with three than with two (the service's gates then web, about 10 minutes under
 * load, are its floor), so three is the width, on 22 logical processors.
 *
 * The web gate's vitests are a gate of their own that builds none, `web-unit`. It takes the first free place `web` does
 * not take: `web` is declared before it, so a place service frees goes to `web`, and the floor is service then the page's
 * build and browser suite. By the figures above the desktop's fast halves end before service, so it runs beside service.
 * A fourth place would start it at once, beside three heavy gates, for no shorter floor: a 600 ms timing bound already
 * failed beside two, so the width stays three until the parent's full sets measure otherwise.
 */
export const LIGHT_WIDTH = 3;

/** The root script an `npm run <script>` command names, past its flags, or null. */
const scriptNamed = (command) => /^npm\s+run\s+(?:-\S+\s+)*([^\s-]\S*)/.exec(command.trim())?.[1] ?? null;

/**
 * The project two components both reference, by the component that builds it as well: the contracts the service serves and the
 * desktop's driver consumes. A gate of either holds it, so no two gates build it at once.
 */
export const SHARED_PROJECTS = new Map([
  ['src/Daoris.Desktop', ['src/Daoris.Contracts', 'tools/contracts']],
  ['src/Daoris.Service', ['src/Daoris.Contracts']],
]);

/**
 * The tools that start `dotnet test` themselves, with the components they build: the reached checks run the filtered
 * classes of the desktop's and the service's test projects, so no other gate builds either beside them.
 */
export const TOOL_COMPONENTS = new Map([
  ['tools/reached-checks.mjs', ['src/Daoris.Desktop', 'src/Daoris.Service']],
]);

/**
 * The .NET components a gate builds: its command's (`dotnetComponent`), and each one a root script it names
 * builds, read through the scripts those name in turn. The CLI's `npm run verify` ends in the devkit's universal gates
 * and `npm run test:web` builds the HTTP host, so each shares a component with a .NET gate, and no two gates that build
 * one component run at once. A gate that builds none (orient-index) is a component of its own.
 */
export function gateComponents(gate, scripts = {}) {
  const found = new Set();
  const read = (command, seen) => {
    const own = dotnetComponent(command);
    if (own) found.add(own);
    for (const component of TOOL_COMPONENTS.get(/^node (tools\/\S+\.mjs)/.exec(command.trim())?.[1]) ?? []) {
      found.add(component);
      for (const shared of SHARED_PROJECTS.get(component) ?? []) found.add(shared);
    }
    for (const shared of SHARED_PROJECTS.get(own) ?? []) found.add(shared);
    const script = scriptNamed(command);
    if (!script || seen.has(script) || typeof scripts[script] !== 'string') return;
    for (const part of scripts[script].split(/&&|\|\||;/)) read(part, new Set([...seen, script]));
  };
  read(gate.run, new Set());
  return [...found];
}

/** The options of `dotnet test` that change what it runs, not what it builds: each takes a value. */
const RUN_ONLY = new Set(['--filter', '--settings', '-s', '--logger', '-l', '--results-directory']);

/**
 * What a `dotnet test <project>` command builds, as a key two commands share when they build the same: the
 * project, its path's separators and quotes read away, and every option but those that only choose what runs. A Process
 * half and the fast half of one test project differ by their `--filter` and `--settings` alone. Null for any other
 * command, and for one whose project is not its first word: a build this cannot read is a build that runs.
 */
export function testBuild(run) {
  const words = run.trim().match(/"[^"]*"|'[^']*'|\S+/g) ?? [];
  if (words[0] !== 'dotnet' || words[1] !== 'test' || !words[2] || words[2].startsWith('-')) return null;
  const project = words[2].replace(/^["']|["']$/g, '').split(/[\\/]+/).filter((part) => part && part !== '.').join('/');
  const options = [];
  for (let i = 3; i < words.length; i += 1) {
    const [name] = words[i].split('=');
    if (!RUN_ONLY.has(name)) options.push(words[i]);
    else if (!words[i].includes('=')) i += 1;
  }
  return ['dotnet test', project, ...options].join(' ');
}

/** A `dotnet test` command that runs what is already built. */
export const noBuildCommand = (run) => `${run.trim()} --no-build`;

/**
 * Whether a Process half runs its fast half's build: only when that half passed in this run on the tree the
 * Process half starts on. A changed tree builds, and so does a tree git could not name.
 */
export const reuseBuild = (built, tree) => Boolean(built?.tree && tree && built.tree === tree);

/**
 * The two lanes of a run, and what each Process-lane gate starts after. Two builds of one project at once
 * fight over its `obj/` and `bin/`, so a Process-lane gate whose command names a component waits for the light gates of
 * that component, which run first in their lane, and one that names none (a rehearsal: it builds the hosts behind its
 * script's name, and the release and deployment rehearsals stage the dist/ the cli gate's bin prefers) waits for the whole
 * light lane. Within each rule, the plan's order holds.
 *
 * Up to `width` light gates run at once, never two that build one component (`components`, read through the root
 * `scripts` a gate names). `reuses` names, for each Process half, the light gate that builds the same test project the
 * same way (`testBuild`), its fast half.
 *
 * `beside` names, for each Process-lane gate, the ones it may run beside: a git half and each rehearsal of
 * `BESIDE_GIT_HALF`. The lane's order is the plan's, but each such rehearsal goes right after the last git half, and a
 * Process-lane gate starts once every gate before it in the lane has ended but those it may run beside. Those
 * rehearsals are never beside each other, so each starts once the one before it has ended, all beside the git half
 * (the release, the family, then the deployment).
 *
 * `byMemory`, a CI run's schedule, drops the wait for the heavy light gates. A Process half waits only for the light
 * gates that build its component, and the heavy light gates run beside it, each starting once the memory reading finds
 * its free memory, as the half did. The build rule stays (two builds of one project fight over its `obj/`), and so does a rehearsal's wait for
 * the whole light lane, which is about what it builds and stages, not load. Every other rule is the merge's.
 */
export function laneSchedule(gates, { scripts = {}, width = LIGHT_WIDTH, byMemory = false } = {}) {
  const lane = gates.filter(isProcessLane);
  const besideGit = lane.some(isGitHalf) ? lane.filter(runsBesideGitHalf) : [];
  const process = lane.filter((gate) => !besideGit.includes(gate));
  process.splice(process.findLastIndex(isGitHalf) + 1, 0, ...besideGit);
  const beside = {};
  for (const gate of process) {
    const others = isGitHalf(gate) ? besideGit : besideGit.includes(gate) ? process.filter(isGitHalf) : [];
    if (others.length) beside[gate.name] = others.map((other) => other.name);
  }
  const planned = gates.filter((gate) => !isProcessLane(gate));
  const components = Object.fromEntries(planned.map((gate) => [gate.name, gateComponents(gate, scripts)]));
  const sharing = (component) => planned.filter((other) => component !== null && components[other.name].includes(component));
  const first = new Set(process.flatMap((gate) => sharing(dotnetComponent(gate.run))).map((gate) => gate.name));
  const light = [...planned.filter((gate) => first.has(gate.name)), ...planned.filter((gate) => !first.has(gate.name))];
  const waits = Object.fromEntries(process.map((gate) => {
    const component = dotnetComponent(gate.run);
    // Beside the component's own gates, the heavy light gates, so a Process half's timing tests run with no suite loading the CPU.
    // By memory, the component's own alone; the memory reading holds each heavy gate until its memory is free.
    const after = component === null ? light : light.filter((other) => sharing(component).includes(other) || (!byMemory && isHeavyGate(other)));
    return [gate.name, { after: after.map((other) => other.name), component }];
  }));
  const reuses = {};
  for (const gate of process) {
    const build = testBuild(gate.run);
    const fast = build && planned.find((other) => testBuild(other.run) === build);
    if (fast) reuses[gate.name] = fast.name;
  }
  return { light, process, waits, width: Math.max(1, width), components, reuses, beside, ...(byMemory ? { byMemory: true } : {}) };
}

/** The root `package.json`'s scripts, which `gateComponents` reads behind a gate's `npm run`; none when it has none. */
export function rootScripts(root) {
  try {
    const { scripts } = JSON.parse(readFileSync(join(root, 'package.json'), 'utf8'));
    return scripts && typeof scripts === 'object' ? scripts : {};
  } catch {
    return {};
  }
}

/** A name safe as one path segment: a log file, a scratch folder. */
export const slug = (name) => name.replace(/[^A-Za-z0-9._-]+/g, '-').replace(/^-+|-+$/g, '') || 'gate';

/**
 * What kind of gate a command is, which orders it and never drops it: a devkit `dotnet run` and a tool's
 * `--check` (the orientation index), `npm run typecheck*` and `npm run check:*` are checks, `npm run rehearse*` and
 * `npm run test:*` are rehearsals, and anything else is a suite. A command run through `tools/as-merged.mjs` is the kind of
 * what it runs: the universal gates read history as the merge would commit it, and are still the devkit's check.
 */
export function gateKind(run) {
  const command = run.trim().replace(/^node tools\/orient-index\.mjs\s+&&\s+/, '').replace(/^node tools\/as-merged\.mjs\s+/, '');
  if (/^dotnet run\b/.test(command) || /^node tools\/\S+\.mjs --check$/.test(command)) return 'check';
  const script = /^npm run (\S+)$/.exec(command)?.[1];
  if (script && /^(typecheck\b|check:)/.test(script)) return 'check';
  if (script && /^(rehearse|test:)/.test(script)) return 'rehearsal';
  return 'suite';
}

/** The scripts a workflow runs as whole steps (`run: npm run <script>`), in file order, each once. */
export function workflowScripts(text) {
  const scripts = text.replace(/\r\n/g, '\n').split('\n')
    .filter((line) => !/^\s*#/.test(line))
    .map((line) => /^\s*(?:-\s+)?run:\s*npm run (\S+)\s*$/.exec(line)?.[1])
    .filter(Boolean);
  return [...new Set(scripts)];
}

/**
 * The ordered gates: every declared gate, and every workflow script the declaration does not run. By
 * kind, fast first; within a kind, workflow-only gates first, then the declared order.
 */
export function gatePlan(declared, workflow = '', { keepDeclared = false } = {}) {
  const runs = new Set(declared.map((gate) => gate.run.trim()));
  const extras = workflowScripts(workflow)
    .filter((script) => !runs.has(`npm run ${script}`))
    .map((script) => ({ name: script, run: `npm run ${script}` }));
  const taken = new Set();
  return [...extras, ...declared]
    .map((gate, index) => ({ gate, index, kind: gateKind(gate.run) }))
    .sort((a, b) => KINDS.indexOf(a.kind) - KINDS.indexOf(b.kind) || a.index - b.index)
    .map(({ gate, kind }) => {
      let name = slug(gate.name);
      for (let n = 2; taken.has(name); n += 1) name = `${slug(gate.name)}-${n}`;
      taken.add(name);
      return {
        name,
        // The declared runner reports a gate under the name the declaration writes, where `name` is the slug its log is kept by.
        ...(keepDeclared ? { declared: gate.name } : {}),
        run: gate.run.trim(),
        kind,
        ...(gate.cwd ? { cwd: gate.cwd } : {}),
        ...(gate.clock ? { clock: true } : {}),
        ...(typeof gate.memoryGb === 'number' ? { memoryGb: gate.memoryGb } : {}),
        ...(typeof gate.reuse === 'string' ? { reuse: gate.reuse } : {}),
        ...(typeof gate.regenerate === 'string' ? { regenerate: gate.regenerate } : {}),
      };
    });
}

/** `daoris.gates.json`'s gates, read as the devkit reads them: a name, a command line, an optional cwd. */
export function readDeclared(root) {
  const file = join(root, 'daoris.gates.json');
  if (!existsSync(file)) throw new Refusal(`no daoris.gates.json at ${root}: there is no declared gate to run`);
  return declaredIn(readFileSync(file, 'utf8'), 'daoris.gates.json');
}

/** The gates of a declaration's text, as `readDeclared` reads a file's; `label` says whose text it is in a refusal (the declared runner reads one it is handed). */
export function declaredIn(text, label) {
  let parsed;
  try {
    parsed = JSON.parse(text);
  } catch (error) {
    throw new Refusal(`${label} is not valid JSON: ${error.message}`);
  }
  const gates = Array.isArray(parsed?.gates) ? parsed.gates : [];
  if (gates.length === 0) throw new Refusal(`${label} declares no gate`);
  for (const gate of gates) {
    if (typeof gate?.name !== 'string' || typeof gate?.run !== 'string') {
      throw new Refusal(`${label}: every gate needs both 'name' and 'run'`);
    }
    // A need of memory the tool cannot read is refused, never read as none.
    if ('memoryGb' in gate && !(typeof gate.memoryGb === 'number' && Number.isFinite(gate.memoryGb) && gate.memoryGb >= 0)) {
      throw new Refusal(`${label}: gate '${gate.name}': 'memoryGb' is the free memory it needs before it starts, in GB, a number 0 or more`);
    }
  }
  // `"clock": true` marks a gate whose tests are bounded by the wall clock, which a run beside raises.
  // `"memoryGb"` is the free memory a gate needs before it starts, whatever its class (`memoryNeed`).
  // `"reuse"` (`inputs`, or `inputs+diff` for a gate that also reads the merge's changed paths) lets a standing pass on equal
  // inputs be replayed; `"regenerate"` is the command that makes a generated input the gate checks current before it runs.
  return gates.map(({ name, run, cwd, clock, memoryGb, reuse, regenerate }) => ({
    name, run, ...(typeof cwd === 'string' ? { cwd } : {}), ...(clock === true ? { clock: true } : {}),
    ...(typeof memoryGb === 'number' ? { memoryGb } : {}),
    ...(typeof reuse === 'string' ? { reuse } : {}), ...(typeof regenerate === 'string' ? { regenerate } : {}),
  }));
}

export function readPlan(root) {
  const workflow = join(root, '.github', 'workflows', 'release.yml');
  return gatePlan(readDeclared(root), existsSync(workflow) ? readFileSync(workflow, 'utf8') : '');
}
