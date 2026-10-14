// A tool's archive, unpacked whole (TOOLS4, D121 §3.6). Git, Node.js, PowerShell, GitHub CLI and Azure CLI
// publish a zip per version for Windows, and the version Daoris keeps is the archive unpacked whole, top folder
// included, so the `exe` a list names is the path as published and there is no strip rule to get wrong.
//
// Only ever opened AFTER its hash and size matched the list's. That makes the bytes the ones the list vouched
// for; it does not make every name inside safe to write, so the rules below hold whoever wrote the archive, and
// each refusal names its check, a code the driver's `ToolInstall` spells too (one table holds both):
//
//   absolute   a name from the root, or on a drive
//   outside    a name with a `..` part, which would land outside the folder it is unpacked into
//   stream     a part holding `:`, which Windows reads as a stream of another file
//   link       a symbolic link: a name that can point anywhere
//   encrypted  an encrypted entry, which nothing here can check
//   method     a compression method other than stored or deflate
//   checksum   an entry whose bytes do not match its CRC-32 or its stated size
//   truncated  an archive that stops before its end record, or an entry that runs past its end
//   damaged    records that do not read as a zip's
//
// No dependency: inflating is `node:zlib`, and a zip is a directory of fixed records at its end. Zip64's records
// are read, since an archive of more than 65,535 entries needs them. Streaming, one entry at a time, because a
// tool's archive is tens of megabytes and an executable inside it as many again.

import { chmodSync, closeSync, createReadStream, createWriteStream, fstatSync, mkdirSync, openSync, readSync } from 'node:fs';
import { join, resolve, sep } from 'node:path';
import { Readable, Transform } from 'node:stream';
import { pipeline } from 'node:stream/promises';
import * as zlib from 'node:zlib';
import { RefusalError } from './errors.ts';

const END_RECORD = 0x06054b50;
const ZIP64_LOCATOR = 0x07064b50;
const ZIP64_END = 0x06064b50;
const DIRECTORY_ENTRY = 0x02014b50;
const LOCAL_ENTRY = 0x04034b50;

/** The end record is 22 bytes and may carry a comment of up to 65,535 after it. */
const END_SEARCH = 22 + 0xffff;

const UNIX_TYPE = 0o170000;
const UNIX_LINK = 0o120000;

/** The CRC-32 a zip states for each entry: Node's own where it has one (22.2 on), else a table. */
const TABLE = (() => {
  const table = new Uint32Array(256);
  for (let n = 0; n < 256; n += 1) {
    let c = n;
    for (let k = 0; k < 8; k += 1) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1;
    table[n] = c >>> 0;
  }
  return table;
})();

export function crc32(chunk: Buffer, previous = 0): number {
  if (typeof zlib.crc32 === 'function') return zlib.crc32(chunk, previous) >>> 0;
  let c = (previous ^ 0xffffffff) >>> 0;
  for (const byte of chunk) c = TABLE[(c ^ byte) & 0xff]! ^ (c >>> 8);
  return (c ^ 0xffffffff) >>> 0;
}

/** One entry of the central directory, as the archive states it. */
interface Entry {
  name: string;
  flags: number;
  method: number;
  crc: number;
  compressed: number;
  size: number;
  offset: number;
  mode: number;
}

/**
 * Unpack a zip into `into`, creating it. @returns the files written, relative, `/`-separated.
 *
 * @throws RefusalError naming its check, when an entry would land outside `into`, is a link, is encrypted or
 * compressed another way, fails its CRC, or when the archive is truncated or damaged.
 */
export async function extractZip(file: string, into: string): Promise<string[]> {
  const root = resolve(into);
  mkdirSync(root, { recursive: true });
  const fd = openSync(file, 'r');
  const written: string[] = [];
  try {
    const size = fstatSync(fd).size;
    for (const entry of directory(fd, size)) {
      const target = placed(entry.name, root);
      if ((entry.mode & UNIX_TYPE) === UNIX_LINK) {
        throw new RefusalError('link', `the archive holds a symbolic link at \`${entry.name}\`, and Daoris creates no link from `
          + 'an archive — a link is a name that can point anywhere');
      }
      if (entry.flags & 1) {
        throw new RefusalError('encrypted', `the archive holds \`${entry.name}\` encrypted, and an entry nothing can check is not unpacked`);
      }
      if (entry.method !== 0 && entry.method !== 8) {
        throw new RefusalError('method', `the archive holds \`${entry.name}\` compressed by method ${entry.method}, and only stored `
          + 'and deflate are unpacked');
      }
      if (target === null) continue;

      const start = dataStart(fd, size, entry);
      if (/[\\/]$/.test(entry.name)) {
        mkdirSync(target.path, { recursive: true });
        continue;
      }

      mkdirSync(resolve(target.path, '..'), { recursive: true });
      await unpackEntry(file, entry, start, target.path);
      if (process.platform !== 'win32' && entry.mode !== 0) chmodSync(target.path, entry.mode & 0o111 ? 0o755 : 0o644);
      written.push(target.relative);
    }
  } finally {
    closeSync(fd);
  }
  return written;
}

/** The central directory's entries, in order, from the end record (and zip64's, where it says so). */
function directory(fd: number, size: number): Entry[] {
  const tailLength = Math.min(size, END_SEARCH);
  const tail = read(fd, size - tailLength, tailLength);

  let at = -1;
  for (let i = tail.length - 22; i >= 0; i -= 1) {
    if (tail.readUInt32LE(i) === END_RECORD && i + 22 + tail.readUInt16LE(i + 20) === tail.length) {
      at = i;
      break;
    }
  }
  if (at < 0) {
    throw new RefusalError('truncated', 'the archive has no end record — it is truncated or damaged, and a partial archive is not '
      + 'unpacked as if it were whole');
  }

  const endAt = size - tailLength + at;
  if (tail.readUInt16LE(at + 4) !== 0 || tail.readUInt16LE(at + 6) !== 0) {
    throw new RefusalError('damaged', 'the archive is split across disks, which a tool\'s archive never is');
  }
  let count = tail.readUInt16LE(at + 10);
  let length = tail.readUInt32LE(at + 12);
  let offset = tail.readUInt32LE(at + 16);
  let limit = endAt;

  if (count === 0xffff || length === 0xffffffff || offset === 0xffffffff) {
    const locatorAt = endAt - 20;
    if (locatorAt < 0 || read(fd, locatorAt, 4).readUInt32LE(0) !== ZIP64_LOCATOR) {
      throw new RefusalError('damaged', 'the archive\'s end record points to a zip64 record it does not carry');
    }
    const zip64At = whole(read(fd, locatorAt + 8, 8).readBigUInt64LE(0));
    if (zip64At + 56 > locatorAt) throw new RefusalError('truncated', 'the archive\'s zip64 record runs past where it ends');
    const record = read(fd, zip64At, 56);
    if (record.readUInt32LE(0) !== ZIP64_END) throw new RefusalError('damaged', 'the archive\'s zip64 record is not one');
    count = whole(record.readBigUInt64LE(32));
    length = whole(record.readBigUInt64LE(40));
    offset = whole(record.readBigUInt64LE(48));
    limit = zip64At;
  }

  if (offset + length > limit) {
    throw new RefusalError('truncated', 'the archive\'s directory runs past where it ends — it is truncated or damaged');
  }

  const records = read(fd, offset, length);
  const entries: Entry[] = [];
  let p = 0;
  for (let n = 0; n < count; n += 1) {
    if (p + 46 > records.length || records.readUInt32LE(p) !== DIRECTORY_ENTRY) {
      throw new RefusalError('damaged', 'the archive\'s directory holds a record that is not an entry');
    }
    const nameLength = records.readUInt16LE(p + 28);
    const extraLength = records.readUInt16LE(p + 30);
    const commentLength = records.readUInt16LE(p + 32);
    if (p + 46 + nameLength + extraLength + commentLength > records.length) {
      throw new RefusalError('damaged', 'the archive\'s directory holds an entry that runs past it');
    }

    const entry: Entry = {
      name: records.toString('utf8', p + 46, p + 46 + nameLength),
      flags: records.readUInt16LE(p + 8),
      method: records.readUInt16LE(p + 10),
      crc: records.readUInt32LE(p + 16),
      compressed: records.readUInt32LE(p + 20),
      size: records.readUInt32LE(p + 24),
      offset: records.readUInt32LE(p + 42),
      mode: records.readUInt32LE(p + 38) >>> 16,
    };
    zip64Fields(records.subarray(p + 46 + nameLength, p + 46 + nameLength + extraLength), entry);
    entries.push(entry);
    p += 46 + nameLength + extraLength + commentLength;
  }
  return entries;
}

/** Zip64's extended information (0x0001): the fields its directory entry left at their limit, in order. */
function zip64Fields(extra: Buffer, entry: Entry): void {
  let p = 0;
  while (p + 4 <= extra.length) {
    const id = extra.readUInt16LE(p);
    const length = extra.readUInt16LE(p + 2);
    if (id === 0x0001) {
      let q = p + 4;
      const next = (): number => {
        if (q + 8 > p + 4 + length) throw new RefusalError('damaged', `the archive's zip64 fields for \`${entry.name}\` are short`);
        const value = whole(extra.readBigUInt64LE(q));
        q += 8;
        return value;
      };
      if (entry.size === 0xffffffff) entry.size = next();
      if (entry.compressed === 0xffffffff) entry.compressed = next();
      if (entry.offset === 0xffffffff) entry.offset = next();
      return;
    }
    p += 4 + length;
  }
}

/** Where an entry's bytes begin: after its local record, whose name and extra may differ from the directory's. */
function dataStart(fd: number, size: number, entry: Entry): number {
  if (entry.offset + 30 > size) throw new RefusalError('truncated', `the archive ends before \`${entry.name}\` does`);
  const local = read(fd, entry.offset, 30);
  if (local.readUInt32LE(0) !== LOCAL_ENTRY) {
    throw new RefusalError('damaged', `the archive's record for \`${entry.name}\` is not where its directory says`);
  }
  const start = entry.offset + 30 + local.readUInt16LE(26) + local.readUInt16LE(28);
  if (start + entry.compressed > size) throw new RefusalError('truncated', `the archive ends before \`${entry.name}\` does`);
  return start;
}

/** One entry's bytes to `to`, inflated where they are deflated, held to the CRC-32 and size its directory states. */
async function unpackEntry(file: string, entry: Entry, start: number, to: string): Promise<void> {
  let crc = 0;
  let length = 0;
  const count = new Transform({
    transform(chunk: Buffer, _encoding, done) {
      crc = crc32(chunk, crc);
      length += chunk.length;
      done(null, chunk);
    },
  });

  const source = entry.compressed === 0
    ? Readable.from([])
    : createReadStream(file, { start, end: start + entry.compressed - 1 });
  try {
    await (entry.method === 8
      ? pipeline(source, zlib.createInflateRaw(), count, createWriteStream(to))
      : pipeline(source, count, createWriteStream(to)));
  } catch (error) {
    const code = (error as NodeJS.ErrnoException).code ?? '';
    if (code.startsWith('Z_')) {
      throw new RefusalError('damaged', `the archive's \`${entry.name}\` did not inflate (${(error as Error).message})`);
    }
    throw error;
  }

  if (length !== entry.size || crc !== entry.crc) {
    throw new RefusalError('checksum', `the archive's \`${entry.name}\` fails its own check — ${length} bytes with CRC-32 `
      + `${hex(crc)}, where it states ${entry.size} with ${hex(entry.crc)}`);
  }
}

/**
 * Where an entry lands, or null for the archive's own root; refused when that is outside. The same rule
 * `tarball.ts` holds, and the driver's `ToolInstall.Placed`.
 */
export function placed(name: string, root: string): { path: string; relative: string } | null {
  if (/^[\\/]/.test(name) || /^[A-Za-z]:/.test(name)) {
    throw new RefusalError('absolute', `the archive names \`${name}\`, an absolute path — an entry lands inside the folder it is `
      + 'unpacked into or nowhere');
  }

  const segments = name.split(/[\\/]+/).filter((segment) => segment !== '' && segment !== '.');
  if (segments.includes('..')) {
    throw new RefusalError('outside', `the archive names \`${name}\`, which would land outside the folder it is unpacked into`);
  }
  if (segments.some((segment) => segment.includes(':'))) {
    throw new RefusalError('stream', `the archive names \`${name}\`, which Windows would read as a stream of another file`);
  }
  if (segments.length === 0) return null;

  const path = join(root, ...segments);
  if (!path.startsWith(root + sep)) {
    throw new RefusalError('outside', `the archive names \`${name}\`, which would land outside the folder it is unpacked into`);
  }
  return { path, relative: segments.join('/') };
}

function read(fd: number, position: number, length: number): Buffer {
  const buffer = Buffer.alloc(length);
  let done = 0;
  while (done < length) {
    const got = readSync(fd, buffer, done, length - done, position + done);
    if (got === 0) throw new RefusalError('truncated', 'the archive ends before a record it names');
    done += got;
  }
  return buffer;
}

/** A 64-bit field as a number, refused where a number cannot hold it exactly. */
function whole(value: bigint): number {
  if (value > BigInt(Number.MAX_SAFE_INTEGER)) throw new RefusalError('damaged', 'the archive names an offset no file has');
  return Number(value);
}

function hex(value: number): string {
  return value.toString(16).padStart(8, '0');
}
