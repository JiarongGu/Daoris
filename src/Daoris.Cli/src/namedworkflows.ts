// Named workflows and their versions (WORKFLOW1d, D157 points 5, 6 and 8; the workflow design §2.4–§2.6, §3.2, §3.3, §3.9):
// a workflow the person saves under `<home>/workflows/<id>.json`, its versions whole and never edited, only added to; what
// each version may say and the first problem in what it does not; a version's digest; the diff between two versions, by step
// id; the presets built in, and the line a workflow is shown by, composed from its steps.
//
// 🔴 A TWIN with the driver's `WorkflowNamed.cs`, `WorkflowDiff.cs`, `WorkflowPresets.cs` and `WorkflowStore.cs`: the CLI and the
// driver share no code, so both hold ONE table, the driver suite's `fixtures/workflow-named.json`, cell for cell, every
// sentence and every digest included (`.claude/knowledge/twins.md`).
//
// Nothing reads a named workflow at a gate yet: choosing one is WORKFLOW1e's, and the gate's WORKFLOW1f's. This module stores,
// validates, versions and compares. Pure: it reads no file, reaches no network and spawns nothing.

import { createHash } from 'node:crypto';
import { sameName } from './casefold.ts';
import { isoMoment } from './cooling.ts';
import { landingProblem } from './driverconfig.ts';
import { isPluginId } from './plugins.ts';

/** How much of a kind this build runs (design §3.2, §3.9): `none` is never offered, and a version naming it does not read. */
export type KindRuntime = 'built' | 'partial' | 'declared' | 'none';

/** The shape of a field's value (design §3.2). */
export type FieldType = 'names' | 'flag' | 'choice' | 'environment' | 'pattern' | 'plugin-or-none' | 'text' | 'plugin'
  | 'checks' | 'id' | 'members';

/** One field a kind takes, in the order the version reads it: its type, whether it must be named, and what absence reads as. */
export interface KindField {
  name: string;
  type: FieldType;
  choices?: readonly string[];
  required: boolean;
  default: string | boolean | null;
}

/** One kind of step: how much of it this build runs, the limit it is said with, and its fields. */
export interface KindRow {
  kind: string;
  runtime: KindRuntime;
  /** A code of `WORKFLOW_LIMITS` (`workflows.ts`), or null where this build runs it whole or not at all. */
  limit: string | null;
  fields: readonly KindField[];
}

const FAILED: KindField = { name: 'failed', type: 'choice', choices: ['wait', 'send-back', 'stop'], required: false, default: 'wait' };

/**
 * The kinds, as main runs them (D157 point 8) — the driver's `WorkflowKinds.Table`, cell for cell. 🔴 The opinion is
 * `declared` until XAGENT1f's gate reads it, then `partial`, and its limit's sentence moves with it.
 */
export const WORKFLOW_KINDS: readonly KindRow[] = [
  { kind: 'work', runtime: 'built', limit: null, fields: [] },
  {
    kind: 'opinion', runtime: 'declared', limit: 'opinion-declared', fields: [
      { name: 'reviewers', type: 'names', required: false, default: null },
      { name: 'required', type: 'flag', required: false, default: false },
      { name: 'steps', type: 'flag', required: false, default: false },
      { name: 'recheck', type: 'flag', required: false, default: true },
    ],
  },
  { kind: 'look', runtime: 'partial', limit: 'look-partial', fields: [{ name: 'environment', type: 'environment', required: false, default: null }] },
  {
    kind: 'landing', runtime: 'built', limit: null, fields: [
      { name: 'form', type: 'choice', choices: ['merge', 'branch'], required: true, default: null },
      { name: 'accept', type: 'choice', choices: ['you', 'automatic'], required: true, default: null },
      { name: 'pattern', type: 'pattern', required: false, default: null },
      { name: 'plugin', type: 'plugin-or-none', required: false, default: null },
    ],
  },
  { kind: 'pull-request', runtime: 'partial', limit: 'pull-request-at-clean-up', fields: [] },
  {
    kind: 'go-ahead', runtime: 'partial', limit: 'go-ahead-partial', fields: [
      { name: 'act', type: 'text', required: true, default: null },
      { name: 'on', type: 'text', required: true, default: null },
      { name: 'refused', type: 'choice', choices: ['stop', 'wait'], required: false, default: 'stop' },
    ],
  },
  {
    kind: 'check', runtime: 'none', limit: null, fields: [
      { name: 'plugin', type: 'plugin', required: true, default: null },
      { name: 'checks', type: 'checks', required: false, default: 'required' },
      FAILED,
    ],
  },
  {
    kind: 'stage', runtime: 'none', limit: null, fields: [
      { name: 'plugin', type: 'plugin', required: true, default: null },
      { name: 'stage', type: 'id', required: true, default: null },
      { name: 'start', type: 'choice', choices: ['you', 'automatic'], required: false, default: 'you' },
      FAILED,
    ],
  },
  { kind: 'all', runtime: 'none', limit: null, fields: [{ name: 'steps', type: 'members', required: true, default: null }, FAILED] },
];

/** At most this many steps in a version, a group's members counted (design §2.5). */
export const MAX_STEPS = 24;

/** A workflow keeps its newest this many versions, and every version a kept run names (design §2.6). */
export const KEPT_VERSIONS = 20;

/** A field's value as a version reads it: text or none, a switch, a list of names, or a group's members. */
export type StepValue = string | null | boolean | string[] | NamedStep[];

/** One step as a version reads it: its id and kind, and every field its kind takes, in order, absence read as its default. */
export interface NamedStep {
  id: string;
  kind: string;
  fields: [string, StepValue][];
}

/** One version as read: its number, when and through which door it was saved, and its steps, or the first problem in it. */
export interface NamedVersion {
  version: number;
  at: string | null;
  door: string | null;
  steps: NamedStep[] | null;
  problem: string | null;
  /** The first 12 hex characters of the SHA-256 of `versionCanonical`'s text, where the version reads. */
  digest: string | null;
}

/** A workflow file as read: its id and name and every version, each readable or not; or the file's own first problem. */
export interface WorkflowRead {
  id: string | null;
  name: string | null;
  versions: NamedVersion[];
  problem: string | null;
}

const ID_SHAPE = /^[a-z0-9]+(?:-[a-z0-9]+)*$/;
const ID_RULE = 'lower-case letters, digits and dashes, at most 40';
const ENVIRONMENT_SHAPE = ID_SHAPE;
const DOOR_SHAPE = /^ask-daoris:[A-Za-z0-9_-]{1,64}$/;
const KIND_LIST = '`work`, `opinion`, `look`, `landing`, `pull-request`, `go-ahead`, `check`, `stage` or `all`';

const FILE_SHAPE = 'a workflow is a JSON object: its `id`, its `name` and its `versions`.';
const ID_PROBLEM = `a workflow's \`id\` is ${ID_RULE}, such as \`docs-to-pr\`.`;
const CURRENT = '`current` names Current, the workflow drawn from the rules as they stand, so no saved workflow takes it.';
const NAME_PROBLEM = 'a workflow\'s `name` is your words for it, 1 to 60 characters.';
const VERSIONS_PROBLEM = 'a workflow\'s `versions` is a list of at least one.';
const VERSION_SHAPE = 'each of its versions is a JSON object with its `version`, `at`, `door` and `steps`.';
const VERSION_NUMBER = 'each version\'s `version` is a whole number from 1, each greater than the one before it.';

/** A workflow's or a step's id: lower-case letters, digits and dashes, at most 40. */
export function isWorkflowId(value: unknown): value is string {
  return typeof value === 'string' && value.length <= 40 && ID_SHAPE.test(value);
}

/** What is wrong with a workflow's id, or null. `current` is Current's (design §2.5). */
export function workflowIdProblem(value: unknown): string | null {
  if (typeof value !== 'string' || value.length === 0) return ID_PROBLEM;
  if (!isWorkflowId(value)) return `\`${value}\` is not a workflow's id — ${ID_RULE}, such as \`docs-to-pr\`.`;
  return value === 'current' ? CURRENT : null;
}

/** What is wrong with a workflow's name, or null: the person's words, 1 to 60 characters (content, D142). */
export function workflowNameProblem(value: unknown): string | null {
  return typeof value === 'string' && value.trim().length > 0 && value.length <= 60 ? null : NAME_PROBLEM;
}

function isObject(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function isWhole(value: unknown): value is number {
  return typeof value === 'number' && Number.isInteger(value) && value >= 1 && value <= 2147483647;
}

/** A kind's row, or undefined for a kind nobody has. */
export function kindOf(kind: string): KindRow | undefined {
  return WORKFLOW_KINDS.find((row) => row.kind === kind);
}

/**
 * A workflow file read whole (design §2.5, §2.6): the file's own problem first (its shape, id, name, and its versions'
 * numbers, each greater than the one before), then each version on its own, so a version a newer build or a hand edit wrote is
 * kept and unreadable here while the others read. A step's id an earlier readable version used for another kind makes the
 * later version unreadable.
 */
export function readWorkflowFile(value: unknown): WorkflowRead {
  const none = (problem: string): WorkflowRead => ({ id: null, name: null, versions: [], problem });
  if (!isObject(value)) return none(FILE_SHAPE);
  const idProblem = workflowIdProblem(value['id']);
  if (idProblem !== null) return none(idProblem);
  const nameProblem = workflowNameProblem(value['name']);
  if (nameProblem !== null) return none(nameProblem);
  const versions = value['versions'];
  if (!Array.isArray(versions) || versions.length === 0) return none(VERSIONS_PROBLEM);
  let last = 0;
  for (const version of versions) {
    if (!isObject(version)) return none(VERSION_SHAPE);
    const number = version['version'];
    if (!isWhole(number) || number <= last) return none(VERSION_NUMBER);
    last = number;
  }

  const read: NamedVersion[] = [];
  for (const version of versions as Record<string, unknown>[]) {
    read.push(readVersion(version, version['version'] as number, read.filter((each) => each.steps !== null)));
  }

  return { id: value['id'] as string, name: value['name'] as string, versions: read, problem: null };
}

/**
 * One version read (design §2.5): its `at`, its `door`, then its steps, the first problem said with the version's number.
 *
 * @param prior The versions before it that read, which a step's id is held to: an id is never used for another kind.
 */
export function readVersion(value: Record<string, unknown>, number: number, prior: readonly NamedVersion[]): NamedVersion {
  const at = typeof value['at'] === 'string' ? value['at'] : null;
  const door = typeof value['door'] === 'string' ? value['door'] : null;
  const unread = (problem: string): NamedVersion => ({ version: number, at, door, steps: null, problem, digest: null });
  if (isoMoment(value['at']) === null) {
    return unread(`v${number}: its \`at\` is a moment as ISO 8601 writes it, such as \`2026-10-09T09:12:00Z\`.`);
  }
  if (door === null || !(door === 'screen' || door === 'terminal' || DOOR_SHAPE.test(door))) {
    return unread(`v${number}: its \`door\` is \`screen\`, \`terminal\` or \`ask-daoris:<proposal>\`.`);
  }

  const { steps, problem } = readSteps(value['steps'], number, prior);
  return steps === null ? unread(problem!) : { version: number, at, door, steps, problem: null, digest: versionDigest(steps) };
}

/**
 * A version's steps read (design §2.5, §3.2, §3.3, §3.9), the first problem said: the list and its bound, then each step's
 * shape in order (a group's members inside it), then the order the runtime keeps, then whether this build runs each kind.
 */
export function readSteps(value: unknown, number: number, prior: readonly NamedVersion[] = []): { steps: NamedStep[] | null; problem: string | null } {
  const v = `v${number}`;
  if (!Array.isArray(value) || value.length === 0) return { steps: null, problem: `${v}: its \`steps\` is a list of at least one.` };
  const count = value.length + value.reduce<number>((sum, each) => sum
    + (isObject(each) && each['kind'] === 'all' && Array.isArray(each['steps']) ? (each['steps'] as unknown[]).length : 0), 0);
  if (count > MAX_STEPS) {
    return { steps: null, problem: `${v}: it has ${count} steps, counting each group's; a workflow holds at most ${MAX_STEPS}.` };
  }

  const seen = new Set<string>();
  const steps: NamedStep[] = [];
  for (const [index, raw] of value.entries()) {
    const read = readStep(raw, index + 1, null, seen, prior, v);
    if (read.problem !== null) return { steps: null, problem: read.problem };
    steps.push(read.step!);
  }

  const order = orderProblem(steps, v);
  if (order !== null) return { steps: null, problem: order };
  for (const step of steps) {
    for (const each of [step, ...members(step)]) {
      if (kindOf(each.kind)!.runtime === 'none') {
        return { steps: null, problem: `${v}, step \`${each.id}\`: this build does not run a step of kind \`${each.kind}\` yet.` };
      }
    }
  }

  return { steps, problem: null };
}

function members(step: NamedStep): NamedStep[] {
  const value = field(step, 'steps');
  return step.kind === 'all' && Array.isArray(value) ? value as NamedStep[] : [];
}

/** A field's value as the step reads it. */
export function field(step: NamedStep, name: string): StepValue | undefined {
  return step.fields.find(([each]) => each === name)?.[1];
}

/** `a`, `a` or `b`, `a`, `b` or `c`: a field's choices, as its sentence names them. */
function either(choices: readonly string[]): string {
  const named = choices.map((choice) => `\`${choice}\``);
  return named.length === 1 ? named[0]! : `${named.slice(0, -1).join(', ')} or ${named[named.length - 1]}`;
}

/** The fields a kind takes, as its refusal names them: none, `a`, `a` and `b`, `a`, `b` and `c`. */
function fieldList(fields: readonly KindField[]): string {
  const named = fields.map((each) => `\`${each.name}\``);
  if (named.length === 0) return 'none';
  return named.length === 1 ? named[0]! : `${named.slice(0, -1).join(', ')} and ${named[named.length - 1]}`;
}

/** What a field's value is, as its refusal says it. */
function what(each: KindField): string {
  switch (each.type) {
    case 'names': return 'a list of names, at least one, none twice in any case';
    case 'flag': return 'true or false';
    case 'choice': return either(each.choices!);
    case 'environment': return 'an environment\'s name: lower-case letters, digits and dashes, at most 32';
    case 'pattern': return 'a branch pattern, such as `work/{quest}-{slug}`';
    case 'plugin-or-none': return '`none` or a plugin\'s id: lowercase letters, digits, dots and dashes';
    case 'text': return 'your words, 1 to 300 characters';
    case 'plugin': return 'a plugin\'s id: lowercase letters, digits, dots and dashes';
    case 'checks': return '`required` or a list of check names, at least one, none twice in any case';
    case 'id': return ID_RULE;
    case 'members': return 'a list of the checks and stages it runs side by side, at least one';
  }
}

/** Names as a list reads them: at least one, each text that is not blank, kept without the spaces around it, none twice in any case. */
function names(value: unknown): string[] | null {
  if (!Array.isArray(value) || value.length === 0) return null;
  const kept: string[] = [];
  for (const each of value) {
    if (typeof each !== 'string' || each.trim().length === 0) return null;
    const name = each.trim();
    if (kept.some((other) => sameName(other, name))) return null;
    kept.push(name);
  }
  return kept;
}

/** A field's value read, or why not: a sentence after the step's prefix. Absent and JSON null are both absence. */
function readField(each: KindField, raw: unknown): { value: StepValue; problem: string | null } {
  const wrong = { value: null, problem: `its \`${each.name}\` is ${what(each)}.` };
  if (raw === undefined || raw === null) return each.required ? wrong : { value: each.default, problem: null };
  switch (each.type) {
    case 'names': {
      const read = names(raw);
      return read === null ? wrong : { value: read, problem: null };
    }
    case 'checks': {
      if (raw === 'required') return { value: raw, problem: null };
      const read = names(raw);
      return read === null ? wrong : { value: read, problem: null };
    }
    case 'flag':
      return typeof raw === 'boolean' ? { value: raw, problem: null } : wrong;
    case 'choice':
      return typeof raw === 'string' && each.choices!.includes(raw) ? { value: raw, problem: null } : wrong;
    case 'environment':
      return typeof raw === 'string' && raw.length <= 32 && ENVIRONMENT_SHAPE.test(raw) ? { value: raw, problem: null } : wrong;
    case 'id':
      return isWorkflowId(raw) ? { value: raw, problem: null } : wrong;
    case 'text':
      return typeof raw === 'string' && raw.trim().length > 0 && raw.length <= 300 ? { value: raw, problem: null } : wrong;
    case 'plugin':
      return typeof raw === 'string' && isPluginId(raw) ? { value: raw, problem: null } : wrong;
    case 'plugin-or-none':
      return typeof raw === 'string' && (raw === 'none' || isPluginId(raw)) ? { value: raw, problem: null } : wrong;
    case 'pattern': {
      if (typeof raw !== 'string') return wrong;
      // The landing twin's own check, asked of its one owner, so a step and a rule refuse one pattern in the same words.
      const problem = landingProblem({ form: 'branch', pattern: raw });
      return problem === null ? { value: raw, problem: null } : { value: null, problem: `its \`pattern\` will not do: ${problem}` };
    }
    case 'members':
      return Array.isArray(raw) && raw.length > 0 ? { value: [], problem: null } : wrong;
  }
}

/** One step's shape read (design §3.2): an object, its id once, its kind, the group's rule, its fields; a group's members inside it. */
function readStep(
  raw: unknown, position: number, group: string | null, seen: Set<string>, prior: readonly NamedVersion[], v: string,
): { step: NamedStep | null; problem: string | null } {
  const at = group === null ? `${v}, step ${position}: ` : `${v}, step ${position} in \`${group}\`: `;
  const fail = (problem: string) => ({ step: null, problem });
  if (!isObject(raw)) return fail(`${at}a step is a JSON object with its \`id\` and \`kind\`.`);
  const id = raw['id'];
  if (!isWorkflowId(id)) return fail(`${at}its \`id\` is ${ID_RULE}, such as \`land\`.`);
  if (seen.has(id)) return fail(`${at}\`${id}\` is an earlier step's id too; each step's id is its own.`);
  seen.add(id);

  const p = group === null ? `${v}, step \`${id}\`: ` : `${v}, step \`${id}\` in \`${group}\`: `;
  const kind = raw['kind'];
  if (typeof kind !== 'string') return fail(`${p}its \`kind\` is one of ${KIND_LIST}.`);
  const row = kindOf(kind);
  if (row === undefined) return fail(`${p}\`${kind}\` is no kind of step — one of ${KIND_LIST}.`);
  if (group !== null && kind === 'all') return fail(`${p}a group holds no group: one level of grouping.`);
  if (group !== null && kind !== 'check' && kind !== 'stage') {
    return fail(`${p}a group holds checks and stages, side by side; \`${kind}\` is neither.`);
  }

  for (const earlier of prior) {
    const was = [...earlier.steps!, ...earlier.steps!.flatMap(members)].find((each) => each.id === id);
    if (was !== undefined && was.kind !== kind) {
      return fail(`${p}\`${id}\` was a step of kind \`${was.kind}\` in v${earlier.version}; an id is never used for another kind.`);
    }
  }

  for (const key of Object.keys(raw)) {
    if (key !== 'id' && key !== 'kind' && !row.fields.some((each) => each.name === key)) {
      return fail(`${p}a step of kind \`${kind}\` takes no \`${key}\` — it takes ${fieldList(row.fields)}.`);
    }
  }

  const fields: [string, StepValue][] = [];
  for (const each of row.fields) {
    const read = readField(each, raw[each.name]);
    if (read.problem !== null) return fail(p + read.problem);
    fields.push([each.name, read.value]);
  }
  const step: NamedStep = { id, kind, fields };

  if (kind === 'landing' && field(step, 'form') === 'merge') {
    // A merge writes into the person's checkout (D145 point 1, D51 rule 6), and makes no branch for a plugin (D100).
    if (field(step, 'accept') === 'automatic') {
      return fail(`${p}only a branch lands with no press — a merge writes into your checkout, and with no press nothing would `
        + 'stand between the work and the line.');
    }
    if (field(step, 'pattern') !== null || field(step, 'plugin') !== null) {
      return fail(`${p}a merge makes no branch, so it names no \`pattern\` and no \`plugin\`.`);
    }
  }

  if (kind === 'all') {
    const read: NamedStep[] = [];
    for (const [index, member] of (raw['steps'] as unknown[]).entries()) {
      const each = readStep(member, index + 1, id, seen, prior, v);
      if (each.problem !== null) return each;
      read.push(each.step!);
    }
    step.fields = step.fields.map(([name, value]) => [name, name === 'steps' ? read : value]);
  }

  return { step, problem: null };
}

/**
 * The order the runtime keeps (design §2.2, §2.5, §3.3, §3.6): the work first and once; at most one opinion and one look
 * before the landing, the opinion first, and a go-ahead before them; one landing; after it, the pull request straight after
 * a landing on a branch whose plugin opens one, then checks, stages, groups and go-aheads, none straight before a stage
 * the person starts.
 */
function orderProblem(steps: readonly NamedStep[], v: string): string | null {
  let landing: NamedStep | null = null;
  let opinion: NamedStep | null = null;
  let look: NamedStep | null = null;
  let pullRequest: NamedStep | null = null;
  for (const [index, step] of steps.entries()) {
    const p = `${v}, step \`${step.id}\`: `;
    if (index === 0 && step.kind !== 'work') return `${v}: the first step is the \`work\`: the agent's work comes first, once.`;
    switch (step.kind) {
      case 'work':
        if (index > 0) return `${p}there is one \`work\`, the first step.`;
        break;
      case 'landing':
        if (landing !== null) return `${p}there is one \`landing\` already, \`${landing.id}\`.`;
        landing = step;
        break;
      case 'opinion':
        if (landing !== null) return `${p}a step of kind \`opinion\` comes before the landing, in the order the runtime keeps.`;
        if (opinion !== null) return `${p}there is one \`opinion\` already, \`${opinion.id}\`.`;
        if (look !== null) return `${p}the second opinion is read before your look, so the \`opinion\` comes first.`;
        opinion = step;
        break;
      case 'look':
        if (landing !== null) return `${p}a step of kind \`look\` comes before the landing, in the order the runtime keeps.`;
        if (look !== null) return `${p}there is one \`look\` already, \`${look.id}\`.`;
        look = step;
        break;
      case 'go-ahead': {
        if (landing === null && (opinion !== null || look !== null)) {
          return `${p}a go-ahead before the landing sits with the work, before the second opinion and your look.`;
        }
        const next = steps[index + 1];
        if (landing !== null && next?.kind === 'stage' && field(next, 'start') === 'you') {
          return `${p}a go-ahead straight before a stage you start is not needed: starting it is your go-ahead.`;
        }
        break;
      }
      case 'pull-request':
        if (landing === null) return `${p}a step of kind \`pull-request\` comes after the landing.`;
        if (pullRequest !== null) return `${p}there is one \`pull-request\` already, \`${pullRequest.id}\`.`;
        if (steps[index - 1] !== landing) return `${p}the pull request comes straight after the landing.`;
        if (field(landing, 'form') === 'merge') {
          return `${p}a pull request needs a landing on a branch whose plugin opens one; \`${landing.id}\` merges.`;
        }
        if (field(landing, 'plugin') === 'none') {
          return `${p}a pull request needs a landing on a branch whose plugin opens one; \`${landing.id}\` names no plugin.`;
        }
        pullRequest = step;
        break;
      default:
        if (landing === null) return `${p}a step of kind \`${step.kind}\` comes after the landing.`;
    }
  }

  return landing === null ? `${v}: it has no \`landing\`; work lands once, after the gates before it.` : null;
}

/**
 * The text a version's digest is of, built by hand so both runtimes write the same bytes (the driver's
 * `WorkflowNamed.Canonical`): `workflow`, then each step's line, its quoted id and its kind, each followed by every field its
 * kind takes, a line each in order, absence written as its default. Text is quoted, with `\`, `"` and each control character
 * escaped; none is `-`; a list is its quoted items between brackets; a group's members follow it, a step deeper.
 */
export function versionCanonical(steps: readonly NamedStep[]): string {
  const lines = ['workflow'];
  const write = (step: NamedStep, indent: string): void => {
    lines.push(`${indent}step ${quoted(step.id)} ${step.kind}`);
    for (const [name, value] of step.fields) {
      if (Array.isArray(value) && value.length > 0 && typeof value[0] !== 'string') {
        lines.push(`${indent}  ${name}:`);
        for (const member of value as NamedStep[]) write(member, `${indent}    `);
      } else {
        lines.push(`${indent}  ${name}=${canonicalValue(value as string | null | boolean | string[])}`);
      }
    }
  };
  for (const step of steps) write(step, '');
  return lines.join('\n');
}

/** A version's digest: the first 12 hex characters of the SHA-256 of `versionCanonical`'s text. */
export function versionDigest(steps: readonly NamedStep[]): string {
  return createHash('sha256').update(versionCanonical(steps), 'utf8').digest('hex').slice(0, 12);
}

function canonicalValue(value: string | null | boolean | string[]): string {
  if (value === null) return '-';
  if (typeof value === 'boolean') return value ? 'true' : 'false';
  if (Array.isArray(value)) return `[${value.map(quoted).join(',')}]`;
  return quoted(value);
}

function quoted(text: string): string {
  return `"${text.replace(/[\\"\u0000-\u001f]/g, (c) => (c === '\\' ? '\\\\' : c === '"' ? '\\"'
    : `\\u${c.charCodeAt(0).toString(16).padStart(4, '0')}`))}"`;
}

/** One step in a diff (design §8.2): matched by id, added, removed or kept, the fields changed in the kind's order, and whether it moved. */
export interface DiffEntry {
  id: string;
  kind: string;
  change: 'added' | 'removed' | 'kept';
  fields: string[];
  moved: boolean;
}

function same(a: StepValue | undefined, b: StepValue | undefined): boolean {
  if (Array.isArray(a) && Array.isArray(b)) {
    if (a.length !== b.length) return false;
    return a.every((each, index) => (typeof each === 'string'
      ? each === b[index]
      : versionCanonical([each as NamedStep]) === versionCanonical([b[index] as NamedStep])));
  }
  return a === b;
}

/**
 * The kept steps that moved: those outside the longest run both orders share, found by the table every side builds the same
 * way, a tie skipping the newer order's step, so a step moved earlier is the one marked.
 */
function movedOf(before: readonly string[], after: readonly string[]): Set<string> {
  const longest: number[][] = Array.from({ length: before.length + 1 }, () => new Array<number>(after.length + 1).fill(0));
  for (let i = before.length - 1; i >= 0; i -= 1) {
    for (let j = after.length - 1; j >= 0; j -= 1) {
      longest[i]![j] = before[i] === after[j] ? longest[i + 1]![j + 1]! + 1 : Math.max(longest[i + 1]![j]!, longest[i]![j + 1]!);
    }
  }
  const shared = new Set<string>();
  let i = 0;
  let j = 0;
  while (i < before.length && j < after.length) {
    if (before[i] === after[j]) {
      shared.add(before[i]!);
      i += 1;
      j += 1;
    } else if (longest[i + 1]![j]! > longest[i]![j + 1]!) {
      i += 1;
    } else {
      j += 1;
    }
  }
  return new Set(after.filter((id) => !shared.has(id)));
}

/**
 * Two versions compared by step id (design §8.2), in the newer one's order with each removed step where it stood, after the
 * kept step before it: each entry, then the lines it is said in — the person's part first, then the steps, then what leaves
 * this machine with no press of theirs that did not before. A null `before` is a new workflow.
 */
export function workflowDiff(before: readonly NamedStep[] | null, after: readonly NamedStep[]): { entries: DiffEntry[]; said: string[] } {
  const old = before ?? [];
  const afterIds = new Set(after.map((step) => step.id));
  const oldById = new Map(old.map((step) => [step.id, step]));
  const kept = new Set(after.filter((step) => oldById.has(step.id)).map((step) => step.id));
  const moved = movedOf(old.filter((step) => kept.has(step.id)).map((step) => step.id), after.filter((step) => kept.has(step.id)).map((step) => step.id));

  // Each removed step after the kept step that stood before it, or first where none did.
  const removedAfter = new Map<string | null, NamedStep[]>();
  let anchor: string | null = null;
  for (const step of old) {
    if (afterIds.has(step.id)) anchor = step.id;
    else removedAfter.set(anchor, [...(removedAfter.get(anchor) ?? []), step]);
  }

  const entries: DiffEntry[] = [];
  const pairs: [NamedStep | undefined, NamedStep | undefined][] = [];
  const removed = (at: string | null): void => {
    for (const step of removedAfter.get(at) ?? []) {
      entries.push({ id: step.id, kind: step.kind, change: 'removed', fields: [], moved: false });
      pairs.push([step, undefined]);
    }
  };
  removed(null);
  for (const step of after) {
    const was = oldById.get(step.id);
    if (was === undefined) {
      entries.push({ id: step.id, kind: step.kind, change: 'added', fields: [], moved: false });
    } else {
      const fields = step.fields.filter(([name, value]) => !same(field(was, name), value)).map(([name]) => name);
      entries.push({ id: step.id, kind: step.kind, change: 'kept', fields, moved: moved.has(step.id) });
    }
    pairs.push([was, step]);
    if (was !== undefined) removed(step.id);
  }

  const part: string[] = [];
  for (const [was, now] of pairs) {
    const id = (now ?? was)!.id;
    const wasYours = partOf(was);
    const isYours = partOf(now);
    if (wasYours !== null && wasYours === isYours) part.push(`  = ${id}: you still ${isYours}.`);
    else {
      if (wasYours !== null) part.push(`  − ${id}: you no longer ${wasYours}.`);
      if (isYours !== null) part.push(`  + ${id}: you ${isYours}.`);
    }
  }

  const steps = entries.map((entry, index) => {
    const [was, now] = pairs[index]!;
    let mark = ' ';
    let detail = '';
    if (entry.change === 'added') {
      mark = '+';
      const row = kindOf(now!.kind)!;
      const shown = now!.fields.filter(([name, value]) => !same(value, row.fields.find((each) => each.name === name)!.default));
      if (shown.length > 0) detail = `: ${shown.map(([name, value]) => `${name} ${rendered(value)}`).join('; ')}`;
    } else if (entry.change === 'removed') {
      mark = '−';
    } else if (entry.fields.length > 0) {
      mark = '~';
      detail = `: ${entry.fields.map((name) => `${name} ${rendered(field(was!, name)!)} → ${rendered(field(now!, name)!)}`).join('; ')}`
        + (entry.moved ? '; moved' : '');
    } else if (entry.moved) {
      mark = '↕';
    }
    return `  ${mark} ${entry.id} · ${entry.kind}${detail}`;
  });

  const standingBefore = standingOf(old);
  const standing = standingOf(after).filter((sentence) => !standingBefore.includes(sentence));
  return {
    entries,
    said: [
      'Your part:',
      ...(part.length > 0 ? part : ['  nothing in it is yours.']),
      'Steps, by id:',
      ...steps,
      'Without asking you each time:',
      ...(standing.length > 0 ? standing.map((sentence) => `  ${sentence}`) : ['  nothing new leaves this machine without you.']),
    ],
  };
}

/** The person's part in a step, as the diff says it after *you*, or null where they take none (design §3.1). */
function partOf(step: NamedStep | undefined): string | null {
  if (step === undefined) return null;
  switch (step.kind) {
    case 'look': return 'look at it running before it lands';
    case 'landing': return field(step, 'accept') === 'you' ? 'accept each piece of work before it lands' : null;
    case 'pull-request': return 'merge its pull request on the platform';
    case 'go-ahead': return `answer a go-ahead: "${field(step, 'act') as string}"`;
    case 'stage': return field(step, 'start') === 'you' ? `start \`${field(step, 'stage') as string}\`` : null;
    default: return null;
  }
}

/** A field's value as a diff says it: absence as declared, a switch, text and names in backticks, a group by its size. */
function rendered(value: StepValue): string {
  if (value === null) return 'as declared';
  if (typeof value === 'boolean') return value ? 'true' : 'false';
  if (typeof value === 'string') return `\`${value}\``;
  if (value.length > 0 && typeof value[0] !== 'string') return `a group of ${value.length}`;
  return (value as string[]).map((each) => `\`${each}\``).join(', ');
}

/** What a version makes leave this machine with no press of the person's (design §8.2): a push, a stage started. */
function standingOf(steps: readonly NamedStep[]): string[] {
  const said: string[] = [];
  for (const step of [...steps, ...steps.flatMap(members)]) {
    if (step.kind === 'landing' && field(step, 'form') === 'branch' && field(step, 'accept') === 'automatic') {
      const plugin = field(step, 'plugin');
      if (plugin === null) said.push('the plugin the landing rule names, if any, pushes each branch with no press of yours.');
      else if (plugin !== 'none') said.push(`\`${plugin as string}\` pushes each branch with no press of yours.`);
    }
    if (step.kind === 'stage' && field(step, 'start') === 'automatic') {
      said.push(`\`${field(step, 'plugin') as string}\` starts \`${field(step, 'stage') as string}\` with no press of yours.`);
    }
  }
  return said;
}

/** A step as a version saves it: its id, its kind and the fields it names. */
export type SavedStep = Record<string, unknown> & { id: string; kind: string };

/** One preset built in (design §2.7): its id, its name and its steps as saved. Its line is composed (`workflowLine`). */
export interface Preset {
  id: string;
  name: string;
  steps: readonly SavedStep[];
}

const WORK: SavedStep = { id: 'work', kind: 'work' };
const PULL_REQUEST: SavedStep = { id: 'pull-request', kind: 'pull-request' };

/** The presets built in, in order — the driver's `WorkflowPresets.Table`, cell for cell; nothing is stored until one is saved. */
export const WORKFLOW_PRESETS: readonly Preset[] = [
  { id: 'merge-after-accept', name: 'Merge after you accept', steps: [WORK, { id: 'landing', kind: 'landing', form: 'merge', accept: 'you' }] },
  {
    id: 'branch-to-push', name: 'A branch for you to push',
    steps: [WORK, { id: 'landing', kind: 'landing', form: 'branch', accept: 'you', plugin: 'none' }],
  },
  {
    id: 'pull-request-after-accept', name: 'A pull request after you accept',
    steps: [WORK, { id: 'landing', kind: 'landing', form: 'branch', accept: 'you' }, PULL_REQUEST],
  },
  {
    id: 'pull-request-no-press', name: 'A pull request with no press',
    steps: [WORK, { id: 'landing', kind: 'landing', form: 'branch', accept: 'automatic' }, PULL_REQUEST],
  },
];

/**
 * Steps with a second opinion and your look added where they are not (design §2.7's *with a second opinion*, *with your look
 * first*), each reading what the repository declares: the opinion before the look, both before the landing.
 */
export function withGates(steps: readonly SavedStep[], gates: { opinion: boolean; look: boolean }): SavedStep[] {
  const next = steps.map((step) => ({ ...step }));
  const at = (kind: string): number => next.findIndex((step) => step.kind === kind);
  if (gates.opinion && at('opinion') === -1) {
    const look = at('look');
    next.splice(look === -1 ? at('landing') : look, 0, { id: 'opinion', kind: 'opinion' });
  }
  if (gates.look && at('look') === -1) next.splice(at('landing'), 0, { id: 'look', kind: 'look' });
  return next;
}

/**
 * The line a workflow is shown by (design §2.5, §2.7), composed from its steps so it cannot drift from them: who reads it
 * first, whether you look, how it lands and what follows, and the go-aheads on the way.
 */
export function workflowLine(steps: readonly NamedStep[]): string {
  const said: string[] = [];
  const has = (kind: string): boolean => steps.some((step) => step.kind === kind);
  if (has('opinion')) said.push('Another agent reads the work first.');
  if (has('look')) said.push('You look at it running before it lands.');
  const landing = steps.find((step) => step.kind === 'landing')!;
  const you = field(landing, 'accept') === 'you';
  if (field(landing, 'form') === 'merge') said.push('You accept each piece of work, and it merges into the line.');
  else if (has('pull-request')) {
    said.push(you ? 'You accept; the plugin pushes and opens a pull request; you merge it.'
      : 'A pull request opens once its quest is done; you merge it.');
  } else if (field(landing, 'plugin') === 'none') {
    said.push(you ? 'You accept each piece of work onto a branch, and push it yourself.'
      : 'Each piece of work lands on a branch once its quest is done, and you push it yourself.');
  } else {
    said.push(you ? 'You accept each piece of work onto a branch, and its plugin pushes it.'
      : 'Each piece of work lands on a branch once its quest is done, and its plugin pushes it.');
  }
  const goAheads = steps.filter((step) => step.kind === 'go-ahead').length;
  if (goAheads === 1) said.push('You answer a go-ahead on the way.');
  else if (goAheads > 1) said.push(`You answer ${goAheads} go-aheads on the way.`);
  return said.join(' ');
}

/** A version to add to a workflow's file: its steps as saved, when, and through which door; the name only for a new one. */
export interface VersionAdded {
  id: string;
  name: string | null;
  door: string;
  at: string;
  steps: unknown;
}

/**
 * A version added to a workflow's file, planned and not written (design §2.6): the next number after the last, the file's
 * other fields and every version kept as written, a version that does not read here included, then the newest 20 kept with
 * every version a kept run names. A file that does not read is never written over, and steps that do not read write nothing.
 *
 * @param file The file as it stands, parsed, or null for a new workflow.
 * @param kept The versions a kept run names.
 */
export function planAddVersion(file: unknown, add: VersionAdded, kept: readonly number[] = []):
  { result: Record<string, unknown> | null; version: number | null; problem: string | null } {
  const refused = (problem: string) => ({ result: null, version: null, problem });
  const version = { version: 0, at: add.at, door: add.door, steps: add.steps };
  if (file === null) {
    const problem = workflowIdProblem(add.id) ?? workflowNameProblem(add.name);
    if (problem !== null) return refused(problem);
    version.version = 1;
    const read = readVersion(version, 1, []);
    return read.problem !== null ? refused(read.problem) : { result: { id: add.id, name: add.name, versions: [version] }, version: 1, problem: null };
  }

  const read = readWorkflowFile(file);
  if (read.problem !== null) return refused(`nothing was written: the workflow's file cannot be read — ${read.problem}`);
  if (read.id !== add.id) return refused(`nothing was written: the file holds the workflow \`${read.id}\`, not \`${add.id}\`.`);
  version.version = read.versions[read.versions.length - 1]!.version + 1;
  const candidate = readVersion(version, version.version, read.versions.filter((each) => each.steps !== null));
  if (candidate.problem !== null) return refused(candidate.problem);

  const versions = [...((file as Record<string, unknown>)['versions'] as Record<string, unknown>[]), version];
  while (versions.length > KEPT_VERSIONS) {
    const oldest = versions.findIndex((each, index) => index < versions.length - 1 && !kept.includes(each['version'] as number));
    if (oldest === -1) break;
    versions.splice(oldest, 1);
  }
  return { result: { ...(file as Record<string, unknown>), versions }, version: version.version, problem: null };
}
