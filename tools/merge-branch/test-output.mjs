/**
 * What a gate's output says it failed, read by the merge tool (`tools/merge-branch.mjs`, which exports each again under its own
 * name): a `dotnet test` run's failed tests and counts, whether its failures are re-run alone as flakes, a Playwright run's
 * timed specs, the folders and failures of a `node --test` run, and the lines of a log that are the gate's own.
 */
import { readFileSync } from 'node:fs';
import { join } from 'node:path';

const FAILED_TEST = /^\s+Failed (\S.*?) \[[^\]]*\]\s*$/;
const SUMMARY = /^\s*(?:Passed|Failed)!\s+-\s+Failed:\s+(\d+),\s+Passed:\s+(\d+)/;

/** The simple name of a test's class: the segment before the method, a nested class's outer name. */
export const testClass = (test) => (test.split('.').at(-2) ?? '').split('+')[0];
/** A test as a line says it: its class and method. */
export const short = (test) => test.split('.').slice(-2).join('.');

/**
 * What a `dotnet test` run says it failed: each failed test once (a theory by its method), how many
 * `Failed` lines there were, and the summary lines' counts summed (a solution prints one per project).
 */
export function testSummary(output) {
  const summary = { failed: [], failedLines: 0, failedCount: 0, passedCount: 0, summaries: 0 };
  for (const line of output.replace(/\r\n/g, '\n').split('\n')) {
    const failed = FAILED_TEST.exec(line);
    if (failed) {
      summary.failedLines += 1;
      const test = failed[1].replace(/\(.*$/, '');
      if (!summary.failed.includes(test)) summary.failed.push(test);
      continue;
    }
    const counts = SUMMARY.exec(line);
    if (counts) {
      summary.summaries += 1;
      summary.failedCount += Number(counts[1]);
      summary.passedCount += Number(counts[2]);
    }
  }
  return summary;
}

/**
 * Whether a failed run's failures are re-run alone: all of them named, and all real-process tests. The caller
 * asks only of a Process gate, where every test is one, so the default says yes.
 */
export function flakeDecision(output, isProcess = () => true) {
  const summary = testSummary(output);
  if (summary.summaries === 0) return { rerun: [], reason: 'the suite printed no result: it did not build, or its run was cut off' };
  if (summary.failedLines === 0) return { rerun: [], reason: 'no failing test is named: the run failed outside a test' };
  if (summary.failedLines !== summary.failedCount) {
    return { rerun: [], reason: `${summary.failedCount} failed but ${summary.failedLines} are named` };
  }
  const others = summary.failed.filter((test) => !isProcess(test));
  if (others.length) return { rerun: [], reason: `not a real-process test: ${others.map(short).join(', ')}` };
  return { rerun: summary.failed };
}

export const rerunCommand = (run, test) => `${run.trim()} --no-build --filter "FullyQualifiedName=${test}"`;

/**
 * The lines of a gate's log that are the gate's own: after the steps before it (`command` names the
 * gate's, and its output starts after the last `$ <command>` line), and without what this tool writes
 * around a step (`$ …`, `(exited …)`, `could not start: …`), npm's lines around a script
 * (`> pkg@version script`, `> command`, `npm error …`), and blank lines.
 */
export function ownOutput(log, command) {
  const all = log.replace(/\r\n/g, '\n').split('\n');
  const from = command === undefined ? -1 : all.lastIndexOf(`$ ${command}`);
  return all.slice(from + 1).filter((line) => line.trim()
    && !/^\$ /.test(line)
    && !/^\(exited /.test(line)
    && !/^could not start: /.test(line)
    && !/^> /.test(line)
    && !/^npm (?:error|ERR!|warn|WARN|notice)\b/.test(line));
}

/**
 * Whether a Playwright run's failures are re-run alone: all of them in timed specs (a title path through
 * `timed`), all named. Read from the summary the run ends with (`N failed`, then one spec per line), which the list and dot
 * reporters both print, so a retry's lines above it and the `[project] ›` prefix do not matter. `playwright` is false for a
 * log with no such summary; `rerun` is then each failed spec's `file:line`, once.
 */
export function timedDecision(output) {
  const own = ownOutput(output);
  const at = own.findLastIndex((line) => /^\s*\d+ failed\s*$/.test(line));
  if (at < 0) return { playwright: false, rerun: [], reason: 'it printed no Playwright summary of failed specs' };
  const count = Number(/\d+/.exec(own[at])[0]);
  const specs = [];
  for (const line of own.slice(at + 1)) {
    const spec = /^\s+(?:\[[^\]]+\]\s+›\s+)?(\S+?):(\d+):\d+\s+›\s+(.*?)\s*$/.exec(line);
    if (!spec) break;
    specs.push({ location: `${spec[1].replaceAll('\\', '/')}:${spec[2]}`, timed: line.includes(' › timed › ') });
  }
  if (specs.length !== count) return { playwright: true, rerun: [], reason: `${count} failed but ${specs.length} are named` };
  const others = specs.filter((spec) => !spec.timed);
  if (others.length) return { playwright: true, rerun: [], reason: `not a timed spec: ${[...new Set(others.map((spec) => spec.location.replace(/^.*\//, '')))].join(', ')}` };
  return { playwright: true, rerun: [...new Set(specs.map((spec) => spec.location))] };
}

/** The gate's command run for the specs named, alone: `npm run` forwards what follows its second `--` to Playwright. */
export const timedRerunCommand = (run, specs) => `${run.trim()} -- -- ${specs.join(' ')}`;

/**
 * The folders a gate's `node --test` runs in, read from the npm scripts the gate's command runs (`npm run x`,
 * `npm test`, with `-w <package>` to a workspace) and the `&&` steps of each; none for a gate that runs no node test.
 */
export function nodeTestDirs(root, gate) {
  const found = [];
  const seen = new Set();
  const read = (dir) => {
    try { return JSON.parse(readFileSync(join(dir, 'package.json'), 'utf8')); } catch { return null; }
  };
  const workspace = (name) => (read(root)?.workspaces ?? []).map((entry) => join(root, entry)).find((dir) => read(dir)?.name === name) ?? null;
  const walk = (command, dir) => {
    for (const part of command.split('&&')) {
      const step = part.trim();
      if (/^node --test\b/.test(step)) {
        if (!found.includes(dir)) found.push(dir);
        continue;
      }
      const npm = /^npm (?:run )?([\w:.-]+)(?:\s+(?:-w|--workspace)[ =](\S+))?/.exec(step);
      if (!npm) continue;
      const from = npm[2] ? workspace(npm[2]) : dir;
      const key = `${from}|${npm[1]}`;
      const script = from && !seen.has(key) ? read(from)?.scripts?.[npm[1]] : null;
      seen.add(key);
      if (typeof script === 'string') walk(script, from);
    }
  };
  walk(gate.run.trim(), gate.cwd ? join(root, gate.cwd) : root);
  return found;
}

/**
 * Whether a `node --test` run's failures are re-run alone: all named in its `✖ failing tests` list (a
 * `test at <file>:<line>` line, then `✖ <title> (<ms>)`), and the list as long as the run's `ℹ fail` counts say. `rerun` is each
 * file's failed titles, once, with the file as node printed it.
 */
export function nodeTestDecision(output) {
  const lines = output.replace(/\r\n/g, '\n').split('\n');
  let summaries = 0;
  let failedCount = 0;
  let named = 0;
  const files = new Map();
  lines.forEach((line, i) => {
    const total = /^ℹ fail (\d+)\s*$/.exec(line);
    if (total) {
      summaries += 1;
      failedCount += Number(total[1]);
      return;
    }
    const at = /^test at (.+?):\d+:\d+\s*$/.exec(line);
    const test = at && /^✖ (.*?)(?: \(\d+(?:\.\d+)?ms\))?\s*$/.exec(lines[i + 1] ?? '');
    if (!test) return;
    named += 1;
    const file = at[1].replaceAll('\\', '/');
    if (!files.has(file)) files.set(file, []);
    if (!files.get(file).includes(test[1])) files.get(file).push(test[1]);
  });
  if (summaries === 0) return { node: false, rerun: [], reason: 'no failed node test is named: it printed no node test result' };
  if (named === 0) return { node: true, rerun: [], reason: 'no failed node test is named: the run failed outside a test' };
  if (named !== failedCount) return { node: true, rerun: [], reason: `${failedCount} failed but ${named} are named` };
  return { node: true, rerun: [...files].map(([file, tests]) => ({ file, tests })) };
}

/**
 * One test of a file run alone, by its own title as node printed it, anchored: node matches a nested test by its own name too.
 * A character a shell reads inside double quotes is matched by a dot; the rest that a pattern reads is escaped.
 */
export const nodeTestRerunCommand = (file, title) => {
  const pattern = title.replace(/(["%$`\\!])|([.*+?^${}()|[\]])/g, (match, shell) => (shell ? '.' : `\\${match}`));
  return `node --test --test-name-pattern="^${pattern}$" ${file}`;
};
