// The review rule, `reviews` and `workspaceReviews` in `driver.json` (REVIEWENV1a, D154 point 2, the review-environment
// design §1.1–§1.3, §1.7): where a repository's work runs for the person to look at before it lands, read, refused, said and
// edited exactly as the driver's `ReviewRules` does.
//
// 🔴 A TWIN with the driver's `ReviewRules.cs`: the CLI and the driver share no code, so both hold ONE table,
// the driver's `Daoris.Desktop.Driver.Tests/fixtures/review-rules.json`, cell for cell — the reading and its precedence, every refusal in the same words, each
// door's sentences, the edits, and what a checkout holding a procedure means (`.claude/knowledge/twins.md`).
//
// Pure but for `holdsProcedure`, which reads a checkout's file system. It reaches no network and spawns nothing: the
// registry's checkouts are handed in by `cli/driver.ts` through `service.ts`, the one module that may (D50).

import { lstatSync } from 'node:fs';
import { join } from 'node:path';
import { DaorisError } from './errors.ts';
import { atName, findName, sameName } from './casefold.ts';
import { normalizeWorkspace } from './remotemap.ts';

/** One review environment, as kept: its name, kind and procedure, a local one's address, and a local one's command. */
export interface ReviewEnvironment {
  name: string;
  kind: string;
  procedure: string;
  address?: string;
  run?: string;
}

/** A rule as kept: `required` written only when on, then the environments, the first the default. */
export interface ReviewRule {
  required?: true;
  environments: ReviewEnvironment[];
}

/** A repository's entry: a rule, or `false`, which says it has none whatever its workspace says. */
export type ReviewSetting = ReviewRule | false;

/** The two maps of `driver.json` a review rule lives in. */
export interface ReviewMaps {
  reviews: Record<string, ReviewSetting>;
  workspaceReviews: Record<string, ReviewRule>;
}

/** The kinds a review environment may be (design §1.1) — the driver's `ReviewRules.Kinds`. */
export const REVIEW_KINDS = ['local', 'deployed'] as const;

/**
 * The words a name reads as production by, whole or as a word between its dashes — a deliberate copy of the place words the
 * service's `GoAheads.cs` reads `production` by, and of the driver's `ReviewRules.Production`.
 */
export const PRODUCTION_WORDS = ['production', 'prod', 'prd', 'live'] as const;

/**
 * Said by each door after what the rule lets a step do (REVIEWENV1c2): what the landing's gate does with it since REVIEWENV1c,
 * the person's verdict by the terminal's door, and that the intake composes no set-up step yet (REVIEWENV1f). The driver's
 * `ReviewRules.Waiting`, held to the shared table's `gate` rows.
 */
export const REVIEW_WAITING = 'Where work here waits for your review, it lands only once you say it is reviewed, '
  + '`daoris-driver quest review <quest> reviewed`, or skip the review, `daoris-driver quest review <quest> skip`. '
  + 'No set-up step is composed for you yet.';

/** What stands where nothing is set anywhere (design §1.8): today's behaviour, said as such. */
export const REVIEW_NONE_SET = 'None: work is offered to land once its quest is done.';

const RULE_SHAPE = 'a review rule is its environments and whether it is required, or `false` for none here.';
const NO_ENVIRONMENTS = 'a review rule names at least one environment; the first is the default.';
const ENVIRONMENT_SHAPE = 'each environment is an object: its `name`, `kind` and `procedure`, and a local one\'s `address`.';
const NAME_RULE = 'lower-case letters, digits and dashes, at most 32 characters, such as `local` or `dev`.';
const KIND_MISSING = 'an environment\'s `kind` is `local` or `deployed`.';
const PROCEDURE_MISSING = 'an environment names its `procedure`: the path, in the repository, of the document or skill that '
  + 'says how work reaches it, such as `README.md`.';
const ADDRESS_MISSING = 'a local environment needs its `address`: where the app normally runs, such as `http://localhost:4200`, '
  + 'the only place a set-up step may show the work.';
const RUN_DEPLOYED = 'only a local environment carries `run`: a deployed one is reached by its procedure, under your go-aheads.';
const RUN_SHAPE = '`run` is one command on one line: the one that starts work needing a process of its own.';
const SCOPE = 'a review rule is set for a repository or a workspace — name exactly one.';
const ONE_AT_A_TIME = 'one change at a time: add an environment (and whether it is required), drop one, say none, say whether '
  + 'it is required, or clear.';
const NONE_WORKSPACE = '`none` is a repository\'s: a workspace with no review environment sets none, and `--clear` takes its rule away.';
const NONE_HERE = 'No review environment here, whatever its workspace says: work is offered to land once its quest is done.';

const NAME_SHAPE = /^[a-z0-9]+(?:-[a-z0-9]+)*$/;
const ADDRESS_SHAPE = /^https?:\/\/(?:\[[0-9A-Fa-f:.]+\]|[A-Za-z0-9](?:[A-Za-z0-9-]*[A-Za-z0-9])?(?:\.[A-Za-z0-9](?:[A-Za-z0-9-]*[A-Za-z0-9])?)*)(?::([0-9]{1,5}))?\/?$/;

/** An environment as an entry spells it, before it is judged: a field that is not text is absent. */
interface Spelled {
  name?: string | undefined;
  kind?: string | undefined;
  procedure?: string | undefined;
  address?: string | undefined;
  run?: string | undefined;
}

function isObject(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

const text = (value: unknown): string | undefined => (typeof value === 'string' ? value : undefined);

/**
 * A repository's entry read as a rule, with its first problem: `false` is none here; anything else must be an object naming
 * at least one environment, each judged in order. A field that is not text is absent; only JSON `true` is required; what
 * an environment has no field for is not kept. The driver's `ReviewRules.Read`.
 */
export function reviewRuleOf(value: unknown): { rule: ReviewSetting | null; problem: string | null } {
  if (value === false) return { rule: false, problem: null };
  if (!isObject(value)) return { rule: null, problem: RULE_SHAPE };
  const { required, environments } = value;
  if (!Array.isArray(environments) || environments.length === 0) return { rule: null, problem: NO_ENVIRONMENTS };

  const kept: ReviewEnvironment[] = [];
  for (const each of environments) {
    if (!isObject(each)) return { rule: null, problem: ENVIRONMENT_SHAPE };
    const spelled: Spelled = {
      name: text(each.name), kind: text(each.kind), procedure: text(each.procedure), address: text(each.address), run: text(each.run),
    };
    const problem = environmentProblem(spelled, kept);
    if (problem !== null) return { rule: null, problem };
    kept.push(keptEnvironment(spelled));
  }

  return { rule: { ...(required === true ? { required: true } : {}), environments: kept }, problem: null };
}

/** The first problem of one environment, judged after those before it in its rule — the driver's `ReviewRules.Problem`. */
function environmentProblem(environment: Spelled, earlier: readonly ReviewEnvironment[]): string | null {
  const { name, kind, procedure, address, run } = environment;
  if (name === undefined || name.trim().length === 0) return `an environment is named: ${NAME_RULE}`;
  if (!NAME_SHAPE.test(name) || name.length > 32) return `\`${name}\` is not an environment's name — ${NAME_RULE}`;
  if (name === 'none') return '`none` says a repository has no review environment, so no environment is named it.';
  if (readsAsProduction(name)) {
    return `\`${name}\` reads as production, and production is never a review environment — name where work is looked at `
      + 'before it lands, such as `local` or `dev`.';
  }
  if (earlier.some((each) => each.name === name)) return `\`${name}\` is named twice in one rule — each environment has a name of its own.`;

  if (kind === undefined) return KIND_MISSING;
  if (!(REVIEW_KINDS as readonly string[]).includes(kind)) return `\`${kind}\` is not a kind of review environment — \`local\` or \`deployed\`.`;

  if (procedure === undefined || procedure.trim().length === 0) return PROCEDURE_MISSING;
  if (!isProcedurePath(procedure)) {
    return `\`${procedure}\` is not a path in the repository — a procedure is named from its root with forward slashes, and `
      + 'no `..`, root or drive, such as `docs/deploying-to-dev.md`.';
  }

  if (address === undefined && kind === 'local') return ADDRESS_MISSING;
  if (address !== undefined && !isAddress(address)) {
    return `\`${address}\` is not an address — an absolute \`http\` or \`https\` origin, such as \`http://localhost:4200\`.`;
  }

  if (run !== undefined && kind !== 'local') return RUN_DEPLOYED;
  if (run !== undefined && (run.trim().length === 0 || /[\r\n]/.test(run))) return RUN_SHAPE;
  return null;
}

/** Whether a name reads as production: the whole of it, or a word between its dashes, is one of `PRODUCTION_WORDS`. */
export function readsAsProduction(name: string): boolean {
  return name.split('-').some((word) => (PRODUCTION_WORDS as readonly string[]).includes(word));
}

/**
 * A path a procedure may be (design §1.1, as KNOWUSE1c names a router): from the repository's root, with forward slashes,
 * no space around it, and no `..`, `.`, empty part, root, drive or control character.
 */
function isProcedurePath(path: string): boolean {
  if (path !== path.trim() || path.startsWith('/') || path.includes('\\') || path.includes(':')) return false;
  if ([...path].some((c) => c.codePointAt(0)! < 0x20 || c === '\u007f')) return false;
  return path.split('/').every((part) => part.length > 0 && part !== '.' && part !== '..');
}

/** An absolute `http` or `https` origin: a host or a bracketed IPv6 address, a port from 1 to 65535, nothing after but one `/`. */
function isAddress(address: string): boolean {
  const match = ADDRESS_SHAPE.exec(address);
  if (match === null) return false;
  const port = match[1];
  return port === undefined || (Number(port) >= 1 && Number(port) <= 65_535);
}

/** An environment as kept: in the file's order, an address without its last slash, and absent fields left out. */
function keptEnvironment(environment: Spelled): ReviewEnvironment {
  const { name, kind, procedure, address, run } = environment;
  return {
    name: name!, kind: kind!, procedure: procedure!,
    ...(address !== undefined ? { address: address.endsWith('/') ? address.slice(0, -1) : address } : {}),
    ...(run !== undefined ? { run } : {}),
  };
}

/**
 * A map of review rules as the driver reads it: each name trimmed, a blank one not read; a rule with a problem, and `true`,
 * not read; `false` read only where `allowNone` (a repository's, never a workspace's); a name written twice in any case read
 * where first readable. A map that is not one is none.
 */
export function reviewsOf(value: unknown, allowNone: true): Record<string, ReviewSetting>;
export function reviewsOf(value: unknown, allowNone: false): Record<string, ReviewRule>;
export function reviewsOf(value: unknown, allowNone: boolean): Record<string, ReviewSetting> {
  if (!isObject(value)) return {};
  const held: Record<string, ReviewSetting> = {};
  for (const [name, entry] of Object.entries(value)) {
    const named = name.trim();
    if (named.length === 0 || (entry === false && !allowNone)) continue;
    const { rule, problem } = reviewRuleOf(entry);
    if (problem !== null || rule === null || findName(Object.keys(held), named) !== null) continue;
    held[named] = rule;
  }

  return held;
}

/** A rule as it resolves for a repository (design §1.3), and where it was set. */
export interface ResolvedReview {
  source: 'repository' | 'workspace';
  rule: ReviewSetting;
}

/**
 * The review rule standing for a repository: its own (a rule or none), else its workspace's (one in no workspace being in
 * `default`), else null, which is nothing set anywhere — the driver's `ReviewRules.Resolve`, names matched without case.
 */
export function reviewFor(maps: ReviewMaps, repository: string, workspace: string | null): ResolvedReview | null {
  const own = atName(maps.reviews, repository.trim());
  if (own !== undefined) return { source: 'repository', rule: own };
  const shared = atName(maps.workspaceReviews, normalizeWorkspace(workspace));
  return shared === undefined ? null : { source: 'workspace', rule: shared };
}

/** Names in backticks, the last after `or`: `a`, `a or b`, `a, b or c`. */
function either(names: readonly string[]): string {
  const ticked = names.map((name) => `\`${name}\``);
  return ticked.length <= 1 ? ticked.join('') : `${ticked.slice(0, -1).join(', ')} or ${ticked[ticked.length - 1]}`;
}

/**
 * What each door says as a rule is set (design §1.7): whether work waits for the person's look and where, then what a
 * set-up step may do in each environment, in order — the driver's `ReviewRules.Says`, word for word.
 */
export function reviewSays(rule: ReviewSetting): string[] {
  if (rule === false) return [NONE_HERE];
  const [first, ...others] = rule.environments;
  const said: string[] = [];
  if (rule.required) {
    said.push(`Before work here lands, it is shown to you in \`${first!.name}\` and waits for you to say it is right.`);
    if (others.length > 0) said.push(`A task may ask for ${either(others.map((each) => each.name))} instead.`);
  } else {
    said.push(`When a task asks for it, work here may be shown to you in ${either(rule.environments.map((each) => each.name))}; `
      + 'nothing waits for it.');
  }

  for (const environment of rule.environments) {
    if (environment.kind === 'local') {
      said.push(`A set-up step here builds the work in its own tree and shows it in Daoris's browser at \`${environment.address}\`; `
        + 'your own servers and processes are not touched.');
      if (environment.run !== undefined) {
        said.push(`A set-up step here may run \`${environment.run}\` in its tree on a port nobody holds, without asking you each `
          + 'time; it stops once you have reviewed.');
      }
    } else {
      said.push(`A set-up step here follows \`${environment.procedure}\` toward \`${environment.name}\`; each deploy or write `
        + 'there asks your go-ahead once per ask.');
    }
  }

  return said;
}

/**
 * What each door says after the rule's own sentences (REVIEWENV1c2): what the landing's gate does with work that waits for the
 * person's review; nothing after none here, where nothing waits. The driver's `ReviewRules.Gate`, by the table's `gate` rows.
 */
export function reviewGate(rule: ReviewSetting): string[] {
  return rule === false ? [] : [REVIEW_WAITING];
}

/** A rule in a line of `driver list`: each environment with its kind, address, procedure and command, then whether it waits. */
export function reviewListed(rule: ReviewSetting): string {
  if (rule === false) return 'none here, whatever its workspace says';
  const environments = rule.environments.map((each) => `${each.name} (${[
    each.kind, each.address, `by ${each.procedure}`, each.run !== undefined ? `runs \`${each.run}\`` : undefined,
  ].filter((part) => part !== undefined).join(', ')})`);
  return `${environments.join('; ')}, ${rule.required ? 'required before landing' : 'on a task\'s asking'}`;
}

/**
 * One change to a review rule, as both doors make it (design §1.7): for a `repository` or a `workspace`, add or replace one
 * environment (`put`, with `required` if given), say a repository has `none`, `drop` one, set `required` alone, or `clear`.
 */
export interface ReviewEdit {
  repository?: string;
  workspace?: string;
  put?: Spelled;
  required?: boolean;
  none?: boolean;
  drop?: string;
  clear?: boolean;
}

/**
 * The maps with one change made, or the refusal thrown in the driver's words, nothing changed — the driver's
 * `ReviewRules.Apply`. A repository's rule starts afresh rather than from its workspace's, since it replaces that whole; an
 * environment put under a name already there replaces it where it stands; the key keeps the spelling first written.
 */
export function applyReviewEdit(maps: ReviewMaps, edit: ReviewEdit): ReviewMaps {
  const repository = typeof edit.repository === 'string' && edit.repository.trim().length > 0 ? edit.repository.trim() : null;
  const workspace = typeof edit.workspace === 'string' && edit.workspace.trim().length > 0 ? edit.workspace.trim() : null;
  if ((repository === null) === (workspace === null)) throw new DaorisError(SCOPE);

  const put = isObject(edit.put) ? edit.put : undefined;
  const none = edit.none === true;
  const drop = typeof edit.drop === 'string' ? edit.drop : undefined;
  const clear = edit.clear === true;
  const required = typeof edit.required === 'boolean' ? edit.required : undefined;
  const changes = [put !== undefined, none, drop !== undefined, clear].filter(Boolean).length;
  if (changes > 1 || (changes === 0 && required === undefined) || (required !== undefined && changes === 1 && put === undefined)) {
    throw new DaorisError(ONE_AT_A_TIME);
  }
  if (none && workspace !== null) throw new DaorisError(NONE_WORKSPACE);

  const map: Record<string, ReviewSetting> = repository !== null ? maps.reviews : maps.workspaceReviews;
  const named = (repository ?? workspace)!;
  const key = findName(Object.keys(map), named) ?? named;
  const whose = repository !== null ? `\`${key}\`` : `the workspace \`${key}\``;
  const own = Object.hasOwn(map, key) ? map[key] : undefined;
  const ownRule = own === undefined || own === false ? null : own;

  let next: ReviewSetting | null;
  if (clear) {
    next = null;
  } else if (none) {
    next = false;
  } else if (drop !== undefined) {
    if (ownRule === null || !ownRule.environments.some((each) => each.name === drop)) {
      throw new DaorisError(`${whose} has no environment \`${drop}\` of its own to drop.`);
    }
    if (ownRule.environments.length === 1) {
      throw new DaorisError(`\`${drop}\` is the only environment of ${whose} — \`--clear\` hands it back to what stands above it, `
        + 'and for a repository `none` says it has none.');
    }
    next = { ...(ownRule.required ? { required: true } : {}), environments: ownRule.environments.filter((each) => each.name !== drop) };
  } else if (put === undefined) {
    if (ownRule === null) throw new DaorisError(`${whose} has no review rule of its own — add an environment to it first.`);
    next = { ...(required ? { required: true } : {}), environments: ownRule.environments };
  } else {
    const spelled: Spelled = {
      name: text(put.name), kind: text(put.kind), procedure: text(put.procedure), address: text(put.address), run: text(put.run),
    };
    const base = ownRule?.environments ?? [];
    const at = base.findIndex((each) => each.name === spelled.name);
    const environments: unknown[] = at === -1 ? [...base, spelled] : base.map((each, index) => (index === at ? spelled : each));
    const read = reviewRuleOf({ ...((required ?? ownRule?.required === true) ? { required: true } : {}), environments });
    if (read.problem !== null) throw new DaorisError(read.problem);
    next = read.rule;
  }

  const rest = Object.fromEntries(Object.entries(map).filter(([name]) => name !== key));
  const changed = next === null ? rest : { ...map, [key]: next };
  return repository !== null
    ? { reviews: changed, workspaceReviews: maps.workspaceReviews }
    : { reviews: maps.reviews, workspaceReviews: changed as Record<string, ReviewRule> };
}

/**
 * Whether a checkout holds a procedure (design §1.1): a regular file at that path, reached through folders, with no link on
 * the way — the driver's `ReviewRules.Holds`. Read where the registry says the checkout is, never for meaning.
 */
export function holdsProcedure(root: string, procedure: string): boolean {
  if (!isProcedurePath(procedure)) return false;
  const parts = procedure.split('/');
  let at = root;
  for (const [index, part] of parts.entries()) {
    at = join(at, part);
    let found;
    try {
      found = lstatSync(at);
    } catch {
      return false;
    }
    if (found.isSymbolicLink()) return false;
    if (index < parts.length - 1 ? !found.isDirectory() : !found.isFile()) return false;
  }

  return true;
}

/** One registered repository as `driver review` checks a procedure in: its workspace, and its checkout here or none. */
export interface Checkout {
  repository: string;
  workspace: string | null;
  root: string | null;
}

/** How `driver review` reads the registry's checkouts: handed in by `cli/driver.ts` through `service.ts` (D50). */
export type CheckoutsReader = () => Promise<{ checkouts: Checkout[] } | { unread: string }>;

/** Whether a registered repository is the one named, or one of the workspace named, matched without case. */
export function inScope(checkout: Checkout, scope: { repository: string } | { workspace: string }): boolean {
  return 'repository' in scope
    ? sameName(checkout.repository, scope.repository)
    : sameName(normalizeWorkspace(checkout.workspace), normalizeWorkspace(scope.workspace));
}
