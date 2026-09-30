import { test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import {
  AGENT_EFFORTS, AGENT_MODELS, AGENT_SETTINGS_FILE, readAgentSettings, writeAgentSettings,
} from '../src/agentsettings.ts';
import { TOOLCHAINS, commandHarness, profileHome, writeHarnessSettings } from '../src/toolchain.ts';
import { captureError, makeFixture } from './_fixture.ts';

/**
 * `daoris agent settings` — an account's own model and effort (AGT6, D98).
 *
 * The file is Claude Code's, in the account's configuration home: its `settings.json`, the one the
 * tool reads as the user tier and its ACP adapter watches at `CLAUDE_CONFIG_DIR/settings.json`. Daoris
 * changes two keys in it — `model` and `effortLevel`, and an effort per model under `modelSettings` —
 * and leaves every other key the tool keeps there exactly as it was.
 *
 * The CLI's half of a TWIN CONTRACT: the driver's `AgentSettings` reads and writes the same file from
 * the Settings screen. The two share no code, so the same seven rules are asserted on both sides:
 *
 *   1. The file is the tool's own `settings.json`, in the account's configuration home.
 *   2. No file is the tool's defaults: nothing set, and reading creates nothing.
 *   3. A file that is not a JSON object is a sentence to a read and a refusal to a write, which leaves
 *      it exactly as it was.
 *   4. A write changes only what it names; every other key keeps its place and its value.
 *   5. An effort is low, medium, high or xhigh — `max` is refused, since the tool keeps it for one
 *      session only; a model is one word, and the tool's own aliases are offered first.
 *   6. Clearing removes the key; an entry per model left empty goes, and so does an empty
 *      `modelSettings`.
 *   7. A new file holds exactly what was written, in the tool's formatting: two-space indent, LF, and
 *      a final newline.
 *
 * Every case runs against a fixture. No account's real file is read or written by a test.
 */

// ——— Rule 1: the file, and which tools declare one.

test('the file is the tool’s own settings.json, declared by Claude Code alone', () => {
  assert.equal(AGENT_SETTINGS_FILE, 'settings.json');
  const declaring = Object.entries(TOOLCHAINS).filter(([, toolchain]) => toolchain.settingsFile).map(([name]) => name);
  // The ACP door runs as Claude Code's accounts (AGT7), so its settings are its owner's file.
  assert.deepEqual(declaring, ['claude-code']);
  assert.equal(TOOLCHAINS['claude-code']!.settingsFile, AGENT_SETTINGS_FILE);
});

test('the choices offered are the tool’s own: its aliases, and the efforts its settings keep', () => {
  // The Agent SDK's alias list at 0.3.284, which `claude-agent-acp` 0.84.0 carries, with `default`.
  assert.deepEqual(AGENT_MODELS, [
    'default', 'sonnet', 'opus', 'haiku', 'fable', 'best', 'sonnet[1m]', 'opus[1m]', 'fable[1m]', 'opusplan',
  ]);
  // The settings schema's `effortLevel`: `max` is session-only and never written to a file.
  assert.deepEqual(AGENT_EFFORTS, ['low', 'medium', 'high', 'xhigh']);
});

// ——— Rule 2: no file.

test('no file is the tool’s defaults, and reading it creates nothing', () => {
  const fx = makeFixture('agent-settings-absent');
  const file = join(fx.root, 'account', AGENT_SETTINGS_FILE);

  assert.deepEqual(readAgentSettings(file), { model: null, effort: null, perModel: [], problem: null });
  assert.equal(existsSync(file), false);
  fx.cleanup();
});

test('a file is read for its model, its effort and each model’s own effort', () => {
  const fx = makeFixture('agent-settings-read');
  const file = fx.write(AGENT_SETTINGS_FILE, JSON.stringify({
    model: 'opus[1m]',
    effortLevel: 'high',
    modelSettings: { 'claude-opus-5': { effortLevel: 'xhigh' }, 'claude-haiku-4-5': { maxEffortLevel: 'low' } },
  }));

  assert.deepEqual(readAgentSettings(file), {
    model: 'opus[1m]', effort: 'high', perModel: [{ model: 'claude-opus-5', effort: 'xhigh' }], problem: null,
  });
  fx.cleanup();
});

// ——— Rule 3: a file that is not a JSON object.

test('a file that is not a JSON object is a sentence to a read, and a write leaves it as it was', () => {
  const fx = makeFixture('agent-settings-malformed');
  for (const text of ['not json at all', '["an", "array"]', '"a string"']) {
    const file = fx.write(AGENT_SETTINGS_FILE, text);

    const read = readAgentSettings(file);
    assert.equal(read.model, null);
    assert.match(read.problem ?? '', /could not be read|not a JSON object/, text);

    const error = captureError(() => writeAgentSettings(file, { model: 'opus' }));
    assert.match(error.message, /nothing was written/);
    assert.equal(readFileSync(file, 'utf8'), text, 'the file is exactly as it was');
  }
  fx.cleanup();
});

// ——— Rule 4: a write changes only what it names.

test('a write changes the two keys and keeps every other key in its place', () => {
  const fx = makeFixture('agent-settings-preserve');
  const file = fx.write(AGENT_SETTINGS_FILE, `${JSON.stringify({
    $schema: 'https://json.schemastore.org/claude-code-settings.json',
    model: 'sonnet',
    permissions: { allow: ['Bash(ls)'], deny: [] },
    env: { FOO: '1' },
    alwaysThinkingEnabled: true,
  }, null, 2)}\n`);

  const after = writeAgentSettings(file, { model: 'opus', effort: 'high' });

  assert.equal(after.model, 'opus');
  assert.equal(after.effort, 'high');
  const written = JSON.parse(readFileSync(file, 'utf8')) as Record<string, unknown>;
  assert.deepEqual(Object.keys(written), ['$schema', 'model', 'permissions', 'env', 'alwaysThinkingEnabled', 'effortLevel']);
  assert.deepEqual(written.permissions, { allow: ['Bash(ls)'], deny: [] });
  assert.deepEqual(written.env, { FOO: '1' });
  assert.equal(written.alwaysThinkingEnabled, true);
  fx.cleanup();
});

test('an effort per model is written under that model, beside what the entry already holds', () => {
  const fx = makeFixture('agent-settings-per-model');
  const file = fx.write(AGENT_SETTINGS_FILE, JSON.stringify({ modelSettings: { 'claude-opus-5': { maxEffortLevel: 'max' } } }));

  const after = writeAgentSettings(file, { perModel: { 'claude-opus-5': 'medium', 'claude-sonnet-5': 'low' } });

  assert.deepEqual(after.perModel, [{ model: 'claude-opus-5', effort: 'medium' }, { model: 'claude-sonnet-5', effort: 'low' }]);
  assert.deepEqual(JSON.parse(readFileSync(file, 'utf8')).modelSettings, {
    'claude-opus-5': { maxEffortLevel: 'max', effortLevel: 'medium' },
    'claude-sonnet-5': { effortLevel: 'low' },
  });
  fx.cleanup();
});

// ——— Rule 5: what may be written.

test('an effort is one of the four the settings keep; max is refused as session-only', () => {
  const fx = makeFixture('agent-settings-effort');
  const file = fx.write(AGENT_SETTINGS_FILE, '{"model":"opus"}\n');

  const max = captureError(() => writeAgentSettings(file, { effort: 'max' }));
  assert.match(max.message, /`max`/);
  assert.match(max.message, /one session/);
  const other = captureError(() => writeAgentSettings(file, { effort: 'ultra' }));
  assert.match(other.message, /low, medium, high, xhigh/);
  assert.equal(readFileSync(file, 'utf8'), '{"model":"opus"}\n', 'a refused write writes nothing');

  for (const effort of AGENT_EFFORTS) assert.equal(writeAgentSettings(file, { effort }).effort, effort);
  fx.cleanup();
});

test('a model is one word: an alias or a full id, never blank and never with a space', () => {
  const fx = makeFixture('agent-settings-model');
  const file = join(fx.root, AGENT_SETTINGS_FILE);

  for (const model of ['', '   ', 'claude opus']) {
    assert.match(captureError(() => writeAgentSettings(file, { model })).message, /model/, JSON.stringify(model));
  }
  assert.equal(existsSync(file), false, 'a refused write makes no file');
  for (const model of ['opus[1m]', 'claude-opus-5-5', 'default']) {
    assert.equal(writeAgentSettings(file, { model }).model, model);
  }
  fx.cleanup();
});

// ——— Rule 6: clearing.

test('clearing removes the key, an emptied entry, and an emptied modelSettings', () => {
  const fx = makeFixture('agent-settings-clear');
  const file = fx.write(AGENT_SETTINGS_FILE, JSON.stringify({
    model: 'opus', effortLevel: 'high', theme: 'dark',
    modelSettings: { 'claude-opus-5': { effortLevel: 'xhigh' }, 'claude-sonnet-5': { effortLevel: 'low', maxEffortLevel: 'high' } },
  }));

  writeAgentSettings(file, { model: null, effort: null, perModel: { 'claude-opus-5': null } });
  assert.deepEqual(JSON.parse(readFileSync(file, 'utf8')), {
    theme: 'dark', modelSettings: { 'claude-sonnet-5': { effortLevel: 'low', maxEffortLevel: 'high' } },
  });

  writeAgentSettings(file, { perModel: { 'claude-sonnet-5': null } });
  assert.deepEqual(JSON.parse(readFileSync(file, 'utf8')), {
    theme: 'dark', modelSettings: { 'claude-sonnet-5': { maxEffortLevel: 'high' } },
  });

  const bare = fx.write('bare.json', JSON.stringify({ modelSettings: { 'claude-opus-5': { effortLevel: 'xhigh' } } }));
  writeAgentSettings(bare, { perModel: { 'claude-opus-5': null } });
  assert.deepEqual(JSON.parse(readFileSync(bare, 'utf8')), {});
  fx.cleanup();
});

// ——— Rule 7: a new file.

test('a new file holds exactly what was written, in the tool’s formatting', () => {
  const fx = makeFixture('agent-settings-new');
  const file = join(fx.root, AGENT_SETTINGS_FILE);

  writeAgentSettings(file, { model: 'sonnet' });

  assert.equal(readFileSync(file, 'utf8'), '{\n  "model": "sonnet"\n}\n');
  fx.cleanup();
});

// ——— The terminal door: `daoris agent settings <agent> [--account <name>] [model <v>] [effort <v>]`.

function run(argv: string[], home: string): { code: number; out: string } {
  const saved = process.env.DAORIS_HARNESS_CONFIG;
  process.env.DAORIS_HARNESS_CONFIG = join(home, 'harnesses.json');
  const lines: string[] = [];
  try {
    const code = commandHarness({
      root: process.cwd(), argv, write: (line) => lines.push(line), packageRoot: process.cwd(),
    }) as number;
    return { code, out: lines.join('\n') };
  } finally {
    if (saved === undefined) delete process.env.DAORIS_HARNESS_CONFIG;
    else process.env.DAORIS_HARNESS_CONFIG = saved;
  }
}

function account(home: string, name: string, settings?: unknown): string {
  const where = profileHome(home, 'claude-code', name);
  mkdirSync(where, { recursive: true });
  if (settings !== undefined) writeFileSync(join(where, AGENT_SETTINGS_FILE), JSON.stringify(settings));
  return join(where, AGENT_SETTINGS_FILE);
}

test('`agent settings` given nothing prints what the account’s file says, naming the file', () => {
  const fx = makeFixture('agent-settings-verb-read');
  const file = account(fx.root, 'work', { model: 'opus', modelSettings: { 'claude-opus-5': { effortLevel: 'xhigh' } } });

  const { code, out } = run(['settings', 'claude-code', '--account', 'work'], fx.root);

  assert.equal(code, 0);
  assert.match(out, /account `work`/);
  assert.ok(out.includes(file), 'the file is named');
  assert.match(out, /model\s+opus/);
  assert.match(out, /effort\s+the tool's own default/);
  assert.match(out, /claude-opus-5\s+xhigh/);
  fx.cleanup();
});

test('`agent settings … model <v> effort <v>` writes both and says so', () => {
  const fx = makeFixture('agent-settings-verb-write');
  const file = account(fx.root, 'work', { theme: 'dark' });

  const { code, out } = run(['settings', 'claude-code', '--account', 'work', 'model', 'sonnet', 'effort', 'xhigh'], fx.root);

  assert.equal(code, 0);
  assert.deepEqual(JSON.parse(readFileSync(file, 'utf8')), { theme: 'dark', model: 'sonnet', effortLevel: 'xhigh' });
  assert.match(out, /model\s+sonnet/);
  assert.match(out, /effort\s+xhigh/);
  fx.cleanup();
});

test('`effort <v> --for <model>` sets that model’s effort, and `unset` clears a key', () => {
  const fx = makeFixture('agent-settings-verb-for');
  const file = account(fx.root, 'work', { model: 'opus' });

  assert.equal(run(['settings', 'claude-code', '--account', 'work', 'effort', 'low', '--for', 'claude-opus-5'], fx.root).code, 0);
  assert.equal(run(['settings', 'claude-code', '--account', 'work', 'model', 'unset'], fx.root).code, 0);

  assert.deepEqual(JSON.parse(readFileSync(file, 'utf8')), { modelSettings: { 'claude-opus-5': { effortLevel: 'low' } } });
  fx.cleanup();
});

test('with no account named, the machine’s default account is the one', () => {
  const fx = makeFixture('agent-settings-verb-default');
  const file = account(fx.root, 'work');
  writeHarnessSettings(join(fx.root, 'harnesses.json'), {
    defaults: { 'claude-code': 'work' }, workspaces: {}, versions: {}, workspaceVersions: {}, rest: {},
  });

  assert.equal(run(['settings', 'claude-code', 'model', 'haiku'], fx.root).code, 0);
  assert.equal(JSON.parse(readFileSync(file, 'utf8')).model, 'haiku');
  fx.cleanup();
});

test('the tool’s own home is never touched: with no account anywhere, the verb says how instead', () => {
  const fx = makeFixture('agent-settings-verb-own');

  const error = captureError(() => run(['settings', 'claude-code', 'model', 'opus'], fx.root));

  assert.match(error.message, /own configuration home/);
  assert.match(error.message, /--account/);
  fx.cleanup();
});

test('an account that does not exist is refused, naming the ones that do', () => {
  const fx = makeFixture('agent-settings-verb-missing');
  account(fx.root, 'work');

  const error = captureError(() => run(['settings', 'claude-code', '--account', 'play', 'model', 'opus'], fx.root));

  assert.match(error.message, /no account `play`/);
  assert.match(error.message, /work/);
  assert.equal(existsSync(profileHome(fx.root, 'claude-code', 'play')), false, 'no account is made by a setting');
  fx.cleanup();
});

test('a door onto the tool acts on its owner’s account, and says so', () => {
  const fx = makeFixture('agent-settings-verb-door');
  const file = account(fx.root, 'work');

  const { code, out } = run(['settings', 'claude-code-acp', '--account', 'work', 'effort', 'medium'], fx.root);

  assert.equal(code, 0);
  assert.match(out, /runs as `claude-code`'s accounts/);
  assert.equal(JSON.parse(readFileSync(file, 'utf8')).effortLevel, 'medium');
  fx.cleanup();
});

test('a tool whose settings Daoris does not know is offered nothing, said in one line', () => {
  const fx = makeFixture('agent-settings-verb-codex');

  const read = run(['settings', 'codex'], fx.root);
  assert.equal(read.code, 0);
  assert.equal(read.out.split('\n').length, 1);
  assert.match(read.out, /Daoris does not know/);

  assert.match(captureError(() => run(['settings', 'dsh', 'model', 'x'], fx.root)).message, /Daoris does not know/);
  fx.cleanup();
});

test('an unknown setting, or one with no value, is refused naming the two there are', () => {
  const fx = makeFixture('agent-settings-verb-shape');
  account(fx.root, 'work');

  assert.match(captureError(() => run(['settings', 'claude-code', '--account', 'work', 'theme', 'dark'], fx.root)).message, /model.*effort/);
  assert.match(captureError(() => run(['settings', 'claude-code', '--account', 'work', 'model'], fx.root)).message, /needs a value/);
  assert.match(captureError(() => run(['settings', 'claude-code', '--account', 'work', 'model', 'opus', '--for', 'claude-opus-5'], fx.root)).message, /--for/);
  fx.cleanup();
});
