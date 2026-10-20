import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { commandDriver, readDriverChoices, writeDriverChoices } from '../../src/driver/driverconfig.ts';
import { applyBrainstormEdit, brainstormFor, brainstormListed, brainstormRuleOf } from '../../src/driver/brainstorms.ts';
import { homeEntry } from '../../src/platform/home.ts';
import { makeFixture, captureError } from '../_fixture.ts';

/**
 * The brainstorm policy, `brainstorms` and `workspaceBrainstorms` in `driver.json` (BRAINSTORM1b, D176 point 9).
 * 🔴 A TWIN with the driver's `BrainstormRules`: both hold ONE table, the driver suite's `fixtures/brainstorm-rules.json`, cell for
 * cell — the reading and its precedence, every refusal in the same words, how a rule is listed and the edits both doors make. The
 * driver's `BrainstormRulesTests` reads the same file.
 */
const TABLE = JSON.parse(readFileSync(join(dirname(dirname(fileURLToPath(import.meta.url))), '..', '..', 'Daoris.Desktop',
  'Daoris.Desktop.Driver.Tests', 'fixtures', 'brainstorm-rules.json'), 'utf8')) as {
  read: [name: string, file: string, workspace: string | null, source: string, rule: string][];
  problems: [name: string, value: string, problem: string | null][];
  listed: [name: string, rule: string, said: string][];
  edits: [name: string, file: string, edit: string, after: string | null, refusal: string | null][];
};

function at(fx: { root: string }): string {
  const path = homeEntry(fx.root, 'driver');
  mkdirSync(dirname(path), { recursive: true });
  return path;
}

function run(argv: string[], path: string): { code: number; out: string } {
  const saved = process.env.DAORIS_DRIVER_CONFIG;
  process.env.DAORIS_DRIVER_CONFIG = path;
  const lines: string[] = [];
  try {
    const code = commandDriver({
      root: process.cwd(), argv, write: (line) => lines.push(line), packageRoot: process.cwd(),
    }) as number;
    return { code, out: lines.join('\n') };
  } finally {
    if (saved === undefined) delete process.env.DAORIS_DRIVER_CONFIG;
    else process.env.DAORIS_DRIVER_CONFIG = saved;
  }
}

test('a brainstorms rule resolves as the driver resolves it (the shared table)', () => {
  const fx = makeFixture('driver-brainstorms-read');
  assert.ok(TABLE.read.length > 0, 'the table holds read rows');
  for (const [name, file, workspace, source, rule] of TABLE.read) {
    writeFileSync(at(fx), file, 'utf8');
    const read = brainstormFor(readDriverChoices(at(fx)), workspace);
    assert.equal(read.source, source, name);
    assert.deepEqual(read.rule, JSON.parse(rule), `${name}: the rule`);
  }
  fx.cleanup();
});

test('a brainstorms rule\'s first problem is the driver\'s, in its words, and a rule is listed in its words (the shared table)', () => {
  assert.ok(TABLE.problems.length > 0, 'the table holds problem rows');
  for (const [name, value, problem] of TABLE.problems) {
    assert.equal(brainstormRuleOf(JSON.parse(value)).problem, problem, name);
  }
  for (const [name, rule, said] of TABLE.listed) {
    assert.equal(brainstormListed(brainstormRuleOf(JSON.parse(rule)).rule!), said, name);
  }
});

test('a brainstorms edit writes what the driver writes, or refuses in its words (the shared table)', () => {
  const fx = makeFixture('driver-brainstorms-edits');
  assert.ok(TABLE.edits.length > 0, 'the table holds edit rows');
  for (const [name, file, edit, after, refusal] of TABLE.edits) {
    writeFileSync(at(fx), file, 'utf8');
    const choices = readDriverChoices(at(fx));
    if (refusal !== null) {
      assert.equal(captureError(() => applyBrainstormEdit(choices, JSON.parse(edit))).message, refusal, name);
      continue;
    }

    writeDriverChoices(at(fx), { ...choices, ...applyBrainstormEdit(choices, JSON.parse(edit)) });
    const written = JSON.parse(readFileSync(at(fx), 'utf8'));
    const kept = Object.fromEntries(['brainstorms', 'workspaceBrainstorms'].filter((key) => key in written).map((key) => [key, written[key]]));
    assert.deepEqual(kept, JSON.parse(after!), name);
  }
  fx.cleanup();
});

test('driver brainstorms sets this machine\'s rule and a workspace\'s, says them, lists them and clears them', () => {
  const fx = makeFixture('driver-brainstorms');
  assert.match(run(['list'], at(fx)).out, /brainstorms\s+none set — ask; the agents each request names; 10 minutes a pass/);

  const said = run(['brainstorms', '--agents', 'claude-code, codex-acp', '--pass-minutes', '5'], at(fx));
  assert.equal(said.code, 0);
  assert.deepEqual(JSON.parse(readFileSync(at(fx), 'utf8')).brainstorms, {
    mode: 'ask', agents: ['claude-code', 'codex-acp'], minutes: { pass: 5, synthesis: 10, challenge: 10, request: 30 },
  });
  assert.match(said.out, /brainstorms on this machine: ask; `claude-code`, then `codex-acp`; 5 minutes a pass/);

  const workspace = run(['brainstorms', '--workspace', 'work', '--mode', 'off', '--request-minutes', '40'], at(fx));
  assert.match(workspace.out, /brainstorms for questions about repositories in the workspace `work`: off; the agents each request names; 10 minutes a pass, 10 the synthesis, 10 the challenge, 40 the request/);
  assert.match(run(['list'], at(fx)).out, /brainstorms\s+workspace work\s+off;/);

  const shown = run(['brainstorms'], at(fx));
  assert.match(shown.out, /this machine: ask; `claude-code`, then `codex-acp`/);
  assert.match(shown.out, /workspace `work`: off;/);

  assert.match(run(['brainstorms', '--workspace', 'Work', '--clear'], at(fx)).out, /questions about repositories in the workspace `work` take this machine's brainstorms rule again/);
  assert.match(run(['brainstorms', '--clear'], at(fx)).out, /this machine's brainstorms take the default again/);
  assert.equal('brainstorms' in JSON.parse(readFileSync(at(fx), 'utf8')), false);
  assert.match(captureError(() => run(['brainstorms', '--mode', 'on'], at(fx))).message, /`mode` is `ask` or `off`/);
  assert.match(captureError(() => run(['brainstorms', '--synthesis-minutes', '25'], at(fx))).message, /reserved from the start: 25 and 10 here/);
  assert.match(captureError(() => run(['brainstorms', '--force'], at(fx))).message, /is not a flag `driver brainstorms` takes/);
  fx.cleanup();
});

test('a driver.json keeps a brainstorms rule the CLI did not write, and writes none where none is set', () => {
  const fx = makeFixture('driver-brainstorms-kept');
  writeFileSync(at(fx), JSON.stringify({ cap: 3, brainstorms: { mode: 'off', agents: ['dsh'] } }), 'utf8');
  const choices = readDriverChoices(at(fx));
  writeDriverChoices(at(fx), { ...choices, cap: 4 });
  assert.deepEqual(JSON.parse(readFileSync(at(fx), 'utf8')).brainstorms, { mode: 'off', agents: ['dsh'] });
  writeFileSync(at(fx), '{}', 'utf8');
  writeDriverChoices(at(fx), readDriverChoices(at(fx)));
  assert.equal('brainstorms' in JSON.parse(readFileSync(at(fx), 'utf8')), false);
  assert.equal('workspaceBrainstorms' in JSON.parse(readFileSync(at(fx), 'utf8')), false);
  fx.cleanup();
});
