import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { PERMISSIONS_FILE, readPermissions } from '../src/permissions.ts';
import { PROPOSALS_FOLDER, answerProposal, describeChange, loadProposals } from '../src/ruleproposals.ts';
import { commandHarness } from '../src/toolchain.ts';
import { captureError, makeFixture } from './_fixture.ts';
import type { Fixture } from './_fixture.ts';

/**
 * `daoris agent rules proposals|accept|decline` — an agent's proposals to change the rules (PERM2, D74),
 * answered from a terminal (D50).
 *
 * The CLI's half of a TWIN CONTRACT: the connector (the service's `RuleProposalBox`) writes one file per
 * proposal under the home, the driver's `RuleProposals.cs` settles it at its tick, and this reads and
 * answers the same file. They share no code — THE FILE is the contract, so the file below is written
 * exactly as the connector writes it.
 */

const AT = '2026-09-24T10:00:00.0000000+00:00';

interface Shape {
  action: 'add' | 'remove' | 'default';
  scope?: 'machine' | 'workspace' | 'repository';
  name?: string | null;
  list?: 'allow' | 'ask' | 'deny' | null;
  rule?: string | null;
  default?: string | null;
  on?: boolean | null;
  state?: string;
  session?: string | null;
  ask?: string | null;
  proposed?: string;
}

/** A proposal exactly as the connector writes it. */
function propose(fx: Fixture, id: string, shape: Shape): string {
  return fx.write(join(PROPOSALS_FOLDER, `${id}.json`), `${JSON.stringify({
    id,
    proposed: shape.proposed ?? AT,
    by: { session: shape.session === undefined ? 's1a2b3c4' : shape.session, ask: shape.ask ?? null, folder: 'C:/somewhere/engine' },
    change: {
      action: shape.action,
      scope: shape.scope ?? 'machine',
      name: shape.name ?? null,
      list: shape.list ?? null,
      rule: shape.rule ?? null,
      default: shape.default ?? null,
      on: shape.on ?? null,
    },
    why: 'The tests need it.',
    state: shape.state ?? 'proposed',
  }, null, 2)}\n`);
}

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

const rules = (fx: Fixture) => readPermissions(join(fx.root, PERMISSIONS_FILE));
const read = (path: string) => JSON.parse(readFileSync(path, 'utf8'));

// ——— Reading.

test('proposals read newest first, a file that is not one left out, and each says who made it', () => {
  const fx = makeFixture('proposals-read');
  try {
    propose(fx, 'p0000001', { action: 'add', list: 'deny', rule: 'Bash(rm:*)' });
    propose(fx, 'p0000002', { action: 'add', list: 'allow', rule: 'WebFetch', session: 'i9n8t7k6', ask: 'a1b2c3', proposed: '2026-09-24T11:00:00.0000000+00:00' });
    fx.write(join(PROPOSALS_FOLDER, 'broken.json'), '{ not json');

    const read = loadProposals(join(fx.root, PROPOSALS_FOLDER));
    assert.deepEqual(read.map((p) => p.id), ['p0000002', 'p0000001']);
    assert.equal(read[0]!.ask, 'a1b2c3');
    assert.equal(read[0]!.change.list, 'allow');
    assert.deepEqual(loadProposals(join(fx.root, 'nowhere')), []);
  } finally {
    fx.cleanup();
  }
});

test('a change reads in a line in the driver\'s words', () => {
  assert.equal(describeChange({ action: 'add', scope: 'machine', name: null, list: 'allow', rule: 'WebFetch', default: null, on: null }),
    'allow `WebFetch` for every session on this machine');
  assert.equal(describeChange({ action: 'remove', scope: 'repository', name: 'engine', list: null, rule: 'Bash(make:*)', default: null, on: null }),
    'remove `Bash(make:*)` from repository `engine`');
  assert.equal(describeChange({ action: 'default', scope: 'machine', name: null, list: null, rule: null, default: 'tree-guard', on: false }),
    'switch the default `tree-guard` off');
});

test('`rules proposals` lists each with its state, its author and its reason, and says how to answer', () => {
  const fx = makeFixture('proposals-list');
  try {
    propose(fx, 'p0000001', { action: 'add', list: 'allow', rule: 'WebFetch', state: 'waiting', session: 'i9n8t7k6', ask: 'a1b2c3' });
    propose(fx, 'p0000002', { action: 'remove', scope: 'workspace', name: 'default', rule: 'Bash(make:*)', state: 'applied', session: null });

    const { code, out } = run(['proposals'], fx.root);
    assert.equal(code, 0);
    assert.match(out, /#p0000001\s+waiting\s+allow `WebFetch` for every session on this machine/);
    assert.match(out, /session i9n8t7k6 \(ask #a1b2c3\)/);
    assert.match(out, /a session the driver did not start/);
    assert.match(out, /The tests need it\./);
    assert.match(out, /daoris agent rules accept/);
    // Said where a person reads it: a narrowing needs no one, a widening needs them.
    assert.match(out, /widening waits for your yes/);
  } finally {
    fx.cleanup();
  }
});

test('`rules proposals` with none says so', () => {
  const fx = makeFixture('proposals-none');
  try {
    const { code, out } = run(['proposals'], fx.root);
    assert.equal(code, 0);
    assert.match(out, /No agent has proposed a change/);
  } finally {
    fx.cleanup();
  }
});

// ——— The person's answer.

test('`rules accept` applies a waiting widening, records the person, and keeps what the connector wrote', () => {
  const fx = makeFixture('proposals-accept');
  try {
    const path = propose(fx, 'p0000007', { action: 'add', scope: 'workspace', name: 'default', list: 'allow', rule: 'WebFetch', state: 'waiting' });

    const { code, out } = run(['accept', 'p0000007'], fx.root);
    assert.equal(code, 0);
    assert.match(out, /WebFetch/);
    assert.deepEqual(rules(fx).workspaces.default?.allow, ['WebFetch']);

    const settled = read(path);
    assert.equal(settled.state, 'accepted');
    assert.equal(settled.settled.by, 'the person');
    assert.equal(settled.by.folder, 'C:/somewhere/engine');
    assert.equal(settled.why, 'The tests need it.');
    const text = readFileSync(path, 'utf8');
    assert.equal(text.includes('\r'), false);
    assert.equal(text.charCodeAt(0) === 0xfeff, false);
  } finally {
    fx.cleanup();
  }
});

test('`rules decline` changes nothing and keeps the person\'s reason', () => {
  const fx = makeFixture('proposals-decline');
  try {
    const path = propose(fx, 'p0000008', { action: 'add', list: 'allow', rule: 'Bash(curl:*)', state: 'waiting' });

    assert.equal(run(['decline', '#p0000008', '--note', 'Not from a session.'], fx.root).code, 0);
    assert.deepEqual(rules(fx).machine.allow, []);
    const settled = read(path);
    assert.equal(settled.state, 'declined');
    assert.equal(settled.settled.note, 'Not from a session.');
  } finally {
    fx.cleanup();
  }
});

test('the person may answer a proposal before the driver has seen it, a default as well as a rule', () => {
  const fx = makeFixture('proposals-early');
  try {
    propose(fx, 'p0000009', { action: 'default', default: 'tree-guard', on: false });
    answerProposal(fx.root, 'p0000009', true, null, AT);
    assert.deepEqual(rules(fx).defaultsOff, ['tree-guard']);
  } finally {
    fx.cleanup();
  }
});

test('a settled proposal is history, and an unknown or path-shaped id is no proposal', () => {
  const fx = makeFixture('proposals-refusals');
  try {
    propose(fx, 'p0000001', { action: 'add', list: 'deny', rule: 'Bash(rm:*)', state: 'applied' });
    fx.write(PERMISSIONS_FILE, '{"id":"x","change":{"action":"add"}}');

    assert.match(captureError(() => run(['accept', 'p0000001'], fx.root)).message, /already applied/);
    const unknown = captureError(() => run(['decline', 'nope1234'], fx.root));
    assert.match(unknown.message, /nope1234/);
    assert.match(unknown.message, /daoris agent rules proposals/);
    assert.match(captureError(() => run(['accept', '../permissions'], fx.root)).message, /no proposal/);
    assert.match(captureError(() => run(['accept'], fx.root)).message, /id/);
  } finally {
    fx.cleanup();
  }
});

test('a yes the rules cannot take writes nothing and leaves the proposal waiting', () => {
  const fx = makeFixture('proposals-malformed');
  try {
    const path = propose(fx, 'p0000003', { action: 'add', list: 'deny', rule: 'rm -rf everything', state: 'waiting' });

    assert.match(captureError(() => run(['accept', 'p0000003'], fx.root)).message, /not a permission rule/);
    assert.equal(fx.exists(PERMISSIONS_FILE), false);
    assert.equal(read(path).state, 'waiting');
  } finally {
    fx.cleanup();
  }
});

test('without a home the verbs refuse naming DAORIS_HOME (D63)', () => {
  const saved = process.env.DAORIS_HOME;
  delete process.env.DAORIS_HOME;
  try {
    const error = captureError(() => commandHarness({
      root: process.cwd(), argv: ['rules', 'proposals'], write: () => {}, packageRoot: process.cwd(),
    }));
    assert.match(error.message, /DAORIS_HOME/);
  } finally {
    if (saved !== undefined) process.env.DAORIS_HOME = saved;
  }
});
