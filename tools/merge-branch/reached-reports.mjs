/**
 * Each reached command's report of what passed, so a pass is recorded per unit and not per command: a trx for a .NET class, a
 * junit report for a node test file and vitest's json for a page or merge tool test file. A command that wrote none records its units only where
 * it exited clean.
 */
import { existsSync, readFileSync } from 'node:fs';
import { posix, resolve } from 'node:path';
import { junitPasses, vitestPasses } from '../merge/file-passes.ts';
import { passedClasses } from '../merge/trx-passes.ts';

const FOLDER = '_fixtures/test-timing';
const same = (a, b) => resolve(a).replaceAll('\\', '/').toLowerCase() === resolve(b).replaceAll('\\', '/').toLowerCase();

/** The command's arguments with the report its runner writes under `name`, and where the report lands from the repository root. */
export function withReport(each, name) {
  if (each.command === 'dotnet') return { args: [...each.args, '--results-directory', FOLDER, '--logger', `trx;LogFileName=${name}.trx`], report: `${FOLDER}/${name}.trx` };
  if (each.args[0]?.endsWith('vitest/vitest.mjs')) {
    // Vitest reads the report's path from its root, the folder the command runs in.
    const report = `${FOLDER}/${name}.json`;
    return { args: [...each.args, '--reporter=default', '--reporter=json', `--outputFile.json=${posix.relative(each.cwd, report)}`], report };
  }
  // The runner's options come before the files it names.
  const at = each.args.indexOf('--test-force-exit') + 1;
  const reporters = ['--test-reporter=spec', '--test-reporter-destination=stdout', '--test-reporter=junit', `--test-reporter-destination=../../${FOLDER}/${name}.xml`];
  return { args: [...each.args.slice(0, at), ...reporters, ...each.args.slice(at)], report: `${FOLDER}/${name}.xml` };
}

/** The units of `group` its command's report says passed, or all of them where there is no report and the command exited clean. */
export function passesOf(group, exitCode, report, root) {
  if (!report || !existsSync(report)) return exitCode === 0 ? group.units : [];
  const text = readFileSync(report, 'utf8');
  if (group.runner === 'dotnet') {
    const passed = passedClasses(text);
    return group.units.filter((unit) => passed.has(unit.unit));
  }
  const passed = group.runner === 'vitest' ? vitestPasses(text) : junitPasses(text);
  return group.units.filter((unit) => [...passed].some((file) => same(file, resolve(root, unit.file))));
}
