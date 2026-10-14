// The commit rule, `commits` and `workspaceCommits` in `driver.json` (PROCESS1a, D164's amendment point 1): how work is
// committed in a repository, read, refused, said and edited exactly as the driver's `CommitRules` does.
//
// 🔴 A TWIN with the driver's `CommitRules.cs`: the CLI and the driver share no code, so both hold ONE table, the driver's
// `Daoris.Desktop.Driver.Tests/fixtures/commit-rules.json`, cell for cell — the reading and its precedence, every refusal in
// the same words, how a rule is listed, and the edits (`.claude/knowledge/twins.md`).
//
// Pure: it reads no file, reaches no network and spawns nothing.

import { DaorisError } from '../platform/errors.ts';
import { atName, findName } from '../platform/casefold.ts';
import { normalizeWorkspace } from '../service/remotemap.ts';

/** How work is committed: each piece as it passes its checks, after the person's yes, or once at the end. */
export const COMMIT_MODES = ['as-you-go', 'ask', 'at-end'] as const;
export type CommitMode = (typeof COMMIT_MODES)[number];

/** The longest convention kept, in characters. */
export const COMMIT_CONVENTION_LIMIT = 400;

/** A rule as kept: its mode, and the person's words on how a commit is written where they gave any. */
export interface CommitRule {
  mode: CommitMode;
  convention?: string;
}

/** The two maps of `driver.json` a commit rule lives in. */
export interface CommitMaps {
  commits: Record<string, CommitRule>;
  workspaceCommits: Record<string, CommitRule>;
}

/** What stands where nothing is set: commits as the work goes, with no convention named. */
export const COMMIT_DEFAULT: CommitRule = { mode: 'as-you-go' };

/** Where a rule was set: `default` where nothing was. */
export type CommitSource = 'repository' | 'workspace' | 'default';

const RULE_SHAPE = 'a commit rule is an object with its `mode` and an optional `convention`.';
const MODE_SHAPE = '`mode` is `as-you-go`, `ask` or `at-end`: whether commits are made as the work goes, wait for your yes first, or are made once at the end.';
const CONVENTION_SHAPE = '`convention` is your words on how a commit is written: one paragraph with no line break, at most 400 characters.';
const SCOPE = 'a commit rule is set for a repository or a workspace — name exactly one.';
const ONE_AT_A_TIME = 'one change at a time: set its mode or its convention, or clear.';

function isObject(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

/**
 * An entry read as a rule, with its first problem: an object whose mode is one of the three (absent or null is `as-you-go`) and
 * whose convention is one short paragraph (absent, null or blank is none). The driver's `CommitRules.Read`.
 */
export function commitRuleOf(value: unknown): { rule: CommitRule | null; problem: string | null } {
  if (!isObject(value)) return { rule: null, problem: RULE_SHAPE };
  return judge(value.mode === null ? undefined : value.mode, value.convention === null ? undefined : value.convention);
}

/** The rule judged in order, mode then convention, the first problem said, or the rule as kept. */
function judge(mode: unknown, convention: unknown): { rule: CommitRule | null; problem: string | null } {
  if (mode !== undefined && (typeof mode !== 'string' || !(COMMIT_MODES as readonly string[]).includes(mode))) {
    return { rule: null, problem: MODE_SHAPE };
  }

  let words: string | undefined;
  if (convention !== undefined) {
    if (typeof convention !== 'string') return { rule: null, problem: CONVENTION_SHAPE };
    const trimmed = convention.trim();
    if (trimmed.length > COMMIT_CONVENTION_LIMIT || /[\r\n]/.test(trimmed)) return { rule: null, problem: CONVENTION_SHAPE };
    if (trimmed.length > 0) words = trimmed;
  }

  const kept = { mode: (mode as CommitMode | undefined) ?? 'as-you-go' } as CommitRule;
  if (words !== undefined) kept.convention = words;
  return { rule: kept, problem: null };
}

/**
 * A map of commit rules as the driver reads it: each name trimmed, a blank one and a rule with a problem not read, a name
 * written twice in any case read where first readable. A map that is not one is none.
 */
export function commitsOf(value: unknown): Record<string, CommitRule> {
  if (!isObject(value)) return {};
  const held: Record<string, CommitRule> = {};
  for (const [name, entry] of Object.entries(value)) {
    const named = name.trim();
    if (named.length === 0) continue;
    const { rule, problem } = commitRuleOf(entry);
    if (problem !== null || rule === null || findName(Object.keys(held), named) !== null) continue;
    held[named] = rule;
  }

  return held;
}

/**
 * The commit rule standing for a repository: its own, else its workspace's (one in no workspace being in `default`), else the
 * default — the driver's `CommitRules.Resolve`, names matched without case.
 */
export function commitFor(maps: CommitMaps, repository: string, workspace: string | null): { source: CommitSource; rule: CommitRule } {
  const own = atName(maps.commits, repository.trim());
  if (own !== undefined) return { source: 'repository', rule: own };
  const shared = atName(maps.workspaceCommits, normalizeWorkspace(workspace));
  return shared === undefined ? { source: 'default', rule: COMMIT_DEFAULT } : { source: 'workspace', rule: shared };
}

/** A rule as the list says it: its mode, then its convention where it has one — the driver's `CommitRules.Listed`. */
export function commitListed(rule: CommitRule): string {
  return rule.convention === undefined ? rule.mode : `${rule.mode}: ${rule.convention}`;
}

/**
 * One change to a commit rule, as both doors make it: for a `repository` or a `workspace`, `set` what it names over the rule set
 * there, or the default (`mode`, `convention`; an empty convention takes it away), or `clear`. A value of the wrong kind is kept,
 * to be refused in the driver's words.
 */
export interface CommitEdit {
  repository?: string;
  workspace?: string;
  set?: Record<string, unknown>;
  clear?: boolean;
}

/**
 * The maps with one change made, or the refusal thrown in the driver's words, nothing changed — the driver's
 * `CommitRules.Apply`. A set changes what it names over the rule set there, or over the default; the key keeps the spelling
 * first written.
 */
export function applyCommitEdit(maps: CommitMaps, edit: CommitEdit): CommitMaps {
  const repository = typeof edit.repository === 'string' && edit.repository.trim().length > 0 ? edit.repository.trim() : null;
  const workspace = typeof edit.workspace === 'string' && edit.workspace.trim().length > 0 ? edit.workspace.trim() : null;
  if ((repository === null) === (workspace === null)) throw new DaorisError(SCOPE);

  const set = isObject(edit.set) && (Object.hasOwn(edit.set, 'mode') || Object.hasOwn(edit.set, 'convention')) ? edit.set : undefined;
  const clear = edit.clear === true;
  if ([set !== undefined, clear].filter(Boolean).length !== 1) throw new DaorisError(ONE_AT_A_TIME);

  const map: Record<string, CommitRule> = repository !== null ? maps.commits : maps.workspaceCommits;
  const named = (repository ?? workspace)!;
  const key = findName(Object.keys(map), named) ?? named;
  const own = Object.hasOwn(map, key) ? map[key] : undefined;

  let next: CommitRule | null = null;
  if (!clear) {
    const given = set!;
    // A mode or convention given as null or any other kind is refused in the driver's words; one left out keeps the standing one.
    const mode = Object.hasOwn(given, 'mode') ? (typeof given.mode === 'string' ? given.mode : '') : own?.mode;
    const convention = Object.hasOwn(given, 'convention') ? (given.convention ?? false) : own?.convention;
    const judged = judge(mode, convention);
    if (judged.problem !== null) throw new DaorisError(judged.problem);
    next = judged.rule;
  }

  const rest: Record<string, CommitRule> = Object.fromEntries(Object.entries(map).filter(([name]) => name !== key));
  const changed: Record<string, CommitRule> = next === null ? rest : { ...map, [key]: next };
  return repository !== null
    ? { commits: changed, workspaceCommits: maps.workspaceCommits }
    : { commits: maps.commits, workspaceCommits: changed };
}
