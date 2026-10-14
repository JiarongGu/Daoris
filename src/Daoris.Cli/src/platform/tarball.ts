// A vendor's package, unpacked whole (AGT2b). Codex ships as a gzipped tar whose executable finds its
// helpers through the package's own layout, so the bare executable alone is not the tool — the whole
// archive is.
//
// Only ever opened AFTER its hash matched the vendor's published one. That makes the bytes the
// vendor's; it does not make every name inside safe to write, so the rules below hold for whoever
// wrote the archive: nothing lands outside the directory given, no link is created, and an archive
// that stops early is refused rather than unpacked in part. Streaming, because a package is tens of
// megabytes and there is no reason to hold it twice.
//
// No dependency: gzip is `node:zlib`, and tar is a 512-byte header format with three ways of
// spelling a long name.
//
// A tool's `tar.gz` is unpacked here too (TOOLS4, D121 §3.6), so each refusal names its check — the code
// `zipfile.ts` and the driver's `ToolInstall` spell, held by one table: absolute, outside, stream, link,
// entry (neither a file nor a folder), checksum, truncated and damaged.

import { chmodSync, closeSync, createReadStream, mkdirSync, openSync, writeSync } from 'node:fs';
import { join, resolve, sep } from 'node:path';
import { createGunzip } from 'node:zlib';
import { DaorisError, RefusalError } from './errors.ts';

const BLOCK = 512;

/** The most a name-carrying entry may hold. A real one is a path; a larger one is not a name. */
const METADATA_LIMIT = 64 * 1024;

type State =
  | { kind: 'header' }
  | { kind: 'file'; remaining: number; padding: number; fd: number }
  | { kind: 'metadata'; type: string; remaining: number; padding: number; chunks: Buffer[] }
  | { kind: 'skip'; remaining: number }
  | { kind: 'ended' };

/**
 * Unpack a `.tar.gz` into `into`, creating it. @returns the files written, relative, `/`-separated.
 *
 * @throws DaorisError naming the entry, when one would land outside `into`, is a link or a device,
 * or when the archive is damaged or ends early.
 */
export async function extractTarGz(file: string, into: string): Promise<string[]> {
  const root = resolve(into);
  mkdirSync(root, { recursive: true });

  const written: string[] = [];
  let pending: Buffer = Buffer.alloc(0);
  // Asserted rather than annotated: the steps below move it from inside a closure, which the
  // compiler cannot see, so an annotated start would be narrowed to `header` for good.
  let state = { kind: 'header' } as State;
  // What an extended header said about the entry that follows it.
  let next: { path?: string; size?: number } = {};

  const stream = createReadStream(file).pipe(createGunzip());
  try {
    for await (const chunk of stream as AsyncIterable<Buffer>) {
      pending = pending.length === 0 ? chunk : Buffer.concat([pending, chunk]);
      pending = consume(pending);
    }
  } catch (error) {
    if (state.kind === 'file') closeSync(state.fd);
    if (error instanceof DaorisError) throw error;
    // zlib's own word for a stream that stops before its trailer; anything else did not decompress at all.
    const cut = (error as NodeJS.ErrnoException).code === 'Z_BUF_ERROR';
    throw new RefusalError(cut ? 'truncated' : 'damaged', cut
      ? `the package ends before its gzip trailer does — it is truncated (${(error as Error).message})`
      : `the package is damaged — it did not decompress (${(error as Error).message})`);
  }

  if (state.kind === 'file') closeSync(state.fd);
  if (state.kind !== 'ended') {
    throw new RefusalError('truncated', 'the package ends before its last entry does — it is truncated or damaged, '
      + 'and a partial package is not unpacked as if it were whole');
  }

  return written;

  /** Take everything the buffer holds a whole step for, and return what is left over. */
  function consume(buffer: Buffer): Buffer {
    let at = 0;
    for (;;) {
      if (state.kind === 'ended') return Buffer.alloc(0);

      if (state.kind === 'header') {
        if (buffer.length - at < BLOCK) break;
        const header = buffer.subarray(at, at + BLOCK);
        at += BLOCK;
        state = begin(header);
        continue;
      }

      if (state.kind === 'skip') {
        const take = Math.min(state.remaining, buffer.length - at);
        at += take;
        state.remaining -= take;
        if (state.remaining > 0) break;
        state = { kind: 'header' };
        continue;
      }

      const take = Math.min(state.remaining, buffer.length - at);
      if (state.kind === 'file') writeSync(state.fd, buffer, at, take);
      else state.chunks.push(Buffer.from(buffer.subarray(at, at + take)));
      at += take;
      state.remaining -= take;
      if (state.remaining > 0) break;

      if (state.kind === 'file') closeSync(state.fd);
      else absorb(state.type, Buffer.concat(state.chunks));
      state = state.padding > 0 ? { kind: 'skip', remaining: state.padding } : { kind: 'header' };
    }

    return buffer.subarray(at);
  }

  /** Read one header and decide what its entry is. */
  function begin(header: Buffer): State {
    if (header.every((byte) => byte === 0)) return { kind: 'ended' };
    checksum(header);

    const type = String.fromCharCode(header[156]!) || '0';
    const size = next.size ?? sizeOf(header);
    const padding = (BLOCK - (size % BLOCK)) % BLOCK;
    const raw = next.path ?? nameOf(header);
    next = {};

    switch (type) {
      case 'x': case 'L': case 'K': case 'g':
        if (size > METADATA_LIMIT) throw new RefusalError('damaged', `the package holds a ${size}-byte name, which is not a name`);
        return { kind: 'metadata', type, remaining: size, padding, chunks: [] };

      case '0': case '\0': case '7': {
        const target = placed(raw);
        if (target === null) return { kind: 'skip', remaining: size + padding };
        mkdirSync(resolve(target.path, '..'), { recursive: true });
        const fd = openSync(target.path, 'w');
        if (process.platform !== 'win32') {
          chmodSync(target.path, (octalOf(header, 100, 8) & 0o111) ? 0o755 : 0o644);
        }
        written.push(target.relative);
        if (size === 0) {
          closeSync(fd);
          return padding > 0 ? { kind: 'skip', remaining: padding } : { kind: 'header' };
        }
        return { kind: 'file', remaining: size, padding, fd };
      }

      case '5': {
        const target = placed(raw);
        if (target) mkdirSync(target.path, { recursive: true });
        return { kind: 'skip', remaining: size + padding };
      }

      case '1': case '2':
        throw new RefusalError(
          'link',
          `the package holds a ${type === '2' ? 'symbolic' : 'hard'} link at \`${raw}\`, and Daoris `
          + 'creates no link from an archive — a link is a name that can point anywhere');

      default:
        throw new RefusalError('entry', `the package holds \`${raw}\` as an entry of type '${type}', which is not a file or a directory`);
    }
  }

  /** An extended header's body: the name, and possibly the size, of the entry after it. */
  function absorb(type: string, body: Buffer): void {
    if (type === 'L') {
      next.path = body.toString('utf8').replace(/\0+$/, '');
      return;
    }
    if (type !== 'x') return; // A link target (`K`) or a global header (`g`) says nothing we use.

    let at = 0;
    const text = body.toString('utf8');
    while (at < text.length) {
      const space = text.indexOf(' ', at);
      const length = Number(text.slice(at, space));
      if (!Number.isInteger(length) || length <= 0) throw new RefusalError('damaged', 'the package holds an unreadable extended header');
      const record = text.slice(space + 1, at + length - 1);
      const equals = record.indexOf('=');
      const key = record.slice(0, equals);
      const value = record.slice(equals + 1);
      if (key === 'path') next.path = value;
      if (key === 'size') next.size = Number(value);
      at += length;
    }
  }

  /** Where an entry lands, or null for the archive's own root; refused when that is outside. */
  function placed(name: string): { path: string; relative: string } | null {
    if (/^[\\/]/.test(name) || /^[A-Za-z]:/.test(name)) {
      throw new RefusalError('absolute', `the package names \`${name}\`, an absolute path — an entry lands inside the directory it is unpacked into or nowhere`);
    }

    const segments = name.split(/[\\/]+/).filter((segment) => segment !== '' && segment !== '.');
    if (segments.includes('..')) {
      throw new RefusalError('outside', `the package names \`${name}\`, which would land outside the directory it is unpacked into`);
    }
    if (segments.some((segment) => segment.includes(':'))) {
      throw new RefusalError('stream', `the package names \`${name}\`, which Windows would read as a stream of another file`);
    }
    if (segments.length === 0) return null;

    const path = join(root, ...segments);
    if (!path.startsWith(root + sep)) {
      throw new RefusalError('outside', `the package names \`${name}\`, which would land outside the directory it is unpacked into`);
    }

    return { path, relative: segments.join('/') };
  }
}

function checksum(header: Buffer): void {
  const stated = octalOf(header, 148, 8);
  let sum = 0;
  for (let at = 0; at < BLOCK; at++) sum += at >= 148 && at < 156 ? 0x20 : header[at]!;
  if (sum !== stated) {
    throw new RefusalError('checksum', 'the package holds a header that fails its own checksum — it is damaged, and a tarball is not guessed at');
  }
}

function nameOf(header: Buffer): string {
  const name = text(header, 0, 100);
  // ustar splits a long name across a prefix and the name field.
  const prefix = header.subarray(257, 262).toString('latin1') === 'ustar' ? text(header, 345, 155) : '';
  return prefix ? `${prefix}/${name}` : name;
}

function sizeOf(header: Buffer): number {
  // GNU's base-256 form, for sizes the octal field cannot hold.
  if (header[124]! & 0x80) {
    let value = 0;
    for (let at = 125; at < 136; at++) value = value * 256 + header[at]!;
    return value;
  }
  return octalOf(header, 124, 12);
}

function octalOf(header: Buffer, at: number, length: number): number {
  const digits = text(header, at, length).trim();
  return digits ? parseInt(digits, 8) : 0;
}

function text(header: Buffer, at: number, length: number): string {
  const field = header.subarray(at, at + length);
  const end = field.indexOf(0);
  return field.subarray(0, end < 0 ? length : end).toString('utf8');
}
