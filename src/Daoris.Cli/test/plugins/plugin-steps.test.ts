import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { MANIFEST, pluginsRoot, readPlugins } from '../../src/plugins/plugins.ts';
import { stepsDeclared } from '../../src/plugins/steps.ts';
import type { NodeDeclaration, PluginResource } from '../../src/plugins/steps.ts';
import { driverRows, driverTestFile } from '../_csharp.ts';
import { makeFixture } from '../_fixture.ts';

/**
 * FABRIC-UNIT1d (the fabric events design §D4, §D6, §D9, §E2) — a plugin's steps and resources, the CLI's half of a twin with
 * the driver's `PluginSteps.cs` and `NodeDeclarations.Problem`: a step declared once in the manifest is the one node
 * declaration a built-in is. 🔴 `PluginStepsTests.cs`'s theory is parsed below and held to `STEP_ROWS`, cell for cell and in
 * order, so a row changed on one side alone fails `npm run verify`. A problem refuses the plugin.
 */

const DRIVER_TESTS = driverTestFile('PluginStepsTests.cs');

type Row = [name: string, declared: string, code: string | null, problem: string | null, steps: string, resources: string];

const ID = 'a declared step needs an `id` — lowercase letters, digits and dashes, at most 40; its kind is `<plugin>/<id>`.';
const VERSION = "step `deploy`'s `version` must be a whole number, 1 or more: an acceptance binds it, so a new version lapses the old ones.";
const REACH = "step `deploy`'s `reach` must be `observed`, which the plugin reports, or `performed`, which Daoris asks the plugin to do.";
const INPUTS = "step `deploy`'s `inputs` must name each input, in lowercase letters, digits and dashes, with the schema it takes, like `acme.build/artifact@1`.";
const OUTPUTS = "step `deploy`'s `outputs` must name each output, in lowercase letters, digits and dashes and never `evidence`, with its schema, like `acme.deploy/preview@1`.";
const EVIDENCE = "step `deploy`'s `evidence` must be an object whose `schema` names what it is reached on, like `acme.deploy/receipt@1`.";
const RETRY_RULE = "`retry` must be `maxAttempts`, 1 or more, and `delaysSeconds`, each 0 or more; more than one attempt only where its `perform` lists `transient` codes and is `idempotent` or has `reconcile`.";
const RETRY = `step \`deploy\`'s ${RETRY_RULE}`;
const TIMEOUT = "step `deploy`'s `timeoutSeconds` must be a whole number of seconds, above 0.";
const PERFORM_RULE = "`perform` is a `performed` step's alone: an object whose `point`, where it names one, is `work/perform`.";
const RECONCILE = "step `deploy`'s `reconcile` must be `work/reconcile`, the point that answers what became of an attempt.";
const TRANSIENT = "step `deploy`'s `transient` must list the failure codes worth retrying, once each, in lowercase letters, digits and dashes, and only with a `retry` of more than one attempt.";
const SCHEMA = 'a declared resource needs a `schema` — what it is, like `acme.deploy/preview@1`.';
const CLEANUP = "resource `acme.deploy/preview@1`'s `cleanup` must be `work/cleanup`: the plugin removes what it made through its own cleanup point, and Daoris never does.";
const POINT = 'step `deploy` is performed, so `hooks.points` must name `work/perform`: the point Daoris asks it at.';
const WHOLE = 'acme.deploy/deploy@2 performed in release=acme.build/artifact@1 out log=acme.deploy/log@1,preview=acme.deploy/preview@1 evidence acme.deploy/receipt@1 retry 3/5+30 timeout 600 no-skip reject perform idempotent reconcile transient=unavailable+rate-limited+timeout';

/**
 * [case, what the manifest declares after its id, the problem's code and its words or null and null, then the steps read and
 * the resources read, each as `said` says it, joined by `; `]. The rules are checked in this order and the first broken one
 * is said. The twin is `PluginStepsTests.The_steps_read_as_the_cli_reads_them`.
 */
const STEP_ROWS: Row[] = [
  ['no steps', '', null, null, '', ''],
  ['none declared', ', "steps": [], "resources": [] ', null, null, '', ''],
  ['an observed step, the least', ', "steps": [{ "id": "deployed", "reach": "observed" }] ', null, null, 'acme.deploy/deployed@1 observed', ''],
  ['a performed step, the least', ', "steps": [{ "id": "deploy", "reach": "performed" }] ', null, null, 'acme.deploy/deploy@1 performed', ''],
  ['a whole performed step', ', "steps": [{ "id": "deploy", "version": 2, "reach": "performed", "inputs": { "release": "acme.build/artifact@1" }, "outputs": { "preview": "acme.deploy/preview@1", "log": "acme.deploy/log@1" }, "evidence": { "schema": "acme.deploy/receipt@1" }, "retry": { "maxAttempts": 3, "delaysSeconds": [5, 30] }, "timeoutSeconds": 600, "skippable": false, "rejectable": true, "perform": { "point": "work/perform", "idempotent": true, "reconcile": "work/reconcile", "transient": ["unavailable", "rate-limited", "timeout"] } }] ', null, null, WHOLE, ''],
  ['a retry with no delays, reconciled', ', "steps": [{ "id": "deploy", "reach": "performed", "retry": { "maxAttempts": 2 }, "perform": { "reconcile": "work/reconcile", "transient": ["unavailable"] } }] ', null, null, 'acme.deploy/deploy@1 performed retry 2/ perform reconcile transient=unavailable', ''],
  ['a retry of one attempt is none', ', "steps": [{ "id": "deploy", "reach": "performed", "retry": { "maxAttempts": 1 } }] ', null, null, 'acme.deploy/deploy@1 performed retry 1/', ''],
  ['perform naming no point', ', "steps": [{ "id": "deploy", "reach": "performed", "perform": {} }] ', null, null, 'acme.deploy/deploy@1 performed perform', ''],
  ['two steps, in their order', ', "steps": [{ "id": "build", "reach": "performed" }, { "id": "deployed", "reach": "observed", "skippable": true, "rejectable": false }] ', null, null, 'acme.deploy/build@1 performed; acme.deploy/deployed@1 observed', ''],
  ['a version written 1.0', ', "steps": [{ "id": "deploy", "version": 1.0, "reach": "performed" }] ', null, null, 'acme.deploy/deploy@1 performed', ''],
  ['an id of 40', ', "steps": [{ "id": "abcdefghijabcdefghijabcdefghijabcdefghij", "reach": "observed" }] ', null, null, 'acme.deploy/abcdefghijabcdefghijabcdefghijabcdefghij@1 observed', ''],
  ['a field it does not know is passed over', ', "steps": [{ "id": "deploy", "reach": "performed", "label": "Deploy the preview" }] ', null, null, 'acme.deploy/deploy@1 performed', ''],
  ['resources', ', "resources": [{ "schema": "acme.deploy/preview@1", "cleanup": "work/cleanup" }, { "schema": "acme.deploy/site@1", "role": "result", "cleanup": "work/cleanup" }] ', null, null, '', 'acme.deploy/preview@1 temporary work/cleanup; acme.deploy/site@1 result work/cleanup'],
  ['steps not an array', ', "steps": { "id": "deploy" } ', 'steps', '`steps` must be an array of the steps the plugin declares.', '', ''],
  ['steps that are null', ', "steps": null ', 'steps', '`steps` must be an array of the steps the plugin declares.', '', ''],
  ['a step that is not an object', ', "steps": ["deploy"] ', 'id', ID, '', ''],
  ['a step with no id', ', "steps": [{ "reach": "performed" }] ', 'id', ID, '', ''],
  ['an id in capitals', ', "steps": [{ "id": "Deploy", "reach": "performed" }] ', 'id', ID, '', ''],
  ['an id with a slash', ', "steps": [{ "id": "acme/deploy", "reach": "performed" }] ', 'id', ID, '', ''],
  ['an id of 41', ', "steps": [{ "id": "abcdefghijabcdefghijabcdefghijabcdefghijk", "reach": "observed" }] ', 'id', ID, '', ''],
  ['a step twice', ', "steps": [{ "id": "deploy", "reach": "performed" }, { "id": "deploy", "reach": "observed" }] ', 'step-twice', 'step `deploy` is declared twice.', '', ''],
  ['a version that is text', ', "steps": [{ "id": "deploy", "version": "2", "reach": "performed" }] ', 'version', VERSION, '', ''],
  ['a version of 0', ', "steps": [{ "id": "deploy", "version": 0, "reach": "performed" }] ', 'version', VERSION, '', ''],
  ['a version with a fraction', ', "steps": [{ "id": "deploy", "version": 1.5, "reach": "performed" }] ', 'version', VERSION, '', ''],
  ['no reach', ', "steps": [{ "id": "deploy" }] ', 'reach', REACH, '', ''],
  ['a reach a person presses', ', "steps": [{ "id": "deploy", "reach": "pressed" }] ', 'reach', REACH, '', ''],
  ['a reach that is not text', ', "steps": [{ "id": "deploy", "reach": 1 }] ', 'reach', REACH, '', ''],
  ['inputs that are a list', ', "steps": [{ "id": "deploy", "reach": "performed", "inputs": ["release"] }] ', 'inputs', INPUTS, '', ''],
  ['an input with no schema', ', "steps": [{ "id": "deploy", "reach": "performed", "inputs": { "release": "artifact" } }] ', 'inputs', INPUTS, '', ''],
  ['an input named in capitals', ', "steps": [{ "id": "deploy", "reach": "performed", "inputs": { "Release": "acme.build/artifact@1" } }] ', 'inputs', INPUTS, '', ''],
  ['an output named evidence', ', "steps": [{ "id": "deploy", "reach": "performed", "outputs": { "evidence": "acme.deploy/receipt@1" } }] ', 'outputs', OUTPUTS, '', ''],
  ['an output schema with a colon', ', "steps": [{ "id": "deploy", "reach": "performed", "outputs": { "preview": "acme.deploy/preview@1:p1" } }] ', 'outputs', OUTPUTS, '', ''],
  ['evidence as text', ', "steps": [{ "id": "deploy", "reach": "performed", "evidence": "acme.deploy/receipt@1" }] ', 'evidence', EVIDENCE, '', ''],
  ['evidence with no version', ', "steps": [{ "id": "deploy", "reach": "performed", "evidence": { "schema": "acme.deploy/receipt" } }] ', 'evidence', EVIDENCE, '', ''],
  ['a retry of no attempts', ', "steps": [{ "id": "deploy", "reach": "performed", "retry": { "maxAttempts": 0 } }] ', 'retry', RETRY, '', ''],
  ['a negative delay', ', "steps": [{ "id": "deploy", "reach": "performed", "retry": { "maxAttempts": 1, "delaysSeconds": [-5] } }] ', 'retry', RETRY, '', ''],
  ['a retry with no maxAttempts', ', "steps": [{ "id": "deploy", "reach": "performed", "retry": { "delaysSeconds": [5] } }] ', 'retry', RETRY, '', ''],
  ['a retry with no transient code', ', "steps": [{ "id": "deploy", "reach": "performed", "retry": { "maxAttempts": 3 }, "perform": { "idempotent": true } }] ', 'retry', RETRY, '', ''],
  ['a retry neither idempotent nor reconciled', ', "steps": [{ "id": "deploy", "reach": "performed", "retry": { "maxAttempts": 3 }, "perform": { "transient": ["unavailable"] } }] ', 'retry', RETRY, '', ''],
  ['a retry on an observed step', ', "steps": [{ "id": "deployed", "reach": "observed", "retry": { "maxAttempts": 2 } }] ', 'retry', `step \`deployed\`'s ${RETRY_RULE}`, '', ''],
  ['a timeout of 0', ', "steps": [{ "id": "deploy", "reach": "performed", "timeoutSeconds": 0 }] ', 'timeoutSeconds', TIMEOUT, '', ''],
  ['a timeout that is text', ', "steps": [{ "id": "deploy", "reach": "performed", "timeoutSeconds": "600" }] ', 'timeoutSeconds', TIMEOUT, '', ''],
  ['skippable as text', ', "steps": [{ "id": "deploy", "reach": "performed", "skippable": "no" }] ', 'skippable', "step `deploy`'s `skippable` must be `true` or `false`: whether the person may pass it over.", '', ''],
  ['rejectable as a number', ', "steps": [{ "id": "deploy", "reach": "performed", "rejectable": 1 }] ', 'rejectable', "step `deploy`'s `rejectable` must be `true` or `false`: whether the person may reject it.", '', ''],
  ['perform on an observed step', ', "steps": [{ "id": "deployed", "reach": "observed", "perform": {} }] ', 'perform', `step \`deployed\`'s ${PERFORM_RULE}`, '', ''],
  ['perform as true', ', "steps": [{ "id": "deploy", "reach": "performed", "perform": true }] ', 'perform', `step \`deploy\`'s ${PERFORM_RULE}`, '', ''],
  ['perform at another point', ', "steps": [{ "id": "deploy", "reach": "performed", "perform": { "point": "work/land" } }] ', 'perform', `step \`deploy\`'s ${PERFORM_RULE}`, '', ''],
  ['idempotent as text', ', "steps": [{ "id": "deploy", "reach": "performed", "perform": { "idempotent": "yes" } }] ', 'idempotent', "step `deploy`'s `idempotent` must be `true` or `false`: whether the plugin deduplicates a redelivered key.", '', ''],
  ['reconcile as true', ', "steps": [{ "id": "deploy", "reach": "performed", "perform": { "reconcile": true } }] ', 'reconcile', RECONCILE, '', ''],
  ['reconcile at another point', ', "steps": [{ "id": "deploy", "reach": "performed", "perform": { "reconcile": "work/perform" } }] ', 'reconcile', RECONCILE, '', ''],
  ['transient as text', ', "steps": [{ "id": "deploy", "reach": "performed", "retry": { "maxAttempts": 3 }, "perform": { "idempotent": true, "transient": "unavailable" } }] ', 'transient', TRANSIENT, '', ''],
  ['a transient code in capitals', ', "steps": [{ "id": "deploy", "reach": "performed", "retry": { "maxAttempts": 3 }, "perform": { "idempotent": true, "transient": ["Unavailable"] } }] ', 'transient', TRANSIENT, '', ''],
  ['a transient code twice', ', "steps": [{ "id": "deploy", "reach": "performed", "retry": { "maxAttempts": 3 }, "perform": { "idempotent": true, "transient": ["unavailable", "unavailable"] } }] ', 'transient', TRANSIENT, '', ''],
  ['transient codes with no retry', ', "steps": [{ "id": "deploy", "reach": "performed", "perform": { "idempotent": true, "transient": ["unavailable"] } }] ', 'transient', TRANSIENT, '', ''],
  ['the first broken step is said', ', "steps": [{ "id": "deploy", "reach": "performed" }, { "id": "Smoke", "reach": "observed" }] ', 'id', ID, '', ''],
  ['resources not an array', ', "resources": { "schema": "acme.deploy/preview@1" } ', 'resources', "`resources` must be an array of what the plugin's steps create.", '', ''],
  ['a resource with no schema', ', "resources": [{ "cleanup": "work/cleanup" }] ', 'schema', SCHEMA, '', ''],
  ['a resource schema with no owner', ', "resources": [{ "schema": "preview@1", "cleanup": "work/cleanup" }] ', 'schema', SCHEMA, '', ''],
  ['a resource twice', ', "resources": [{ "schema": "acme.deploy/preview@1", "cleanup": "work/cleanup" }, { "schema": "acme.deploy/preview@1", "role": "result", "cleanup": "work/cleanup" }] ', 'resource-twice', 'resource `acme.deploy/preview@1` is declared twice.', '', ''],
  ['a role it does not know', ', "resources": [{ "schema": "acme.deploy/preview@1", "role": "evidence", "cleanup": "work/cleanup" }] ', 'role', "resource `acme.deploy/preview@1`'s `role` must be `temporary`, removed at the end, or `result`, kept as the work's result.", '', ''],
  ['no cleanup', ', "resources": [{ "schema": "acme.deploy/preview@1" }] ', 'cleanup', CLEANUP, '', ''],
  ['cleanup at another point', ', "resources": [{ "schema": "acme.deploy/preview@1", "cleanup": "work/perform" }] ', 'cleanup', CLEANUP, '', ''],
  ["a step's problem before a resource's", ', "steps": [{ "id": "deploy" }], "resources": [{ "schema": "preview" }] ', 'reach', REACH, '', ''],
  ['a performed step asked nowhere', ', "hooks": { "command": ["node", "deploy.mjs"], "points": ["work/watch", "work/step"] }, "steps": [{ "id": "deploy", "reach": "performed" }] ', 'perform-point', POINT, '', ''],
  ['an observed step asked nowhere', ', "hooks": { "command": ["node", "deploy.mjs"], "points": ["work/watch", "work/step"] }, "steps": [{ "id": "deployed", "reach": "observed" }] ', null, null, 'acme.deploy/deployed@1 observed', ''],
  ["a step's own problem before its point", ', "hooks": { "command": ["node", "deploy.mjs"], "points": ["work/watch"] }, "steps": [{ "id": "deploy", "reach": "performed", "timeoutSeconds": 0 }] ', 'timeoutSeconds', TIMEOUT, '', ''],
  ['the second step asked nowhere', ', "hooks": { "command": ["node", "deploy.mjs"], "points": ["quest/consider"] }, "steps": [{ "id": "deployed", "reach": "observed" }, { "id": "deploy", "reach": "performed" }] ', 'perform-point', POINT, '', ''],
];

const ordinal = (a: string, b: string): number => (a < b ? -1 : a > b ? 1 : 0);

/** One step as the table says it. Twin: `PluginStepsTests.Said`. */
function said(step: NodeDeclaration): string {
  const ports = (declared: Record<string, string>) => Object.keys(declared).sort(ordinal).map((name) => `${name}=${declared[name]}`).join(',');
  const parts = [`${step.kind}@${step.version} ${step.reach}`];
  if (Object.keys(step.inputs).length > 0) parts.push(`in ${ports(step.inputs)}`);
  if (Object.keys(step.outputs).length > 0) parts.push(`out ${ports(step.outputs)}`);
  if (step.evidence !== null) parts.push(`evidence ${step.evidence}`);
  if (step.retry !== null) parts.push(`retry ${step.retry.maxAttempts}/${step.retry.delaysSeconds.join('+')}`);
  if (step.timeoutSeconds !== null) parts.push(`timeout ${step.timeoutSeconds}`);
  if (!step.skippable) parts.push('no-skip');
  if (step.rejectable) parts.push('reject');
  if (step.perform !== null) {
    parts.push('perform');
    if (step.perform.idempotent) parts.push('idempotent');
    if (step.perform.reconcile) parts.push('reconcile');
    if (step.perform.transient.length > 0) parts.push(`transient=${step.perform.transient.join('+')}`);
  }
  return parts.join(' ');
}

const saidResource = (resource: PluginResource): string => `${resource.schema} ${resource.role} ${resource.cleanup}`;

/**
 * The hooks a row's manifest speaks with where it names none (FABRIC-PERFORM1a): a performed step is asked at `work/perform`, so
 * the hooks must name that point, and a row about anything else is not refused for it. Twin: `PluginStepsTests.Hooks`.
 */
const HOOKS = ', "hooks": { "command": ["node", "deploy.mjs"], "points": ["work/perform"] }';

/** The manifest a row declares: its id, the default hooks unless it names its own, and what it declares. Twin: `Manifest`. */
const manifest = (declared: string): string => `{ "id": "acme.deploy"${declared.includes('"hooks"') ? '' : HOOKS}${declared} }`;

function plugin(home: string, declared: string): void {
  const folder = join(pluginsRoot(home), 'acme.deploy');
  mkdirSync(folder, { recursive: true });
  writeFileSync(join(folder, MANIFEST), manifest(declared));
}

test('the driver’s steps table is this table, row for row and in this order', () => {
  assert.deepEqual(driverRows(readFileSync(DRIVER_TESTS, 'utf8'), 'The_steps_read_as_the_cli_reads_them'), STEP_ROWS);
});

test('the steps read as the driver reads them: each a node declaration, or the first problem refusing the plugin', () => {
  STEP_ROWS.forEach(([name, declared, code, problem, steps, resources], at) => {
    const fx = makeFixture(`pluginSteps-${at}`);
    plugin(fx.root, declared);

    const [entry] = readPlugins(fx.root).plugins;
    assert.equal(entry!.problem, problem, name);
    assert.equal(entry!.manifest.steps.map(said).join('; '), steps, name);
    assert.equal(entry!.manifest.resources.map(saidResource).join('; '), resources, name);
    for (const step of entry!.manifest.steps) assert.equal(step.id, step.kind, name);

    const read = stepsDeclared('acme.deploy', JSON.parse(manifest(declared)) as Record<string, unknown>);
    assert.deepEqual([read.code, read.problem], [code, problem], name);
    fx.cleanup();
  });
});

test('a performed step needs its point in the hooks, and an observed step needs none (FABRIC-PERFORM1a)', () => {
  const cases: [string, string | null][] = [
    ['{ "id": "acme.deploy", "steps": [{ "id": "deploy", "reach": "performed" }] }', 'perform-point'],
    ['{ "id": "acme.deploy", "hooks": { "command": ["node", "deploy.mjs"] }, "steps": [{ "id": "deploy", "reach": "performed" }] }', 'perform-point'],
    ['{ "id": "acme.deploy", "hooks": { "command": ["node", "deploy.mjs"], "points": "work/perform" }, "steps": [{ "id": "deploy", "reach": "performed" }] }', 'perform-point'],
    ['{ "id": "acme.deploy", "steps": [{ "id": "deployed", "reach": "observed" }] }', null],
    ['{ "id": "acme.deploy", "hooks": { "command": ["node", "deploy.mjs"], "points": ["work/perform"] }, "steps": [{ "id": "deploy", "reach": "performed" }] }', null],
  ];
  for (const [written, code] of cases) {
    const read = stepsDeclared('acme.deploy', JSON.parse(written) as Record<string, unknown>);
    assert.deepEqual([read.code, read.problem], [code, code === null ? null : POINT], written);
  }
});

test('a manifest’s step is the node declaration built by hand', () => {
  const fx = makeFixture('pluginSteps-whole');
  plugin(fx.root, `, "hooks": { "command": ["node", "deploy.mjs"], "points": ["quest/consider", "work/perform"] },
    "steps": [{ "id": "deploy", "version": 2, "reach": "performed",
      "inputs": { "release": "acme.build/artifact@1" }, "outputs": { "preview": "acme.deploy/preview@1" },
      "evidence": { "schema": "acme.deploy/receipt@1" }, "retry": { "maxAttempts": 3, "delaysSeconds": [5, 30] },
      "timeoutSeconds": 600, "skippable": false, "rejectable": true,
      "perform": { "point": "work/perform", "idempotent": true, "reconcile": "work/reconcile", "transient": ["unavailable", "rate-limited"] } },
      { "id": "smoke", "reach": "observed" }],
    "resources": [{ "schema": "acme.deploy/preview@1", "role": "temporary", "cleanup": "work/cleanup" }]`);

  const [entry] = readPlugins(fx.root).plugins;
  assert.equal(entry!.problem, null);
  const byHand: NodeDeclaration = {
    id: 'acme.deploy/deploy', kind: 'acme.deploy/deploy', version: 2, reach: 'performed',
    inputs: { release: 'acme.build/artifact@1' }, outputs: { preview: 'acme.deploy/preview@1' }, evidence: 'acme.deploy/receipt@1',
    retry: { maxAttempts: 3, delaysSeconds: [5, 30] }, timeoutSeconds: 600, rejectable: true, skippable: false,
    perform: { idempotent: true, reconcile: true, transient: ['unavailable', 'rate-limited'] },
  };
  const observed: NodeDeclaration = {
    id: 'acme.deploy/smoke', kind: 'acme.deploy/smoke', version: 1, reach: 'observed', inputs: {}, outputs: {}, evidence: null,
    retry: null, timeoutSeconds: null, rejectable: false, skippable: true, perform: null,
  };
  assert.deepEqual(entry!.manifest.steps, [byHand, observed]);
  assert.deepEqual(entry!.manifest.resources, [{ schema: 'acme.deploy/preview@1', role: 'temporary', cleanup: 'work/cleanup' }]);
  fx.cleanup();
});

test('a manifest without steps reads as it did before: no steps, no resources, nothing else moved', () => {
  const fx = makeFixture('pluginSteps-before');
  plugin(fx.root, `, "name": "Acme deploys", "version": "1.0.0", "description": "Deploys.",
    "hooks": { "command": ["node", "deploy.mjs"], "points": ["work/land"] }, "skills": [{ "name": "triage" }]`);

  const [entry] = readPlugins(fx.root).plugins;
  assert.equal(entry!.problem, null);
  assert.deepEqual(entry!.manifest, {
    id: 'acme.deploy', apiVersion: 1, name: 'Acme deploys', version: '1.0.0', description: 'Deploys.', harnesses: [],
    hooks: { command: ['node', 'deploy.mjs'], points: ['work/land'] }, servers: [], skills: [{ name: 'triage', to: ['sessions'] }],
    steps: [], resources: [], icon: null, iconProblem: null, tools: [], toolsProblem: null,
  });
  fx.cleanup();
});

test('a step’s problem refuses the plugin, and nothing of it is taken', () => {
  const fx = makeFixture('pluginSteps-refused');
  plugin(fx.root, `, "hooks": { "command": ["node", "deploy.mjs"], "points": ["work/land"] },
    "steps": [{ "id": "deploy", "reach": "performed", "perform": { "reconcile": "yes" } }],
    "resources": [{ "schema": "acme.deploy/preview@1", "cleanup": "work/cleanup" }]`);

  const catalog = readPlugins(fx.root);
  const [entry] = catalog.plugins;
  assert.equal(entry!.problem, RECONCILE);
  assert.deepEqual(catalog.contributing, []);
  assert.equal(entry!.manifest.hooks, null);
  assert.deepEqual([entry!.manifest.steps, entry!.manifest.resources], [[], []]);
  fx.cleanup();
});
