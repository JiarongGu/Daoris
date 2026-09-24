import { test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, mkdirSync, readdirSync, readFileSync, statSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { grantTrust, isTrusted, trustKey } from '../src/trust.ts';
import { commandHarness } from '../src/toolchain.ts';
import { makeFixture, captureError } from './_fixture.ts';

/**
 * `daoris agent trust` — the harness's trust flag, asked and then written (D73).
 *
 * The CLI's half of a TWIN CONTRACT: the driver's `ClaudeTrust` reads the same flag in the same file
 * before every start, and writes it from the screen's confirmation. The two share no code, so the
 * FILE is the contract — the same cases are asserted on both sides, and they move together.
 *
 * Every case runs against a fixture. The account's real file is never read or written by a test.
 */

// ——— The question: the twin of `ClaudeTrust.Accepted`.

test('an accepted folder is trusted; one never recorded, or declined, is not', () => {
  const fx = makeFixture('trust-question');
  const file = fx.write('.claude.json', JSON.stringify({
    projects: { 'D:\\fam\\engine': { hasTrustDialogAccepted: true }, 'D:/fam/declined': { hasTrustDialogAccepted: false } },
  }));

  assert.equal(isTrusted(file, 'D:\\fam\\engine'), true);
  // Absent is NOT unknown here: the harness prompts on first visit, so never-recorded is never-accepted.
  assert.equal(isTrusted(file, 'D:\\fam\\other'), false);
  assert.equal(isTrusted(file, 'D:\\fam\\declined'), false);
});

test('the same folder spelled differently is the same folder', () => {
  const fx = makeFixture('trust-spellings');
  const file = fx.write('.claude.json', JSON.stringify({ projects: { 'D:\\fam\\engine': { hasTrustDialogAccepted: true } } }));

  for (const spelling of ['d:\\fam\\engine', 'D:/fam/engine', 'D:\\fam\\engine\\']) {
    assert.equal(isTrusted(file, spelling), true, spelling);
  }
});

test('no file, or one this build cannot read, is unknown rather than untrusted', () => {
  const fx = makeFixture('trust-unknown');
  assert.equal(isTrusted(join(fx.root, 'absent.json'), 'D:\\fam\\engine'), null);
  for (const text of ['not json at all', '{"projects":"a string where an object was"}', '{}']) {
    assert.equal(isTrusted(fx.write('.claude.json', text), 'D:\\fam\\engine'), null, text);
  }
});

// ——— The grant: the twin of `ClaudeTrust.Grant`.

test('a grant records a new folder in the form the harness writes today, and the question then says yes', () => {
  const fx = makeFixture('trust-new');
  const file = fx.write('.claude.json', JSON.stringify({ numStartups: 3, projects: { 'D:/fam/other': { hasTrustDialogAccepted: true } } }));

  const grant = grantTrust(file, 'D:\\fam\\engine');

  assert.deepEqual(grant, { key: 'D:/fam/engine', changed: true, verified: true });
  assert.equal(isTrusted(file, 'D:\\fam\\engine'), true);
  assert.equal(trustKey('D:\\fam\\engine\\'), 'D:/fam/engine');
});

test('a grant changes the one flag, in place under the key the harness wrote, and nothing else', () => {
  const fx = makeFixture('trust-in-place');
  const file = fx.write('.claude.json', `${JSON.stringify({
    numStartups: 42,
    userID: 'abc',
    projects: {
      'd:\\fam\\engine': { allowedTools: [], lastCost: 0.1234, hasTrustDialogAccepted: false, lastSessionId: 's1' },
      'D:/fam/other': { hasTrustDialogAccepted: true, mcpServers: {} },
    },
    oauthAccount: { emailAddress: 'someone@example.invalid' },
  }, null, 2)}\n`);

  const grant = grantTrust(file, 'D:\\fam\\engine\\');

  const after = JSON.parse(readFileSync(file, 'utf8'));
  assert.equal(grant.key, 'd:\\fam\\engine');
  assert.equal(after.numStartups, 42);
  assert.equal(after.userID, 'abc');
  assert.equal(after.oauthAccount.emailAddress, 'someone@example.invalid');
  assert.deepEqual(Object.keys(after.projects), ['d:\\fam\\engine', 'D:/fam/other']);
  assert.deepEqual(after.projects['d:\\fam\\engine'],
    { allowedTools: [], lastCost: 0.1234, hasTrustDialogAccepted: true, lastSessionId: 's1' });
  // The harness's own formatting: two-space indent, LF, the trailing newline it had.
  const text = readFileSync(file, 'utf8');
  assert.ok(text.startsWith('{\n  "numStartups"'));
  assert.ok(text.endsWith('}\n'));
  assert.ok(!text.includes('\r'));
});

test('granting what is already granted writes nothing', () => {
  const fx = makeFixture('trust-already');
  const file = fx.write('.claude.json', '{"projects":{"D:/fam/engine":{"hasTrustDialogAccepted":true}}}');
  const before = statSync(file).mtimeMs;

  const grant = grantTrust(file, 'D:\\fam\\engine');

  assert.deepEqual(grant, { key: 'D:/fam/engine', changed: false, verified: true });
  assert.equal(readFileSync(file, 'utf8'), '{"projects":{"D:/fam/engine":{"hasTrustDialogAccepted":true}}}');
  assert.equal(statSync(file).mtimeMs, before);
});

test('a grant into a home with no file yet makes one holding only the grant', () => {
  const fx = makeFixture('trust-no-file');
  const file = join(fx.root, 'profile', '.claude.json');

  const grant = grantTrust(file, 'D:\\fam\\engine');

  assert.equal(grant.changed, true);
  assert.equal(isTrusted(file, 'D:\\fam\\engine'), true);
});

// 🔴 A file this build cannot read is never overwritten: for a WRITE, unknown is a refusal.
test('a file this build cannot read is refused and left exactly as it was', () => {
  const fx = makeFixture('trust-unreadable');
  for (const text of ['not json at all', '{"projects":"a string where an object was"}', '[1,2,3]']) {
    const file = fx.write('.claude.json', text);
    const error = captureError(() => grantTrust(file, 'D:\\fam\\engine'));
    assert.match(error.message, /nothing was written/);
    assert.equal(readFileSync(file, 'utf8'), text);
  }
});

test('a grant leaves nothing beside the file', () => {
  const fx = makeFixture('trust-beside');
  const file = fx.write('.claude.json', '{"projects":{}}');

  grantTrust(file, 'D:\\fam\\engine');

  assert.deepEqual(readdirSync(fx.root), ['.claude.json']);
});

// ——— The verb: `daoris agent trust <agent> <folder> [--profile <name>] --yes`.

/**
 * A machine in a fixture: a Daoris home with a named Claude Code account, a folder to trust, and the
 * account's OWN home redirected — the default account lives under the user profile, which a test must
 * never touch.
 */
function machine(name: string) {
  const fx = makeFixture(name);
  const folder = join(fx.root, 'engine');
  mkdirSync(folder, { recursive: true });
  const profileFile = join(fx.root, 'data', 'harnesses', 'claude-code', 'work', '.claude.json');
  mkdirSync(join(fx.root, 'data', 'harnesses', 'claude-code', 'work'), { recursive: true });
  const ownHome = join(fx.root, 'own-home');
  mkdirSync(ownHome, { recursive: true });
  return { fx, folder, profileFile, ownHome, config: join(fx.root, 'data', 'harnesses.json') };
}

function run(argv: string[], m: ReturnType<typeof machine>): { code: number; out: string } {
  const saved = { config: process.env.DAORIS_HARNESS_CONFIG, home: process.env.HOME, profile: process.env.USERPROFILE };
  process.env.DAORIS_HARNESS_CONFIG = m.config;
  process.env.HOME = m.ownHome;
  process.env.USERPROFILE = m.ownHome;
  const lines: string[] = [];
  try {
    const code = commandHarness({ root: process.cwd(), argv, write: (line) => lines.push(line), packageRoot: process.cwd() }) as number;
    return { code, out: lines.join('\n') };
  } finally {
    for (const [key, value] of [['DAORIS_HARNESS_CONFIG', saved.config], ['HOME', saved.home], ['USERPROFILE', saved.profile]] as const) {
      if (value === undefined) delete process.env[key];
      else process.env[key] = value;
    }
  }
}

test('asked without --yes, it says exactly what it would grant, and to whom — and grants nothing', () => {
  const m = machine('trust-verb-ask');

  const { code, out } = run(['trust', 'claude-code', m.folder, '--profile', 'work'], m);

  assert.equal(code, 1);
  assert.ok(out.includes(m.folder), out);
  assert.ok(out.includes(m.profileFile), out);
  // What trusting a folder means, in the harness's own terms.
  assert.match(out, /permissions\.allow/);
  assert.match(out, /--yes/);
  assert.ok(!existsSync(m.profileFile));
});

test('--dry-run prints the same grant and writes nothing', () => {
  const m = machine('trust-verb-dry');

  const { code, out } = run(['trust', 'claude-code', m.folder, '--profile', 'work', '--dry-run'], m);

  assert.equal(code, 0);
  assert.ok(out.includes(m.profileFile), out);
  assert.ok(!existsSync(m.profileFile));
});

test('with --yes it writes the grant into that account\'s own file', () => {
  const m = machine('trust-verb-yes');

  const { code, out } = run(['trust', 'claude-code', m.folder, '--profile', 'work', '--yes'], m);

  assert.equal(code, 0, out);
  assert.equal(isTrusted(m.profileFile, m.folder), true);
  assert.ok(!existsSync(join(m.ownHome, '.claude.json')), 'the other account was not touched');
});

test('with no profile named, it is the agent\'s own home — the account a session with none runs as', () => {
  const m = machine('trust-verb-own');

  const { code } = run(['trust', 'claude-code', m.folder, '--yes'], m);

  assert.equal(code, 0);
  assert.equal(isTrusted(join(m.ownHome, '.claude.json'), m.folder), true);
});

test('the machine\'s default account is the one granted when none is named', () => {
  const m = machine('trust-verb-default');
  writeFileSync(m.config, JSON.stringify({ defaults: { 'claude-code': 'work' } }));

  const { code } = run(['trust', 'claude-code', m.folder, '--yes'], m);

  assert.equal(code, 0);
  assert.equal(isTrusted(m.profileFile, m.folder), true);
});

test('a door is granted in its owner\'s account, and says so', () => {
  const m = machine('trust-verb-door');

  const { code, out } = run(['trust', 'claude-code-acp', m.folder, '--profile', 'work', '--yes'], m);

  assert.equal(code, 0);
  assert.match(out, /runs as `claude-code`'s accounts/);
  assert.equal(isTrusted(m.profileFile, m.folder), true);
});

test('an agent with no trust question, or a folder that is not there, is refused', () => {
  const m = machine('trust-verb-refused');

  assert.match(captureError(() => run(['trust', 'codex', m.folder, '--yes'], m)).message, /no trust question/);
  assert.match(
    captureError(() => run(['trust', 'claude-code', join(m.fx.root, 'nowhere'), '--yes'], m)).message,
    /no folder/);
  assert.match(captureError(() => run(['trust', 'claude-code'], m)).message, /<agent> <folder>/);
});
