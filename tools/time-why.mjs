/**
 * Why a time breach happened, read from what the test or the gate did, and the row that owns its fix.
 *
 * A breach is a test over its time ratchet's limit (`tools/time-ratchet.mjs`), a unit test over the layers census's second
 * (`tools/layers-census.mjs`), a test the blame collector killed at its hang timeout (`hangBreaches`, read from a gate's log) or a
 * gate killed at its budget (`tools/merge/timeout.ts`). Each is one record: what breached, its time
 * and its limit, its cause, what the cause is read from (`evidence`), the fix path (the row that owns the fix) and whether it fails
 * the run. A tool says each as a machine line (`time-breach: {…}`); the merge reads them from a gate's log and the ratchet's output,
 * writes them to `breaches.json` beside the run's gate logs, carries them in the merge record and the queue's scope-run record, and
 * prints a line each in its summary (`breachLines`).
 *
 * A test's cause, the first that reads:
 * - `process`: the Process category (the `Category.Process` trait, a Process or git half's record, an end-to-end spec), the class's
 *   process starts or git sites (the layers census's counters), or a node test's spawn;
 * - `disk`: a file watcher, the OS temp folder, a special folder or a real store file, all beyond the test's scratch;
 * - `network`: a listener, a socket, a pipe or a web host;
 * - `clock`: a wall-clock read or a real wait (the layers census's counter), or a node test's timer with no fake timers;
 * - `scan`: a walk of the repository's sources (`SourceFiles`, a compilation, `repositoryFiles`, `git ls-files`);
 * - `load`: timed only while other heavy runs are under way (`tools/time-load.mjs`);
 * - `unclassed`: none of these, or no source found for the class. Never a guess.
 * No test host records per test what it started, opened or waited on, so the reading is the test's category and its class's
 * source; a ledger of what each test did, where one is kept, is the better reading. A gate killed at its budget is `load` where more than its own place is held at the load
 * sampler's peak during its run (the merge then runs it again alone once the machine is free); otherwise the cause is its longest
 * step's (reached's census and plan a scan; a restore, a build or a discovery a process; a suite's run its category), and with no
 * step clocks its own category's.
 *
 * Where three or more classes of one domain (a test project's first folder, a tool test's folder) share a cause other than load or
 * unclassed in one run, the domain is a separation candidate: a package of its own with its ports.
 */
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { repositoryFiles, writeAtomic } from './fsx.mjs';
import { clockSites } from './layers-census/clock.mjs';
import { gitSites } from './layers-census/git.mjs';
import { processSites } from './layers-census/processes.mjs';
import { isRealProcess } from './layers-census/real-process.mjs';
import { ALONE_HOLDS, foreignTrees, heldNow, loadSampler } from './time-load.mjs';

const MARK = 'time-breach: ';

/** The gates whose time a row owns, whatever the cause. */
const GATE_ROWS = { reached: 'REACHCOST1', cli: 'CLIREACH1', 'tool-e2e': 'E2EFAST1', censuses: 'CENSUSDIFF1', layers: 'CENSUSDIFF1', 'web-unit': 'WEBREACH1' };

/** What each cause reads in a source, by language: a count of the lines that hold it. */
const READS = {
  disk: { cs: /\bFileSystemWatcher\b|\bPath\.GetTempPath\s*\(|\bEnvironment\.(?:GetFolderPath|SpecialFolder)\b|\bSqliteConnection\b/, js: /\b(?:tmpdir|homedir)\s*\(|\bfs\.watch\s*\(|\bchokidar\b/ },
  network: { cs: /\b(?:TcpListener|TcpClient|HttpListener|NamedPipe(?:Server|Client)Stream|ClientWebSocket|WebApplication)\b|\bnew\s+Socket\s*\(/, js: /\bcreateServer\s*\(|\bnet\.connect\s*\(|\bnew\s+WebSocket\s*\(|\.listen\s*\(/ },
  scan: { cs: /\bSourceFiles\.\w+|\bCSharp(?:SyntaxTree|Compilation)\b/, js: /\brepositoryFiles\s*\(|ls-files|\bglobSync\s*\(/ },
};
const SPAWN = /\b(?:spawn|spawnSync|execFile|execFileSync|execSync|fork|spawnGit|execGit)\s*\(/;
const TIMER = /\b(?:setTimeout|setInterval)\s*\(|timers\/promises/;

/** The lines of a source that are code: a line comment or a block comment's line read as none. */
const codeLines = (text) => text.split('\n').map((line) => line.trim()).filter((line) => line && !line.startsWith('//') && !line.startsWith('*') && !line.startsWith('/*'));
const linesHolding = (text, pattern) => codeLines(text).filter((line) => pattern.test(line)).length;
const sites = (count, one, many) => `${count} ${count === 1 ? one : many}`;

/** The test's category, said, where the source, the record or the file says it is a real-process one; else null. */
function categoryOf(text, file, record) {
  if (file.endsWith('.cs') && isRealProcess(text)) return 'its Process category';
  if (/process|git|rehears/i.test(record)) return `its record ${record}, a Process half's`;
  if (/\.e2e\.[cm]?[jt]sx?$/.test(file)) return 'the end-to-end tier';
  return null;
}

/** The cause of one test's time, read from its class's source `text` at `file`, and the reading that names it. */
export function causeOf({ text, file, record = '' }) {
  if (typeof text !== 'string' || !file) return { cause: 'unclassed', evidence: 'no source found for its class' };
  const cs = file.endsWith('.cs');
  const lang = cs ? 'cs' : 'js';
  const category = categoryOf(text, file, record);
  if (category) return { cause: 'process', evidence: category, real: true };
  const starts = cs ? processSites(text) : linesHolding(text, SPAWN);
  if (starts) return { cause: 'process', evidence: sites(starts, cs ? 'process start' : 'line that starts a process', cs ? 'process starts' : 'lines that start processes') };
  const git = cs ? gitSites(text) : 0;
  if (git) return { cause: 'process', evidence: sites(git, 'git site', 'git sites') };
  for (const cause of ['disk', 'network']) {
    const found = linesHolding(text, READS[cause][lang]);
    if (found) return { cause, evidence: sites(found, `line that reaches the ${cause}`, `lines that reach the ${cause}`) };
  }
  const waits = cs ? clockSites(text) : /useFakeTimers/.test(text) ? 0 : linesHolding(text, TIMER);
  if (waits) return { cause: 'clock', evidence: sites(waits, 'wall-clock read or wait', 'wall-clock reads or waits') };
  const scans = linesHolding(text, READS.scan[lang]);
  if (scans) return { cause: 'scan', evidence: sites(scans, 'source scan', 'source scans') };
  return { cause: 'unclassed', evidence: 'none of the causes read in its source' };
}

/** The row that owns a test's fix: the end-to-end tier's, a scan's, a unit test's that is not pure, a real-process test's domain. */
function fixOf(cause, { file = '', real = false, record = '' }) {
  if (cause === 'load') return 'none: measured again alone when the machine is free';
  if (cause === 'unclassed') return 'TESTSLOW1';
  if (/\.e2e\.[cm]?[jt]sx?$/.test(file)) return 'E2EFAST1';
  if (cause === 'scan') return 'CENSUSDIFF1';
  if (!real) return 'UNITPURE1';
  return /git/i.test(record) ? 'LAYERS1 f' : 'COMPONENT1';
}

/** The domain a test's file is in: a test project's first folder (`Daoris.Desktop.Driver.Tests/Loop`), else the file's folder. */
export function domainOf(file) {
  const parts = file.replace(/\\/g, '/').split('/');
  const project = parts.findIndex((part) => /\.(?:Tests|Rehearsals)$/.test(part));
  if (project >= 0) return parts.slice(project, Math.min(project + 2, parts.length - 1)).join('/');
  return parts.slice(0, -1).join('/') || '.';
}

/** The class a test key names: the file of a `file::name` key, else the key less its arguments and its method. */
const unitOf = (key) => (key.includes('::') ? key.split('::')[0] : key.split('(')[0].replace(/\.[^.]*$/, ''));

/**
 * A reader of a test's source by its key, over the tracked files of `root`: a `file::name` key's file, else the one `<Class>.cs` the
 * namespace's project holds. `files` and `read` are seams.
 */
export function sourceReader(root, { files = null, read = (path) => readFileSync(join(root, path), 'utf8') } = {}) {
  let tracked = files;
  return (key) => {
    const unit = unitOf(key);
    let file = key.includes('::') ? unit.replace(/\\/g, '/') : null;
    if (!file) {
      tracked ??= repositoryFiles(root);
      const name = unit.split('.').at(-1)?.split('+')[0];
      const found = tracked.filter((path) => path.endsWith(`/${name}.cs`));
      // Among files of one name, the one under the folder the namespace names most of (`Daoris.Desktop.Driver.Tests`).
      const named = (path) => Math.max(0, ...path.split('/').filter((part) => unit.startsWith(`${part}.`)).map((part) => part.length));
      const best = Math.max(0, ...found.map(named));
      const ranked = found.filter((path) => named(path) === best);
      file = found.length === 1 ? found[0] : best > 0 && ranked.length === 1 ? ranked[0] : null;
    }
    if (!file) return null;
    try {
      return { file, text: read(file) };
    } catch {
      return null;
    }
  };
}

/**
 * One test's breach: `kind` `test` (the time ratchet's) or `census` (the layers census's second), its key, time and limit, whether it
 * fails the run, the record that times it, and `loaded` where only a run under load timed it.
 */
export function testBreach({ kind = 'test', key, ms, limit, fails, record = '', source = null, loaded = false, note = '' }) {
  const read = loaded ? { cause: 'load', evidence: 'timed only while other heavy runs were under way' } : causeOf({ text: source?.text, file: source?.file, record });
  const evidence = note ? `${read.evidence}; ${note}` : read.evidence;
  return {
    kind, name: key, ms, limit, cause: read.cause, evidence, fix: fixOf(read.cause, { file: source?.file, real: read.real === true, record }), fails,
    unit: unitOf(key), ...(source?.file ? { domain: domainOf(source.file) } : {}),
  };
}

/**
 * The time ratchet's breaches: `found` (the merge's, failing unless `listedAt` holds it at no less), `inherited` (main's: a known
 * red with its cause and row, failing nothing; `main`, main's alone record, where there is one) and `loaded` (timed only under
 * load), each key -> { ms, limit }. `records()` maps a key to its record.
 */
export function ratchetBreaches({ found, inherited = {}, loaded = {}, listedAt = () => undefined, records = () => new Map(), source = () => null }) {
  if (!Object.keys(found).length && !Object.keys(inherited).length && !Object.keys(loaded).length) return [];
  const timedIn = records();
  const one = (key, { ms, limit }, given) => testBreach({ key, ms, limit, record: timedIn.get(key) ?? '', source: source(key), ...given });
  return [
    ...Object.entries(found).map(([key, over]) => {
      const listed = listedAt(key);
      return one(key, over, { fails: !(listed >= over.ms), ...(listed === undefined ? {} : { note: `listed at ${listed} ms` }) });
    }),
    ...Object.entries(inherited).map(([key, over]) => ({
      ...one(key, over, { fails: false, note: `inherited from main: the merge does not change its file${typeof over.main === 'number' ? `, and it ran within half again main's alone record, ${over.main} ms` : ', and main keeps no alone record of it'}` }),
      inherited: true,
    })),
    ...Object.entries(loaded).map(([key, over]) => one(key, over, { fails: false, loaded: true })),
  ];
}

const HANG = /The specified inactivity time of (\d+) (seconds?|minutes?) has elapsed/;
const HUNG = /The tests? running when the crash occurred:[^\S\n]*\r?\n((?:[^\S\n]*\S+[^\S\n]*\r?\n?)+)/g;

/**
 * The blame collector's hang kills a .NET gate's `log` says (`VSTestBlameHangTimeout`): each test it names as running when it killed
 * the host, as a breach of the hang timeout with its cause: `load`, failing nothing, where `peak` held more than the gate's own
 * place; a kill in the run again (`alone`) fails.
 */
export function hangBreaches(log, source, peak = null, { alone = false } = {}) {
  const at = HANG.exec(String(log ?? ''));
  if (!at) return [];
  const ms = Number(at[1]) * (at[2].startsWith('minute') ? 60_000 : 1000);
  const loaded = typeof peak === 'number' && peak > ALONE_HOLDS;
  const load = loaded ? `; ${peak} heavy runs under way at the gate's peak${alone ? '' : ': run again alone once the machine is free'}` : '';
  const keys = [...String(log).matchAll(HUNG)].flatMap((found) => found[1].split(/\r?\n/).map((line) => line.trim()).filter(Boolean));
  const under = loaded && !alone;
  return [...new Set(keys)].map((key) => testBreach({ kind: 'hang', key, ms, limit: ms, fails: !under, loaded: under, source: source(key), note: `killed by the hang timeout${load}` }));
}

export const hungUnderLoad = (breaches) => breaches.some((each) => each.kind === 'hang' && each.cause === 'load' && !each.fails);

/** The cause a gate's step reads as, by its name. */
function stepCause(step) {
  if (/^(?:census|plan)\b/.test(step)) return 'scan';
  if (/^(?:restore|build|discovery)\b/.test(step)) return 'process';
  if (/^run /.test(step)) return /process|git|rehears|e2e/i.test(step) ? 'process' : 'unclassed';
  return 'unclassed';
}

/**
 * A gate past its budget, killed or run on: `load` where the sampler's `peak` held more than the gate's own place, else its longest
 * step's cause (the step clocks), else its own category's by its name.
 */
export function gateBreach({ gate, ms, budget, steps = [], peak = null, fails }) {
  const loaded = typeof peak === 'number' && peak > ALONE_HOLDS;
  const longest = [...steps].sort((a, b) => b.ms - a.ms)[0];
  let cause = 'unclassed';
  let evidence = 'no step clocks, and its name says no category';
  if (loaded) {
    cause = 'load';
    evidence = `${peak} heavy runs under way at its peak, its own among them`;
  } else if (longest) {
    cause = stepCause(longest.step);
    evidence = `its longest step: ${longest.step} ${Math.round(longest.ms / 1000)} s${longest.open ? ', running when it was killed' : ''}`;
  } else if (/process|git|rehears|e2e/.test(gate)) {
    cause = 'process';
    evidence = 'its Process category';
  } else if (/census|layers/.test(gate)) {
    cause = 'scan';
    evidence = 'its category: a census of the sources';
  }
  const fix = cause === 'load' ? 'none: it ran on under load, and the heavy tier measures it alone' : GATE_ROWS[gate] ?? (cause === 'process' ? (/git/.test(gate) ? 'LAYERS1 f' : 'COMPONENT1') : cause === 'scan' ? 'CENSUSDIFF1' : 'TESTSLOW1');
  return { kind: 'gate', name: gate, ms, limit: budget, cause, evidence, fix, fails };
}

/** The domains where `least` or more classes share a cause other than load or unclassed: each a separation candidate. */
export function separations(breaches, least = 3) {
  const groups = new Map();
  for (const breach of breaches) {
    if (!breach.domain || !breach.unit || breach.cause === 'load' || breach.cause === 'unclassed') continue;
    const at = `${breach.domain}\0${breach.cause}`;
    groups.set(at, (groups.get(at) ?? new Set()).add(breach.unit));
  }
  return [...groups].filter(([, units]) => units.size >= least).map(([at, units]) => {
    const [domain, cause] = at.split('\0');
    return { domain, cause, classes: [...units].sort(), fix: 'COMPONENT1' };
  });
}

/** A breach as the machine line a tool says and the merge reads. */
export const sayBreach = (breach) => `${MARK}${JSON.stringify(breach)}`;

/** The breaches a log or an output says, in order; a line that does not read is skipped. */
export function parseBreaches(text) {
  const found = [];
  for (const line of String(text ?? '').split(/\r?\n/)) {
    const at = line.indexOf(MARK);
    if (at < 0 || line.slice(0, at).trim()) continue;
    try {
      const breach = JSON.parse(line.slice(at + MARK.length));
      if (typeof breach?.name === 'string' && typeof breach.cause === 'string') found.push(breach);
    } catch {
      // not a breach line
    }
  }
  return found;
}

/** Whether a line is a breach's machine line, which a person's note leaves out. */
export const isBreachLine = (line) => line.trimStart().startsWith(MARK);

const span = (ms) => (ms < 120_000 ? `${Math.round(ms / 1000)} s` : `${Number((ms / 60_000).toFixed(1))} min`);

const SAID = { gate: ['killed after', 'budget'], hang: ['hung', 'hang timeout'], census: ['took', 'unit budget'], test: ['took', 'limit'] };

/** One breach as a person reads it. */
export function breachLine(breach) {
  const [did, bound] = SAID[breach.kind] ?? SAID.test;
  return `${breach.name} ${did} ${span(breach.ms)}, its ${bound} ${span(breach.limit)}: ${breach.cause} (${breach.evidence}); fix: ${breach.fix}${breach.fails ? '; it fails the run' : breach.inherited ? '; main\'s known red, failing nothing' : ''}`;
}

/** The summary's lines: a head naming the file, each failing or load breach first, at most `max`, then the separation candidates. */
export function breachLines(breaches, found = separations(breaches), { file = null, max = 10 } = {}) {
  if (!breaches.length) return [];
  const failing = breaches.filter((each) => each.fails).length;
  const order = [...breaches].sort((a, b) => Number(b.fails) - Number(a.fails) || Number(b.cause === 'load') - Number(a.cause === 'load') || b.ms - a.ms);
  return [
    `  time breaches: ${breaches.length}, ${failing} failing the run${file ? `; each with its cause in ${file}` : ''}`,
    ...order.slice(0, max).map((each) => `  breach: ${breachLine(each)}`),
    ...(order.length > max ? [`  and ${order.length - max} more${file ? ` in ${file}` : ''}`] : []),
    ...found.map((each) => `  separation candidate: ${each.domain}, ${each.classes.length} classes over their time by ${each.cause}; fix: ${each.fix}`),
  ];
}

/** The breaches written to `breaches.json` in `dir` with their separation candidates; the file's path, or null with none. */
export function writeBreaches(dir, breaches, found = separations(breaches)) {
  if (!breaches.length) return null;
  const file = join(dir, 'breaches.json');
  writeAtomic(file, `${JSON.stringify({ breaches, separations: found }, null, 2)}\n`);
  return file;
}

/**
 * The machine's load as the merge reads it around a gate, over the admission of `home` (the Daoris home the tool started with) and
 * the test runs of trees other than `root`: a sampler of the peak while a gate runs (the merge starts it at half the gate's budget),
 * and whether the machine is free now.
 */
export function loadSeam(home, root) {
  const count = () => heldNow(home || '') + foreignTrees(root);
  return {
    sample: () => loadSampler({ every: 30_000, count }),
    free: () => count() <= ALONE_HOLDS,
    sleep: (ms) => new Promise((done) => { setTimeout(done, ms); }),
    now: Date.now,
  };
}
