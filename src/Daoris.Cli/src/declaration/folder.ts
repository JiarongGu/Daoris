import { CONCERNS, HOME_WHOLE, SPLIT_SECTIONS } from './members.ts';
import type { Declaration, HomeValues } from './split.ts';

/** A declaration read back from the folder's files and the home's values, in the old file's shape, and what stopped it reading. */
export interface Folded {
  declaration: Declaration;
  /** Why the folder is no declaration (a ring of `after`, a gate in a file of another name, a file nobody knows), each in words. */
  refusals: string[];
}

type Obj = Record<string, unknown>;

const isObject = (value: unknown): value is Obj => value !== null && typeof value === 'object' && !Array.isArray(value);
const GATE_FILE = /^gates\/([^/]+)\.json$/;

/**
 * The gates in the order the files give them: each gate after the gates it follows, and where several wait, the smaller name first,
 * so no file lists the order. A ring or a gate that follows one no file declares is refused, naming them.
 */
function ordered(gates: ReadonlyMap<string, readonly string[]>, refusals: string[]): string[] {
  const names = [...gates.keys()].sort();
  for (const [name, follows] of gates) {
    for (const before of follows) if (!gates.has(before)) refusals.push(`gate ${name} follows ${before}, which no file declares`);
  }
  const placed: string[] = [];
  while (placed.length < names.length) {
    const next = names.find((name) => !placed.includes(name) && (gates.get(name) ?? []).every((before) => placed.includes(before) || !gates.has(before)));
    if (next === undefined) {
      refusals.push(`gates ${names.filter((name) => !placed.includes(name)).join(', ')} follow each other in a ring`);
      break;
    }
    placed.push(next);
  }
  return placed;
}

/**
 * The declaration the folder and the home say, in the old file's shape so it is read as the old file is: each gate's file with the
 * home's values for it, each set from its gate's file with its own, each other concern's file, the home's whole members. Nothing in
 * it comes from the old file, so what it equals is what the conversion kept.
 */
export function readFolder(folder: Readonly<Record<string, unknown>>, home: Readonly<HomeValues>): Folded {
  const refusals: string[] = [];
  const known = new Set<string>([...CONCERNS.map((each) => each.file), ...Object.values(SPLIT_SECTIONS).map((each) => each.file)]);
  const declaration: Obj = {};

  const files = new Map<string, Obj>();
  for (const [path, content] of Object.entries(folder)) {
    const gate = GATE_FILE.exec(path);
    if (gate) {
      if (!isObject(content) || content.name !== gate[1]) refusals.push(`${path} declares ${isObject(content) ? JSON.stringify(content.name) : 'no gate'}, not a gate of its own name`);
      else files.set(gate[1] as string, content);
    } else if (!known.has(path)) refusals.push(`${path} is no file of the folder's format`);
  }

  const follows = new Map<string, readonly string[]>();
  for (const [name, file] of files) {
    const after = file.after;
    if (after !== undefined && !(Array.isArray(after) && after.every((each) => typeof each === 'string'))) refusals.push(`gate ${name}'s after is no list of gate names`);
    follows.set(name, Array.isArray(after) ? (after as string[]) : []);
  }
  const names = ordered(follows, refusals);

  const machineGates = isObject(home.gates) ? (home.gates as Record<string, Obj>) : {};
  const machineSets = isObject(home.sets) ? (home.sets as Record<string, Obj>) : {};
  for (const name of Object.keys(machineGates)) if (!files.has(name)) refusals.push(`the home holds values for gate ${name}, which no file declares`);

  const gates: Obj[] = [];
  const sets: Obj[] = [];
  for (const name of names) {
    const { after: _after, tests, ...rest } = files.get(name) as Obj;
    gates.push({ ...rest, ...machineGates[name] });
    for (const set of Array.isArray(tests) ? (tests as Obj[]) : []) sets.push({ ...set, gate: name, ...(machineSets[set.name as string] ?? {}) });
  }
  for (const name of Object.keys(machineSets)) if (!sets.some((set) => set.name === name)) refusals.push(`the home holds values for test set ${name}, which no file declares`);
  if (gates.length) declaration.gates = gates;
  if (sets.length) declaration.tests = { sets };

  for (const concern of CONCERNS) {
    const content = folder[concern.file];
    if (content !== undefined) declaration[concern.member] = concern.wrap && isObject(content) ? content[concern.member] : content;
  }
  for (const [name, section] of Object.entries(SPLIT_SECTIONS)) {
    const kept = folder[section.file];
    const machine = home[name];
    if (kept !== undefined || machine !== undefined) declaration[name] = { ...(isObject(kept) ? kept : {}), ...(isObject(machine) ? machine : {}) };
  }
  for (const name of HOME_WHOLE) if (home[name] !== undefined) declaration[name] = home[name];

  const homeKnown = new Set<string>([...HOME_WHOLE, ...Object.keys(SPLIT_SECTIONS), 'gates', 'sets']);
  for (const name of Object.keys(home)) if (!homeKnown.has(name)) refusals.push(`the home holds ${name}, which is no value of the home's format`);
  return { declaration, refusals };
}
