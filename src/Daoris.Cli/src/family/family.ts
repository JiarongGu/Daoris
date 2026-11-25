// A family of repositories, judged from what each member declares.
//
// The family file is workspace data: it names the members, by the names the registry knows them by, in layers lowest first, and the
// pairs it denies. It lives in the folder the members' checkouts share, the parent a workspace's new repositories are made under, and
// no member tracks it, since nothing about a workspace enters a member's tracked file. Each member keeps its own edges in its
// `daoris.json` `uses`, and each consumer its pins in its own `daoris.family.lock`: one full commit per producer. No producer keeps a
// list of its consumers. This module reads the two files and judges the graph; what a pin's commit is, is `pins.ts`'s.

import { DaorisError } from '../platform/errors.ts';

/** The family file's name, in the folder the members' checkouts share. */
export const FAMILY_FILE = 'daoris.family.json';

/** A consumer's pins, at its own root: a pin move is a change to this path alone. */
export const LOCK_FILE = 'daoris.family.lock';

/** A pair the family refuses whatever the layers allow, with the reason a refusal says. */
export interface DeniedPair {
  consumer: string;
  producer: string;
  why?: string;
}

export interface FamilyFile {
  /** The members, by layer, lowest first: a member uses only members of a lower layer. */
  layers: string[][];
  denied: DeniedPair[];
}

/** One producer's commit, as a consumer pins it. */
export interface Pin {
  producer: string;
  commit: string;
}

/** What one member declares, as its checkout beside the family file holds it. */
export interface MemberState {
  /** False where no checkout stands beside the family file under the member's name; then nothing else is known of it. */
  checkout: boolean;
  /** Its `daoris.json` `uses`, read by `usesOf`. */
  uses: string[];
  /** Its `daoris.family.lock`'s pins, in the file's order; empty where it has none. */
  pins: Pin[];
}

export type FindingCode =
  | 'no-checkout' | 'non-member' | 'layer' | 'denied' | 'cycle' | 'no-uses' | 'second-pin' | 'no-pin'
  | 'not-full' | 'branch' | 'tag' | 'no-repository' | 'unknown-commit' | 'not-commit' | 'no-line' | 'off-line';

export interface Finding {
  verdict: 'refused' | 'warned';
  code: FindingCode;
  message: string;
}

/** A pin the check reads against its producer's checkout: its consumer uses the producer, a member, and pins it once. */
export interface ReadablePin {
  consumer: string;
  producer: string;
  commit: string;
}

type Json = unknown;

const isObject = (value: Json): value is Record<string, Json> => typeof value === 'object' && value !== null && !Array.isArray(value);
const isName = (value: Json): value is string => typeof value === 'string' && value.trim().length > 0;

function parse(text: string, where: string): Json {
  try {
    return JSON.parse(text);
  } catch (error) {
    throw new DaorisError(`${where} is not JSON: ${(error as Error).message}`);
  }
}

/** The family file, read whole or refused as a tool error naming it: a family of the wrong shape is never read as no family. */
export function readFamily(text: string, where: string): FamilyFile {
  const json = parse(text, where);
  const shape = `${where} holds \`layers\`, a list of layers lowest first, each a list of member names, and optionally \`denied\`, `
    + 'a list of `{ "consumer", "producer", "why" }`';
  if (!isObject(json) || !Array.isArray(json.layers) || json.layers.length === 0
    || !json.layers.every((layer) => Array.isArray(layer) && layer.length > 0 && layer.every(isName))) {
    throw new DaorisError(shape);
  }
  const denied = json.denied ?? [];
  if (!Array.isArray(denied) || !denied.every((pair) => isObject(pair) && isName(pair.consumer) && isName(pair.producer)
    && (pair.why === undefined || typeof pair.why === 'string'))) {
    throw new DaorisError(shape);
  }
  const layers = (json.layers as string[][]).map((layer) => layer.map((name) => name.trim()));
  const seen = new Set<string>();
  for (const name of layers.flat()) {
    if (seen.has(name.toLowerCase())) throw new DaorisError(`${where}: \`${name}\` is named in two layers; a member stands in one`);
    seen.add(name.toLowerCase());
  }
  return {
    layers,
    denied: (denied as Record<string, string>[]).map((pair) => ({
      consumer: pair.consumer!.trim(), producer: pair.producer!.trim(), ...(pair.why ? { why: pair.why } : {}),
    })),
  };
}

/** A consumer's lock: its pins in the file's order, a second pin of one producer kept, so the judgement can refuse it. */
export function readLock(text: string, where: string): Pin[] {
  const json = parse(text, where);
  if (!isObject(json) || !Array.isArray(json.pins) || !json.pins.every((pin) => isObject(pin) && isName(pin.producer) && isName(pin.commit))) {
    throw new DaorisError(`${where} holds \`pins\`, a list of \`{ "producer", "commit" }\`, one per producer this repository uses`);
  }
  return (json.pins as Record<string, string>[]).map((pin) => ({ producer: pin.producer!.trim(), commit: pin.commit!.trim() }));
}

/**
 * What a manifest says it uses, by the rule the service's `Declared.Uses` and the driver's `LineRegistration.UsesOf` hold: each name
 * trimmed, an empty one and the repository's own name dropped, a repeat dropped (in any case), the first kept.
 */
export function usesOf(names: Json, repository: string): string[] {
  const kept: string[] = [];
  for (const raw of Array.isArray(names) ? names : []) {
    const name = typeof raw === 'string' ? raw.trim() : '';
    if (name.length === 0 || name.toLowerCase() === repository.toLowerCase()) continue;
    if (kept.some((k) => k.toLowerCase() === name.toLowerCase())) continue;
    kept.push(name);
  }
  return kept;
}

/** The members in layer order, each with its layer (1 is the lowest). */
function layerOf(family: FamilyFile): Map<string, { name: string; layer: number }> {
  const members = new Map<string, { name: string; layer: number }>();
  family.layers.forEach((layer, index) => layer.forEach((name) => members.set(name.toLowerCase(), { name, layer: index + 1 })));
  return members;
}

/** Each member's edges to other members, by their names as the family file writes them. */
function edgesOf(family: FamilyFile, members: ReadonlyMap<string, MemberState>): Map<string, string[]> {
  const layers = layerOf(family);
  const edges = new Map<string, string[]>();
  for (const { name } of layers.values()) {
    const state = members.get(name);
    edges.set(name, (state?.checkout ? state.uses : []).flatMap((used) => {
      const producer = layers.get(used.toLowerCase());
      return producer ? [producer.name] : [];
    }));
  }
  return edges;
}

/** How many edges join members: the graph the check judges. */
export function edgeCount(family: FamilyFile, members: ReadonlyMap<string, MemberState>): number {
  return [...edgesOf(family, members).values()].reduce((sum, list) => sum + list.length, 0);
}

/** The pins the check reads against their producers' checkouts: a member producer the consumer uses, its first pin of it. */
export function readablePins(family: FamilyFile, members: ReadonlyMap<string, MemberState>): ReadablePin[] {
  const edges = edgesOf(family, members);
  const readable: ReadablePin[] = [];
  for (const [consumer, producers] of edges) {
    const seen = new Set<string>();
    for (const pin of members.get(consumer)?.pins ?? []) {
      const producer = producers.find((p) => p.toLowerCase() === pin.producer.toLowerCase());
      if (!producer || seen.has(producer)) continue;
      seen.add(producer);
      readable.push({ consumer, producer, commit: pin.commit });
    }
  }
  return readable;
}

/** Every cycle once, each from the first of its members in layer order, as the path the edges walk. */
function cyclesOf(order: string[], edges: ReadonlyMap<string, string[]>): string[][] {
  const cycles = new Map<string, string[]>();
  const done = new Set<string>();
  const visit = (name: string, path: string[]): void => {
    if (done.has(name)) return;
    const at = path.indexOf(name);
    if (at >= 0) {
      const cycle = path.slice(at);
      const key = [...cycle].sort().join('\u0000');
      if (!cycles.has(key)) cycles.set(key, [...cycle, name]);
      return;
    }
    for (const next of edges.get(name) ?? []) visit(next, [...path, name]);
    if (path.length === 0) done.add(name);
  };
  for (const name of order) visit(name, []);
  return [...cycles.values()];
}

/**
 * The family's graph judged with no git: a member with no checkout, a non-member, an edge to an equal or a higher layer, a denied
 * pair, a cycle (once, with its path), a pin with no `uses`, a second pin of one producer refused; a `uses` with no pin warned.
 */
export function judgeFamily(family: FamilyFile, members: ReadonlyMap<string, MemberState>): Finding[] {
  const layers = layerOf(family);
  const findings: Finding[] = [];
  const refuse = (code: FindingCode, message: string) => findings.push({ verdict: 'refused', code, message });

  for (const { name } of layers.values()) {
    if (members.get(name)?.checkout) continue;
    refuse('no-checkout', `${name} has no checkout beside the family file: a member's checkout is the folder of its name there`);
  }

  for (const { name: consumer, layer } of layers.values()) {
    const state = members.get(consumer);
    if (!state?.checkout) continue;
    for (const used of state.uses) {
      const producer = layers.get(used.toLowerCase());
      if (!producer) {
        refuse('non-member', `${consumer} uses ${used}, which is no member of the family`);
        continue;
      }
      if (producer.layer === layer) {
        refuse('layer', `${consumer} → ${producer.name}: ${consumer} and ${producer.name} are both in layer ${layer}; a member uses only lower layers`);
      } else if (producer.layer > layer) {
        refuse('layer', `${consumer} → ${producer.name}: ${consumer} is in layer ${layer} and ${producer.name} in layer ${producer.layer}; `
          + 'a member uses only lower layers');
      }
      const denied = family.denied.find((pair) => pair.consumer.toLowerCase() === consumer.toLowerCase()
        && pair.producer.toLowerCase() === producer.name.toLowerCase());
      if (denied) refuse('denied', `${consumer} → ${producer.name} is denied: ${denied.why ?? 'the family file denies the pair'}`);
    }
  }

  const edges = edgesOf(family, members);
  for (const cycle of cyclesOf([...layers.values()].map((m) => m.name), edges)) refuse('cycle', `a cycle: ${cycle.join(' → ')}`);

  for (const [consumer, producers] of edges) {
    const pins = members.get(consumer)?.pins ?? [];
    const counted = new Map<string, number>();
    for (const pin of pins) {
      const used = members.get(consumer)!.uses.some((u) => u.toLowerCase() === pin.producer.toLowerCase());
      if (!used) {
        refuse('no-uses', `${consumer} pins ${pin.producer} and does not use it: a pin needs its edge in daoris.json's uses`);
        continue;
      }
      const key = pin.producer.toLowerCase();
      counted.set(key, (counted.get(key) ?? 0) + 1);
      if (counted.get(key) === 2) refuse('second-pin', `${consumer} pins ${pin.producer} twice: one pin per producer`);
    }
    for (const producer of producers) {
      if (pins.some((pin) => pin.producer.toLowerCase() === producer.toLowerCase())) continue;
      findings.push({ verdict: 'warned', code: 'no-pin', message: `${consumer} uses ${producer} and pins no commit of it yet` });
    }
  }
  return findings;
}
