// A plugin's steps and resources (FABRIC-UNIT1d; the fabric events design §D4, §D6, §D9, §E2) — the CLI's half of a twin
// with the driver's `PluginSteps.cs` and `NodeDeclarations.Problem`. A plugin declares a step once in its manifest, and it is
// read into the one node declaration a built-in is, of the plugin's own kind `<plugin>/<step>`: never a second model.
//
// 🔴 A problem refuses the plugin, as a server's or a skill's does (§C4): the rules are checked in one order, the first broken
// one said, in the same code and words as the driver. `plugin-steps.test.ts` parses `PluginStepsTests.cs`'s theory and holds
// its own table to it, cell for cell. Reading starts nothing: no transport, no dispatch (PERFORM1a onward).

import { isPluginId } from './plugins.ts';

/** A node's declared retry (§D4, §D6). Twin: `NodeRetry`. */
export interface NodeRetry { maxAttempts: number; delaysSeconds: number[] }

/** How a plugin's performed node is performed (§D6, the performer's contract). Twin: `NodePerform`. */
export interface NodePerform { idempotent: boolean; reconcile: boolean; transient: string[] }

/**
 * One node (§E2), as a plugin's manifest declares it: `id` and `kind` are both `<plugin>/<step>`. Twin: the driver's
 * `NodeDeclaration`, field for field.
 */
export interface NodeDeclaration {
  id: string;
  kind: string;
  version: number;
  reach: string;
  /** Each input by name, with the schema it takes; ordered by name. */
  inputs: Record<string, string>;
  /** Each output by name, with its schema; ordered by name. */
  outputs: Record<string, string>;
  /** The schema of the evidence it is reached on, or null. */
  evidence: string | null;
  retry: NodeRetry | null;
  timeoutSeconds: number | null;
  rejectable: boolean;
  skippable: boolean;
  perform: NodePerform | null;
}

/** What a step creates that a unit's cleanup must account for (§D3). Twin: `PluginResource`. */
export interface PluginResource { schema: string; role: string; cleanup: string }

/** The manifest's `steps` and `resources` as read: each list, or the first problem's code and words. Twin: `PluginStepsRead`. */
export interface StepsRead { steps: NodeDeclaration[]; resources: PluginResource[]; code: string | null; problem: string | null }

/** The point a performed step is asked on (§D6). Twin: `PluginSteps.PerformPoint`. */
export const PERFORM_POINT = 'work/perform';

/** The point that answers what became of an attempt (§D6). Twin: `PluginSteps.ReconcilePoint`. */
export const RECONCILE_POINT = 'work/reconcile';

/** The point a plugin removes what it created on (§D3). Twin: `PluginSteps.CleanupPoint`. */
export const CLEANUP_POINT = 'work/cleanup';

/** A declared resource's role, `temporary` where none is said. Twin: `PluginResourceRoles`. */
export const RESOURCE_ROLES: readonly string[] = Object.freeze(['temporary', 'result']);

/** A step's, a port's or a failure code's name: lowercase letters, digits and dashes, at most 40. Twin: `PluginSteps.NameShape`, `NodePerform.CodeShape`. */
const STEP_NAME = /^[a-z0-9][a-z0-9-]{0,39}$/;

const SCHEMA_PARTS = /^([^/]+)\/[a-z0-9]+(?:[.-][a-z0-9]+)*@[1-9][0-9]*$/;

/**
 * A schema's name, `<owner>/<name>@<version>`: its owner a plugin id by `isPluginId`, the one owner of that shape (REFAC1);
 * its name words of lowercase letters and digits parted by single dots or dashes; its version a whole number from 1. Twin:
 * `PluginSteps.IsSchema`.
 */
export function isSchema(text: string): boolean {
  const parts = SCHEMA_PARTS.exec(text);
  return parts !== null && isPluginId(parts[1]!);
}

/** Whether a kind is a plugin's own node: `<plugin>/<node>`, both parts named. Twin: `NodeKinds.IsPlugin`. */
function isPluginKind(kind: string): boolean {
  const slash = kind.indexOf('/');
  return slash > 0 && slash < kind.length - 1 && kind.indexOf('/', slash + 1) < 0 && !/\s/.test(kind);
}

/**
 * A declaration's own checks, for a plugin's own kind, the only kind a manifest declares: the twin of the driver's
 * `NodeDeclarations.Problem`, in its order and codes. A plugin's node may always be failed, rejected where it says so and
 * skipped unless it says not, so the built-ins' checks on those never apply here.
 */
export function declarationProblem(node: NodeDeclaration): string | null {
  if (node.id.trim() === '') return 'id';
  if (!isPluginKind(node.kind)) return 'unknown-kind';
  if (node.reach !== 'observed' && node.reach !== 'performed') return 'reach';
  if (node.version < 1) return 'version';
  if (node.retry !== null && (node.retry.maxAttempts < 1 || node.retry.delaysSeconds.some((delay) => delay < 0))) return 'retry';
  if (node.timeoutSeconds !== null && node.timeoutSeconds <= 0) return 'timeoutSeconds';
  // FABRIC-WIRE1a: a data wire names a node's evidence by this port, so no output may take its name.
  if (Object.hasOwn(node.outputs, 'evidence')) return 'outputs';
  // FABRIC-UNIT1d (§D6): how a plugin performs its node is that node's alone, and a retry or a transient code is refused where
  // it could never be taken.
  const retries = node.retry !== null && node.retry.maxAttempts > 1;
  if (node.perform !== null) {
    if (node.reach !== 'performed') return 'perform';
    const codes = node.perform.transient;
    if (codes.some((code) => !STEP_NAME.test(code)) || new Set(codes).size !== codes.length || (codes.length > 0 && !retries)) {
      return 'transient';
    }
  }
  if (retries && !(node.perform !== null && node.perform.transient.length > 0 && (node.perform.idempotent || node.perform.reconcile))) {
    return 'retry';
  }
  return null;
}

/** A whole number a manifest writes, `2` or `2.0` alike, within a 32-bit integer; null for anything else. Twin: `PluginSteps.Whole`. */
function whole(value: unknown): number | null {
  return typeof value === 'number' && Number.isInteger(value) && value >= -2147483648 && value <= 2147483647 ? value : null;
}

/** A JSON `true` or `false`, `absent` where it is not written; null for anything else. */
function flag(row: Record<string, unknown>, field: string, absent: boolean): boolean | null {
  const value = row[field];
  return value === undefined ? absent : typeof value === 'boolean' ? value : null;
}

const isObject = (value: unknown): value is Record<string, unknown> =>
  typeof value === 'object' && value !== null && !Array.isArray(value);

const schemaOf = (value: unknown): string | null => (typeof value === 'string' && isSchema(value) ? value : null);

const ordinal = (a: string, b: string): number => (a < b ? -1 : a > b ? 1 : 0);

/** A port map: absent is none; otherwise each name of `STEP_NAME` with its schema, ordered by name; null where it breaks that. */
function ports(row: Record<string, unknown>, field: string): Record<string, string> | null {
  const declared = row[field];
  if (declared === undefined) return {};
  if (!isObject(declared)) return null;
  const read: Array<[string, string]> = [];
  for (const [name, value] of Object.entries(declared)) {
    const schema = schemaOf(value);
    if (!STEP_NAME.test(name) || schema === null) return null;
    read.push([name, schema]);
  }
  return Object.fromEntries(read.sort(([a], [b]) => ordinal(a, b)));
}

/** The words each code is said in, naming the step by its manifest's id or the resource by its schema. Twin: `PluginSteps.Words`. */
function words(code: string, name: string | null): string {
  switch (code) {
    case 'steps': return '`steps` must be an array of the steps the plugin declares.';
    case 'id': return 'a declared step needs an `id` — lowercase letters, digits and dashes, at most 40; its kind is `<plugin>/<id>`.';
    case 'step-twice': return `step \`${name}\` is declared twice.`;
    case 'version': return `step \`${name}\`'s \`version\` must be a whole number, 1 or more: an acceptance binds it, so a new version lapses the old ones.`;
    case 'reach': return `step \`${name}\`'s \`reach\` must be \`observed\`, which the plugin reports, or \`performed\`, which Daoris asks the plugin to do.`;
    case 'inputs': return `step \`${name}\`'s \`inputs\` must name each input, in lowercase letters, digits and dashes, with the schema it takes, like \`acme.build/artifact@1\`.`;
    case 'outputs': return `step \`${name}\`'s \`outputs\` must name each output, in lowercase letters, digits and dashes and never \`evidence\`, with its schema, like \`acme.deploy/preview@1\`.`;
    case 'evidence': return `step \`${name}\`'s \`evidence\` must be an object whose \`schema\` names what it is reached on, like \`acme.deploy/receipt@1\`.`;
    case 'retry': return `step \`${name}\`'s \`retry\` must be \`maxAttempts\`, 1 or more, and \`delaysSeconds\`, each 0 or more; more than one attempt only where its \`perform\` lists \`transient\` codes and is \`idempotent\` or has \`reconcile\`.`;
    case 'timeoutSeconds': return `step \`${name}\`'s \`timeoutSeconds\` must be a whole number of seconds, above 0.`;
    case 'skippable': return `step \`${name}\`'s \`skippable\` must be \`true\` or \`false\`: whether the person may pass it over.`;
    case 'rejectable': return `step \`${name}\`'s \`rejectable\` must be \`true\` or \`false\`: whether the person may reject it.`;
    case 'perform': return `step \`${name}\`'s \`perform\` is a \`performed\` step's alone: an object whose \`point\`, where it names one, is \`work/perform\`.`;
    case 'idempotent': return `step \`${name}\`'s \`idempotent\` must be \`true\` or \`false\`: whether the plugin deduplicates a redelivered key.`;
    case 'reconcile': return `step \`${name}\`'s \`reconcile\` must be \`work/reconcile\`, the point that answers what became of an attempt.`;
    case 'transient': return `step \`${name}\`'s \`transient\` must list the failure codes worth retrying, once each, in lowercase letters, digits and dashes, and only with a \`retry\` of more than one attempt.`;
    case 'resources': return "`resources` must be an array of what the plugin's steps create.";
    case 'schema': return 'a declared resource needs a `schema` — what it is, like `acme.deploy/preview@1`.';
    case 'resource-twice': return `resource \`${name}\` is declared twice.`;
    case 'role': return `resource \`${name}\`'s \`role\` must be \`temporary\`, removed at the end, or \`result\`, kept as the work's result.`;
    case 'cleanup': return `resource \`${name}\`'s \`cleanup\` must be \`work/cleanup\`: the plugin removes what it made through its own cleanup point, and Daoris never does.`;
    default: return `step \`${name}\` is no node this build reads (\`${code}\`).`;
  }
}

const refused = (code: string, name: string | null): StepsRead => ({ steps: [], resources: [], code, problem: words(code, name) });

type StepRead = { step: NodeDeclaration; code: null; name: string } | { step: null; code: string; name: string | null };

/** One declared step, or the code of its first problem and the name it is said by. Twin: `PluginSteps.Step`. */
function stepDeclared(plugin: string, row: unknown, read: NodeDeclaration[]): StepRead {
  const name = isObject(row) && typeof row.id === 'string' ? row.id : null;
  if (name === null || !STEP_NAME.test(name)) return { step: null, code: 'id', name: null };
  const fields = row as Record<string, unknown>;
  const kind = `${plugin}/${name}`;
  if (read.some((step) => step.kind === kind)) return { step: null, code: 'step-twice', name };
  const broken = (code: string): StepRead => ({ step: null, code, name });

  let version = 1;
  if (fields.version !== undefined) {
    const declared = whole(fields.version);
    if (declared === null) return broken('version');
    version = declared;
  }

  let reach = '';
  if (fields.reach !== undefined) {
    if (typeof fields.reach !== 'string') return broken('reach');
    reach = fields.reach;
  }

  const inputs = ports(fields, 'inputs');
  if (inputs === null) return broken('inputs');
  const outputs = ports(fields, 'outputs');
  if (outputs === null) return broken('outputs');

  let evidence: string | null = null;
  if (fields.evidence !== undefined) {
    evidence = isObject(fields.evidence) ? schemaOf(fields.evidence.schema) : null;
    if (evidence === null) return broken('evidence');
  }

  let retry: NodeRetry | null = null;
  if (fields.retry !== undefined) {
    const declared = fields.retry;
    const most = isObject(declared) ? whole(declared.maxAttempts) : null;
    if (!isObject(declared) || most === null) return broken('retry');
    const delaysSeconds: number[] = [];
    if (declared.delaysSeconds !== undefined) {
      if (!Array.isArray(declared.delaysSeconds)) return broken('retry');
      for (const delay of declared.delaysSeconds) {
        const seconds = whole(delay);
        if (seconds === null) return broken('retry');
        delaysSeconds.push(seconds);
      }
    }
    retry = { maxAttempts: most, delaysSeconds };
  }

  let timeoutSeconds: number | null = null;
  if (fields.timeoutSeconds !== undefined) {
    timeoutSeconds = whole(fields.timeoutSeconds);
    if (timeoutSeconds === null) return broken('timeoutSeconds');
  }

  const skippable = flag(fields, 'skippable', true);
  if (skippable === null) return broken('skippable');
  const rejectable = flag(fields, 'rejectable', false);
  if (rejectable === null) return broken('rejectable');

  let perform: NodePerform | null = null;
  if (fields.perform !== undefined) {
    const declared = fields.perform;
    if (!isObject(declared) || (declared.point !== undefined && declared.point !== PERFORM_POINT)) return broken('perform');
    const idempotent = flag(declared, 'idempotent', false);
    if (idempotent === null) return broken('idempotent');
    if (declared.reconcile !== undefined && declared.reconcile !== RECONCILE_POINT) return broken('reconcile');
    const transient: string[] = [];
    if (declared.transient !== undefined) {
      if (!Array.isArray(declared.transient)) return broken('transient');
      for (const code of declared.transient) {
        if (typeof code !== 'string') return broken('transient');
        transient.push(code);
      }
    }
    perform = { idempotent, reconcile: declared.reconcile === RECONCILE_POINT, transient };
  }

  const step: NodeDeclaration = {
    id: kind, kind, version, reach, inputs, outputs, evidence, retry, timeoutSeconds, rejectable, skippable, perform,
  };
  // The one model's own checks, whose code is the field the words name.
  const problem = declarationProblem(step);
  return problem === null ? { step, code: null, name } : broken(problem);
}

/**
 * The manifest's `steps` and `resources`, read from its root object for plugin `plugin`: absent is none, so a manifest written
 * before reads as it did. Twin: `PluginSteps.Read`.
 */
export function stepsDeclared(plugin: string, root: Record<string, unknown>): StepsRead {
  const steps: NodeDeclaration[] = [];
  if (root.steps !== undefined) {
    if (!Array.isArray(root.steps)) return refused('steps', null);
    for (const row of root.steps) {
      const read = stepDeclared(plugin, row, steps);
      if (read.code !== null) return refused(read.code, read.name);
      steps.push(read.step);
    }
  }

  const resources: PluginResource[] = [];
  if (root.resources !== undefined) {
    if (!Array.isArray(root.resources)) return refused('resources', null);
    for (const row of root.resources) {
      const schema = isObject(row) ? schemaOf(row.schema) : null;
      if (schema === null) return refused('schema', null);
      if (resources.some((resource) => resource.schema === schema)) return refused('resource-twice', schema);
      const fields = row as Record<string, unknown>;
      if (fields.role !== undefined && !(typeof fields.role === 'string' && RESOURCE_ROLES.includes(fields.role))) {
        return refused('role', schema);
      }
      // §D3: who cleans it is said, never guessed; an unknown owner would block the unit's cleanup.
      if (fields.cleanup !== CLEANUP_POINT) return refused('cleanup', schema);
      resources.push({ schema, role: (fields.role as string | undefined) ?? 'temporary', cleanup: CLEANUP_POINT });
    }
  }

  return { steps, resources, code: null, problem: null };
}
