import { readdirSync, readFileSync } from 'node:fs';
import { join, relative, sep } from 'node:path';

/**
 * What the route censuses beside `bridge/state/heldRoutes.test.ts` read: each route's marks from the desktop's sources, and its
 * callers, the page's and the terminal's. A source is `{ file, text }`, its file named from the repository's `src/`; a census takes
 * its sources as an argument, so a planted one shows it finds what it is for.
 */

export type Source = { file: string; text: string };

/** A route a source names, and where. */
export type Named = { file: string; route: string };

const SRC = join(process.cwd(), '..');
const SKIPPED = ['obj', 'bin', 'node_modules', 'generated'];

const read = (folder: string, keep: (name: string) => boolean): Source[] =>
  readdirSync(folder, { withFileTypes: true }).flatMap((entry) => {
    const path = join(folder, entry.name);
    if (entry.isDirectory()) return SKIPPED.includes(entry.name) || /\.(Tests|Rehearsals)$/.test(entry.name) ? [] : read(path, keep);
    return keep(entry.name) ? [{ file: relative(SRC, path).split(sep).join('/'), text: readFileSync(path, 'utf8') }] : [];
  });

const once = <T>(make: () => T): (() => T) => {
  let made: T | undefined;
  return () => (made ??= make());
};

/** The desktop's sources, its test projects aside. */
export const desktopSources = once(() => read(join(SRC, 'Daoris.Desktop'), (name) => name.endsWith('.cs')));

/** The terminal's: the driver's sources, whose verbs reach the running Daoris over its pipe. */
export const terminalSources = once(() => desktopSources().filter(({ file }) => file.startsWith('Daoris.Desktop/Daoris.Desktop.Driver/')));

/** The page's sources, its tests, stories and test doubles aside. */
export const pageSources = once(() => read(join(SRC, 'Daoris.Web', 'src'), (name) => /\.(ts|tsx)$/.test(name) && !/\.(test|stories)\.tsx?$/.test(name))
  .filter(({ file }) => !file.startsWith('Daoris.Web/src/test/')));

/** The reads the shell tells by event (`[ByEvent("…")]`), `numbered` the ones its mark numbers (`Numbered = true`), or the rest. */
export function heldRoutes(numbered: boolean, sources = desktopSources()): string[] {
  const marks = new Map<string, boolean>();
  for (const { text } of sources) {
    for (const found of text.matchAll(/\[ByEvent\("([A-Z0-9_]+)"(.*)$/gm)) {
      marks.set(found[1]!, marks.get(found[1]!) === true || /\bNumbered\s*=\s*true\b/.test(found[2]!));
    }
  }
  return [...marks].filter(([, each]) => each === numbered).map(([route]) => route).sort();
}

/** The page's plain doors onto a route: the bridge's `call`, and a driver `invoke` that goes round it. */
export const PLAIN = String.raw`\bcall(?:<[^>(]*>)?\(\s*(?:/\*[^*]*\*/\s*)?|\.invoke(?:<[^>(]*>)?\(\s*'DAORIS\.DRIVER',\s*`;

/** The page's held readers: `askHeld` and `askSettled`. */
export const HELD = String.raw`\bask(?:Held|Settled)(?:<[^>(]*>)?\(\s*`;

/** Each route a source names through `door`. */
export const through = (door: string, sources: readonly Source[]): Named[] =>
  sources.flatMap(({ file, text }) => [...text.matchAll(new RegExp(`(?:${door})['"\`]([A-Z][A-Z0-9_]*)['"\`]`, 'g'))]
    .map((found) => ({ file, route: found[1]! })));

/** `reads` as a census says them, `file: ROUTE` and what is wrong. */
export const said = (reads: readonly Named[], wrong = '') => reads.map(({ file, route }) => `${file}: ${route}${wrong ? ` ${wrong}` : ''}`);

/** The arguments of the call whose `(` stands at `open`, each trimmed: what brackets and quotes hold stays whole. */
export function argumentsAt(text: string, open: number): string[] {
  const parts: string[] = [];
  let depth = 0;
  let start = open + 1;
  for (let at = open + 1; at < text.length; at += 1) {
    const char = text[at]!;
    const comment = char === '/' ? text[at + 1] : undefined;
    if (comment === '/' || comment === '*') {
      at = comment === '/' ? text.indexOf('\n', at) : text.indexOf('*/', at) + 1;
      if (at <= 0) break;
    } else if (char === "'" || char === '"' || char === '`') {
      do at = text.indexOf(char, at + 1); while (at > 0 && text[at - 1] === '\\');
      if (at < 0) break;
    } else if ('([{'.includes(char)) depth += 1;
    else if (')]}'.includes(char)) {
      if (depth === 0) {
        parts.push(text.slice(start, at).trim());
        break;
      }
      depth -= 1;
    } else if (char === ',' && depth === 0) {
      parts.push(text.slice(start, at).trim());
      start = at + 1;
    }
  }
  return parts.filter((part) => part !== '');
}

// A reader that hands the answer on as it came, `(answer) => answer as T`, reads the shell's nothing-held-yet as the answer.
const PASSED_ON = /^\(?\s*(\w+)\s*\)?\s*(?::[^=]*)?=>\s*\(?\s*\1\s+as\b/;

/** Each held read whose reader, which its type makes it name, takes nothing held yet for the answer: it hands the answer on as it came. */
export const readersUnread = (sources: readonly Source[]): string[] =>
  sources.flatMap(({ file, text }) => [...text.matchAll(/\bask(Held|Settled)(?:<[^>(]*>)?\(/g)].flatMap((found) => {
    const parts = argumentsAt(text, found.index + found[0].length - 1);
    const route = /^['"`]([A-Z][A-Z0-9_]*)['"`]$/.exec(parts[0] ?? '')?.[1];
    return route && PASSED_ON.test(parts[found[1] === 'Held' ? 4 : 2] ?? '') ? [`${file}: ${route} hands its answer on as it came`] : [];
  }));

/** A read a terminal's verb sends: where, the route, and the door it goes through (`ThroughAsync`, `TryReadAsync`, …), or none found. */
export type TerminalRead = Named & { door: string | null };

const classAt = (text: string, at: number) => [...text.slice(0, at).matchAll(/\b(?:class|record|struct)\s+(\w+)/g)].at(-1)?.[1] ?? '';

/**
 * Each read of `routes` a terminal's verb sends, by the `InstanceRoute` that names it (`OpenRoutes.PlacePlan`): every door it is
 * handed to, by its class and name, or within its own file by its name alone. A route named by a constant (`SupervisorRoutes.Waiting`)
 * is read by the constant's text. An `InstanceRoute` of one of `routes` handed to no door is said with none.
 */
export function terminalReads(routes: readonly string[], sources: readonly Source[] = terminalSources()): TerminalRead[] {
  const constants = new Map<string, string>();
  for (const { text } of sources) {
    for (const found of text.matchAll(/\bconst string (\w+) = "([A-Z][A-Z0-9_]*)";/g)) constants.set(`${classAt(text, found.index)}.${found[1]}`, found[2]!);
  }
  return sources.flatMap(({ file, text }) => [...text.matchAll(/\b(?:public|internal|private) static InstanceRoute (\w+) \{ get; \} = ([^;]+);/g)]
    .flatMap((found): TerminalRead[] => {
      const route = /"([A-Z][A-Z0-9_]*)"/.exec(found[2]!)?.[1]
        ?? [...found[2]!.matchAll(/\b(\w+\.\w+)\b/g)].map((each) => constants.get(each[1]!)).find(Boolean);
      if (!route || !routes.includes(route)) return [];
      const owner = classAt(text, found.index);
      const door = new RegExp(String.raw`\b(TryReadAsync|ReadAsync|ThroughAsync|PressAsync)\(\s*(?:[\w.?!]+\s*,\s*)?(?:(\w+)\.)?${found[1]}\s*,`, 'g');
      const reads = sources.flatMap((each) => [...each.text.matchAll(door)]
        .filter((call) => (call[2] === undefined ? each.file === file : call[2] === owner))
        .map((call) => ({ file: each.file, route, door: call[1]! })));
      return reads.length > 0 ? reads : [{ file, route, door: null }];
    }));
}
