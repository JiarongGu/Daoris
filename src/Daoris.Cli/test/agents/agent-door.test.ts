import { test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync } from 'node:fs';
import { commandHarness, profileHome } from '../../src/agents/toolchain.ts';
import type { VerbPress } from '../../src/platform/door.ts';
import { makeFixture, type Fixture } from '../_fixture.ts';

/**
 * RPC1f (D171 point 3, the terminal RPC design §7.2, §7.3 and §9): an `agent` verb that changes the home is the window's route for the
 * same change while Daoris runs on it (the Agents and Accounts places), with the payload the window sends, said in the alone path's
 * words and made by the instance alone; the rules, a trust and an account's settings are the driver program's since PUSH1f2d. A door that answers as the running Daoris would is a test's here: the client's own rows
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
  // The verb as a lease names it: `profile`'s own verb with it, never an argument.
  assert.equal(client.press?.what, argv[0] === 'profile' ?`agent ${argv[0]} ${argv[1]}` : `agent ${argv[0]}`);
  assert.equal(client.out, run(fx, argv).out, argv.join(' '));
}

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

    same(fx, ['unpin', 'claude-code'], 'HARNESS_ACTION', { action: 'unpin', harness: 'claude-code' });
    const scoped = sent(fx, ['unpin', 'claude-code', '--workspace', 'aurora']);
    assert.deepEqual([scoped.press?.what, scoped.press?.route], ['agent unpin', null], 'HARNESS_ACTION\'s unpin names no workspace');

    for (const argv of [['list'], ['profile', 'list'], ['profile', 'use', 'claude-code']]) {
      assert.equal(sent(fx, argv).press, null, `${argv.join(' ')} is a read`);
    }
  } finally {
    fx.cleanup();
  }
});
