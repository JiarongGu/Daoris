// `daoris family materialize`: this repository's pins turned into what its build reads, by the producer's tree at each pin.
//
// It is one way a repository may turn its pins into its build's input, the one Daoris supplies; a repository declares it, or a command
// of its own (a package restored, a reference generated), as its gates' `regenerate`, which runs before each gate and when a tree is
// prepared, so its gates build against the pin. Each pin of this repository's own `daoris.family.lock` (a session's tree reads its
// own, where a pin move is made) is judged as `family check` judges a pin, then written as its producer's tree at the pinned commit,
// read from the producer's git store beside the family file and never from its working tree, which stands wherever its own work left
// it. Each producer goes to `<into>/<producer>/` (`pinned` by default, a folder inside this repository its ignore file should name),
// with a `.daoris-pin` naming the commit: the same pin is left as it is, a moved one replaces the folder whole, and a folder with no
// such file is the repository's own and refused, never replaced. Any refusal writes nothing. A link is written as a file holding its
// target, as git writes one where links are off; a submodule is said and not written. It starts no process.

import { chmodSync, existsSync, mkdirSync, readFileSync, realpathSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, isAbsolute, join, relative, resolve, sep } from 'node:path';
import { DaorisError, type ExitCode } from '../platform/errors.ts';
import type { CommandArgs } from '../platform/types.ts';
import { memberAt, optionOf } from './check.ts';
import { LOCK_FILE, readFamily, readLock } from './family.ts';
import { openRepository, type Repository, type TreeFile } from './gitread.ts';
import { familyOf } from './member.ts';
import { judgePin } from './pins.ts';

const USAGE = 'usage: daoris family materialize [--family <file>] [--into <folder>]';

/** The file in each producer's folder that says materialize wrote it, and at which commit. */
export const PIN_MARKER = '.daoris-pin';

const DEFAULT_INTO = 'pinned';
const SUBMODULE = '160000';
const EXECUTABLE = '100755';

interface Planned {
  producer: string;
  commit: string;
  target: string;
  repository: Repository;
  files: TreeFile[];
  unchanged: boolean;
}

const plural = (count: number, one: string, many: string) => `${count} ${count === 1 ? one : many}`;
const short = (id: string) => id.slice(0, 12);

/** The marker a producer's folder holds, or null where it holds none (or one that is not materialize's). */
function markerOf(target: string): { producer: string; commit?: string } | null {
  const file = join(target, PIN_MARKER);
  if (!existsSync(file)) return null;
  try {
    const json = JSON.parse(readFileSync(file, 'utf8')) as { producer?: unknown; commit?: unknown };
    if (typeof json.producer !== 'string') return null;
    return { producer: json.producer, ...(typeof json.commit === 'string' ? { commit: json.commit } : {}) };
  } catch {
    return null;
  }
}

/** A path of a tree that cannot be written under the producer's folder as it names itself: out of it, into a `.git`, or not a name here. */
export function unsafePath(path: string, platform: NodeJS.Platform = process.platform): boolean {
  return path.split('/').some((part) => part === '' || part === '.' || part === '..' || part.toLowerCase() === '.git'
    || part.includes('\\') || part.includes('\0') || (platform === 'win32' && /[:<>"|?*]/.test(part)));
}

/** Whether `into` stands inside `root` by its real path, a link leading out counting as outside. */
function inside(root: string, into: string): boolean {
  const lexical = relative(root, into);
  if (lexical === '' || lexical.startsWith('..') || isAbsolute(lexical)) return false;
  let probe = into;
  while (!existsSync(probe)) probe = dirname(probe);
  const real = relative(realpathSync(root), realpathSync(probe));
  return !real.startsWith('..') && !isAbsolute(real);
}

export function commandFamilyMaterialize({ root, argv, write }: Pick<CommandArgs, 'root' | 'argv' | 'write'>): ExitCode {
  const named = optionOf(argv, '--family', USAGE);
  const intoAsked = optionOf(argv, '--into', USAGE) ?? DEFAULT_INTO;
  const into = resolve(root, intoAsked);
  if (!inside(root, into)) throw new DaorisError(`--into ${intoAsked} is no folder inside this repository; materialize writes only there`);
  const shown = (path: string) => relative(root, path).split(sep).join('/');

  const lock = join(root, LOCK_FILE);
  const pins = existsSync(lock) ? readLock(readFileSync(lock, 'utf8'), lock) : [];
  if (pins.length === 0) {
    write(`${existsSync(lock) ? `${LOCK_FILE} pins nothing` : `no ${LOCK_FILE} here`}: nothing pinned, nothing to materialize`);
    return 0;
  }

  const { member, file } = familyOf(root, named);
  const refusals: string[] = [];
  const planned: Planned[] = [];
  if (!existsSync(file)) {
    refusals.push(`${member} pins ${plural(pins.length, 'producer', 'producers')} and no family file is at ${file}: a pin is read from its `
      + 'producer\'s checkout beside it (--family names another)');
  } else {
    const family = readFamily(readFileSync(file, 'utf8'), file);
    const names = family.layers.flat();
    const state = memberAt(root, member);
    if (!names.some((name) => name.toLowerCase() === member.toLowerCase())) refusals.push(`${member} is no member of the family at ${file}`);
    const seen = new Set<string>();
    for (const pin of pins) {
      const producer = names.find((name) => name.toLowerCase() === pin.producer.toLowerCase());
      if (!producer) {
        refusals.push(`${member} pins ${pin.producer}, which is no member of the family`);
        continue;
      }
      if (!state.uses.some((used) => used.toLowerCase() === producer.toLowerCase())) {
        refusals.push(`${member} pins ${producer} and does not use it: a pin needs its edge in daoris.json's uses`);
        continue;
      }
      if (seen.has(producer)) {
        refusals.push(`${member} pins ${producer} twice: one pin per producer`);
        continue;
      }
      seen.add(producer);
      const repository = openRepository(join(dirname(file), producer));
      const judged = judgePin({ consumer: member, producer, commit: pin.commit }, repository);
      if (judged.verdict !== 'ok' || !repository) {
        refusals.push(judged.message);
        continue;
      }
      const commit = pin.commit.toLowerCase();
      const target = join(into, producer);
      const marker = markerOf(target);
      if (existsSync(target) && marker === null) {
        refusals.push(`${shown(target)} holds files materialize did not write (no ${PIN_MARKER}): move them, or name another folder with --into`);
        continue;
      }
      const files = repository.filesOf(commit) ?? [];
      const unsafe = files.find((each) => unsafePath(each.path));
      if (unsafe) {
        refusals.push(`${producer} at ${short(commit)} holds \`${unsafe.path}\`, which cannot be written under ${shown(target)} on this machine`);
        continue;
      }
      planned.push({ producer, commit, target, repository, files, unchanged: marker?.producer === producer && marker.commit === commit });
    }
  }

  if (refusals.length > 0) {
    for (const message of refusals) write(`  ${'refused'.padEnd(9)} ${message}`);
    write(`nothing materialized: ${plural(refusals.length, 'refused', 'refused')}`);
    return 1;
  }

  for (const { producer, commit, target, repository, files, unchanged } of planned) {
    if (unchanged) {
      write(`  ${'unchanged'.padEnd(9)} ${producer} at ${short(commit)} in ${shown(target)}`);
      continue;
    }
    rmSync(target, { recursive: true, force: true });
    mkdirSync(target, { recursive: true });
    // The marker first names no commit, so a write cut short is read as materialize's own, and replaced, never as done.
    writeFileSync(join(target, PIN_MARKER), `${JSON.stringify({ producer }, null, 2)}\n`);
    const submodules: string[] = [];
    for (const entry of files) {
      if (entry.mode === SUBMODULE) {
        submodules.push(entry.path);
        continue;
      }
      const data = repository.blob(entry.id);
      if (!data) throw new DaorisError(`${producer} at ${short(commit)}: the blob of ${entry.path} is not in its repository`);
      const path = join(target, ...entry.path.split('/'));
      mkdirSync(dirname(path), { recursive: true });
      writeFileSync(path, data);
      if (entry.mode === EXECUTABLE && process.platform !== 'win32') chmodSync(path, 0o755);
    }
    writeFileSync(join(target, PIN_MARKER), `${JSON.stringify({ producer, commit }, null, 2)}\n`);
    const count = files.length - submodules.length;
    write(`  ${'wrote'.padEnd(9)} ${producer} at ${short(commit)} into ${shown(target)} (${plural(count, 'file', 'files')})`);
    for (const path of submodules) write(`  ${'skipped'.padEnd(9)} ${producer}'s ${path}: a submodule's commit is another repository's, and none is written`);
  }
  write(`materialized ${plural(planned.length, 'pin', 'pins')} into ${shown(into)}`);
  return 0;
}
