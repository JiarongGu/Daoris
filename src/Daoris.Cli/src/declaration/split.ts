import { DaorisError } from '../platform/errors.ts';
import {
  CONCERNS, GATE_MEMBERS, GATE_ORDER, HOME_WHOLE, INTEGRATION_MEMBERS, SET_MEMBERS, SET_ORDER, SPLIT_SECTIONS, TESTS_PRODUCT,
  TOP_GATES, TOP_TESTS, isExplanation,
} from './members.ts';

/** A parsed `daoris.gates.json`, or the shape the folder and the home resolve to. */
export type Declaration = Record<string, unknown>;

/** What the machine keeps: the values the member table sorts to the home, by what they belong to. */
export type HomeValues = Record<string, unknown>;

/** The converted declaration: the folder's files by their path under `daoris/`, the home's values, and what the product documents instead. */
export interface Split {
  folder: Record<string, unknown>;
  home: HomeValues;
  /** The members written nowhere, for the product's documentation holds them (`tests._why`). */
  product: string[];
}

type Obj = Record<string, unknown>;

const isObject = (value: unknown): value is Obj => value !== null && typeof value === 'object' && !Array.isArray(value);
const FILE_SAFE = /^[A-Za-z0-9][A-Za-z0-9._-]*$/;

/** The members of `source` in `order`, then its explanations, each as it is. */
function inOrder(source: Obj, order: readonly string[], skip: readonly string[] = []): Obj {
  const out: Obj = {};
  for (const name of order) if (name in source && !skip.includes(name)) out[name] = source[name];
  for (const name of Object.keys(source)) if (isExplanation(name)) out[name] = source[name];
  return out;
}

/**
 * The gates each follow the latest earlier gate whose name sorts after theirs, and no other. A reader that has only the files puts
 * the gates in name order, a gate after those it follows; this is the least a file can say for that to give the old order back, so
 * a gate added later edits no file but its own.
 */
function follows(names: readonly string[]): Map<string, string> {
  const after = new Map<string, string>();
  names.forEach((name, at) => {
    const earlier = names.slice(0, at).filter((each) => each > name).at(-1);
    if (earlier !== undefined) after.set(name, earlier);
  });
  return after;
}

/**
 * Splits a declaration by the member table (`members.ts`): its gates one file each with the test sets they run, each other concern a small
 * file, the machine's values to the home. A member the table does not name, a gate named twice and a set of no gate stop it, every one
 * named; it writes nothing, so a conversion that fails leaves nothing behind.
 */
export function splitDeclaration(declaration: Declaration): Split {
  if (!isObject(declaration)) throw new DaorisError('the declaration is no object, so there is nothing to split', 1);
  const problems: string[] = [];
  const folder: Obj = {};
  const home: HomeValues = {};
  const product: string[] = [];
  const unsorted = (path: string): void => { problems.push(`${path} is in no side of the member table: say where it goes before it is converted`); };

  for (const [name, value] of Object.entries(declaration)) {
    const concern = CONCERNS.find((each) => each.member === name);
    const section = SPLIT_SECTIONS[name];
    if ((HOME_WHOLE as readonly string[]).includes(name)) {
      home[name] = value;
    } else if (concern) {
      if (name === 'integration' && isObject(value)) {
        for (const member of Object.keys(value)) if (!(INTEGRATION_MEMBERS as readonly string[]).includes(member) && !isExplanation(member)) unsorted(`${name}.${member}`);
      }
      folder[concern.file] = concern.wrap ? { [name]: value } : value;
    } else if (section && isObject(value)) {
      const kept: Obj = {};
      const machine: Obj = {};
      for (const [member, each] of Object.entries(value)) {
        if (section.home.includes(member)) machine[member] = each;
        else if (section.repository.includes(member) || isExplanation(member)) kept[member] = each;
        else unsorted(`${name}.${member}`);
      }
      folder[section.file] = kept;
      if (Object.keys(machine).length) home[name] = machine;
    } else if (name !== TOP_GATES && name !== TOP_TESTS) {
      unsorted(name);
    }
  }

  const gates = Array.isArray(declaration[TOP_GATES]) ? (declaration[TOP_GATES] as unknown[]) : [];
  const sets = isObject(declaration[TOP_TESTS]) && Array.isArray(declaration[TOP_TESTS].sets) ? (declaration[TOP_TESTS].sets as unknown[]) : [];
  const names: string[] = [];
  const own: Record<string, Obj> = {};
  const machine: Record<string, Obj> = {};
  for (const gate of gates) {
    if (!isObject(gate) || typeof gate.name !== 'string' || !FILE_SAFE.test(gate.name)) {
      problems.push(`a gate has no name a file can carry: ${JSON.stringify(gate)?.slice(0, 80)}`);
      continue;
    }
    if (names.includes(gate.name)) {
      problems.push(`gate ${gate.name} is declared twice`);
      continue;
    }
    names.push(gate.name);
    const repository: Obj = {};
    const tuned: Obj = {};
    for (const [member, value] of Object.entries(gate)) {
      const side = GATE_MEMBERS[member] ?? (isExplanation(member) ? 'repository' : undefined);
      if (side === 'home') tuned[member] = value;
      else if (side === 'repository') repository[member] = value;
      else unsorted(`gates[${gate.name}].${member}`);
    }
    own[gate.name] = repository;
    if (Object.keys(tuned).length) machine[gate.name] = tuned;
  }

  const held: Record<string, Obj[]> = {};
  const machineSets: Record<string, Obj> = {};
  if (isObject(declaration[TOP_TESTS])) {
    for (const member of Object.keys(declaration[TOP_TESTS])) {
      if ((TESTS_PRODUCT as readonly string[]).includes(member)) product.push(`${TOP_TESTS}.${member}`);
      else if (member !== 'sets') unsorted(`${TOP_TESTS}.${member}`);
    }
  }
  for (const set of sets) {
    if (!isObject(set) || typeof set.name !== 'string') {
      problems.push(`a test set has no name: ${JSON.stringify(set)?.slice(0, 80)}`);
      continue;
    }
    if (typeof set.gate !== 'string' || !names.includes(set.gate)) {
      problems.push(`test set ${set.name} names gate ${String(set.gate)}, which is no gate`);
      continue;
    }
    const repository: Obj = {};
    for (const [member, value] of Object.entries(set)) {
      if (member === 'gate') continue;
      const side = SET_MEMBERS[member] ?? (isExplanation(member) ? 'repository' : undefined);
      if (side === 'home') (machineSets[set.name] ??= {})[member] = value;
      else if (side === 'repository') repository[member] = value;
      else unsorted(`${TOP_TESTS}.sets[${set.name}].${member}`);
    }
    (held[set.gate] ??= []).push(inOrder(repository, SET_ORDER));
  }

  if (problems.length) throw new DaorisError(`the declaration cannot be split:\n${problems.map((each) => `  ${each}`).join('\n')}`, 1);

  const after = follows(names);
  for (const name of names) {
    const file = { ...own[name] } as Obj;
    const before = after.get(name);
    if (before !== undefined) file.after = [before];
    if (held[name]) file.tests = held[name];
    folder[`gates/${name}.json`] = inOrder(file, GATE_ORDER);
  }
  if (Object.keys(machine).length) home.gates = machine;
  if (Object.keys(machineSets).length) home.sets = machineSets;
  return { folder, home, product };
}
