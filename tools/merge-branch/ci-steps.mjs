/**
 * What a CI run saves itself: a gate whose work a gate beside it already does, and the one build the web gates would each wait on.
 */
import { closeSync, openSync, readFileSync } from 'node:fs';
import { join } from 'node:path';

// The duration reporter a gate's command carries (`--results-directory … --logger "trx;…"`) changes nothing a half covers.
const TIMING = String.raw`(?: --results-directory \S+ --logger "trx;LogFileName=[^"]+")?`;
const CENSUS = new RegExp(String.raw`^dotnet test (\S+) --filter Category=Census${TIMING}$`);
const FAST_HALF = new RegExp(String.raw`^dotnet test (\S+) --filter Category!=Process${TIMING}$`);

const projectOf = (path) => path.replace(/^["']|["']$/g, '').split(/[\\/]+/).filter((part) => part && part !== '.').join('/');

/**
 * The gates of `gates` that run only the census classes of test projects whose fast half is among them, each with the halves that
 * run them: the fast half's filter (`Category!=Process`) takes every class that is no Process class, the census among them. A
 * census gate with any project whose fast half is not in `gates` (kept by its verdict, or not declared) is not covered, and is run;
 * nor is a command that does anything else. Read from what each runs, so a renamed gate is still found.
 */
export function censusCovers(gates) {
  const halfOf = (gate) => {
    const found = FAST_HALF.exec(gate.run.trim());
    return found ? projectOf(found[1]) : null;
  };
  const covers = new Map();
  for (const gate of gates) {
    const parts = gate.run.split('&&').map((part) => CENSUS.exec(part.trim()));
    if (parts.some((part) => !part)) continue;
    const halves = parts.map((part) => gates.find((other) => halfOf(other) === projectOf(part[1])));
    if (halves.every(Boolean)) covers.set(gate.name, [...new Set(halves.map((half) => half.name))]);
  }
  return covers;
}

/** Which covered gates their halves passed, and which are unmet, each with the halves that did not pass. */
export function coverOutcome(covers, results) {
  const passed = [];
  const unmet = [];
  for (const [name, by] of covers) {
    const failing = by.filter((half) => results.find((result) => result.gate.name === half)?.verdict !== 'PASS');
    if (failing.length) unmet.push({ name, by: failing });
    else passed.push(name);
  }
  return { passed, unmet };
}

/** Why a covered gate is not run, as the plan and the status say it. */
export const coverNote = (by) => `not run: ${by.join(' and ')} run its classes in this run`;

/**
 * The covered gates settled once the run's gates have ended: one whose halves all passed is recorded as a pass (`record`) and
 * marked done, one with a half that did not pass is marked not run and stays owed. `mark` writes a gate's status entry.
 */
export function settleCovered(covers, results, { record, mark }) {
  const { passed, unmet } = coverOutcome(covers, results);
  for (const name of passed) {
    record(name, covers.get(name));
    mark(name, { state: 'done', verdict: 'PASS', ms: 0, note: `passed by ${covers.get(name).join(' and ')}, which ran its classes` });
  }
  for (const { name, by } of unmet) mark(name, { note: `not run, and no pass: ${by.join(' and ')} did not pass` });
  return { passed, unmet };
}

/** The page package's scripts, none where it has none or does not read. */
export function pageScripts(root) {
  try {
    const { scripts } = JSON.parse(readFileSync(join(root, 'src', 'Daoris.Web', 'package.json'), 'utf8'));
    return scripts && typeof scripts === 'object' ? scripts : {};
  } catch {
    return {};
  }
}

/** The part of a command that names the root script `npm run <script>`, past its flags, or null. */
const rootScript = (text) => /^npm\s+run\s+(?:-\S+\s+)*([^\s-]\S*)/.exec(text)?.[1] ?? null;

/** The part of a command that names a script of the page's package, `npm --prefix <folder> run <script>`, or null. */
const pageScript = (text) => /^npm\s+--prefix\s+\S+\s+run\s+(?:-\S+\s+)*([^\s-]\S*)/.exec(text)?.[1] ?? null;

/**
 * The gates whose command runs a script of the page's package that has a `pre<script>` running the contracts' generator: read
 * through the root scripts a gate names, as `gateComponents` reads them. The page's pre-scripts are what builds the contracts, so
 * these are the gates that wait on it.
 */
export function contractsGates(gates, scripts, pageScripts) {
  const reaches = (command, seen) => command.split(/&&|\|\||;/).some((part) => {
    const text = part.trim();
    const root = rootScript(text);
    if (root) return !seen.has(root) && typeof scripts[root] === 'string' && reaches(scripts[root], new Set([...seen, root]));
    const page = pageScript(text);
    return page !== null && /contracts\.mjs/.test(pageScripts[`pre${page}`] ?? '');
  });
  return gates.filter((gate) => reaches(gate.run, new Set())).map((gate) => gate.name);
}

/**
 * The contracts' generator run once, `--if-stale`, as the page's pre-scripts run it, into `contracts.log` of `dir`: the answer is
 * the exit code, never a rejection. A failed step is no failure of the run: each gate it served runs its own pre-script, which says
 * what failed. `step` is `runStep`'s shape, handed the run's trees.
 */
export async function startContractsStep(root, dir, step) {
  const fd = openSync(join(dir, 'contracts.log'), 'w');
  try {
    return await step('node tools/contracts.mjs --if-stale', root, fd, {});
  } catch {
    return 1;
  } finally {
    closeSync(fd);
  }
}
