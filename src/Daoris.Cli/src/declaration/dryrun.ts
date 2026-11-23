import { existsSync, lstatSync, readFileSync, realpathSync, rmSync } from 'node:fs';
import { join, relative, resolve } from 'node:path';
import { flagValue } from '../platform/args.ts';
import { DaorisError, type ExitCode } from '../platform/errors.ts';
import { listFiles, readJsonObject, writeTextAtomic } from '../platform/fsx.ts';
import type { CommandArgs } from '../platform/types.ts';
import { formatJson } from './json.ts';
import { proveSame, type Difference } from './proof.ts';
import { splitDeclaration, type HomeValues } from './split.ts';
import { nearestExisting, repositoryFiles, within } from './tree.ts';

/** Where a dry run writes by default: the scratch folder of the repository, which its ignore file keeps out of history. */
const SCRATCH = '_fixtures/declaration-split';
const GATES_FILE = 'daoris.gates.json';
/** What a dry run leaves in its folder: it says the `daoris/` and `home.json` there are the command's own to replace, never another's. */
const MARKER = '.declaration-split';

const isLink = (path: string): boolean => {
  try {
    return lstatSync(path).isSymbolicLink();
  } catch {
    return false;
  }
};

/**
 * Checks where a dry run may write and returns it: `out` must lie inside the repository by its real path, so a link in it or above
 * it that leads elsewhere is refused. An existing `daoris/` or `home.json` there that no earlier dry run made (no marker) is left
 * as it is and refused, and a link in their place is never written through.
 */
function scratchFolder(root: string, out: string): string {
  if (relative(root, out) === '' || !within(root, out) || !within(realpathSync(root), realpathSync(nearestExisting(out)))) {
    throw new DaorisError(`--out names ${out}, which is the repository itself or outside it (a link counts where it leads): the dry run writes to a folder of its own inside the repository`);
  }
  for (const own of [join(out, 'daoris'), join(out, 'home.json')]) {
    if (isLink(own)) throw new DaorisError(`${own} is a link, and the dry run writes through none`);
    if (existsSync(own) && !existsSync(join(out, MARKER))) {
      throw new DaorisError(`${own} is there and this command did not make it, so it is left as it is: name a folder of its own with --out`);
    }
  }
  return out;
}

/** The folder and the home as a dry run left them on disk: each file of `daoris/` by its path there, and `home.json`. */
export function readScratch(out: string): { folder: Record<string, unknown>; home: HomeValues } {
  const folder: Record<string, unknown> = {};
  for (const path of listFiles(join(out, 'daoris'))) folder[path] = JSON.parse(readFileSync(join(out, 'daoris', path), 'utf8'));
  return { folder, home: JSON.parse(readFileSync(join(out, 'home.json'), 'utf8')) as HomeValues };
}

const USAGE = 'usage: daoris declaration split --dry-run [--out <folder>]';

const plural = (count: number, noun: string): string => `${count} ${noun}${count === 1 ? '' : 's'}`;

/** A list of names as words: `a`, `a and b`, `a, b and c`. */
const joined = (names: readonly string[]): string => (names.length < 2 ? names.join('') : `${names.slice(0, -1).join(', ')} and ${names.at(-1)}`);

function said(difference: Difference): string {
  const { kind, subject, was, now } = difference;
  if (kind === 'reach') return `the reach of gate ${subject} differs: it loses ${was || 'nothing'} and gains ${now || 'nothing'}`;
  if (kind === 'set') return `the set holding ${subject} differs: ${was} in the old file, ${now} in the folder`;
  if (kind === 'order') return `the gates' order differs: ${was} in the old file, ${now} in the folder`;
  if (kind === 'folder') return `the folder is no declaration: ${now}`;
  return `${subject} differs: ${was} in the old file, ${now} in the folder`;
}

/**
 * `daoris declaration split --dry-run`: converts this repository's `daoris.gates.json` in scratch. It writes the
 * folder of small files and the home's values under `--out` (the repository's scratch by default), reads them back with nothing
 * from the old file (the files as written, read from the disk) and proves they resolve to it, and names each difference and each set
 * that overlaps today. It replaces only the `daoris/` and `home.json` an earlier dry run left there (its marker says so), refuses an
 * `--out` that is outside the repository by its real path, and writes nowhere else; the run that commits the conversion waits for the home to have its door and the readers to take the folder.
 *
 * Exit codes: 0 the conversion would go through, or there is nothing to convert · 1 a difference, an overlap or a member nobody
 * sorted stops it, each named · 2 a tool error.
 */
export function commandDeclaration({ root, argv, write }: CommandArgs): ExitCode {
  if (argv[0] !== 'split') throw new DaorisError(USAGE);
  if (!argv.includes('--dry-run')) {
    throw new DaorisError('daoris declaration split writes only a dry run until the home has its door and the readers take the folder: pass --dry-run. '
      + 'Nothing was written.');
  }

  const file = join(root, GATES_FILE);
  if (!existsSync(file)) {
    write(`no ${GATES_FILE}: nothing to convert`);
    return 0;
  }
  const read = readJsonObject(file);
  if (read.value === null) throw new DaorisError(read.problem ?? `${GATES_FILE} is not a JSON object`);

  const out = resolve(root, flagValue(argv, '--out') ?? SCRATCH);
  const split = splitDeclaration(read.value);
  scratchFolder(root, out);

  writeTextAtomic(join(out, MARKER), 'written by daoris declaration split --dry-run: the next run replaces daoris/ and home.json here\n');
  rmSync(join(out, 'daoris'), { recursive: true, force: true });
  for (const [path, content] of Object.entries(split.folder)) writeTextAtomic(join(out, 'daoris', path), formatJson(content));
  writeTextAtomic(join(out, 'home.json'), formatJson(split.home));
  const proof = proveSame(read.value, { ...readScratch(out), product: split.product }, repositoryFiles(root));

  const where = relative(root, out).replaceAll('\\', '/');
  write(`${GATES_FILE} split in scratch: ${plural(Object.keys(split.folder).length, 'file')} in ${where}/daoris, the home's values in ${where}/home.json`);
  write(`${plural(proof.gates, 'gate')}, ${plural(proof.testFiles, 'test file')} held by a set`);
  for (const path of split.product) write(`${path} is the product's documentation, so it is written nowhere`);

  if (proof.differences.length === 0) {
    write("the new declaration resolves to the old declaration member for member: every gate's commands, its expanded reach, the set holding each test file and the gates' order");
  } else {
    write(`the new declaration does not resolve to the old one: ${plural(proof.differences.length, 'difference')}`);
    for (const difference of proof.differences) write(`  ${said(difference)}`);
  }
  if (proof.overlaps.length) {
    write(`${plural(proof.overlaps.length, 'test file')} held by two sets: the folder's format refuses one unless a set names the other by \`of\`, so resolve each before converting`);
    for (const overlap of proof.overlaps) write(`  ${overlap.file} is held by the sets ${joined(overlap.sets)}; today ${overlap.today}, the first that is no pick, runs it`);
  }
  return proof.differences.length || proof.overlaps.length ? 1 : 0;
}
