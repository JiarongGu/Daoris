// `daoris family materialize`: this repository's pins turned into what its build reads, by the producer's tree at each pin.
//
// It is one way a repository may turn its pins into its build's input, the one Daoris supplies; a repository declares it, or a command
// of its own (a package restored, a reference generated), as its gates' `regenerate`, which runs before each gate and when a tree is
// prepared, so its gates build against the pin. The family is judged as `family check` judges it, this repository read from the folder
// it runs in (a session's tree reads its own lock, where a pin move is made), and each of its pins is written as its producer's tree at
// the pinned commit, read from the producer's git store beside the family file and never from its working tree. Each producer goes to
// `<into>/<producer>/` (`pinned` by default, a folder its ignore file should name), with a `.daoris-pin` naming the commit; a folder with
// no such file is the repository's own and refused. Every object is found before anything is written, and each producer is written
// beside its folder and swapped in, so any refusal writes nothing and a write cut short leaves the old pin. It starts no process.

import { chmodSync, existsSync, mkdirSync, readdirSync, readFileSync, realpathSync, renameSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, isAbsolute, join, relative, resolve, sep } from 'node:path';
import { DaorisError, type ExitCode } from '../platform/errors.ts';
import type { CommandArgs } from '../platform/types.ts';
import { memberAt, optionOf } from './check.ts';
import { LOCK_FILE, judgeFamily, readFamily, readLock, readablePins, type MemberState } from './family.ts';
import { openRepository, type Repository, type TreeFile } from './gitread.ts';
import { familyOf } from './member.ts';
import { judgePin } from './pins.ts';

const USAGE = 'usage: daoris family materialize [--family <file>] [--into <folder>]';

/** The file in each producer's folder that says materialize wrote it, and at which commit. */
export const PIN_MARKER = '.daoris-pin';

const DEFAULT_INTO = 'pinned';
const SUBMODULE = '160000';
const EXECUTABLE = '100755';
const DEVICE = /^(con|prn|aux|nul|com[0-9¹²³]|lpt[0-9¹²³])(\..*)?$/i;

interface Planned {
  producer: string;
  commit: string;
  target: string;
  repository: Repository;
  files: TreeFile[];
  unchanged: boolean;
  lfs: boolean;
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

/**
 * A path of a tree that cannot be written under the producer's folder as it names itself: out of it, into a `.git`, or not a name here.
 * On Windows a part is read as the filesystem reads it, its trailing dots and spaces dropped, and git's NTFS refusals hold: the short
 * name `git~1`, a device name with or without an extension, a control character.
 */
export function unsafePath(path: string, platform: NodeJS.Platform = process.platform): boolean {
  return path.split('/').some((part) => {
    if (part === '' || part === '.' || part === '..' || part.toLowerCase() === '.git' || part.includes('\\') || part.includes('\0')) return true;
    if (platform !== 'win32') return false;
    const read = part.replace(/[. ]+$/, '').toLowerCase();
    return read === '' || read === '.' || read === '..' || read === '.git' || read === 'git~1' || DEVICE.test(read)
      || /[:<>"|?*\u0000-\u001f]/.test(part);
  });
}

const outside = (path: string) => path === '..' || path.startsWith(`..${sep}`) || isAbsolute(path);

/** Why `into` cannot be written, or null: it must stand inside `root` by its real path, a link leading out counting as outside, and outside a `.git`. */
export function intoRefusal(root: string, into: string): string | null {
  const lexical = relative(root, into);
  if (lexical === '' || outside(lexical)) return 'is no folder inside this repository';
  if (lexical.split(sep).some((part) => part.toLowerCase() === '.git')) return 'is inside the repository\'s git folder';
  if (!existsSync(root)) return null;
  let probe = into;
  while (!existsSync(probe)) probe = dirname(probe);
  return outside(relative(realpathSync(root), realpathSync(probe))) ? 'leads out of this repository by a link' : null;
}

/** Every file under `folder`, by its path from it joined by `/`. */
function filesUnder(folder: string, prefix = ''): string[] {
  return readdirSync(join(folder, prefix), { withFileTypes: true })
    .flatMap((entry) => (entry.isDirectory() ? filesUnder(folder, `${prefix}${entry.name}/`) : [`${prefix}${entry.name}`]));
}

/** Whether the folder holds exactly the tree's written files and the marker, so the same pin needs no write. */
function holdsTree(target: string, files: TreeFile[]): boolean {
  const wanted = new Set([PIN_MARKER, ...files.filter((each) => each.mode !== SUBMODULE).map((each) => each.path)]);
  const present = filesUnder(target);
  return present.length === wanted.size && present.every((path) => wanted.has(path));
}

export function commandFamilyMaterialize({ root, argv, write }: Pick<CommandArgs, 'root' | 'argv' | 'write'>): ExitCode {
  const named = optionOf(argv, '--family', USAGE);
  const intoAsked = optionOf(argv, '--into', USAGE) ?? DEFAULT_INTO;
  const into = resolve(root, intoAsked);
  const refusedInto = intoRefusal(root, into);
  if (refusedInto) throw new DaorisError(`--into ${intoAsked} ${refusedInto}; materialize writes only inside it`);
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
    const folder = dirname(file);
    const self = family.layers.flat().find((name) => name.toLowerCase() === member.toLowerCase());
    if (!self) refusals.push(`${member} is no member of the family at ${file}`);
    const members = new Map<string, MemberState>(family.layers.flat()
      .map((name) => [name, name === self ? memberAt(root, name) : memberAt(join(folder, name), name)] as const));
    refusals.push(...judgeFamily(family, members).filter((finding) => finding.verdict === 'refused').map((finding) => finding.message));

    for (const pin of readablePins(family, members).filter((each) => each.consumer === self)) {
      const { producer } = pin;
      const repository = openRepository(join(folder, producer));
      const judged = judgePin(pin, repository);
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
      const missing = files.find((each) => each.mode !== SUBMODULE && !repository.has(each.id));
      if (missing) {
        refusals.push(`${producer} at ${short(commit)}: ${missing.path}'s object is not in ${producer}'s repository (a partial or damaged clone)`);
        continue;
      }
      const lfs = files.some((each) => /(^|\/)\.gitattributes$/.test(each.path) && /\bfilter=lfs\b/.test(repository.blob(each.id)?.toString('utf8') ?? ''));
      const unchanged = marker?.producer === producer && marker.commit === commit && holdsTree(target, files);
      planned.push({ producer, commit, target, repository, files, unchanged, lfs });
    }
  }

  if (refusals.length > 0) {
    for (const message of refusals) write(`  ${'refused'.padEnd(9)} ${message}`);
    write(`nothing materialized: ${plural(refusals.length, 'refused', 'refused')}`);
    return 1;
  }

  for (const { producer, commit, target, repository, files, unchanged, lfs } of planned) {
    if (lfs) write(`  ${'warned'.padEnd(9)} ${producer} at ${short(commit)}: its .gitattributes routes files through LFS; they are written as their pointers`);
    if (unchanged) {
      write(`  ${'unchanged'.padEnd(9)} ${producer} at ${short(commit)} in ${shown(target)}`);
      continue;
    }
    const staged = join(into, `.${producer}.daoris-new`);
    rmSync(staged, { recursive: true, force: true });
    mkdirSync(staged, { recursive: true });
    const submodules: string[] = [];
    for (const entry of files) {
      if (entry.mode === SUBMODULE) {
        submodules.push(entry.path);
        continue;
      }
      const data = repository.blob(entry.id);
      if (!data) throw new DaorisError(`${producer} at ${short(commit)}: ${entry.path}'s object is no blob; nothing of it was swapped in`);
      const path = join(staged, ...entry.path.split('/'));
      mkdirSync(dirname(path), { recursive: true });
      writeFileSync(path, data);
      if (entry.mode === EXECUTABLE && process.platform !== 'win32') chmodSync(path, 0o755);
    }
    writeFileSync(join(staged, PIN_MARKER), `${JSON.stringify({ producer, commit }, null, 2)}\n`);
    rmSync(target, { recursive: true, force: true });
    renameSync(staged, target);
    const count = files.length - submodules.length;
    write(`  ${'wrote'.padEnd(9)} ${producer} at ${short(commit)} into ${shown(target)} (${plural(count, 'file', 'files')})`);
    for (const path of submodules) write(`  ${'skipped'.padEnd(9)} ${producer}'s ${path}: a submodule's commit is another repository's, and none is written`);
  }
  write(`materialized ${plural(planned.length, 'pin', 'pins')} into ${shown(into)}`);
  return 0;
}
