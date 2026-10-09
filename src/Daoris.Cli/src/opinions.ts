// The second-opinion rule, `opinions` and `workspaceOpinions` in `driver.json` (XAGENT1a, D155 point 3, the second-agent design
// §2.2–§2.6): which other agent reads a repository's work before it lands, read, refused, said and edited exactly as the
// driver's `OpinionRules` does.
//
// 🔴 A TWIN with the driver's `OpinionRules.cs`: the CLI and the driver share no code, so both hold ONE table, the driver's
// `Daoris.Desktop.Driver.Tests/fixtures/opinion-rules.json`, cell for cell — the reading and its precedence, every refusal in
// the same words, each door's sentences and what each says after them of the gate, the edits, and which reviewers are the
// working agent's own family (`.claude/knowledge/twins.md`).
//
// Pure: it reads no file, reaches no network and spawns nothing. A family is read from the toolchain table this build
// declares and the plugin catalogue a caller hands it, as the driver reads the machine's adapters, a plugin's among them
// (XAGENT1b2). The driver's gate reads the rule since XAGENT1f, and each door says what it does (`opinionGate`, XAGENT1f4).

import { DaorisError } from './errors.ts';
import { atName, findName, sameName } from './casefold.ts';
import type { PluginCatalog } from './plugins.ts';
import { normalizeWorkspace } from './remotemap.ts';
import { TOOLCHAINS } from './toolchain.ts';

/** A rule as kept: its occasions, landing first, and its reviewers in order, then each switch only where it differs from absent. */
export interface OpinionRule {
  on: string[];
  reviewers: string[];
  required?: true;
  verify?: true;
  minutes?: number;
  recheck?: false;
}

/** A repository's entry: a rule, or `false`, which says there is no second opinion whatever its workspace says. */
export type OpinionSetting = OpinionRule | false;

/** The two maps of `driver.json` a second-opinion rule lives in. */
export interface OpinionMaps {
  opinions: Record<string, OpinionSetting>;
  workspaceOpinions: Record<string, OpinionRule>;
}

/** The occasions a rule may name, in the order a rule keeps them (design §2.1) — the driver's `OpinionRules.Occasions`. */
export const OPINION_OCCASIONS = ['landing', 'steps'] as const;

/** One pass's bound where the rule names none (design §2.3) — the driver's `OpinionRules.DefaultMinutes`. */
export const OPINION_DEFAULT_MINUTES = 20;

/**
 * What each door said after a rule's sentences until the gate read it (XAGENT1a–XAGENT1f), untrue since XAGENT1f. No door says
 * it now (XAGENT1f4, `opinionGate`); it is kept so the tests that hold its absence name it, the workflow table's among them —
 * the driver's `OpinionRules.DeclaredOnly`.
 */
export const OPINION_DECLARED_ONLY = 'Declared only: nothing reads it yet, so no reviewer is chosen and no landing waits for it.';

/**
 * Said by each door after the sentences of a rule that reads at landing (XAGENT1f4, through `opinionGate`): what the landing's
 * gate does with work that waits for another agent's reading since XAGENT1f, and the person's two terminal doors that answer it.
 * The driver's `OpinionRules.Waiting`, held to the shared table's `gate` rows.
 */
export const OPINION_WAITING = 'Where work here waits for another agent\'s reading, it lands only once that reading is settled, '
  + 'or once you go on without it, `daoris-driver opinion anyway <session>`, or say you looked yourself, '
  + '`daoris-driver opinion myself <session>`.';

/** Said after the sentences of a rule that reads before each next step (XAGENT1f4) — the driver's `OpinionRules.StepWaiting`. */
export const OPINION_STEP_WAITING = 'Where a chain\'s next step here waits for another agent\'s reading of the step before it, it '
  + 'starts only once that reading is settled.';

/**
 * Said after the sentences of a rule that lets a reviewer run what is declared safe (XAGENT1f4): nothing hands a reviewer the
 * repository's declared safe commands yet (XAGENT1f3) — the driver's `OpinionRules.SafeNotHanded`.
 */
export const OPINION_SAFE_NOT_HANDED = 'What this repository declares safe is not handed to a reviewer yet.';

/** What stands where nothing is set anywhere (design §2.6): today's behaviour, said as such. */
export const OPINION_NONE_SET = 'None: no other agent reads work here.';

const FEWEST_MINUTES = 5;
const MOST_MINUTES = 120;
const RULE_SHAPE = 'a second-opinion rule is its reviewers and when they read, or `false` for none here.';
const NO_REVIEWERS = 'a second-opinion rule names at least one reviewer: an adapter, in the order they are tried, such as `codex-acp`.';
const REVIEWER_SHAPE = 'each reviewer is an adapter\'s name, such as `codex-acp` or `dsh`.';
const ON_SHAPE = '`on` is `landing`, `steps` or both: when a reviewer reads work here by itself.';
const MINUTES_SHAPE = '`minutes` is a whole number from 5 to 120: how long one pass may take, 20 when absent.';
const SCOPE = 'a second-opinion rule is set for a repository or a workspace — name exactly one.';
const ONE_AT_A_TIME = 'one change at a time: set its reviewers and how they read, say none, or clear.';
const NONE_WORKSPACE = '`none` is a repository\'s: a workspace with no second opinion sets none, and `--clear` takes its rule away.';
const NONE_HERE = 'No second opinion here, whatever its workspace says: no other agent reads work here before it lands.';
const COPY = 'in a copy of its own that nothing is taken back from; its findings go to the session that did the work, and to you.';

/** A rule as an entry or a door spells it, before it is judged: `on` and `minutes` undefined are absent. */
interface Spelled {
  reviewers: unknown;
  on: unknown;
  required: boolean;
  verify: boolean;
  minutes: unknown;
  recheck: boolean;
}

function isObject(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

/**
 * A repository's entry read as a rule, with its first problem: `false` is none here; anything else must be an object naming
 * at least one reviewer, then its occasions and its minutes judged. Only JSON `true` requires or verifies, only JSON `false`
 * turns the recheck off, and what a rule has no field for is not kept. The driver's `OpinionRules.Read`.
 */
export function opinionRuleOf(value: unknown): { rule: OpinionSetting | null; problem: string | null } {
  if (value === false) return { rule: false, problem: null };
  if (!isObject(value)) return { rule: null, problem: RULE_SHAPE };
  return judge({
    reviewers: value.reviewers,
    // Null is absent, as the driver reads it.
    on: value.on === null ? undefined : value.on,
    required: value.required === true,
    verify: value.verify === true,
    minutes: value.minutes === null ? undefined : value.minutes,
    recheck: value.recheck !== false,
  });
}

/** The rule judged in order — reviewers, occasions, minutes — the first problem said, or the rule as kept. */
function judge(spelled: Spelled): { rule: OpinionRule | null; problem: string | null } {
  const { reviewers: named, on, minutes } = spelled;
  if (!Array.isArray(named) || named.length === 0) return { rule: null, problem: NO_REVIEWERS };
  const reviewers: string[] = [];
  for (const each of named) {
    if (typeof each !== 'string' || each.trim().length === 0) return { rule: null, problem: REVIEWER_SHAPE };
    const reviewer = each.trim();
    if (reviewers.some((kept) => sameName(kept, reviewer))) {
      return { rule: null, problem: `\`${reviewer}\` is named twice — each reviewer once, in the order they are tried.` };
    }
    reviewers.push(reviewer);
  }

  const occasions = new Set<string>();
  if (on === undefined) {
    occasions.add('landing');
  } else {
    if (!Array.isArray(on) || on.length === 0) return { rule: null, problem: ON_SHAPE };
    for (const each of on) {
      if (typeof each !== 'string') return { rule: null, problem: ON_SHAPE };
      if (!(OPINION_OCCASIONS as readonly string[]).includes(each)) {
        return { rule: null, problem: `\`${each}\` is not an occasion — \`landing\`, \`steps\` or both.` };
      }
      if (occasions.has(each)) return { rule: null, problem: `\`${each}\` is named twice in \`on\` — \`landing\`, \`steps\` or both.` };
      occasions.add(each);
    }
  }

  if (minutes !== undefined
    && (typeof minutes !== 'number' || !Number.isInteger(minutes) || minutes < FEWEST_MINUTES || minutes > MOST_MINUTES)) {
    return { rule: null, problem: MINUTES_SHAPE };
  }

  return {
    rule: {
      on: OPINION_OCCASIONS.filter((occasion) => occasions.has(occasion)),
      reviewers,
      ...(spelled.required ? { required: true } : {}),
      ...(spelled.verify ? { verify: true } : {}),
      ...(minutes !== undefined ? { minutes: minutes as number } : {}),
      ...(spelled.recheck ? {} : { recheck: false }),
    },
    problem: null,
  };
}

/**
 * A map of second-opinion rules as the driver reads it: each name trimmed, a blank one not read; a rule with a problem, and
 * `true`, not read; `false` read only where `allowNone` (a repository's, never a workspace's); a name written twice in any
 * case read where first readable. A map that is not one is none.
 */
export function opinionsOf(value: unknown, allowNone: true): Record<string, OpinionSetting>;
export function opinionsOf(value: unknown, allowNone: false): Record<string, OpinionRule>;
export function opinionsOf(value: unknown, allowNone: boolean): Record<string, OpinionSetting> {
  if (!isObject(value)) return {};
  const held: Record<string, OpinionSetting> = {};
  for (const [name, entry] of Object.entries(value)) {
    const named = name.trim();
    if (named.length === 0 || (entry === false && !allowNone)) continue;
    const { rule, problem } = opinionRuleOf(entry);
    if (problem !== null || rule === null || findName(Object.keys(held), named) !== null) continue;
    held[named] = rule;
  }

  return held;
}

/** A rule as it resolves for a repository (design §2.3), and where it was set. */
export interface ResolvedOpinion {
  source: 'repository' | 'workspace';
  rule: OpinionSetting;
}

/**
 * The second-opinion rule standing for a repository: its own (a rule or none), else its workspace's (one in no workspace
 * being in `default`), else null, which is nothing set anywhere — the driver's `OpinionRules.Resolve`, names matched
 * without case.
 */
export function opinionFor(maps: OpinionMaps, repository: string, workspace: string | null): ResolvedOpinion | null {
  const own = atName(maps.opinions, repository.trim());
  if (own !== undefined) return { source: 'repository', rule: own };
  const shared = atName(maps.workspaceOpinions, normalizeWorkspace(workspace));
  return shared === undefined ? null : { source: 'workspace', rule: shared };
}

/** Names in backticks, in the order they are tried: `a`, `a, else b`, `a, else b, else c`. */
function orElse(names: readonly string[]): string {
  return names.map((name) => `\`${name}\``).join(', else ');
}

/** Names in backticks, the last after `and`: `a`, `a and b`, `a, b and c`. */
function and(names: readonly string[]): string {
  const ticked = names.map((name) => `\`${name}\``);
  return ticked.length <= 1 ? ticked.join('') : `${ticked.slice(0, -1).join(', ')} and ${ticked[ticked.length - 1]}`;
}

/**
 * What each door says as a rule is set (design §2.5): when its reviewers read and where their findings go, what they may
 * run, what happens when none can read, the bound of a pass, a recheck turned off, and which of them are the working agent's
 * own family — the driver's `OpinionRules.Says`, word for word.
 *
 * @param sameAgent The reviewers of the rule that are the working agent's own family (`sameAgentOf`).
 */
export function opinionSays(rule: OpinionSetting, sameAgent: readonly string[]): string[] {
  if (rule === false) return [NONE_HERE];
  // Several reviewers are set apart by commas: "`codex-acp`, else `dsh`, reads it".
  const reviewers = orElse(rule.reviewers) + (rule.reviewers.length > 1 ? ',' : '');
  const said: string[] = [];
  if (rule.on.includes('landing')) {
    said.push(`Before work here lands, ${reviewers} reads it, ${COPY}`);
    if (rule.on.includes('steps')) said.push('It reads each step\'s work here too, before the chain\'s next step starts.');
  } else {
    said.push(`Before a chain's next step starts, ${reviewers} reads the work of the step before it here, ${COPY}`);
  }

  if (rule.verify) said.push('It may build and run what this repository declares safe, in that copy.');
  said.push(rule.required
    ? 'If no reviewer can read it, the work waits for you.'
    : 'If no reviewer can read it, you are told so, and nothing waits.');
  said.push(`One pass takes at most ${rule.minutes ?? OPINION_DEFAULT_MINUTES} minutes.`);
  if (rule.recheck === false) said.push('Commits made in answer to its findings are not read again.');

  const same = rule.reviewers.filter((reviewer) => sameAgent.some((each) => sameName(each, reviewer)));
  if (same.length > 0) {
    said.push(`${and(same)} ${same.length === 1 ? 'is' : 'are'} the same agent as the one that does the work here: a fresh `
      + 'conversation of the same agent is not an independent reading, and each opinion says so.');
  }

  return said;
}

/**
 * What each door says after the rule's own sentences (XAGENT1f4): what the gate does with work that waits for another agent's
 * reading, at landing and before a chain's next step, as the rule's occasions name them, then that what is declared safe is not
 * handed to a reviewer yet where the rule lets one run it; nothing after none here, where nothing waits. The driver's
 * `OpinionRules.Gate`, by the shared table's `gate` rows.
 */
export function opinionGate(rule: OpinionSetting): string[] {
  if (rule === false) return [];
  return [
    ...(rule.on.includes('landing') ? [OPINION_WAITING] : []),
    ...(rule.on.includes('steps') ? [OPINION_STEP_WAITING] : []),
    ...(rule.verify ? [OPINION_SAFE_NOT_HANDED] : []),
  ];
}

/** A declaration as the driver's `AgentFamily` keeps it: none where it is absent or blank, else trimmed. */
function declaredOf(text: string | null | undefined): string | null {
  return text?.trim() || null;
}

/**
 * An adapter's owner (AGT7) and declared maker, as the driver's `AgentFamily.Of` reads them, the name trimmed: from the
 * toolchain table this build declares, else from the harness a contributing plugin of `plugins` declares (XAGENT1b2), its
 * `accountOf` and its plugin's word for its maker; a name neither declares owns itself and declares no maker.
 */
function familyOf(name: string, plugins: PluginCatalog | null): { owner: string; maker: string | null } {
  const given = name.trim();
  const key = findName(Object.keys(TOOLCHAINS), given);
  if (key !== null) {
    const toolchain = TOOLCHAINS[key]!;
    return { owner: toolchain.accountOf || key, maker: declaredOf(toolchain.maker) };
  }

  for (const plugin of plugins?.contributing ?? []) {
    const harness = plugin.manifest.harnesses.find((each) => sameName(each.name, given));
    if (harness !== undefined) return { owner: harness.accountOf || harness.name, maker: declaredOf(harness.maker) };
  }

  return { owner: given, maker: null };
}

/**
 * Whether two adapters are one family (design §3.1): they run as the same agent's accounts, or both declare the same maker —
 * the driver's `OpinionRules.OneFamily`. A name with no toolchain here and no plugin harness is its own family only by its
 * own name.
 *
 * @param plugins The machine's plugins, whose contributing harnesses count as the driver's choice counts them (XAGENT1b2);
 *   null judges by the toolchain table alone.
 */
export function oneFamily(a: string, b: string, plugins: PluginCatalog | null = null): boolean {
  const first = familyOf(a, plugins);
  const second = familyOf(b, plugins);
  return sameName(first.owner, second.owner)
    || (first.maker !== null && second.maker !== null && sameName(first.maker, second.maker));
}

/**
 * The reviewers of `rule` that are the family of `working`, the agent this machine's work runs on, over the toolchain table
 * and `plugins` — `OpinionRules.SameAgent`.
 */
export function sameAgentOf(rule: OpinionRule, working: string, plugins: PluginCatalog | null = null): string[] {
  return rule.reviewers.filter((reviewer) => oneFamily(working, reviewer, plugins));
}

/** A rule in a line of `driver list`: its reviewers in order, when they read, what they may run, and the bound of a pass. */
export function opinionListed(rule: OpinionSetting): string {
  if (rule === false) return 'none here, whatever its workspace says';
  return [
    rule.reviewers.join(', else '),
    rule.on.includes('landing') ? (rule.on.includes('steps') ? 'before landing and each next step' : 'before landing') : 'before each next step',
    ...(rule.required ? ['required'] : []),
    ...(rule.verify ? ['may build and run what is declared safe'] : []),
    `at most ${rule.minutes ?? OPINION_DEFAULT_MINUTES} minutes a pass`,
    ...(rule.recheck === false ? ['no recheck'] : []),
  ].join('; ');
}

/**
 * One change to a second-opinion rule, as both doors make it (design §2.5): for a `repository` or a `workspace`, `set` what
 * it names over the rule set there (`reviewers`, `on`, `required`, `verify`, `minutes`, `recheck`), say a repository has
 * `none`, or `clear`. A value of the wrong kind is kept, to be refused in the driver's words.
 */
export interface OpinionEdit {
  repository?: string;
  workspace?: string;
  set?: Record<string, unknown>;
  none?: boolean;
  clear?: boolean;
}

/** Whether a set names anything, as the driver's `OpinionSet.Names` reads it: a list or minutes given at all, a switch as a boolean. */
function names(set: Record<string, unknown>): boolean {
  return ['reviewers', 'on', 'minutes'].some((key) => Object.hasOwn(set, key))
    || ['required', 'verify', 'recheck'].some((key) => typeof set[key] === 'boolean');
}

/**
 * The maps with one change made, or the refusal thrown in the driver's words, nothing changed — the driver's
 * `OpinionRules.Apply`. A set changes what it names over the rule set there; a repository's rule starts afresh rather than
 * from its workspace's, since it replaces that whole; the key keeps the spelling first written.
 */
export function applyOpinionEdit(maps: OpinionMaps, edit: OpinionEdit): OpinionMaps {
  const repository = typeof edit.repository === 'string' && edit.repository.trim().length > 0 ? edit.repository.trim() : null;
  const workspace = typeof edit.workspace === 'string' && edit.workspace.trim().length > 0 ? edit.workspace.trim() : null;
  if ((repository === null) === (workspace === null)) throw new DaorisError(SCOPE);

  const set = isObject(edit.set) && names(edit.set) ? edit.set : undefined;
  const none = edit.none === true;
  const clear = edit.clear === true;
  if ([set !== undefined, none, clear].filter(Boolean).length !== 1) throw new DaorisError(ONE_AT_A_TIME);
  if (none && workspace !== null) throw new DaorisError(NONE_WORKSPACE);

  const map: Record<string, OpinionSetting> = repository !== null ? maps.opinions : maps.workspaceOpinions;
  const named = (repository ?? workspace)!;
  const key = findName(Object.keys(map), named) ?? named;
  const whose = repository !== null ? `\`${key}\`` : `the workspace \`${key}\``;
  const standing = Object.hasOwn(map, key) ? map[key] : undefined;
  const own = standing === undefined || standing === false ? null : standing;

  let next: OpinionSetting | null;
  if (clear) {
    next = null;
  } else if (none) {
    next = false;
  } else {
    const given = set!;
    const has = (field: string) => Object.hasOwn(given, field);
    if (own === null && !has('reviewers')) {
      throw new DaorisError(`${whose} has no second-opinion rule of its own — name its reviewers first.`);
    }

    const switched = (field: string, was: boolean) => (typeof given[field] === 'boolean' ? given[field] as boolean : was);
    const judged = judge({
      reviewers: has('reviewers') ? given.reviewers : own!.reviewers,
      on: has('on') ? given.on : own?.on,
      required: switched('required', own?.required === true),
      verify: switched('verify', own?.verify === true),
      minutes: has('minutes') ? given.minutes : own?.minutes,
      recheck: switched('recheck', own?.recheck !== false),
    });
    if (judged.problem !== null) throw new DaorisError(judged.problem);
    next = judged.rule;
  }

  const rest: Record<string, OpinionSetting> = Object.fromEntries(Object.entries(map).filter(([name]) => name !== key));
  const changed: Record<string, OpinionSetting> = next === null ? rest : { ...map, [key]: next };
  return repository !== null
    ? { opinions: changed, workspaceOpinions: maps.workspaceOpinions }
    : { opinions: maps.opinions, workspaceOpinions: changed as Record<string, OpinionRule> };
}
