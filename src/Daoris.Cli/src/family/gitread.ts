// A producer's refs and commits, read from git's own files.
//
// The family check needs no service and no runtime dependency, and the CLI starts one program only, the desktop's (`dogfood.test.ts`
// holds both), so a pin is read here without starting git: the files backend's refs (loose, then `packed-refs`), loose objects and
// version-2 pack indexes with their packs, deltas included, and the alternates a clone may borrow objects from. It reads; it never
// writes. A reftable store is refused by name rather than read as empty.

import { closeSync, existsSync, openSync, readdirSync, readFileSync, readSync, statSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { inflateSync } from 'node:zlib';
import { DaorisError } from '../platform/errors.ts';

export interface Repository {
  /** The id a ref names, through its symbolic refs; null where it names none. */
  ref(name: string): string | null;
  /** The ref a symbolic ref points at (`refs/remotes/origin/HEAD` → `refs/remotes/origin/main`); null where it is none. */
  symbolic(name: string): string | null;
  /** The commit an id is, its annotated tags peeled; null where the repository holds no such object or it is no commit. */
  commitOf(id: string): string | null;
  /** Whether a commit is one of the tips or reached from them by its parents. */
  isAncestor(id: string, tips: readonly string[]): boolean;
  /** True where the history is cut (a shallow clone), so a commit not reached may still be on the line. */
  readonly shallow: boolean;
  /** How many hex digits a full id has here: 40, or 64 in a SHA-256 repository. */
  readonly idLength: number;
}

interface GitObject {
  type: string;
  data: Buffer;
}

const TYPES: Record<number, string> = { 1: 'commit', 2: 'tree', 3: 'blob', 4: 'tag' };
const OFS_DELTA = 6;
const REF_DELTA = 7;

/** The repository whose `.git` (a folder, or a linked worktree's file) stands at `root`; null where none does. Never walks up. */
export function openRepository(root: string): Repository | null {
  const dotGit = join(root, '.git');
  if (!existsSync(dotGit)) return null;
  let gitDir = dotGit;
  if (statSync(dotGit).isFile()) {
    const pointer = /^gitdir:\s*(.+)$/m.exec(readFileSync(dotGit, 'utf8'));
    if (!pointer) return null;
    gitDir = resolve(root, pointer[1]!.trim());
  }
  const commonFile = join(gitDir, 'commondir');
  const commonDir = existsSync(commonFile) ? resolve(gitDir, readFileSync(commonFile, 'utf8').trim()) : gitDir;
  if (existsSync(join(commonDir, 'reftable'))) {
    throw new DaorisError(`${root} keeps its refs in a reftable, which the family check does not read; its pins cannot be judged here`);
  }
  const config = existsSync(join(commonDir, 'config')) ? readFileSync(join(commonDir, 'config'), 'utf8') : '';
  const hashBytes = /^\s*objectformat\s*=\s*sha256\s*$/im.test(config) ? 32 : 20;
  return new FileRepository(gitDir, commonDir, hashBytes);
}

class FileRepository implements Repository {
  readonly idLength: number;
  readonly shallow: boolean;
  private packed: Map<string, string> | null = null;
  private readonly stores: ObjectStore[];
  private readonly gitDir: string;
  private readonly commonDir: string;

  constructor(gitDir: string, commonDir: string, hashBytes: number) {
    this.gitDir = gitDir;
    this.commonDir = commonDir;
    this.idLength = hashBytes * 2;
    this.shallow = existsSync(join(commonDir, 'shallow'));
    this.stores = objectDirs(join(commonDir, 'objects')).map((dir) => new ObjectStore(dir, hashBytes));
  }

  ref(name: string): string | null {
    // A pin's text is asked as a branch and a tag, so a name is read only where git could have written it: never out of the refs.
    if (name !== 'HEAD' && !/^refs\/(?:[A-Za-z0-9._-]+\/)*[A-Za-z0-9._-]+$/.test(name)) return null;
    if (name.split('/').some((part) => part === '..' || part === '.')) return null;
    let current = name;
    for (let depth = 0; depth < 10; depth++) {
      const loose = this.loose(current);
      if (loose === null) return this.packedRefs().get(current) ?? null;
      if (!loose.startsWith('ref:')) return loose.toLowerCase();
      current = loose.slice(4).trim();
    }
    return null;
  }

  symbolic(name: string): string | null {
    const loose = this.loose(name);
    return loose?.startsWith('ref:') ? loose.slice(4).trim() : null;
  }

  commitOf(id: string): string | null {
    let current = id.toLowerCase();
    for (let depth = 0; depth < 10; depth++) {
      const object = this.read(current);
      if (!object) return null;
      if (object.type === 'commit') return current;
      if (object.type !== 'tag') return null;
      const target = /^object ([0-9a-f]+)$/m.exec(object.data.toString('utf8'));
      if (!target) return null;
      current = target[1]!;
    }
    return null;
  }

  isAncestor(id: string, tips: readonly string[]): boolean {
    const wanted = id.toLowerCase();
    const seen = new Set<string>();
    const queue = tips.map((tip) => this.commitOf(tip)).filter((tip): tip is string => tip !== null);
    while (queue.length > 0) {
      const next = queue.pop()!;
      if (next === wanted) return true;
      if (seen.has(next)) continue;
      seen.add(next);
      const object = this.read(next);
      if (!object || object.type !== 'commit') continue;
      for (const parent of parentsOf(object.data)) if (!seen.has(parent)) queue.push(parent);
    }
    return false;
  }

  /** A ref's loose file: per-worktree refs from the tree's own folder, every other from the common one. */
  private loose(name: string): string | null {
    const own = name === 'HEAD' || name.startsWith('refs/worktree/') || name.startsWith('refs/bisect/');
    const file = join(own ? this.gitDir : this.commonDir, ...name.split('/'));
    if (!existsSync(file) || !statSync(file).isFile()) return null;
    return readFileSync(file, 'utf8').trim();
  }

  private packedRefs(): Map<string, string> {
    if (this.packed) return this.packed;
    this.packed = new Map();
    const file = join(this.commonDir, 'packed-refs');
    if (!existsSync(file)) return this.packed;
    for (const line of readFileSync(file, 'utf8').split(/\r?\n/)) {
      const entry = /^([0-9a-f]+) (\S+)$/i.exec(line);
      if (entry) this.packed.set(entry[2]!, entry[1]!.toLowerCase());
    }
    return this.packed;
  }

  private read(id: string): GitObject | null {
    for (const store of this.stores) {
      const object = store.read(id, (base) => this.read(base));
      if (object) return object;
    }
    return null;
  }
}

/** The repository's object folder and those its alternates name, each once. */
function objectDirs(primary: string): string[] {
  const dirs: string[] = [];
  const add = (dir: string) => {
    if (dirs.includes(dir) || !existsSync(dir)) return;
    dirs.push(dir);
    const alternates = join(dir, 'info', 'alternates');
    if (!existsSync(alternates)) return;
    for (const line of readFileSync(alternates, 'utf8').split(/\r?\n/)) {
      const path = line.trim();
      if (path.length > 0 && !path.startsWith('#')) add(resolve(dir, path));
    }
  };
  add(primary);
  return dirs;
}

function parentsOf(data: Buffer): string[] {
  const end = data.indexOf('\n\n');
  const header = data.toString('utf8', 0, end < 0 ? data.length : end);
  return [...header.matchAll(/^parent ([0-9a-f]+)$/gm)].map((match) => match[1]!);
}

/** One object folder: its loose objects, and its packs read through their version-2 indexes. */
class ObjectStore {
  private packs: Pack[] | null = null;
  private readonly dir: string;
  private readonly hashBytes: number;

  constructor(dir: string, hashBytes: number) {
    this.dir = dir;
    this.hashBytes = hashBytes;
  }

  read(id: string, readAnywhere: (id: string) => GitObject | null): GitObject | null {
    const loose = join(this.dir, id.slice(0, 2), id.slice(2));
    if (existsSync(loose)) {
      const raw = inflateSync(readFileSync(loose));
      const nul = raw.indexOf(0);
      return { type: raw.toString('latin1', 0, nul).split(' ')[0]!, data: raw.subarray(nul + 1) };
    }
    for (const pack of this.allPacks()) {
      const offset = pack.offsetOf(id);
      if (offset !== null) return pack.readAt(offset, readAnywhere);
    }
    return null;
  }

  private allPacks(): Pack[] {
    if (this.packs) return this.packs;
    const folder = join(this.dir, 'pack');
    this.packs = existsSync(folder)
      ? readdirSync(folder).filter((name) => name.endsWith('.idx') && existsSync(join(folder, `${name.slice(0, -4)}.pack`)))
        .map((name) => new Pack(join(folder, name.slice(0, -4)), this.hashBytes))
      : [];
    return this.packs;
  }
}

class Pack {
  private readonly index: Buffer;
  private readonly count: number;
  private readonly offsetsAt: number;
  private sorted: number[] | null = null;
  private readonly size: number;
  private readonly base: string;
  private readonly hashBytes: number;

  constructor(base: string, hashBytes: number) {
    this.base = base;
    this.hashBytes = hashBytes;
    this.index = readFileSync(`${base}.idx`);
    if (this.index.readUInt32BE(0) !== 0xff744f63 || this.index.readUInt32BE(4) !== 2) {
      throw new DaorisError(`${base}.idx is not a version-2 pack index, which is the one the family check reads`);
    }
    this.count = this.index.readUInt32BE(8 + 255 * 4);
    this.offsetsAt = 8 + 256 * 4 + this.count * hashBytes + this.count * 4;
    this.size = statSync(`${base}.pack`).size;
  }

  offsetOf(id: string): number | null {
    const wanted = Buffer.from(id, 'hex');
    if (wanted.length !== this.hashBytes) return null;
    const first = wanted[0]!;
    let low = first === 0 ? 0 : this.index.readUInt32BE(8 + (first - 1) * 4);
    let high = this.index.readUInt32BE(8 + first * 4);
    const idsAt = 8 + 256 * 4;
    while (low < high) {
      const middle = (low + high) >>> 1;
      const at = idsAt + middle * this.hashBytes;
      const order = Buffer.compare(this.index.subarray(at, at + this.hashBytes), wanted);
      if (order === 0) return this.offsetAt(middle);
      if (order < 0) low = middle + 1;
      else high = middle;
    }
    return null;
  }

  private offsetAt(position: number): number {
    const small = this.index.readUInt32BE(this.offsetsAt + position * 4);
    if ((small & 0x80000000) === 0) return small;
    const large = this.offsetsAt + this.count * 4 + (small & 0x7fffffff) * 8;
    return Number(this.index.readBigUInt64BE(large));
  }

  /** Where the entry at `offset` ends: the next entry's start, or the pack's trailer. */
  private endOf(offset: number): number {
    if (!this.sorted) {
      this.sorted = Array.from({ length: this.count }, (_, position) => this.offsetAt(position)).sort((a, b) => a - b);
    }
    let low = 0;
    let high = this.sorted.length;
    while (low < high) {
      const middle = (low + high) >>> 1;
      if (this.sorted[middle]! <= offset) low = middle + 1;
      else high = middle;
    }
    return low < this.sorted.length ? this.sorted[low]! : this.size - this.hashBytes;
  }

  readAt(offset: number, readAnywhere: (id: string) => GitObject | null): GitObject | null {
    const entry = Buffer.alloc(this.endOf(offset) - offset);
    const fd = openSync(`${this.base}.pack`, 'r');
    try {
      readSync(fd, entry, 0, entry.length, offset);
    } finally {
      closeSync(fd);
    }
    let at = 0;
    let byte = entry[at++]!;
    const type = (byte >> 4) & 7;
    while (byte & 0x80) byte = entry[at++]!;

    if (TYPES[type]) return { type: TYPES[type]!, data: inflateSync(entry.subarray(at)) };

    let base: GitObject | null;
    if (type === OFS_DELTA) {
      byte = entry[at++]!;
      let distance = byte & 0x7f;
      while (byte & 0x80) {
        byte = entry[at++]!;
        distance = ((distance + 1) * 128) + (byte & 0x7f);
      }
      base = this.readAt(offset - distance, readAnywhere);
    } else if (type === REF_DELTA) {
      base = readAnywhere(entry.toString('hex', at, at + this.hashBytes));
      at += this.hashBytes;
    } else {
      return null;
    }
    return base ? { type: base.type, data: applyDelta(base.data, inflateSync(entry.subarray(at))) } : null;
  }
}

/** A delta's copies and inserts applied to its base. */
function applyDelta(base: Buffer, delta: Buffer): Buffer {
  let at = 0;
  const size = (): number => {
    let value = 0;
    let shift = 0;
    let byte: number;
    do {
      byte = delta[at++]!;
      value += (byte & 0x7f) * 2 ** shift;
      shift += 7;
    } while (byte & 0x80);
    return value;
  };
  size();
  const out = Buffer.alloc(size());
  let written = 0;
  while (at < delta.length) {
    const op = delta[at++]!;
    if (op & 0x80) {
      let from = 0;
      let length = 0;
      for (let bit = 0; bit < 4; bit++) if (op & (1 << bit)) from += delta[at++]! * 2 ** (8 * bit);
      for (let bit = 0; bit < 3; bit++) if (op & (1 << (4 + bit))) length += delta[at++]! * 2 ** (8 * bit);
      if (length === 0) length = 0x10000;
      base.copy(out, written, from, from + length);
      written += length;
    } else if (op > 0) {
      delta.copy(out, written, at, at + op);
      written += op;
      at += op;
    } else {
      throw new DaorisError('a pack holds a delta with an instruction git never writes; the pack cannot be read');
    }
  }
  return out;
}
