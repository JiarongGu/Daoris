// Which workflow work follows (WORKFLOW1e, D157 point 10; the workflow design §2.5, §4.1–§4.3): `workflows` by repository and
// `workspaceWorkflows` by workspace in `driver.json`, each a default and kinds, a workspace's kinds declared with a label and the
// paths work of that kind keeps to; read, written, edited, and resolved in §4.1's order; and the versions a kept run names.
//
// 🔴 A TWIN with the driver's `WorkflowSelection.cs` and `WorkflowRunBindings.KeptVersions`: the CLI and the driver share no
// code, so both hold ONE table, the driver suite's `fixtures/workflow-selection.json`, cell for cell, every sentence included
// (`.claude/knowledge/twins.md`).
//
// A repository's choice replaces its workspace's whole (§4.1), as its landing, review and opinion rules do: a repository that
// names any workflow follows its own kinds and its own default, Current where it names none. `current` at any level is a choice
// like any other. Pure: it reads no file but the runs `readKeptVersions` is pointed at, and reaches no network.

import { existsSync, readdirSync } from 'node:fs';
import { join } from 'node:path';
import { atName, findName } from './casefold.ts';
import { readText } from './fsx.ts';
import { normalizeWorkspace } from './remotemap.ts';
import { ID_SHAPE, isWorkflowId } from './workflowid.ts';

/** The workflow drawn from the rules as they stand (design §2.7). */
export const CURRENT = 'current';

/** The most kinds one choice holds (design §4.2). */
export const MAX_KINDS = 12;

/**
 * A kind as a level's choice holds it: for a workspace, its label, the paths work of that kind keeps to and the workflow it maps
 * to; for a repository, only the workflow. The driver's `WorkflowKindChoice`.
 */
export interface WorkflowKindChoice {
  label: string | null;
  paths: string[];
  /** A workflow's id, `current`, or null where the kind maps to nothing (the level's default). */
  workflow: string | null;
}

/** One level's choice: its default and its kinds, in the order written. The driver's `WorkflowChoice`. */
export interface WorkflowChoice {
  default: string | null;
  kinds: [string, WorkflowKindChoice][];
}

/** Both maps, as `driver.json` holds them. */
export interface WorkflowChoiceMaps {
  workflows: Record<string, WorkflowChoice>;
  workspaceWorkflows: Record<string, WorkflowChoice>;
}

/** The task's own choice (design §4.1 row 1): its ask's latest, each null where it names none. */
export interface WorkflowTaskChoice {
  kind: string | null;
  workflow: string | null;
}

/** Which level of §4.1's table chose a workflow — the driver's `WorkflowLevels`. */
export type WorkflowLevel = 'task' | 'repository-kind' | 'repository' | 'workspace-kind' | 'workspace' | 'current';

/** The workflow work follows, and why — the driver's `WorkflowSelected`. */
export interface WorkflowSelected {
  workflow: string;
  level: WorkflowLevel;
  kind: string | null;
  label: string | null;
  paths: string[];
  /** The task's kind where its workspace does not declare it, read as none. */
  undeclared: string | null;
}

/** An edit to the choices, as both doors make it and the table writes it. */
export type WorkflowChoiceEdit =
  | { use: string | null; repository?: string; workspace?: string; kind?: string; in?: string | null }
  | { declare: string; workspace: string; label: string; paths: string[] }
  | { drop: string; workspace: string };

const SHAPE = 'a workflow choice is an object: its `default`, its `kinds`, or both.';
const DEFAULT = 'its `default` names a workflow: its id, lower-case letters, digits and dashes, at most 40, or `current`.';
const KINDS = 'its `kinds` is an object, each kind by its id.';
const MANY = 'a choice holds at most 12 kinds.';
const NAMED = 'a choice names the repository or the workspace it is for.';

export const kindIdProblem = (kind: string): string =>
  `\`${kind}\` is not a kind's id: lower-case letters, digits and dashes, at most 24, such as \`docs\`.`;
const mappedProblem = (kind: string): string => `kind \`${kind}\` maps to a workflow: its id, or \`current\`.`;
const declaredProblem = (kind: string): string =>
  `kind \`${kind}\` is an object: its \`label\`, and its \`paths\` and \`workflow\` where it has them.`;
export const labelProblem = (kind: string): string => `kind \`${kind}\`'s \`label\` is your words for it, 1 to 60 characters.`;
export const pathsProblem = (kind: string): string =>
  `kind \`${kind}\`'s \`paths\` is a list of at most 20 paths in a repository, each with \`/\` between its parts, none absolute, `
  + 'none with `..` or spaces around it, none twice, each at most 200 characters.';
const kindWorkflowProblem = (kind: string): string => `kind \`${kind}\`'s \`workflow\` names a workflow: its id, or \`current\`.`;
export const workflowChoiceProblem = (workflow: string): string =>
  `\`${workflow}\` is not a workflow's id: lower-case letters, digits and dashes, at most 40, such as \`docs-to-pr\`; or \`current\`.`;
export const undeclaredInWorkspace = (workspace: string, kind: string): string =>
  `workspace \`${workspace}\` declares no kind \`${kind}\`: a kind is declared first, with its label.`;
const undeclaredForRepository = (repository: string, workspace: string, kind: string): string =>
  `\`${repository}\`'s workspace, \`${workspace}\`, declares no kind \`${kind}\`: a kind is declared first, with its label.`;
const noneToDrop = (workspace: string, kind: string): string =>
  `workspace \`${workspace}\` declares no kind \`${kind}\`, so there is none to drop.`;

function isObject(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

/** A kind's id: lower-case letters, digits and dashes, at most 24 — the driver's `WorkflowSelection.IsKindId`. */
export function isKindId(value: unknown): value is string {
  return typeof value === 'string' && value.length >= 1 && value.length <= 24 && ID_SHAPE.test(value);
}

/** A kind's label: the person's words, not blank, at most 60 characters, kept as written. */
export function isLabel(value: unknown): value is string {
  return typeof value === 'string' && value.length <= 60 && value.trim().length > 0;
}

/**
 * A kind's paths: at most 20, each relative with `/` between its parts, no `..` part, no spaces around it and no control
 * character, at most 200 characters, and none twice — the driver's `WorkflowSelection.ArePaths`.
 */
export function arePaths(paths: readonly string[]): boolean {
  return paths.length <= 20
    && paths.every((path) => path.length >= 1 && path.length <= 200 && path === path.trim() && !path.startsWith('/')
      && !path.includes('\\') && !path.split('/').includes('..') && ![...path].some((c) => c.charCodeAt(0) < 0x20))
    && new Set(paths).size === paths.length;
}

/**
 * An entry read (design §2.5): its first problem, or the choice; a choice naming nothing is no problem. A repository's kind maps
 * to a workflow; a workspace's is an object, its label first. Null is absent, and a field a kind has no name for is not kept.
 */
export function workflowChoiceOf(entry: unknown, workspace: boolean): { choice: WorkflowChoice | null; problem: string | null } {
  const refused = (problem: string) => ({ choice: null, problem });
  if (!isObject(entry)) return refused(SHAPE);

  let chosen: string | null = null;
  if (entry['default'] !== undefined && entry['default'] !== null) {
    if (!isWorkflowId(entry['default'])) return refused(DEFAULT);
    chosen = entry['default'];
  }

  const kinds: [string, WorkflowKindChoice][] = [];
  const map = entry['kinds'];
  if (map !== undefined && map !== null) {
    if (!isObject(map)) return refused(KINDS);
    if (Object.keys(map).length > MAX_KINDS) return refused(MANY);
    for (const [id, value] of Object.entries(map)) {
      const { kind, problem } = kindOf(id, value, workspace);
      if (problem !== null) return refused(problem);
      kinds.push([id, kind!]);
    }
  }

  return { choice: { default: chosen, kinds }, problem: null };
}

function kindOf(id: string, value: unknown, workspace: boolean): { kind: WorkflowKindChoice | null; problem: string | null } {
  if (!isKindId(id)) return { kind: null, problem: kindIdProblem(id) };
  if (!workspace) {
    return isWorkflowId(value) ? { kind: { label: null, paths: [], workflow: value }, problem: null } : { kind: null, problem: mappedProblem(id) };
  }

  if (!isObject(value)) return { kind: null, problem: declaredProblem(id) };
  if (!isLabel(value['label'])) return { kind: null, problem: labelProblem(id) };
  let paths: string[] = [];
  if (value['paths'] !== undefined && value['paths'] !== null) {
    const list = value['paths'];
    if (!Array.isArray(list) || list.some((path) => typeof path !== 'string') || !arePaths(list as string[])) {
      return { kind: null, problem: pathsProblem(id) };
    }
    paths = [...(list as string[])];
  }

  let workflow: string | null = null;
  if (value['workflow'] !== undefined && value['workflow'] !== null) {
    if (!isWorkflowId(value['workflow'])) return { kind: null, problem: kindWorkflowProblem(id) };
    workflow = value['workflow'];
  }

  return { kind: { label: value['label'], paths, workflow }, problem: null };
}

/** Whether a choice says nothing: no default and no kind, so it is not kept. */
function namesNothing(choice: WorkflowChoice): boolean {
  return choice.default === null && choice.kinds.length === 0;
}

/**
 * A map of choices as `driver.json` holds it — the driver's `WorkflowSelection.Map`: each name trimmed, a blank one not read; an
 * entry with a problem, and one naming nothing, not read; a name written twice in any case read where first readable. A map that
 * is not one is none.
 */
export function workflowChoicesOf(value: unknown, workspace: boolean): Record<string, WorkflowChoice> {
  if (!isObject(value)) return {};
  const held: Record<string, WorkflowChoice> = {};
  for (const [name, entry] of Object.entries(value)) {
    const named = name.trim();
    if (named.length === 0 || findName(Object.keys(held), named) !== null) continue;
    const { choice, problem } = workflowChoiceOf(entry, workspace);
    if (problem !== null || choice === null || namesNothing(choice)) continue;
    held[named] = choice;
  }
  return held;
}

/** A map as `driver.json` keeps it: each choice's default where it names one, then its kinds where it has any. */
export function writtenChoices(map: Record<string, WorkflowChoice>, workspace: boolean): Record<string, unknown> {
  return Object.fromEntries(Object.entries(map).map(([name, choice]) => [name, {
    ...(choice.default !== null ? { default: choice.default } : {}),
    ...(choice.kinds.length > 0
      ? {
          kinds: Object.fromEntries(choice.kinds.map(([id, kind]) => [id, workspace
            ? {
                label: kind.label,
                ...(kind.paths.length > 0 ? { paths: kind.paths } : {}),
                ...(kind.workflow !== null ? { workflow: kind.workflow } : {}),
              }
            : kind.workflow])),
        }
      : {}),
  }]));
}

function kindIn(choice: WorkflowChoice | undefined, kind: string): WorkflowKindChoice | null {
  return choice?.kinds.find(([id]) => id === kind)?.[1] ?? null;
}

/**
 * The workflow a chain's work in `repository` follows (design §4.1) — the driver's `WorkflowSelection.Resolve`: the task's
 * choice, then the repository's for the task's kind, its default, the workspace's for the kind, its default, then Current. A
 * repository that names any workflow replaces its workspace's whole. The task's kind is read only where its workspace declares it.
 *
 * @param repository The repository, or null for the workspace's own choice, which no repository's reaches.
 * @param workspace The repository's workspace, or null for none, read as the default workspace.
 */
export function resolveWorkflow(
  maps: WorkflowChoiceMaps, repository: string | null, workspace: string | null, task: WorkflowTaskChoice | null,
): WorkflowSelected {
  const shared = atName(maps.workspaceWorkflows, normalizeWorkspace(workspace));
  const declared = task?.kind ? kindIn(shared, task.kind) : null;
  const kind = declared === null ? null : task!.kind;
  const undeclared = declared === null ? task?.kind ?? null : null;
  const pick = (workflow: string, level: WorkflowLevel): WorkflowSelected =>
    ({ workflow, level, kind, label: declared?.label ?? null, paths: declared?.paths ?? [], undeclared });

  if (task?.workflow) return pick(task.workflow, 'task');
  const own = repository === null ? undefined : atName(maps.workflows, repository.trim());
  if (own !== undefined) {
    const mapped = kind === null ? null : kindIn(own, kind)?.workflow ?? null;
    return mapped !== null ? pick(mapped, 'repository-kind') : pick(own.default ?? CURRENT, 'repository');
  }
  if (declared?.workflow) return pick(declared.workflow, 'workspace-kind');
  return shared?.default ? pick(shared.default, 'workspace') : pick(CURRENT, 'current');
}

/**
 * An edit applied to the choices (design §4.7) — the driver's `WorkflowSelection.Apply`: a workflow used or cleared for a
 * repository or a workspace, by kind; a kind declared or dropped. A choice left naming nothing is removed, so its workspace's
 * reaches the repository again; a name is kept under the spelling first written. The edit's first problem refuses it whole.
 */
export function applyWorkflowEdit(maps: WorkflowChoiceMaps, edit: WorkflowChoiceEdit): { maps: WorkflowChoiceMaps | null; refusal: string | null } {
  const refused = (refusal: string) => ({ maps: null, refusal });
  const kept = (scope: 'workflows' | 'workspaceWorkflows', name: string, choice: WorkflowChoice) => {
    const next = { ...maps[scope] };
    const key = findName(Object.keys(next), name) ?? name;
    if (namesNothing(choice)) delete next[key];
    else next[key] = choice;
    return { maps: { ...maps, [scope]: next }, refusal: null };
  };
  const copy = (choice: WorkflowChoice | undefined): WorkflowChoice =>
    ({ default: choice?.default ?? null, kinds: (choice?.kinds ?? []).map(([id, kind]) => [id, { ...kind, paths: [...kind.paths] }]) });

  if ('declare' in edit) {
    const name = edit.workspace.trim();
    if (name.length === 0) return refused(NAMED);
    if (!isKindId(edit.declare)) return refused(kindIdProblem(edit.declare));
    if (!isLabel(edit.label)) return refused(labelProblem(edit.declare));
    if (!arePaths(edit.paths)) return refused(pathsProblem(edit.declare));
    const choice = copy(atName(maps.workspaceWorkflows, name));
    const at = choice.kinds.find(([id]) => id === edit.declare);
    if (at !== undefined) {
      at[1] = { ...at[1], label: edit.label, paths: [...edit.paths] };
    } else {
      if (choice.kinds.length >= MAX_KINDS) return refused(MANY);
      choice.kinds.push([edit.declare, { label: edit.label, paths: [...edit.paths], workflow: null }]);
    }
    return kept('workspaceWorkflows', name, choice);
  }

  if ('drop' in edit) {
    const name = edit.workspace.trim();
    if (name.length === 0) return refused(NAMED);
    const held = atName(maps.workspaceWorkflows, name);
    if (kindIn(held, edit.drop) === null) return refused(noneToDrop(name, edit.drop));
    const choice = copy(held);
    choice.kinds = choice.kinds.filter(([id]) => id !== edit.drop);
    return kept('workspaceWorkflows', name, choice);
  }

  const forWorkspace = edit.repository === undefined;
  const name = (edit.repository ?? edit.workspace ?? '').trim();
  if (name.length === 0) return refused(NAMED);
  if (edit.use !== null && !isWorkflowId(edit.use)) return refused(workflowChoiceProblem(edit.use));
  if (edit.kind !== undefined && !isKindId(edit.kind)) return refused(kindIdProblem(edit.kind));

  const scope = forWorkspace ? 'workspaceWorkflows' : 'workflows';
  const choice = copy(atName(maps[scope], name));
  if (edit.kind === undefined) {
    choice.default = edit.use;
  } else if (forWorkspace) {
    // A kind is declared before it maps to anything; clearing one that is not declared changes nothing.
    const at = choice.kinds.find(([id]) => id === edit.kind);
    if (at === undefined && edit.use !== null) return refused(undeclaredInWorkspace(name, edit.kind));
    if (at !== undefined) at[1] = { ...at[1], workflow: edit.use };
  } else {
    // Checked where the repository's workspace is known; a mapping cleared is never refused.
    if ('in' in edit && edit.use !== null) {
      const circle = normalizeWorkspace(edit.in);
      if (kindIn(atName(maps.workspaceWorkflows, circle), edit.kind) === null) {
        return refused(undeclaredForRepository(name, circle, edit.kind));
      }
    }
    const index = choice.kinds.findIndex(([id]) => id === edit.kind);
    if (edit.use === null) {
      if (index !== -1) choice.kinds.splice(index, 1);
    } else if (index !== -1) {
      choice.kinds[index] = [edit.kind, { label: null, paths: [], workflow: edit.use }];
    } else {
      if (choice.kinds.length >= MAX_KINDS) return refused(MANY);
      choice.kinds.push([edit.kind, { label: null, paths: [], workflow: edit.use }]);
    }
  }
  return kept(scope, name, choice);
}

/**
 * The versions of `workflow` the runs read name (design §2.6), in order, each once — the driver's
 * `WorkflowRunBindings.KeptVersions`: a run whose file is an object naming that workflow exactly and a whole version from 1.
 */
export function keptVersions(runs: readonly unknown[], workflow: string): number[] {
  const versions = runs
    .filter(isObject)
    .filter((run) => run['workflow'] === workflow)
    .map((run) => run['version'])
    .filter((version): version is number => typeof version === 'number' && Number.isInteger(version) && version >= 1 && version <= 2147483647);
  return [...new Set(versions)].sort((a, b) => a - b);
}

/** The folder of the runs, beside the workflows: `<home>/workflows/runs/` (the driver's `WorkflowRunBindings.FolderOf`). */
export const RUNS_FOLDER = 'runs';

/** The versions of `workflow` the runs under `workflowsFolder` name; a run's file that does not read names none. */
export function readKeptVersions(workflowsFolder: string, workflow: string): number[] {
  const folder = join(workflowsFolder, RUNS_FOLDER);
  if (!existsSync(folder)) return [];
  const runs: unknown[] = [];
  for (const name of readdirSync(folder).filter((each) => each.endsWith('.json'))) {
    try {
      runs.push(JSON.parse(readText(join(folder, name))));
    } catch {
      // A run's file that does not read names no version; the workflow keeps its newest as ever.
    }
  }
  return keptVersions(runs, workflow);
}
