import { test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { DEFAULTS } from '../../src/agents/permissions.ts';
import { PROPOSALS_FOLDER } from '../../src/agents/ruleproposals.ts';
import { commandHarness, profileHome } from '../../src/agents/toolchain.ts';
import type { VerbPress } from '../../src/platform/door.ts';
import { homeEntry } from '../../src/platform/home.ts';
import { makeFixture, type Fixture } from '../_fixture.ts';

/**
 * RPC1f (D171 point 3, the terminal RPC design §7.2, §7.3 and §9): an `agent` verb that changes the home is the window's route for the
 * same change while Daoris runs on it (the Agents and Accounts places, the rules), with the payload the window sends, said in the alone
 * path's words and made by the instance alone. A door that answers as the running Daoris would is a test's here: the client's own rows
 * are `service/instance.test.ts`'s. A verb that needs this terminal (its installer, its sign-in, a key on stdin, a pin's download) acts
 * alone, as `door.ts`'s `AGENT_VERBS` says why.
 */

/** A home with nothing in it: the verbs read `DAORIS_HOME`, as an install's `daoris` does. */
function home(name: string): Fixture {
  return makeFixture(name);
}

function withHome<T>(fx: Fixture, act: () => T): T {
  const saved = process.env.DAORIS_HOME;
  const config = process.env.DAORIS_HARNESS_CONFIG;
  process.env.DAORIS_HOME = fx.root;
  delete process.env.DAORIS_HARNESS_CONFIG;
  try {
    return act();
  } finally {
    if (saved === undefined) delete process.env.DAORIS_HOME;
    else process.env.DAORIS_HOME = saved;
    if (config !== undefined) process.env.DAORIS_HARNESS_CONFIG = config;
  }
}

/** The verb alone, as with no Daoris running. */
function run(fx: Fixture, argv: string[]): { code: number; out: string } {
  const lines: string[] = [];
  const code = withHome(fx, () => commandHarness({ root: process.cwd(), argv, write: (line) => lines.push(line), packageRoot: process.cwd() }));
  return { code: code as number, out: lines.join('\n') };
}

/** The verb through a door that answers as the running Daoris would, recording what it was handed. */
function sent(fx: Fixture, argv: string[], answer: unknown = {}): { code: number; out: string; press: VerbPress | null } {
  const lines: string[] = [];
  let press: VerbPress | null = null;
  const code = withHome(fx, () => commandHarness(
    { root: process.cwd(), argv, write: (line) => lines.push(line), packageRoot: process.cwd() }, null, ['npm'],
    (given) => {
      press = given;
      return (given.route ? given.words(answer) : given.alone()) as 0;
    }));
  return { code: code as number, out: lines.join('\n'), press };
}

const routed = (press: VerbPress | null) => [press?.route?.route, press?.route?.class, press?.payload];

/** The client path's text is the alone path's, case for case (§7.3): the client first, so the alone path then makes the change. */
function same(fx: Fixture, argv: string[], route: string, payload: unknown, answer: unknown = {}): void {
  const client = sent(fx, argv, answer);
  assert.deepEqual(routed(client.press), [route, 'person', payload], argv.join(' '));
  // The verb as a lease names it: `profile`'s and `rules`' own verb with it, never an argument.
  assert.equal(client.press?.what, ['profile', 'rules'].includes(argv[0]!) ? `agent ${argv[0]} ${argv[1]}` : `agent ${argv[0]}`);
  assert.equal(client.out, run(fx, argv).out, argv.join(' '));
}

test('the rules and an agent\'s proposals are RULE_ACTION and RULE_PROPOSAL while Daoris runs, in the alone path\'s words', () => {
  const fx = home('agent-door-rules');
  try {
    const rule = 'Bash(npm test:*)';
    const client = sent(fx, ['rules', 'allow', rule]);
    assert.deepEqual(routed(client.press), ['RULE_ACTION', 'person', { action: 'add', scope: 'machine', list: 'allow', rule }]);
    assert.equal(existsSync(homeEntry(fx.root, 'permissions')), false, 'the instance writes the rules, not this terminal');
    assert.equal(client.out, run(fx, ['rules', 'allow', rule]).out);

    same(fx, ['rules', 'deny', rule, '--workspace', 'aurora'], 'RULE_ACTION',
      { action: 'add', scope: 'workspace', name: 'aurora', list: 'deny', rule });
    same(fx, ['rules', 'remove', rule, '--repository', 'engine'], 'RULE_ACTION',
      { action: 'remove', scope: 'repository', name: 'engine', rule });
    same(fx, ['rules', 'default', DEFAULTS[0]!.id, 'off'], 'RULE_ACTION', { action: 'default', id: DEFAULTS[0]!.id, on: false });

    for (const [id, argv, payload] of [
      ['a1b2c3d4', ['rules', 'accept', 'a1b2c3d4'], { id: 'a1b2c3d4', accept: true }],
      ['e5f6a7b8', ['rules', 'decline', '#e5f6a7b8', '--note', 'not now'], { id: 'e5f6a7b8', accept: false, note: 'not now' }],
    ] as const) {
      const file = fx.write(join(PROPOSALS_FOLDER, `${id}.json`), `${JSON.stringify({
        id, proposed: '2026-10-20T08:00:00.0000000+00:00', by: { session: 's1a2b3c4', ask: null, folder: 'X:/daoris/engine' },
        change: { action: 'add', scope: 'machine', name: null, list: 'allow', rule: 'Bash(make:*)', default: null, on: null },
        why: 'The tests need it.', state: 'proposed',
      }, null, 2)}\n`);
      const answered = sent(fx, [...argv]);
      assert.deepEqual(routed(answered.press), ['RULE_PROPOSAL', 'person', payload]);
      assert.equal(JSON.parse(readFileSync(file, 'utf8')).state, 'proposed', 'the instance settles it, not this terminal');
      assert.equal(answered.out, run(fx, [...argv]).out);
    }
  } finally {
    fx.cleanup();
  }
});

test('an account\'s add, default, name, lists, order, use, cool-off and removal are the Accounts place\'s routes while Daoris runs', () => {
  const fx = home('agent-door-accounts');
  try {
    const client = sent(fx, ['profile', 'add', 'claude-code', 'work']);
    assert.deepEqual(routed(client.press), ['HARNESS_ACTION', 'person', { action: 'profile-add', harness: 'claude-code', profile: 'work' }]);
    assert.equal(existsSync(profileHome(fx.root, 'claude-code', 'work')), false, 'the instance makes it');
    assert.equal(client.out, run(fx, ['profile', 'add', 'claude-code', 'work']).out);
    run(fx, ['profile', 'add', 'claude-code', 'home']);

    same(fx, ['profile', 'order', 'claude-code', 'work', 'home'], 'ACCOUNT_USE',
      { action: 'order', harness: 'claude-code', accounts: ['work', 'home'] });
    same(fx, ['profile', 'default', 'claude-code', 'work'], 'HARNESS_ACTION',
      { action: 'profile-default', harness: 'claude-code', profile: 'work' });
    same(fx, ['profile', 'default', 'claude-code', 'home', '--workspace', 'aurora'], 'HARNESS_ACTION',
      { action: 'profile-default', harness: 'claude-code', profile: 'home', workspace: 'aurora' });
    same(fx, ['profile', 'default', 'claude-code', '--clear', '--workspace', 'aurora'], 'HARNESS_ACTION',
      { action: 'profile-default', harness: 'claude-code', workspace: 'aurora' });
    same(fx, ['profile', 'use', 'claude-code', 'goal', '--switch', 'off'], 'ACCOUNT_USE',
      { action: 'use', harness: 'claude-code', use: 'goal', switch: 'off' });
    same(fx, ['profile', 'use', 'claude-code', '--keep', 'home'], 'ACCOUNT_USE', { action: 'use', harness: 'claude-code', keep: 'home' });
    same(fx, ['profile', 'use', 'claude-code', '--no-keep'], 'ACCOUNT_USE', { action: 'use', harness: 'claude-code', noKeep: true });
    same(fx, ['profile', 'use', 'claude-code', '--clear'], 'ACCOUNT_USE', { action: 'use', harness: 'claude-code', clear: true });
    run(fx, ['profile', 'order', 'claude-code', 'work', '--workspace', 'aurora']);
    same(fx, ['profile', 'join', 'claude-code', 'home', 'aurora'], 'HARNESS_ACTION',
      { action: 'profile-join', harness: 'claude-code', profile: 'home', join: ['aurora'] });
    same(fx, ['profile', 'rename', 'claude-code', 'work', 'Work'], 'HARNESS_ACTION',
      { action: 'profile-rename', harness: 'claude-code', profile: 'work', name: 'Work' }, { name: 'Work' });
    same(fx, ['profile', 'ready', 'claude-code', 'work'], 'ACCOUNT_USE', { action: 'ready', harness: 'claude-code', profile: 'work' },
      { ended: false });
    same(fx, ['profile', 'ready', 'claude-code', '--own'], 'ACCOUNT_USE', { action: 'ready', harness: 'claude-code', own: true },
      { ended: false });
    same(fx, ['profile', 'order', 'claude-code', '--clear'], 'ACCOUNT_USE', { action: 'order', harness: 'claude-code', accounts: [] });
    same(fx, ['profile', 'remove', 'claude-code', 'home'], 'HARNESS_ACTION',
      { action: 'profile-remove', harness: 'claude-code', profile: 'home' }, { exitCode: 0 });

    // An account's model and effort, said as the instance answers them.
    const model = sent(fx, ['settings', 'claude-code', '--account', 'work', 'model', 'opus'],
      { harness: 'claude-code', profile: 'work', model: 'opus', effort: null, perModel: [], problem: null });
    assert.deepEqual(routed(model.press), ['SET_AGENT_SETTINGS', 'person', { harness: 'claude-code', profile: 'work', model: 'opus' }]);
    assert.equal(model.out, run(fx, ['settings', 'claude-code', '--account', 'work', 'model', 'opus']).out);
    const settings = sent(fx, ['settings', 'claude-code', '--account', 'work', 'effort', 'high', '--for', 'opus'],
      { harness: 'claude-code', profile: 'work', model: 'opus', effort: null, perModel: [{ model: 'opus', effort: 'high' }], problem: null });
    assert.deepEqual(routed(settings.press), ['SET_AGENT_SETTINGS', 'person',
      { harness: 'claude-code', profile: 'work', perModel: { opus: 'high' } }]);
    assert.equal(settings.out, run(fx, ['settings', 'claude-code', '--account', 'work', 'effort', 'high', '--for', 'opus']).out);
    assert.equal(sent(fx, ['settings', 'claude-code', '--account', 'work']).press, null, 'a read holds nothing');

    same(fx, ['unpin', 'claude-code'], 'HARNESS_ACTION', { action: 'unpin', harness: 'claude-code' });
    const scoped = sent(fx, ['unpin', 'claude-code', '--workspace', 'aurora']);
    assert.deepEqual([scoped.press?.what, scoped.press?.route], ['agent unpin', null], 'HARNESS_ACTION\'s unpin names no workspace');

    for (const argv of [['list'], ['profile', 'list'], ['profile', 'use', 'claude-code'], ['rules'], ['rules', 'proposals']]) {
      assert.equal(sent(fx, argv).press, null, `${argv.join(' ')} is a read`);
    }
  } finally {
    fx.cleanup();
  }
});
