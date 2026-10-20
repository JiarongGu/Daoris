// The hand-over rule, `handOver` and `workspaceHandOver` in `driver.json` (MIXAGENT1c, D163 point 3, the mixed agents design §5):
// what an agent's ask to hand its session over to another agent does in a repository, read, refused, said and edited exactly as
// the driver's `HandOverRules` does.
//
// 🔴 A TWIN with the driver's `HandOverRules.cs`: the CLI and the driver share no code, so both hold ONE table, the driver's
// `Daoris.Desktop.Driver.Tests/fixtures/hand-over-rules.json`, cell for cell — the reading and its precedence, every refusal in
// the same words, and the edits (`.claude/knowledge/twins.md`).
//
// Pure: it reads no file, reaches no network and spawns nothing.

import { DaorisError } from '../platform/errors.ts';
import { atName, findName } from '../platform/casefold.ts';
import { normalizeWorkspace } from '../service/remotemap.ts';

/** What an agent's ask to hand over does: wait for the person's yes, be taken when its turn ends, or be refused. */
export const HAND_OVER_MODES = ['ask', 'on', 'off'] as const;
export type HandOverMode = (typeof HAND_OVER_MODES)[number];

/** A rule as kept: its mode. An object, so a later field is added beside it, never in its place. */
export interface HandOverRule {
  mode: HandOverMode;
}

/** The two maps of `driver.json` a hand-over rule lives in. */
export interface HandOverMaps {
  handOver: Record<string, HandOverRule>;
  workspaceHandOver: Record<string, HandOverRule>;
}

/** What stands where nothing is set: an agent's ask waits for the person's yes. */
export const HAND_OVER_DEFAULT: HandOverRule = { mode: 'ask' };

/** Where a rule was set: `default` where nothing was. */
export type HandOverSource = 'repository' | 'workspace' | 'default';

const RULE_SHAPE = 'a hand-over rule is an object with its `mode`.';
const MODE_SHAPE = '`mode` is `ask`, `on` or `off`: whether an agent\'s ask to hand its session over waits for your yes, is taken when '
  + 'its turn ends, or is refused.';
const SCOPE = 'a hand-over rule is set for a repository or a workspace — name exactly one.';
const ONE_AT_A_TIME = 'one change at a time: set its mode, or clear.';

function isObject(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

/** The rule judged: a mode that is one of the three (absent is `ask`), or the problem said. */
function judge(mode: unknown): { rule: HandOverRule | null; problem: string | null } {
  if (mode !== undefined && (typeof mode !== 'string' || !(HAND_OVER_MODES as readonly string[]).includes(mode))) {
    return { rule: null, problem: MODE_SHAPE };
  }

  return { rule: { mode: (mode as HandOverMode | undefined) ?? 'ask' }, problem: null };
}

/**
 * An entry read as a rule, with its first problem: an object whose mode is one of the three (absent or null is `ask`); a field it
 * has no word for is no problem. The driver's `HandOverRules.Read`.
 */
export function handOverRuleOf(value: unknown): { rule: HandOverRule | null; problem: string | null } {
  if (!isObject(value)) return { rule: null, problem: RULE_SHAPE };
  return judge(value.mode === null ? undefined : value.mode);
}

/**
 * A map of hand-over rules as the driver reads it: each name trimmed, a blank one and a rule with a problem not read, a name
 * written twice in any case read where first readable. A map that is not one is none.
 */
export function handOversOf(value: unknown): Record<string, HandOverRule> {
  if (!isObject(value)) return {};
  const held: Record<string, HandOverRule> = {};
  for (const [name, entry] of Object.entries(value)) {
    const named = name.trim();
    if (named.length === 0) continue;
    const { rule, problem } = handOverRuleOf(entry);
    if (problem !== null || rule === null || findName(Object.keys(held), named) !== null) continue;
    held[named] = rule;
  }

  return held;
}

/**
 * The hand-over rule standing for a repository: its own, else its workspace's (one in no workspace being in `default`), else the
 * default — the driver's `HandOverRules.Resolve`, names matched without case.
 */
export function handOverFor(maps: HandOverMaps, repository: string, workspace: string | null): { source: HandOverSource; rule: HandOverRule } {
  const own = atName(maps.handOver, repository.trim());
  if (own !== undefined) return { source: 'repository', rule: own };
  const shared = atName(maps.workspaceHandOver, normalizeWorkspace(workspace));
  return shared === undefined ? { source: 'default', rule: HAND_OVER_DEFAULT } : { source: 'workspace', rule: shared };
}

/**
 * One change to a hand-over rule, as every door makes it: for a `repository` or a `workspace`, `set` its `mode`, or `clear`. A
 * value of the wrong kind is kept, to be refused in the driver's words.
 */
export interface HandOverEdit {
  repository?: string;
  workspace?: string;
  set?: Record<string, unknown>;
  clear?: boolean;
}

/**
 * The maps with one change made, or the refusal thrown in the driver's words, nothing changed — the driver's
 * `HandOverRules.Apply`. The key keeps the spelling first written.
 */
export function applyHandOverEdit(maps: HandOverMaps, edit: HandOverEdit): HandOverMaps {
  const repository = typeof edit.repository === 'string' && edit.repository.trim().length > 0 ? edit.repository.trim() : null;
  const workspace = typeof edit.workspace === 'string' && edit.workspace.trim().length > 0 ? edit.workspace.trim() : null;
  if ((repository === null) === (workspace === null)) throw new DaorisError(SCOPE);

  const set = isObject(edit.set) && Object.hasOwn(edit.set, 'mode') ? edit.set : undefined;
  const clear = edit.clear === true;
  if ([set !== undefined, clear].filter(Boolean).length !== 1) throw new DaorisError(ONE_AT_A_TIME);

  const map: Record<string, HandOverRule> = repository !== null ? maps.handOver : maps.workspaceHandOver;
  const named = (repository ?? workspace)!;
  const key = findName(Object.keys(map), named) ?? named;

  let next: HandOverRule | null = null;
  if (!clear) {
    // A mode given as null or any other kind is no mode, refused in the driver's words.
    const judged = judge(typeof set!.mode === 'string' ? set!.mode : '');
    if (judged.problem !== null) throw new DaorisError(judged.problem);
    next = judged.rule;
  }

  const rest: Record<string, HandOverRule> = Object.fromEntries(Object.entries(map).filter(([name]) => name !== key));
  const changed: Record<string, HandOverRule> = next === null ? rest : { ...map, [key]: next };
  return repository !== null
    ? { handOver: changed, workspaceHandOver: maps.workspaceHandOver }
    : { handOver: maps.handOver, workspaceHandOver: changed };
}

/** What `driver handover` says of a mode as it is set. */
export function handOverModeSays(mode: string): string {
  return mode === 'ask'
    ? 'An agent\'s ask to hand its session over waits for your yes or no on the session\'s page.'
    : mode === 'on'
      ? 'An agent\'s ask to hand its session over is taken when its turn ends: it goes on with the next agent in your order.'
      : 'An agent\'s ask to hand its session over is refused: only your own press hands one over.';
}
