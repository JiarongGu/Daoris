// The helpers rule, `helpers` and `workspaceHelpers` in `driver.json` (MIXAGENT2a-i, D163 point 5, the mixed agents design §5):
// what a helper an agent asks for does in a repository, read, refused, said and edited exactly as the driver's `HelperRules`
// does.
//
// 🔴 A TWIN with the driver's `HelperRules.cs`: the CLI and the driver share no code, so both hold ONE table, the driver's
// `Daoris.Desktop.Driver.Tests/fixtures/helper-rules.json`, cell for cell — the reading and its precedence, every refusal in
// the same words, how a rule is listed, and the edits (`.claude/knowledge/twins.md`).
//
// Pure: it reads no file, reaches no network and spawns nothing.

import { DaorisError } from './errors.ts';
import { atName, findName } from './casefold.ts';
import { normalizeWorkspace } from './remotemap.ts';

/** What a helper an agent asks for does: wait for the person's yes, start at once, or not be offered. */
export const HELPER_MODES = ['ask', 'on', 'off'] as const;
export type HelperMode = (typeof HELPER_MODES)[number];

/** A rule as kept: its mode, then the agents it may ask, in the order they are tried. */
export interface HelperRule {
  mode: HelperMode;
  agents: string[];
}

/** The two maps of `driver.json` a helpers rule lives in. */
export interface HelperMaps {
  helpers: Record<string, HelperRule>;
  workspaceHelpers: Record<string, HelperRule>;
}

/** What stands where nothing is set: each ask waits for the person's yes, and no agent is named. */
export const HELPER_DEFAULT: HelperRule = { mode: 'ask', agents: [] };

/** Where a rule was set: `default` where nothing was. */
export type HelperSource = 'repository' | 'workspace' | 'default';

const RULE_SHAPE = 'a helpers rule is an object with its `mode` and the `agents` it may ask.';
const MODE_SHAPE = '`mode` is `ask`, `on` or `off`: whether a helper an agent asks for waits for your yes, starts at once, or is not offered.';
const AGENT_SHAPE = 'each agent is an adapter\'s name, such as `codex-acp` or `dsh`.';
const SCOPE = 'a helpers rule is set for a repository or a workspace — name exactly one.';
const ONE_AT_A_TIME = 'one change at a time: set its mode or its agents, or clear.';

function isObject(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

/**
 * An entry read as a rule, with its first problem: an object whose mode is one of the three (absent or null is `ask`) and whose
 * agents are each named once (absent or null is none). The driver's `HelperRules.Read`.
 */
export function helperRuleOf(value: unknown): { rule: HelperRule | null; problem: string | null } {
  if (!isObject(value)) return { rule: null, problem: RULE_SHAPE };
  return judge(value.mode === null ? undefined : value.mode, value.agents === null ? undefined : value.agents);
}

/** The rule judged in order, mode then agents, the first problem said, or the rule as kept. */
function judge(mode: unknown, agents: unknown): { rule: HelperRule | null; problem: string | null } {
  if (mode !== undefined && (typeof mode !== 'string' || !(HELPER_MODES as readonly string[]).includes(mode))) {
    return { rule: null, problem: MODE_SHAPE };
  }

  const kept: string[] = [];
  if (agents !== undefined) {
    if (!Array.isArray(agents)) return { rule: null, problem: AGENT_SHAPE };
    for (const each of agents) {
      if (typeof each !== 'string' || each.trim().length === 0) return { rule: null, problem: AGENT_SHAPE };
      const agent = each.trim();
      if (findName(kept, agent) !== null) {
        return { rule: null, problem: `\`${agent}\` is named twice — each agent once, in the order they are tried.` };
      }

      kept.push(agent);
    }
  }

  return { rule: { mode: (mode as HelperMode | undefined) ?? 'ask', agents: kept }, problem: null };
}

/**
 * A map of helpers rules as the driver reads it: each name trimmed, a blank one and a rule with a problem not read, a name
 * written twice in any case read where first readable. A map that is not one is none.
 */
export function helpersOf(value: unknown): Record<string, HelperRule> {
  if (!isObject(value)) return {};
  const held: Record<string, HelperRule> = {};
  for (const [name, entry] of Object.entries(value)) {
    const named = name.trim();
    if (named.length === 0) continue;
    const { rule, problem } = helperRuleOf(entry);
    if (problem !== null || rule === null || findName(Object.keys(held), named) !== null) continue;
    held[named] = rule;
  }

  return held;
}

/**
 * The helpers rule standing for a repository: its own, else its workspace's (one in no workspace being in `default`), else the
 * default — the driver's `HelperRules.Resolve`, names matched without case.
 */
export function helperFor(maps: HelperMaps, repository: string, workspace: string | null): { source: HelperSource; rule: HelperRule } {
  const own = atName(maps.helpers, repository.trim());
  if (own !== undefined) return { source: 'repository', rule: own };
  const shared = atName(maps.workspaceHelpers, normalizeWorkspace(workspace));
  return shared === undefined ? { source: 'default', rule: HELPER_DEFAULT } : { source: 'workspace', rule: shared };
}

/** A rule as the list and the room say it: its mode, then the agents in the order they are tried — the driver's `HelperRules.Listed`. */
export function helperListed(rule: HelperRule): string {
  return rule.agents.length === 0 ? rule.mode : `${rule.mode}: ${rule.agents.map((agent) => `\`${agent}\``).join(', else ')}`;
}

/**
 * One change to a helpers rule, as both doors make it: for a `repository` or a `workspace`, `set` what it names over the rule set
 * there, or the default (`mode`, `agents`), or `clear`. A value of the wrong kind is kept, to be refused in the driver's words.
 */
export interface HelperEdit {
  repository?: string;
  workspace?: string;
  set?: Record<string, unknown>;
  clear?: boolean;
}

/**
 * The maps with one change made, or the refusal thrown in the driver's words, nothing changed — the driver's
 * `HelperRules.Apply`. A set changes what it names over the rule set there, or over the default; the key keeps the spelling
 * first written.
 */
export function applyHelperEdit(maps: HelperMaps, edit: HelperEdit): HelperMaps {
  const repository = typeof edit.repository === 'string' && edit.repository.trim().length > 0 ? edit.repository.trim() : null;
  const workspace = typeof edit.workspace === 'string' && edit.workspace.trim().length > 0 ? edit.workspace.trim() : null;
  if ((repository === null) === (workspace === null)) throw new DaorisError(SCOPE);

  const set = isObject(edit.set) && (Object.hasOwn(edit.set, 'mode') || Object.hasOwn(edit.set, 'agents')) ? edit.set : undefined;
  const clear = edit.clear === true;
  if ([set !== undefined, clear].filter(Boolean).length !== 1) throw new DaorisError(ONE_AT_A_TIME);

  const map: Record<string, HelperRule> = repository !== null ? maps.helpers : maps.workspaceHelpers;
  const named = (repository ?? workspace)!;
  const key = findName(Object.keys(map), named) ?? named;
  const own = Object.hasOwn(map, key) ? map[key] : undefined;

  let next: HelperRule | null = null;
  if (!clear) {
    const given = set!;
    // A mode given as null or any other kind is no mode, refused in the driver's words; a mode left out keeps the standing one.
    const mode = Object.hasOwn(given, 'mode') ? (typeof given.mode === 'string' ? given.mode : '') : own?.mode;
    const agents = Object.hasOwn(given, 'agents') ? given.agents : own?.agents;
    const judged = judge(mode, agents === null ? [null] : agents);
    if (judged.problem !== null) throw new DaorisError(judged.problem);
    next = judged.rule;
  }

  const rest: Record<string, HelperRule> = Object.fromEntries(Object.entries(map).filter(([name]) => name !== key));
  const changed: Record<string, HelperRule> = next === null ? rest : { ...map, [key]: next };
  return repository !== null
    ? { helpers: changed, workspaceHelpers: maps.workspaceHelpers }
    : { helpers: maps.helpers, workspaceHelpers: changed };
}
