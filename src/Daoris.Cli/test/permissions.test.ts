import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import {
  DEFAULTS, PERMISSIONS_FILE, addRule, commandRules, composeRules, permissionsPath, readPermissions,
  removeRule, ruleRefusal, switchDefault, writePermissions,
} from '../src/permissions.ts';
import { commandHarness } from '../src/toolchain.ts';
import { captureError, makeFixture } from './_fixture.ts';

/**
 * `daoris agent rules` — what an agent Daoris starts may do (PERM1, D72), from a terminal (D50).
 *
 * The CLI's half of a TWIN CONTRACT: the driver's `PermissionRules.cs` reads and writes the same
 * `permissions.json` under the home by the same rules, because the two artefacts share no code — THE
 * FILE is the contract. The union asserted here is the one the driver hands the harness at spawn, and
 * the defaults tables are held together by a driver test that reads this side's source.
 */

const CONNECTOR = [
  'mcp__daoris-knowledge__registry',
  'mcp__daoris-knowledge__knowledge_search',
  'mcp__daoris-knowledge__knowledge_get',
  'mcp__daoris-knowledge__knowledge_repositories',
  'mcp__daoris-knowledge__knowledge_convergence',
  'mcp__daoris-knowledge__quest_list',
  'mcp__daoris-knowledge__quest_respond',
  'mcp__daoris-knowledge__quest_publish',
  'mcp__daoris-knowledge__permission_propose',
];

function run(argv: string[], home: string): { code: number; out: string } {
  const saved = process.env.DAORIS_HOME;
  process.env.DAORIS_HOME = home;
  const lines: string[] = [];
  try {
    const code = commandHarness({
      root: process.cwd(), argv: ['rules', ...argv], write: (line) => lines.push(line), packageRoot: process.cwd(),
    }) as number;
    return { code, out: lines.join('\n') };
  } finally {
    if (saved === undefined) delete process.env.DAORIS_HOME;
    else process.env.DAORIS_HOME = saved;
  }
}

function at(home: string): string {
  return join(home, PERMISSIONS_FILE);
}

// ——— The defaults.

test('nothing written hands the defaults alone: the connector and a commit allowed, a push denied', () => {
  const fx = makeFixture('permissions-defaults');
  try {
    const rules = composeRules(readPermissions(at(fx.root)), 'default', 'engine');
    assert.deepEqual(rules.allow, [...CONNECTOR, 'Bash(cd:*)', 'Bash(git add:*)', 'Bash(git commit:*)']);
    assert.deepEqual(rules.deny, ['Bash(git push)', 'Bash(git push:*)']);
    assert.deepEqual(rules.ask, []);
    // Rebuilding the index is the machine's job, never a session's.
    assert.equal(rules.allow.includes('mcp__daoris-knowledge__knowledge_refresh'), false);
  } finally {
    fx.cleanup();
  }
});

test('a default switched off is not handed, and switching one nobody shipped names the ones that exist', () => {
  const fx = makeFixture('permissions-default-off');
  try {
    const off = switchDefault(readPermissions(at(fx.root)), 'no-push', false);
    assert.deepEqual(composeRules(off, 'default', 'engine').deny, []);
    assert.deepEqual(off.defaultsOff, ['no-push']);
    assert.deepEqual(composeRules(switchDefault(off, 'no-push', true), null, null).deny.includes('Bash(git push:*)'), true);

    const error = captureError(() => switchDefault(off, 'no-rm', false));
    assert.match(error.message, /no-rm/);
    assert.match(error.message, /connector/);
    assert.match(error.message, /no-push/);
  } finally {
    fx.cleanup();
  }
});

test('the defaults table is the one the driver hands', () => {
  assert.deepEqual(DEFAULTS.map((d) => [d.id, d.list]), [
    ['connector', 'allow'], ['commit', 'allow'], ['no-push', 'deny'], ['tree-guard', 'deny'],
  ]);
});

// 🔴 The owner's answer to PERM4 (2026-09-24): a driven session in a folder the agent never trusted made
// its edit and was refused the commit, so Daoris ships the commit — and `no-push` still refuses the push.
test('a session may commit by default, and the person can switch that off', () => {
  const fx = makeFixture('permissions-commit');
  try {
    const commit = ['Bash(cd:*)', 'Bash(git add:*)', 'Bash(git commit:*)'];
    const on = composeRules(readPermissions(at(fx.root)), 'default', 'engine');
    for (const rule of commit) assert.ok(on.allow.includes(rule), rule);
    assert.ok(on.deny.includes('Bash(git push:*)'));

    const off = composeRules(switchDefault(readPermissions(at(fx.root)), 'commit', false), 'default', 'engine');
    for (const rule of commit) assert.equal(off.allow.includes(rule), false, rule);
  } finally {
    fx.cleanup();
  }
});

// PERM3: the tree guard is a hook, not a rule — a default the person switches like the others, which
// adds nothing to the lists and says instead which tools it judges.
test('the tree guard is a default that adds no rule, switched by id from the terminal', () => {
  const fx = makeFixture('permissions-tree-guard');
  try {
    const guard = DEFAULTS.find((d) => d.id === 'tree-guard');
    assert.deepEqual(guard?.rules, []);
    assert.equal(guard?.hook, 'Edit|Write|MultiEdit|NotebookEdit');

    const listed = run(['list'], fx.root);
    assert.match(listed.out, /tree-guard\s+on\s+deny\s+a hook on Edit\|Write\|MultiEdit\|NotebookEdit/);

    assert.equal(run(['default', 'tree-guard', 'off'], fx.root).code, 0);
    assert.deepEqual(JSON.parse(readFileSync(at(fx.root), 'utf8')).defaultsOff, ['tree-guard']);
  } finally {
    fx.cleanup();
  }
});

// ——— The scopes.

test('only the session\'s own scopes are handed: its circle\'s and its repository\'s, never another\'s', () => {
  const fx = makeFixture('permissions-scopes');
  try {
    let file = readPermissions(at(fx.root));
    file = addRule(file, 'machine', null, 'allow', 'Bash(npm run test:*)');
    file = addRule(file, 'workspace', 'default', 'deny', 'Bash(rm -rf:*)');
    file = addRule(file, 'workspace', 'team', 'deny', 'WebFetch');
    file = addRule(file, 'repository', 'engine', 'ask', 'Edit(/docs/**)');
    file = addRule(file, 'repository', 'game', 'allow', 'Bash(make:*)');

    const rules = composeRules(file, 'default', 'engine');
    assert.ok(rules.allow.includes('Bash(npm run test:*)'));
    assert.ok(rules.deny.includes('Bash(rm -rf:*)'));
    assert.deepEqual(rules.ask, ['Edit(/docs/**)']);
    assert.equal(rules.deny.includes('WebFetch'), false);
    assert.equal(rules.allow.includes('Bash(make:*)'), false);

    // An intake belongs to no repository: its circle's rules, and no repository's.
    const intake = composeRules(file, null, null);
    assert.ok(intake.deny.includes('Bash(rm -rf:*)'));
    assert.equal(intake.ask.length, 0);
  } finally {
    fx.cleanup();
  }
});

test('a rule held in two scopes is handed once, and a rule has one place per scope', () => {
  const fx = makeFixture('permissions-once');
  try {
    let file = readPermissions(at(fx.root));
    file = addRule(file, 'machine', null, 'allow', 'Bash(make:*)');
    file = addRule(file, 'repository', 'engine', 'allow', 'Bash(make:*)');
    assert.equal(composeRules(file, 'default', 'engine').allow.filter((r) => r === 'Bash(make:*)').length, 1);

    file = addRule(file, 'machine', null, 'deny', 'Bash(make:*)');
    assert.deepEqual(file.machine.allow, []);
    assert.deepEqual(file.machine.deny, ['Bash(make:*)']);

    file = removeRule(file, 'machine', null, 'Bash(make:*)');
    assert.deepEqual(file.machine, { allow: [], ask: [], deny: [] });
  } finally {
    fx.cleanup();
  }
});

// ——— A rule is the harness's own shape.

test('a tool name with an optional specifier is a rule, and anything else is refused naming the shape', () => {
  for (const rule of ['WebFetch', 'Bash(npm run test:*)', 'Edit(/src/**)', 'mcp__daoris-knowledge__quest_list', 'WebFetch(domain:example.com)']) {
    assert.equal(ruleRefusal(rule), null, rule);
  }
  for (const rule of ['', '   ', 'Bash(', 'rm -rf /', '(npm test)', 'Bash(npm test)\nWebFetch']) {
    const refused = ruleRefusal(rule);
    assert.ok(refused, JSON.stringify(rule));
    assert.match(refused!, /Bash\(npm run test:\*\)/);
  }
});

// ——— The file.

test('the file round-trips, BOM-less LF, keeping what a newer build wrote', () => {
  const fx = makeFixture('permissions-roundtrip');
  try {
    fx.write(PERMISSIONS_FILE, '{"future":{"x":1},"defaultsOff":["connector"]}');
    writePermissions(at(fx.root), addRule(readPermissions(at(fx.root)), 'repository', 'engine', 'deny', 'Bash(rm:*)'));

    const text = readFileSync(at(fx.root), 'utf8');
    assert.equal(text.charCodeAt(0) === 0xfeff, false);
    assert.equal(text.includes('\r'), false);
    const written = JSON.parse(text);
    assert.deepEqual(written.future, { x: 1 });
    assert.deepEqual(written.repositories.engine.deny, ['Bash(rm:*)']);
    assert.deepEqual(written.defaultsOff, ['connector']);
    assert.equal('machine' in written, false, 'an empty scope is left out');
  } finally {
    fx.cleanup();
  }
});

test('a file that is not JSON reads as empty, says why, and the defaults still hold', () => {
  const fx = makeFixture('permissions-broken');
  try {
    fx.write(PERMISSIONS_FILE, '{ not json');
    const file = readPermissions(at(fx.root));
    assert.match(file.problem ?? '', /permissions\.json/);
    assert.ok(composeRules(file, 'default', 'engine').deny.includes('Bash(git push:*)'));

    const { code, out } = run([], fx.root);
    assert.equal(code, 0);
    assert.match(out, /could not be read/);
    assert.match(out, /no-push/);
  } finally {
    fx.cleanup();
  }
});

// ——— The verb.

test('`rules allow|ask|deny` writes the scope named, and `rules` lists every scope with the defaults', () => {
  const fx = makeFixture('permissions-verb');
  try {
    assert.equal(run(['allow', 'Bash(npm run test:*)'], fx.root).code, 0);
    assert.equal(run(['deny', 'Bash(rm -rf:*)', '--workspace', 'default'], fx.root).code, 0);
    const asked = run(['ask', 'Edit(/docs/**)', '--repository', 'engine'], fx.root);
    assert.equal(asked.code, 0);
    assert.match(asked.out, /engine/);

    const written = JSON.parse(readFileSync(at(fx.root), 'utf8'));
    assert.deepEqual(written.machine.allow, ['Bash(npm run test:*)']);
    assert.deepEqual(written.workspaces.default.deny, ['Bash(rm -rf:*)']);
    assert.deepEqual(written.repositories.engine.ask, ['Edit(/docs/**)']);

    const { code, out } = run(['list'], fx.root);
    assert.equal(code, 0);
    assert.match(out, /connector/);
    assert.match(out, /no-push/);
    assert.match(out, /Bash\(npm run test:\*\)/);
    assert.match(out, /workspace `default`/);
    assert.match(out, /repository `engine`/);
    // Said once, where a person reads it: the rules are Claude Code's, and no other agent is handed them.
    assert.match(out, /Claude Code/);
  } finally {
    fx.cleanup();
  }
});

test('`rules remove` takes a rule out of its scope, and `rules default <id> off|on` switches a default', () => {
  const fx = makeFixture('permissions-remove');
  try {
    run(['allow', 'WebFetch', '--repository', 'engine'], fx.root);
    assert.equal(run(['remove', 'WebFetch', '--repository', 'engine'], fx.root).code, 0);
    assert.equal('repositories' in JSON.parse(readFileSync(at(fx.root), 'utf8')), false);

    assert.equal(run(['default', 'no-push', 'off'], fx.root).code, 0);
    assert.deepEqual(JSON.parse(readFileSync(at(fx.root), 'utf8')).defaultsOff, ['no-push']);
    assert.equal(run(['default', 'no-push', 'on'], fx.root).code, 0);
    assert.equal('defaultsOff' in JSON.parse(readFileSync(at(fx.root), 'utf8')), false);
  } finally {
    fx.cleanup();
  }
});

test('the verb refuses a rule that is not one, two scopes at once, and a default switched neither way', () => {
  const fx = makeFixture('permissions-refusals');
  try {
    assert.match(captureError(() => run(['allow', 'rm -rf /'], fx.root)).message, /not a permission rule/);
    assert.match(
      captureError(() => run(['deny', 'WebFetch', '--workspace', 'a', '--repository', 'b'], fx.root)).message,
      /one scope/);
    assert.match(captureError(() => run(['default', 'no-push', 'maybe'], fx.root)).message, /on.*off/);
    assert.match(captureError(() => run(['allow'], fx.root)).message, /rule/);
    assert.match(captureError(() => run(['frobnicate'], fx.root)).message, /allow, ask, deny, remove, default/);
  } finally {
    fx.cleanup();
  }
});

test('without a home the verb refuses naming DAORIS_HOME (D63), and never writes under the profile', () => {
  const saved = process.env.DAORIS_HOME;
  delete process.env.DAORIS_HOME;
  try {
    assert.throws(() => permissionsPath({}), /DAORIS_HOME/);
    const error = captureError(() => commandRules({
      root: process.cwd(), argv: ['list'], write: () => {}, packageRoot: process.cwd(),
    }));
    assert.match(error.message, /DAORIS_HOME/);
  } finally {
    if (saved !== undefined) process.env.DAORIS_HOME = saved;
  }
});
