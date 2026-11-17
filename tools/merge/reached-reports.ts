/**
 * Each reached command's report of what passed, so a pass is recorded per unit and not per command: a trx for a .NET class, a
 * junit report for a node test file and vitest's json for a page or merge tool test file. A command that wrote none records its units only where
 * it exited clean.
 */
import { existsSync, readFileSync } from 'node:fs';
import { posix, resolve } from 'node:path';
import { junitPasses, vitestFailures, vitestPasses } from './file-passes.ts';
import { passedClasses } from './trx-passes.ts';
import type { Command, ReachedGroup } from './reached.ts';

const FOLDER = '_fixtures/test-timing';
const same = (a: string, b: string): boolean => resolve(a).replaceAll('\\', '/').toLowerCase() === resolve(b).replaceAll('\\', '/').toLowerCase();

/** The command's arguments with the report its runner writes under `name`, and where the report lands from the repository root. */
export function withReport(each: Pick<Command, 'command' | 'args' | 'cwd'>, name: string): { args: string[]; report: string } {
  if (each.command === 'dotnet') return { args: [...each.args, '--results-directory', FOLDER, '--logger', `trx;LogFileName=${name}.trx`], report: `${FOLDER}/${name}.trx` };
  if (each.args[0]?.endsWith('vitest/vitest.mjs')) {
    const report = `${FOLDER}/${name}.json`;
    return { args: [...each.args, '--reporter=default', '--reporter=json', `--outputFile.json=${posix.relative(each.cwd, report)}`], report };
  }
  // The runner's options come before the files it names.
  const at = each.args.indexOf('--test-force-exit') + 1;
  const reporters = ['--test-reporter=spec', '--test-reporter-destination=stdout', '--test-reporter=junit', `--test-reporter-destination=../../${FOLDER}/${name}.xml`];
  return { args: [...each.args.slice(0, at), ...reporters, ...each.args.slice(at)], report: `${FOLDER}/${name}.xml` };
}

/** A line for each test a page or merge tool report says failed, with its file, so a failure names its test and can be run alone. */
export function failedTestLines(group: Pick<ReachedGroup, 'runner'>, report: string): string[] {
  if (group.runner !== 'vitest' || !existsSync(report)) return [];
  return vitestFailures(readFileSync(report, 'utf8')).map(({ file, test }) => `${posix.basename(file.replaceAll('\\', '/'))}: failed test '${test}'`);
}

const instant = (text: string | undefined): number => (text ? Date.parse(text.replace(/(\.\d{3})\d+/, '$1')) : NaN);
const spanMs = (text: string | undefined): number => {
  const [h, m, s] = (text ?? '').split(':').map(Number);
  return ((h ?? NaN) * 3600 + (m ?? NaN) * 60 + (s ?? NaN)) * 1000;
};

/**
 * When a trx's first test started, in epoch milliseconds, or null: what came before it of the command (the project's evaluation,
 * the runner's start, the discovery of every case of the assembly) is the suite's discovery, what came after its run. xUnit's
 * results carry their report's moment as their start, so a result's start is its end less its duration where that is earlier;
 * a report with no result falls back to the run's own start (`<Times start>`).
 */
export function runStartOf(trx: string): number | null {
  let first = Infinity;
  for (const [tag] of trx.matchAll(/<UnitTestResult\b[^>]*>/g)) {
    const starts = [
      instant(/\bstartTime="([^"]+)"/.exec(tag)?.[1]),
      instant(/\bendTime="([^"]+)"/.exec(tag)?.[1]) - spanMs(/\bduration="([^"]+)"/.exec(tag)?.[1]),
    ].filter(Number.isFinite);
    if (starts.length) first = Math.min(first, ...starts);
  }
  if (Number.isFinite(first)) return first;
  const at = instant(/<Times\b[^>]*\bstart="([^"]+)"/.exec(trx)?.[1]);
  return Number.isFinite(at) ? at : null;
}

/** The units of `group` its command's report says passed, or all of them where there is no report and the command exited clean. */
export function passesOf<T extends { file: string; unit: string }>(
  group: Pick<ReachedGroup, 'runner'> & { units: T[] },
  exitCode: number | null,
  report: string | null,
  root: string,
): T[] {
  if (!report || !existsSync(report)) return exitCode === 0 ? group.units : [];
  const text = readFileSync(report, 'utf8');
  if (group.runner === 'dotnet') {
    const passed = passedClasses(text);
    return group.units.filter((unit) => passed.has(unit.unit));
  }
  const passed = group.runner === 'vitest' ? vitestPasses(text) : junitPasses(text);
  return group.units.filter((unit) => [...passed].some((file) => same(file, resolve(root, unit.file))));
}
