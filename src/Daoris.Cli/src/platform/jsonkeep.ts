/**
 * An edit written over the file it was read from (FABRIC-GRAPH1's review, findings 1 and 5; FIX-LOG 2026-10-17), the driver's
 * `JsonKeeping`. A writer that rebuilt a home file from the fields it knows deleted every setting it did not, and one that parsed through
 * JavaScript's numbers wrote `9007199254740993` back as `9007199254740992`. Three values make the edit instead: the file's text as it
 * stands, the file as its reader understood it, and the edited choices, the last two built by the same writer. What the edit did not change
 * is written as the file has it, every number by its own text; what it changed is written as the edit has it, an object the three hold alike
 * merged key by key; a key the edit adds goes after the one before it in the writer's order. Both twins are held to
 * `test/fixtures/driver-config-keeps.json`.
 *
 * A name JavaScript reads as an array index (`"2048"`) is laid out before the other names of its object when an edit rewrites the file:
 * a plain object keeps such keys in number order, so the driver's twin, which keeps every key in place, is the exact one there.
 *
 * 🔴 An edit is the set of key paths it changed (a value set, a key added, a key removed), from the text read and the edited choices, and
 * making it touches those paths alone (the second re-check, finding 1): merged over the file as it stands, an object another writer had
 * taken out came back whole with every key the edit never touched, and one another writer had made a string was overwritten by the stale
 * edited object. An object missing now is made again with only the edit's own keys under it; one that is no longer an object refuses the
 * edit (`EditRefused`); a key the edit took that is already gone is nothing.
 */

import { basename } from 'node:path';
import { findName } from './casefold.ts';
import { DaorisError } from './errors.ts';
import { readJsonObject, writeJsonAtomic, writeJsonTextAtomic } from './fsx.ts';
import { withKeepLock } from './keeplock.ts';

/**
 * An edit refused, nothing written: it changes `editPath`, and `holder`, the value that path lies under, is no longer an object in the
 * file as it stands (the second re-check, finding 1), the driver's `JsonEditRefusedException`. Another writer made it a string, a number
 * or an array since this one read it; writing the stale object over it would undo that writer's change. A set (`list`, CONFIGMERGE1) is
 * refused where it is no longer an array.
 */
export class EditRefused extends Error {
  /** The key path the edit changes, its names joined by dots. */
  readonly editPath: string;
  /** The path of the value it lies under that is no longer an object; empty for the file's top. */
  readonly holder: string;

  constructor(editPath: string, holder: string, list = false) {
    super(holder.length === 0
      ? `this edit changes \`${editPath}\`, and the file is no longer a JSON object`
      : `this edit changes \`${editPath}\`, and \`${holder}\` is no longer ${list ? 'a list' : 'an object'} there`);
    this.editPath = editPath;
    this.holder = holder;
  }
}

type Raw = { readonly rawJSON: string };
const rawJSON = (JSON as unknown as { rawJSON?: (text: string) => Raw }).rawJSON;
const isRawJSON = (JSON as unknown as { isRawJSON?: (value: unknown) => boolean }).isRawJSON;

/** `text` parsed with every number kept as its own text, where this Node can (`JSON.rawJSON`); else as JavaScript reads numbers. */
export function parseKeepingNumbers(text: string): unknown {
  if (rawJSON === undefined) return JSON.parse(text);
  return JSON.parse(text, (_key: string, value: unknown, context?: { source?: string }) =>
    typeof value === 'number' && context?.source !== undefined ? rawJSON(context.source) : value);
}

function isObject(value: unknown): value is Record<string, unknown> {
  return value !== null && typeof value === 'object' && !Array.isArray(value) && !(isRawJSON?.(value) ?? false);
}

/**
 * `edited` written over `original`, where `read` is the file as its reader understood it when it read it, written by the same writer as
 * `edited`: the file's text, two spaces deep, one final newline, its lines ended as `original`'s first line is (the re-check's finding
 * 3: an edit wrote LF over a file in CRLF). No edit is `original`'s own text. `original` is the file as it stands when the edit is
 * written, which may be another writer's since the read (finding 1): what that writer changed and this edit did not stays as it is.
 *
 * `sets` names the arrays that are sets (CONFIGMERGE1, D173's CONFIG1 note; `driverconfig.ts`' `DRIVER_SETS`), each a key path, `*` any
 * one name: an edit of one is the names it added and took, compared without case, made onto the file's array as it stands (`merged`),
 * so two writers each adding a name keep both. Every other array is a value, written whole.
 */
export function keepEdits(
  original: string, read: Record<string, unknown>, edited: Record<string, unknown>, sets: readonly string[] = [],
): string {
  const readText = JSON.stringify(read);
  const editedText = JSON.stringify(edited);
  if (readText === editedText) return original;
  const file = parseKeepingNumbers(original);
  const understood = JSON.parse(readText) as Record<string, unknown>;
  const now = JSON.parse(editedText) as Record<string, unknown>;
  if (!isObject(file)) throw new EditRefused(changed(understood, now, '') ?? '', '');
  const text = `${JSON.stringify(plan(file, understood, now, '', true, sets), null, 2)}\n`;
  const end = original.indexOf('\n');
  return end > 0 && original[end - 1] === '\r' ? text.replaceAll('\n', '\r\n') : text;
}

/**
 * An edit of a home file made onto the file as it stands, and written (KEEPALL1): every file a door edits keeps what its writer does not
 * own, as `driver.json` does through `writeDriverChoices`. `read` is the file as its reader understood it when it read it, and `edited`
 * the edited choices, both built by one writer; only the key paths that differ between them are made, under `keepEdits`' rules, onto
 * the file read again here. No edit writes nothing. `found` says a file was there at the read: where there was none then and is none
 * now, `edited` is written whole in the writer's own layout, since there is nothing to keep. The driver's twin is `JsonKeeping.Save`.
 *
 * 🔴 The re-read and the rename are made holding the file's lock (CONFIGLOCK1, D173's CONFIG1 note; `keeplock.ts`), which the driver's
 * `JsonKeeping.Save` takes too: with none, another writer's change landing between them was lost.
 *
 * @throws DaorisError The file there now does not read, holds a key twice in one object, or holds no object where this edit changes
 * something inside one, or another writer held its lock for the whole of the wait; nothing was written.
 */
export function writeKeeping(path: string, found: boolean, read: Record<string, unknown>, edited: Record<string, unknown>): void {
  if (JSON.stringify(read) === JSON.stringify(edited)) return;
  withKeepLock(path, () => writeHeld(path, found, read, edited));
}

/** `writeKeeping` once the file's lock is held. */
function writeHeld(path: string, found: boolean, read: Record<string, unknown>, edited: Record<string, unknown>): void {
  const { problem, text } = readJsonObject(path);
  const name = basename(path);
  // The words every editor of a home file says over one that does not read (REV3, `writeJsonTextAtomic`).
  if (problem !== null) {
    throw new DaorisError(`${problem}. Fix it, or delete it to start from nothing — this command will not overwrite a file it could not understand.`);
  }
  if (text === undefined && !found) {
    writeJsonAtomic(path, edited);
    return;
  }
  const current = text ?? '{}\n';
  const doubled = twice(current);
  if (doubled !== null) {
    throw new DaorisError(`${name} holds the key \`${doubled}\` twice in one object, so an edit cannot tell which it changes; this edit was not written. ${REMEDY}`);
  }
  let kept: string;
  try {
    kept = keepEdits(current, read, edited);
  } catch (error) {
    if (!(error instanceof EditRefused)) throw error;
    throw new DaorisError(`${name} has changed under this edit: ${error.message}, so this edit was not written. ${REMEDY}`);
  }
  writeJsonTextAtomic(path, kept);
}

const REMEDY = 'Fix it, or delete it to start from nothing, then make the change again.';

/**
 * The first key written twice in one object of `text`, a JSON text that parses, in the order the second is written; or null (the
 * re-check's finding 3, the driver's `JsonKeeping.Twice`). `JSON.parse` reads such a key as its last value in its first place, and
 * the driver reads it so too; an edit cannot tell which of the two it changes, so a writer refuses to edit a file holding one.
 */
export function twice(text: string): string | null {
  const objects: (Set<string> | null)[] = [];
  let key = false;
  for (let at = 0; at < text.length; at++) {
    const c = text[at];
    if (c === '"') {
      let end = at + 1;
      while (text[end] !== '"') end += text[end] === '\\' ? 2 : 1;
      if (key) {
        const name = JSON.parse(text.slice(at, end + 1)) as string;
        const names = objects.at(-1)!;
        if (names.has(name)) return name;
        names.add(name);
        key = false;
      }
      at = end;
    } else if (c === '{' || c === '[') {
      objects.push(c === '{' ? new Set() : null);
      key = c === '{';
    } else if (c === '}' || c === ']') {
      objects.pop();
      key = false;
    } else if (c === ',') {
      key = objects.at(-1) != null;
    }
  }
  return null;
}

const same = (a: unknown, b: unknown): boolean => JSON.stringify(a) === JSON.stringify(b);

/**
 * What one object of the file as it stands becomes, key by key in its order, `where` being its path: a key neither its reader nor the edit
 * holds is the file's, kept; one the edit took goes, except that a map emptied at the top loses only the entries its reader read; one its
 * reader did not read and the edit set is the edit's, a map merged into the file's key by key and refused where the file's is no longer an
 * object; one the edit left as read is the file's, as written; an object the edit changed is
 * merged, refused where the file's is no longer an object; else the edit's. A key the edit set that the file no longer holds is added, an
 * object with only the edit's own keys under it (the second re-check, finding 1). A set of `sets` is merged by its names (`merged`), never
 * written whole.
 */
function plan(
  file: Record<string, unknown>, read: Record<string, unknown>, edited: Record<string, unknown>, where: string, top: boolean,
  sets: readonly string[],
): Record<string, unknown> {
  const out: [string, unknown][] = [];
  for (const [name, kept] of Object.entries(file)) {
    const path = joined(where, name);
    const wasRead = Object.hasOwn(read, name);
    const isEdited = Object.hasOwn(edited, name);
    const was = read[name];
    const now = edited[name];
    const set = isSet(sets, where, name);
    if (!wasRead && !isEdited) out.push([name, kept]);
    else if (!isEdited && set) {
      // A set the edit took whole loses only the names its reader read: another writer's stay, and it goes where none is left.
      const rest = Array.isArray(kept) ? merged(kept, was, undefined, false) : null;
      if (rest !== null) out.push([name, rest]);
    } else if (!isEdited) {
      // The writer leaves an emptied map out, so at the top its edit took the entries its reader read, not the map.
      const first = top && isObject(was) ? changed(was, {}, path) : null;
      if (first === null) continue;
      if (!isObject(kept)) throw new EditRefused(first, path);
      const unread = plan(kept, was as Record<string, unknown>, {}, path, false, sets);
      if (Object.keys(unread).length > 0) out.push([name, unread]);
    } else if (set && Array.isArray(now) && !(wasRead && same(was, now))) {
      // CONFIGMERGE1: the names this edit added and took, made onto the set as it stands; one no longer an array refuses it.
      if (!Array.isArray(kept)) throw new EditRefused(path, path, true);
      out.push([name, merged(kept, wasRead ? was : undefined, now, true)]);
    } else if (!wasRead && isObject(now)) {
      // A map its reader never saw is no whole value: its keys are the edit's paths, made into what another writer wrote meanwhile (the
      // re-check of round 3: written whole, it deleted that writer's entries, or overwrote its string).
      if (!isObject(kept)) throw new EditRefused(changed({}, now, path) ?? path, path);
      out.push([name, plan(kept, {}, now, path, false, sets)]);
    } else if (!wasRead) out.push([name, now]);
    else if (same(was, now)) out.push([name, kept]);
    else if (isObject(was) && isObject(now)) {
      if (!isObject(kept)) throw new EditRefused(changed(was, now, path) ?? path, path);
      out.push([name, plan(kept, was, now, path, false, sets)]);
    } else out.push([name, now]);
  }

  // A key the edit adds goes after the edit's key before it, or first; a default the file never wrote stays unwritten, and a key another
  // writer took that this edit left as read stays out.
  let at = 0;
  for (const [name, now] of Object.entries(edited)) {
    const index = out.findIndex(([each]) => each === name);
    if (index >= 0) {
      at = index + 1;
      continue;
    }
    if (Object.hasOwn(file, name)) continue;
    const was = read[name];
    const wasRead = Object.hasOwn(read, name);
    if (isSet(sets, where, name) && Array.isArray(now)) {
      // A set the file no longer holds is made with the names this edit added alone, never the ones it read.
      const made = merged(undefined, wasRead ? was : undefined, now, false);
      if (made !== null) out.splice(at++, 0, [name, made]);
    } else if (!wasRead) out.splice(at++, 0, [name, now]);
    else if (same(was, now)) continue;
    else if (isObject(was) && isObject(now)) {
      // Taken out since the read: made again with only the edit's own keys under it, never the ones it left as read.
      const made = plan({}, was, now, joined(where, name), false, sets);
      if (Object.keys(made).length > 0) out.splice(at++, 0, [name, made]);
    } else out.splice(at++, 0, [name, now]);
  }

  return Object.fromEntries(out);
}

/** Whether the key `name` of the object at `where` is one of `sets`, as the driver's `JsonKeeping.IsSet` reads them. */
function isSet(sets: readonly string[], where: string, name: string): boolean {
  return sets.some((set) => {
    const dot = set.lastIndexOf('.');
    const [parent, leaf] = dot < 0 ? ['', set] : [set.slice(0, dot), set.slice(dot + 1)];
    return parent === where && (leaf === '*' || leaf === name);
  });
}

/**
 * A set made (CONFIGMERGE1, D173's CONFIG1 note), the driver's `JsonKeeping.Merged`: `file`'s array as it stands, undefined for none, with
 * every name the edit from `was` to `now` took gone in any spelling and every name it added that is not there in any case put at the end
 * in the edit's order; every other element, another writer's names and a value of any other kind (a number by its own text), stays where
 * it stands. Names compare through `casefold.ts`, as the driver's `OrdinalIgnoreCase` compares them. Null, written as no key, where
 * nothing is left and `keepEmpty` does not keep an empty array.
 */
function merged(file: unknown[] | undefined, was: unknown, now: unknown, keepEmpty: boolean): unknown[] | null {
  const names = (array: unknown): string[] => (Array.isArray(array) ? array.filter((item): item is string => typeof item === 'string') : []);
  const read = names(was);
  const edited = names(now);
  const taken = read.filter((name) => findName(edited, name) === null);
  const kept = (file ?? []).filter((item) => typeof item !== 'string' || findName(taken, item) === null);
  const present = names(kept);
  const added: string[] = [];
  for (const name of edited) {
    if (findName(read, name) !== null || findName(present, name) !== null) continue;
    present.push(name);
    added.push(name);
  }
  return kept.length + added.length === 0 && !keepEmpty ? null : [...kept, ...added];
}

/**
 * The first key path under `where` the edit from `was` to `now` changes, in the edit's order and then the keys it took; or null where it
 * changes none. What a refusal names, as the driver's `JsonKeeping.Changed`.
 */
function changed(was: Record<string, unknown>, now: Record<string, unknown>, where: string): string | null {
  for (const [name, value] of Object.entries(now)) {
    const path = joined(where, name);
    if (!Object.hasOwn(was, name)) return path;
    const before = was[name];
    if (same(before, value)) continue;
    return isObject(before) && isObject(value) ? changed(before, value, path) ?? path : path;
  }
  for (const name of Object.keys(was)) {
    if (!Object.hasOwn(now, name)) return joined(where, name);
  }
  return null;
}

const joined = (where: string, name: string): string => where.length === 0 ? name : `${where}.${name}`;
