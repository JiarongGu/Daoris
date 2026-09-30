import { createHash } from 'node:crypto';
import { existsSync, mkdirSync, readFileSync, readdirSync, renameSync, writeFileSync } from 'node:fs';
import { dirname, isAbsolute, join } from 'node:path';
import { DaorisError } from './errors.ts';

/**
 * BOM-less, LF. Every comparison in this tool happens on normalized text, so a
 * checkout that converted line endings is not mistaken for a local edit.
 */
export function normalize(text: string): string {
  return text.replace(/^﻿/, '').replace(/\r\n/g, '\n');
}

export function readText(file: string): string {
  return normalize(readFileSync(file, 'utf8'));
}

/**
 * Write beside the target, then rename — a crash never leaves a half-written
 * rule on disk. Node writes UTF-8 without a BOM, and the text is already LF.
 */
export function writeTextAtomic(file: string, text: string): void {
  mkdirSync(dirname(file), { recursive: true });
  const tmp = `${file}.daoris-tmp`;
  writeFileSync(tmp, text, 'utf8');
  renameSync(tmp, file);
}

/**
 * A JSON object out of one of the home's files, which a C# twin reads and edits too: absent is a null
 * value, a BOM is stripped because `File.ReadAllText` strips it on the other side, and anything that is
 * not a JSON object names why rather than reading as empty.
 */
export function readJsonObject(path: string): { value: Record<string, unknown> | null; problem: string | null } {
  if (!existsSync(path)) return { value: null, problem: null };
  let parsed: unknown;
  try {
    parsed = JSON.parse(readText(path));
  } catch (error) {
    return { value: null, problem: `${path} is not readable JSON (${(error as Error).message})` };
  }
  if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) {
    return { value: null, problem: `${path} is not a JSON object` };
  }
  return { value: parsed as Record<string, unknown>, problem: null };
}

/**
 * Write an edited home file — refused while the file on disk is one the editor could not have read.
 *
 * @remarks
 * 🔴 The choke point for every editor (REV3): each reader answers an unreadable file with the empty
 * default, which is the right reading for a session about to spawn and the wrong starting point for an
 * edit. Writing it back erased every deny rule, every other workspace's key and every disabled plugin,
 * and reported success. Checking here holds every editor, including the next one.
 */
export function writeJsonAtomic(path: string, value: unknown): void {
  const { problem } = readJsonObject(path);
  if (problem !== null) {
    throw new DaorisError(`${problem}. Fix it, or delete it to start from nothing — `
      + 'this command will not overwrite a file it could not understand.');
  }
  writeTextAtomic(path, `${JSON.stringify(value, null, 2)}\n`);
}

export function sha256(text: string): string {
  return createHash('sha256').update(normalize(text), 'utf8').digest('hex');
}

/**
 * A file's hash as the lock records it: text hashed as `sha256` hashes it, so a checkout that converted
 * line endings is not an edit, and a binary file (a NUL in it, as git decides) by its exact bytes, which
 * a text decoding would blur.
 */
export function digestBytes(bytes: Buffer): string {
  return bytes.includes(0)
    ? createHash('sha256').update(bytes).digest('hex')
    : sha256(bytes.toString('utf8'));
}

/** `writeTextAtomic` for bytes copied as they are: a mirror of a file that is not its skill's entry (D117 §3.2). */
export function writeBytesAtomic(file: string, bytes: Buffer): void {
  mkdirSync(dirname(file), { recursive: true });
  const tmp = `${file}.daoris-tmp`;
  writeFileSync(tmp, bytes);
  renameSync(tmp, file);
}

/** Sorted, '/'-separated, recursive. An absent directory yields []. */
export function listFiles(dir: string, keep: (name: string) => boolean = () => true): string[] {
  if (!existsSync(dir)) return [];
  const out: string[] = [];
  for (const entry of readdirSync(dir, { withFileTypes: true })) {
    if (entry.isDirectory()) {
      out.push(...listFiles(join(dir, entry.name), keep).map((path) => `${entry.name}/${path}`));
    } else if (keep(entry.name)) {
      out.push(entry.name);
    }
  }
  return out.sort();
}

export const listMarkdown = (dir: string): string[] => listFiles(dir, (name) => name.endsWith('.md'));

/**
 * The file a command names: itself when it is a path, else the first match on `PATH` with Windows's
 * extensions tried, or null where there is none. The CLI's half of the driver's
 * `CommandPresence.Resolve`, with the same defaults.
 *
 * @param startable Only what Windows can START. npm puts an extensionless POSIX script beside
 * `npm.cmd`, and the bare name found first is a file no Windows process can run.
 */
export function onPath(
  command: string,
  { env = process.env, startable = false }: { env?: Record<string, string | undefined>; startable?: boolean } = {},
): string | null {
  if (!command.trim()) return null;
  if (isAbsolute(command) || command.includes('/') || command.includes('\\')) {
    return existsSync(command) ? command : null;
  }

  const windows = process.platform === 'win32';
  const pathExt = (env.PATHEXT ?? '.EXE;.CMD;.BAT;.COM').split(';').filter(Boolean);
  const extensions = !windows ? [''] : startable ? pathExt : ['', ...pathExt];
  for (const directory of (env.PATH ?? '').split(windows ? ';' : ':')) {
    if (!directory) continue;
    for (const extension of extensions) {
      const candidate = join(directory, command + extension);
      if (existsSync(candidate)) return candidate;
    }
  }
  return null;
}
