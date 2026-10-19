// The brainstorm policy, `brainstorms` (this machine's rule) and `workspaceBrainstorms` in `driver.json` (BRAINSTORM1b, D176
// point 9): whether a brainstorm an agent asks for waits for the person's yes, the agents a brainstorm runs and its minutes, read,
// refused, said and edited exactly as the driver's `BrainstormRules` does. A workspace's rule replaces the machine's whole.
//
// 🔴 A TWIN with the driver's `BrainstormRules.cs`: the CLI and the driver share no code, so both hold ONE table, the driver's
// `Daoris.Desktop.Driver.Tests/fixtures/brainstorm-rules.json`, cell for cell — the reading and its precedence, every refusal in
// the same words, how a rule is listed, and the edits (`.claude/knowledge/twins.md`).
//
// Pure: it reads no file, reaches no network and spawns nothing.

import { DaorisError } from '../platform/errors.ts';
import { atName, findName } from '../platform/casefold.ts';

/** Whether a brainstorm an agent asks for waits for the person's yes, or is refused. */
export const BRAINSTORM_MODES = ['ask', 'off'] as const;
export type BrainstormMode = (typeof BRAINSTORM_MODES)[number];

/** A brainstorm's minutes: each generator's, the synthesis's, the challenge's and the whole request's. */
export interface BrainstormMinutes {
  pass: number;
  synthesis: number;
  challenge: number;
  request: number;
}

/** A rule as kept, whole: its mode, the agents it runs in order, and its minutes. */
export interface BrainstormRule {
  mode: BrainstormMode;
  agents: string[];
  minutes: BrainstormMinutes;
}

/** The two keys of `driver.json` a brainstorms rule lives in: this machine's, and each workspace's. */
export interface BrainstormMaps {
  brainstorms: BrainstormRule | null;
  workspaceBrainstorms: Record<string, BrainstormRule>;
}

/** The most agents one brainstorm runs (D176 point 9: at most four makers). */
export const BRAINSTORM_MOST_AGENTS = 4;

const MINUTE_NAMES = ['pass', 'synthesis', 'challenge', 'request'] as const;

/** What stands where nothing is set: D176 point 9's proposed defaults. */
export const BRAINSTORM_DEFAULT: BrainstormRule = {
  mode: 'ask', agents: [], minutes: { pass: 10, synthesis: 10, challenge: 10, request: 30 },
};

/** Where a rule was set: `default` where nothing was. */
export type BrainstormSource = 'workspace' | 'machine' | 'default';

const RULE_SHAPE = 'a brainstorms rule is an object with its `mode`, the `agents` it runs and its `minutes`.';
const MODE_SHAPE = '`mode` is `ask` or `off`: whether a brainstorm an agent asks for waits for your yes, or is refused.';
const AGENT_SHAPE = 'each agent is an adapter\'s name, such as `codex-acp` or `dsh`.';
const MINUTES_SHAPE = '`minutes` holds whole numbers: `pass`, `synthesis` and `challenge` from 1 to 60 each, and `request` up to 240.';
const SCOPE = 'a brainstorms rule is set for this machine or for a workspace — never for one repository.';
const ONE_AT_A_TIME = 'one change at a time: set its mode, its agents or its minutes, or clear.';

function isObject(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

/**
 * An entry read as a rule, with its first problem: its mode (absent or null is `ask`), its agents each named once and at most four
 * (absent or null is none), then its minutes (absent or null is the defaults, each one too). The driver's `BrainstormRules.Read`.
 */
export function brainstormRuleOf(value: unknown): { rule: BrainstormRule | null; problem: string | null } {
  if (!isObject(value)) return { rule: null, problem: RULE_SHAPE };
  return judge(
    value.mode === null ? undefined : value.mode,
    value.agents === null ? undefined : value.agents,
    value.minutes === null ? undefined : value.minutes,
    BRAINSTORM_DEFAULT.minutes);
}

/** The rule judged in order, mode, agents, then minutes over `over`, the first problem said, or the rule as kept. */
function judge(mode: unknown, agents: unknown, minutes: unknown, over: BrainstormMinutes): { rule: BrainstormRule | null; problem: string | null } {
  if (mode !== undefined && (typeof mode !== 'string' || !(BRAINSTORM_MODES as readonly string[]).includes(mode))) {
    return { rule: null, problem: MODE_SHAPE };
  }

  const kept: string[] = [];
  if (agents !== undefined) {
    if (!Array.isArray(agents)) return { rule: null, problem: AGENT_SHAPE };
    for (const each of agents) {
      if (typeof each !== 'string' || each.trim().length === 0) return { rule: null, problem: AGENT_SHAPE };
      const agent = each.trim();
      if (findName(kept, agent) !== null) {
        return { rule: null, problem: `\`${agent}\` is named twice — each agent once, in the order they run.` };
      }

      kept.push(agent);
    }
  }

  if (kept.length > BRAINSTORM_MOST_AGENTS) {
    return { rule: null, problem: `a brainstorm runs at most ${BRAINSTORM_MOST_AGENTS} agents; ${kept.length} are named.` };
  }

  if (minutes !== undefined && !isObject(minutes)) return { rule: null, problem: MINUTES_SHAPE };
  const given: Record<string, unknown> = minutes ?? {};
  const timed: BrainstormMinutes = { ...over };
  for (const name of MINUTE_NAMES) {
    const minute = given[name];
    if (minute === undefined || minute === null) continue;
    const most = name === 'request' ? 240 : 60;
    if (typeof minute !== 'number' || !Number.isInteger(minute) || minute < 1 || minute > most) return { rule: null, problem: MINUTES_SHAPE };
    timed[name] = minute;
  }

  if (timed.request - timed.synthesis - timed.challenge < 1) {
    return {
      rule: null,
      problem: 'the request\'s minutes are more than the synthesis\'s and the challenge\'s together, which are reserved from the start: '
        + `${timed.synthesis} and ${timed.challenge} here.`,
    };
  }

  return {
    rule: {
      mode: (mode as BrainstormMode | undefined) ?? 'ask',
      agents: kept,
      minutes: { pass: timed.pass, synthesis: timed.synthesis, challenge: timed.challenge, request: timed.request },
    },
    problem: null,
  };
}

/** This machine's rule as the driver reads it: null where it is absent or has a problem. */
export function brainstormMachineOf(value: unknown): BrainstormRule | null {
  if (value === undefined) return null;
  return brainstormRuleOf(value).rule;
}

/**
 * A map of workspaces' rules as the driver reads it: each name trimmed, a blank one and a rule with a problem not read, a name
 * written twice in any case read where first readable. A map that is not one is none.
 */
export function brainstormsOf(value: unknown): Record<string, BrainstormRule> {
  if (!isObject(value)) return {};
  const held: Record<string, BrainstormRule> = {};
  for (const [name, entry] of Object.entries(value)) {
    const named = name.trim();
    if (named.length === 0) continue;
    const { rule, problem } = brainstormRuleOf(entry);
    if (problem !== null || rule === null || findName(Object.keys(held), named) !== null) continue;
    held[named] = rule;
  }

  return held;
}

/**
 * The rule standing for a question (D176 point 9): its repository's workspace's whole, else this machine's, else the default. A
 * question with no repository (`workspace` null) takes this machine's — the driver's `BrainstormRules.Resolve`, names matched
 * without case.
 */
export function brainstormFor(maps: BrainstormMaps, workspace: string | null): { source: BrainstormSource; rule: BrainstormRule } {
  const named = workspace?.trim();
  const shared = named ? atName(maps.workspaceBrainstorms, named) : undefined;
  if (shared !== undefined) return { source: 'workspace', rule: shared };
  return maps.brainstorms !== null ? { source: 'machine', rule: maps.brainstorms } : { source: 'default', rule: BRAINSTORM_DEFAULT };
}

/** A rule as the list says it: its mode; its agents in order, or that each request names its own; its minutes. */
export function brainstormListed(rule: BrainstormRule): string {
  const agents = rule.agents.length === 0 ? 'the agents each request names' : rule.agents.map((agent) => `\`${agent}\``).join(', then ');
  const m = rule.minutes;
  return `${rule.mode}; ${agents}; ${m.pass} minutes a pass, ${m.synthesis} the synthesis, ${m.challenge} the challenge, ${m.request} the request`;
}

/**
 * One change to a brainstorms rule, as both doors make it: this machine's, or a `workspace`'s; `set` what it names over the rule
 * set there, or the default (`mode`, `agents`, `minutes` one by one), or `clear`. A value of the wrong kind is kept, to be refused
 * in the driver's words, and so is a `repository`.
 */
export interface BrainstormEdit {
  workspace?: string;
  repository?: string;
  set?: Record<string, unknown>;
  clear?: boolean;
}

/**
 * The maps with one change made, or the refusal thrown in the driver's words, nothing changed — the driver's
 * `BrainstormRules.Apply`. A workspace's key keeps the spelling first written.
 */
export function applyBrainstormEdit(maps: BrainstormMaps, edit: BrainstormEdit): BrainstormMaps {
  if (typeof edit.repository === 'string' && edit.repository.trim().length > 0) throw new DaorisError(SCOPE);
  const workspace = typeof edit.workspace === 'string' && edit.workspace.trim().length > 0 ? edit.workspace.trim() : null;

  const set = isObject(edit.set) && ['mode', 'agents', 'minutes'].some((part) => Object.hasOwn(edit.set!, part)) ? edit.set : undefined;
  const clear = edit.clear === true;
  if ([set !== undefined, clear].filter(Boolean).length !== 1) throw new DaorisError(ONE_AT_A_TIME);

  const key = workspace === null ? null : findName(Object.keys(maps.workspaceBrainstorms), workspace) ?? workspace;
  const own = key === null
    ? maps.brainstorms ?? undefined
    : Object.hasOwn(maps.workspaceBrainstorms, key) ? maps.workspaceBrainstorms[key] : undefined;

  let next: BrainstormRule | null = null;
  if (!clear) {
    const given = set!;
    // A mode given as null or any other kind is no mode, refused in the driver's words; a part left out keeps the standing one.
    const mode = Object.hasOwn(given, 'mode') ? (typeof given.mode === 'string' ? given.mode : '') : own?.mode;
    const agents = Object.hasOwn(given, 'agents') ? (given.agents === null ? [null] : given.agents) : own?.agents;
    const minutes = Object.hasOwn(given, 'minutes') ? (given.minutes === null ? false : given.minutes) : undefined;
    const judged = judge(mode, agents, minutes, own?.minutes ?? BRAINSTORM_DEFAULT.minutes);
    if (judged.problem !== null) throw new DaorisError(judged.problem);
    next = judged.rule;
  }

  if (key === null) return { brainstorms: next, workspaceBrainstorms: maps.workspaceBrainstorms };
  const rest: Record<string, BrainstormRule> = Object.fromEntries(Object.entries(maps.workspaceBrainstorms).filter(([name]) => name !== key));
  return { brainstorms: maps.brainstorms, workspaceBrainstorms: next === null ? rest : { ...maps.workspaceBrainstorms, [key]: next } };
}
