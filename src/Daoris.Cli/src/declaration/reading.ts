import type { Declaration } from './split.ts';

/**
 * What a declaration says that a run depends on, read from one shape: the gates' order, each gate's expanded reach, and which test
 * set holds each test file. The old file and the folder are each read into this and the two readings compared (`proof.ts`), so the
 * proof asks the same questions of both.
 */

type Obj = Record<string, unknown>;

// The lane table's grammar (`tools/merge/lanes-file.ts`), kept equal to it by `declaration/proof.test.ts`: `*` stays in a folder,
// `**` crosses folders, `{a,b}` is either, each alternative a glob of its own.
const escapeRegExp = (text: string): string => text.replace(/[.+^${}()|[\]\\]/g, '\\$&');

function groupAt(glob: string, open: number): { end: number; commas: number[] } | null {
  let depth = 0;
  const commas: number[] = [];
  for (let at = open; at < glob.length; at += 1) {
    if (glob[at] === '{') depth += 1;
    else if (glob[at] === '}' && --depth === 0) return { end: at, commas };
    else if (glob[at] === ',' && depth === 1) commas.push(at);
  }
  return null;
}

function body(glob: string): string {
  let source = '';
  for (let i = 0; i < glob.length; i += 1) {
    const c = glob[i] as string;
    const group = c === '{' ? groupAt(glob, i) : null;
    if (c === '*') {
      if (glob[i + 1] === '*') {
        i += 1;
        if (glob[i + 1] === '/') {
          i += 1;
          source += '(?:.*/)?';
        } else source += '.*';
      } else source += '[^/]*';
    } else if (c === '?') source += '[^/]';
    else if (group) {
      const edges = [i, ...group.commas, group.end];
      source += `(?:${edges.slice(1).map((edge, k) => body(glob.slice((edges[k] as number) + 1, edge))).join('|')})`;
      i = group.end;
    } else source += escapeRegExp(c);
  }
  return source;
}

/** A path pattern as a regular expression. */
export const globToRegExp = (glob: string): RegExp => new RegExp(`^${body(glob)}$`);

/** The files a gate's declared `paths` hold: an entry ending in `/` is a folder, one with `*` a glob, any other a file. */
function reachOf(paths: readonly string[], files: readonly string[]): string[] {
  const held = paths.map((entry) => (entry.endsWith('/')
    ? (file: string) => file.startsWith(entry)
    : entry.includes('*') ? ((pattern) => (file: string) => pattern.test(file))(globToRegExp(entry)) : (file: string) => file === entry));
  return files.filter((file) => held.some((holds) => holds(file))).sort();
}

/** Two sets that hold one file: it is a pick, which is allowed, when one names the other by `of`. */
const picks = (a: Obj, b: Obj): boolean => a.of === b.name || b.of === a.name;

export interface Overlap {
  file: string;
  /** Every set that holds it, in the declaration's order. */
  sets: string[];
  /** The set that holds it today: the first that is not a pick. */
  today: string;
}

export interface Reading {
  /** The gates by name, in the order the declaration gives them. */
  order: string[];
  /** Each gate's expanded reach, sorted; null where it names no paths and so reaches every change. */
  reach: Record<string, string[] | null>;
  /** The set holding each test file: the first that is not a pick, today's rule. */
  holder: Record<string, string>;
  /** Files held by two sets neither of which names the other by `of`. */
  overlaps: Overlap[];
}

const strings = (value: unknown): string[] => (Array.isArray(value) ? value.filter((each): each is string => typeof each === 'string') : []);
const listOf = (value: unknown): Obj[] => (Array.isArray(value) ? value.filter((each): each is Obj => each !== null && typeof each === 'object') : []);

/** What `declaration` says over `files`, the repository's files by their path from its root. */
export function readDeclaration(declaration: Declaration, files: readonly string[]): Reading {
  const gates = listOf(declaration.gates);
  const reading: Reading = { order: gates.map((gate) => String(gate.name)), reach: {}, holder: {}, overlaps: [] };
  for (const gate of gates) reading.reach[String(gate.name)] = Array.isArray(gate.paths) ? reachOf(strings(gate.paths), files) : null;

  const tests = declaration.tests as Obj | undefined;
  const sets = listOf(tests?.sets).map((set) => ({ set, patterns: strings(set.files).map(globToRegExp) }));
  for (const file of files) {
    const holding = sets.filter((each) => each.patterns.some((pattern) => pattern.test(file)));
    if (holding.length === 0) continue;
    const first = holding.find((each) => each.set.of === undefined) ?? holding[0] as (typeof holding)[number];
    reading.holder[file] = String(first.set.name);
    const clash = holding.some((a, at) => holding.slice(at + 1).some((b) => !picks(a.set, b.set)));
    if (clash) reading.overlaps.push({ file, sets: holding.map((each) => String(each.set.name)), today: String(first.set.name) });
  }
  return reading;
}
