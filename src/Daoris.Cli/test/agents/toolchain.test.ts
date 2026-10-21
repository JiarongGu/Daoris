import { test } from 'node:test';
import assert from 'node:assert/strict';
import { chmodSync, existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { dirname, join } from 'node:path';
import { gzipSync } from 'node:zlib';
import {
  TOOLCHAINS, commandHarness, harnessesPath, keyOf, managedBinary, managedHome,
  nextAccount, profileHome, profiles, probe, readHarnessSettings, removeProfile, resolveProfile,
  resolveVersion, versionFromNpm, withDefault, writeHarnessSettings,
} from '../../src/agents/toolchain.ts';
import type { HarnessSettings, Toolchain } from '../../src/agents/toolchain.ts';
import { CLAUDE_LATEST, CODEX_LATEST, CODEX_RELEASES, codexTarget } from '../../src/agents/channels.ts';
import type { Fetcher } from '../../src/agents/channels.ts';
import { homeEntry } from '../../src/platform/home.ts';
import { makeFixture } from '../_fixture.ts';
import { captureError } from '../_fixture.ts';
import { defaulted } from './_wiring.ts';
import { TAR_END, tarEntry } from '../_tar.ts';

/**
 * `daoris agent` — management parity for the toolchain (D49 §4, D50).
 *
 * The whole feature rests on one sentence: **Daoris manages directories and names, and a sign-in stays
 * the tool's.** The one secret Daoris keeps is an API key a person gives it (twin rule 6, D67 §1).
 * A profile is a directory Daoris owns the location of; whatever credential ends up inside it was put
 * there by the harness's own login flow and stays in the harness's own store. These tests are written
 * to fail if that ever stops being true.
 *
 * They also hold the CLI's half of a TWIN CONTRACT. The driver's `Harnesses.cs` implements the same
 * rules in another language against the same file and the same directory layout, because the two
 * artefacts share no code. The three rules below are asserted in both, and they move together.
 */

/** Everything runs against a fixture, never the developer's real home. */
function at(fx: { root: string }): string {
  const path = homeEntry(fx.root, 'harnesses');
  mkdirSync(dirname(path), { recursive: true });
  return path;
}

function run(argv: string[], path: string): { code: number; out: string } {
  const saved = process.env.DAORIS_HARNESS_CONFIG;
  process.env.DAORIS_HARNESS_CONFIG = path;
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

test('the path is the override, or the file under the Daoris home, or a refusal naming what to set', () => {
  assert.equal(harnessesPath({ DAORIS_HARNESS_CONFIG: '/x/h.json' }), '/x/h.json');
  assert.equal(harnessesPath({ DAORIS_HOME: '/x/daoris' }), join('/x/daoris', 'data', 'harnesses.json'));
  // 🔴 No default under the user profile (D63): a machine nobody pointed is refused, not written to.
  assert.throws(() => harnessesPath({}), /DAORIS_HOME/);
});

// ——— Twin rule 1: a profile is a directory, and the directories that exist are the profiles.

test('the profiles that exist are the directories that exist', () => {
  const fx = makeFixture('harness-profiles');

  assert.deepEqual(profiles(fx.root, 'claude-code'), []);
  mkdirSync(profileHome(fx.root, 'claude-code', 'work'), { recursive: true });
  mkdirSync(profileHome(fx.root, 'claude-code', 'personal'), { recursive: true });
  mkdirSync(profileHome(fx.root, 'codex', 'work'), { recursive: true });

  assert.deepEqual(profiles(fx.root, 'claude-code'), ['personal', 'work']);
  assert.deepEqual(profiles(fx.root, 'codex'), ['work']);

  // HELPSTUCK1: a lock's folder beside the accounts (a lockfile library locks a folder by making `<folder>.lock` beside it) and
  // a hidden one are no account, whatever they hold. The driver's `HarnessSettingsTests` holds the same rows.
  mkdirSync(`${profileHome(fx.root, 'claude-code', 'work')}.lock`, { recursive: true });
  mkdirSync(`${profileHome(fx.root, 'claude-code', 'personal')}.LOCK`, { recursive: true });
  mkdirSync(join(profileHome(fx.root, 'claude-code', 'work'), '..', '.hidden'), { recursive: true });
  mkdirSync(profileHome(fx.root, 'claude-code', 'locked-out'), { recursive: true });
  assert.deepEqual(profiles(fx.root, 'claude-code'), ['locked-out', 'personal', 'work']);
  fx.cleanup();
});

test('a profile name that would escape the harnesses directory is refused, not normalized', () => {
  for (const name of ['../escape', 'a/b', 'a\\b', '', '  ', '.', '..']) {
    const error = captureError(() => profileHome('/home', 'claude-code', name));
    assert.match(error.message, /profile name/);
  }
});

// ——— Twin rule 2: chosen, then the workspace's, then the machine's, then none.

test('a profile is the pick, then the workspace default, then the machine default', () => {
  const settings = {
    defaults: { 'claude-code': 'personal' },
    workspaces: { aurora: { 'claude-code': 'work' } },
    versions: {},
    workspaceVersions: {},
    rotation: {}, workspaceRotation: {}, rotationUse: {}, workspaceRotationUse: {}, rest: {},
  };

  assert.equal(resolveProfile(settings, 'claude-code', 'aurora', 'picked'), 'picked');
  assert.equal(resolveProfile(settings, 'claude-code', 'aurora', null), 'work');
  assert.equal(resolveProfile(settings, 'claude-code', 'tools', null), 'personal');
  assert.equal(resolveProfile(settings, 'claude-code', null, null), 'personal');
  assert.equal(resolveProfile(settings, 'codex', 'aurora', null), null);
});

// ——— Twin rule 3: none means the harness's OWN home. This is the one that must never regress.

test('no profile named anywhere means the harness’s own configuration home', () => {
  const empty = { defaults: {}, workspaces: {}, versions: {}, workspaceVersions: {}, rotation: {}, workspaceRotation: {}, rotationUse: {}, workspaceRotationUse: {}, rest: {} };

  // Null, not `"default"`: pointing someone who never asked for profiles at a fresh configuration
  // home would log them out of their own tool, which is the loudest way to break "Daoris works alone".
  assert.equal(resolveProfile(empty, 'claude-code', 'aurora', null), null);
});

// ——— Twin rule 4: the binary is the explicit command, then the managed pin, then PATH (TOOL2/D57).
//
// The fourth rule of the twin contract, and the one that carries D48 §2a: **absent means PATH**, byte
// for byte what a machine did before any of this existed. A machine that installed `claude` itself,
// and a contributor who never ran Daoris, both keep working.

test('a managed harness lives under a version of its own, and the name may not escape', () => {
  assert.equal(
    managedHome('/home', 'claude-code', '1.2.3'),
    join('/home', 'cache', 'toolchain', 'claude-code', '1.2.3'));

  // A version is a directory name like any other, so it takes the same refusal a profile does.
  for (const version of ['../escape', 'a/b', 'a\\b', '', '  ', '.', '..']) {
    assert.match(captureError(() => managedHome('/home', 'claude-code', version)).message, /version/);
  }
});

test('the pin is the pick, then the workspace default, then the machine default, then none', () => {
  const settings = {
    defaults: {}, workspaces: {}, rotation: {}, workspaceRotation: {}, rotationUse: {}, workspaceRotationUse: {}, rest: {},
    versions: { 'claude-code': '1.2.3' },
    workspaceVersions: { aurora: { 'claude-code': '2.0.0' } },
  };

  assert.equal(resolveVersion(settings, 'claude-code', 'aurora', '9.9.9'), '9.9.9');
  assert.equal(resolveVersion(settings, 'claude-code', 'aurora', null), '2.0.0');
  assert.equal(resolveVersion(settings, 'claude-code', 'tools', null), '1.2.3');
  assert.equal(resolveVersion(settings, 'claude-code', null, null), '1.2.3');
  // The same shape as a profile, including the answer when nothing names one.
  assert.equal(resolveVersion(settings, 'codex', 'aurora', null), null);
});

/**
 * CASEFOLD1c: a workspace's default and pin are found in any case, as the driver's `Choose` finds them in dictionaries that
 * ignore case (`OrdinalIgnoreCase`), and a workspace written twice in any case is read once, as first written, holding the
 * later's. A letter whose capital is two letters is still not those two.
 */
test('a workspace\'s default and pin are found in any case, as the driver finds them', () => {
  const fx = makeFixture('harness-workspace-case');
  writeFileSync(at(fx), JSON.stringify({
    defaults: { 'claude-code': 'personal' },
    workspaces: { aurora: { 'claude-code': 'work' }, AURORA: { 'claude-code': 'later' }, STRASSE: { 'claude-code': 'wide' } },
    versions: { 'claude-code': '1.2.3' },
    workspaceVersions: { Aurora: { 'claude-code': '2.0.0' } },
  }), 'utf8');
  const settings = readHarnessSettings(at(fx));

  assert.deepEqual(Object.keys(settings.workspaces), ['aurora', 'STRASSE']);
  assert.equal(resolveProfile(settings, 'claude-code', 'Aurora', null), 'later');
  assert.equal(resolveProfile(settings, 'claude-code', 'straße', null), 'personal');
  assert.equal(resolveVersion(settings, 'claude-code', 'aURORA', null), '2.0.0');

  // A later spelling naming none still replaces a workspace's defaults, and replaces nothing of its orders, as the
  // driver's `ReadCircles` and `ReadOrderCircles` set each.
  writeFileSync(at(fx), JSON.stringify({
    workspaces: { aurora: { 'claude-code': 'work' }, AURORA: {} },
    workspaceRotation: { aurora: { 'claude-code': ['work'] }, AURORA: { 'claude-code': [] } },
  }), 'utf8');
  const later = readHarnessSettings(at(fx));
  assert.deepEqual(later.workspaces, { aurora: {} });
  assert.deepEqual(later.workspaceRotation, { aurora: { 'claude-code': ['work'] } });
  fx.cleanup();
});

/**
 * 🔴 The rule that must never regress, and the twin of "no profile means the harness's own home".
 * Nothing pinned means `PATH` — not an empty managed directory, and not a refusal.
 */
test('no version pinned anywhere means whatever the machine has on PATH', () => {
  const empty = { defaults: {}, workspaces: {}, versions: {}, workspaceVersions: {}, rotation: {}, workspaceRotation: {}, rotationUse: {}, workspaceRotationUse: {}, rest: {} };

  assert.equal(resolveVersion(empty, 'claude-code', 'aurora', null), null);
  assert.equal(managedBinary('/home', 'claude-code', null, ['claude']), null);
});

test('a pinned harness resolves to the binary inside the directory Daoris owns', () => {
  const fx = makeFixture('harness-managed');
  const bin = join(managedHome(fx.root, 'claude-code', '1.2.3'), 'node_modules', '.bin');
  mkdirSync(bin, { recursive: true });
  // npm writes a shim per platform; the resolver takes whichever exists.
  const shim = join(bin, process.platform === 'win32' ? 'claude.cmd' : 'claude');
  writeFileSync(shim, '', 'utf8');

  assert.equal(managedBinary(fx.root, 'claude-code', '1.2.3', ['claude']), shim);
  fx.cleanup();
});

/**
 * A pin naming a version that was never installed is a fact the person must be told, not a silent
 * fall back to `PATH` — that would run a different tool than the one they asked for and say nothing.
 */
test('a pin whose directory is not there resolves to nothing rather than to PATH', () => {
  const fx = makeFixture('harness-managed-missing');
  assert.equal(managedBinary(fx.root, 'claude-code', '9.9.9', ['claude']), null);
  fx.cleanup();
});

test('the pin survives edits made by verbs that do not know it', () => {
  const fx = makeFixture('harness-pin-preserve');
  const settings = readHarnessSettings(at(fx));
  writeHarnessSettings(at(fx), { ...settings, versions: { 'claude-code': '1.2.3' } });

  mkdirSync(profileHome(fx.root, 'claude-code', 'work'), { recursive: true });
  defaulted(at(fx), 'claude-code', 'work', null);

  const after = readHarnessSettings(at(fx));
  assert.equal(after.versions['claude-code'], '1.2.3');
  assert.equal(after.defaults['claude-code'], 'work');
  fx.cleanup();
});

test('a missing file is a machine that named no profiles, and so is an unreadable one', () => {
  const fx = makeFixture('harness-missing');

  assert.deepEqual(readHarnessSettings(at(fx)).defaults, {});
  writeFileSync(at(fx), '{ not json at all', 'utf8');
  assert.deepEqual(readHarnessSettings(at(fx)).defaults, {});
  fx.cleanup();
});

test('an edit preserves everything in the file it did not touch', () => {
  const fx = makeFixture('harness-preserve');
  writeFileSync(at(fx), `${JSON.stringify({
    defaults: { 'claude-code': 'work' },
    somethingANewerBuildWrote: { keep: 'me' },
  }, null, 2)}\n`, 'utf8');

  const settings = readHarnessSettings(at(fx));
  writeHarnessSettings(at(fx), { ...settings, defaults: { ...settings.defaults, codex: 'personal' } });

  const back = JSON.parse(readFileSync(at(fx), 'utf8'));
  assert.deepEqual(back.somethingANewerBuildWrote, { keep: 'me' });
  assert.equal(back.defaults['claude-code'], 'work');
  assert.equal(back.defaults.codex, 'personal');
  fx.cleanup();
});

// ——— The verbs.

/**
 * 🔴 The twin's table (LEFT3 e): an account's default, set and cleared, as `HARNESS_ACTION`'s `profile-default` writes
 * it for the screen. `ProfileDefaultTwinTests` in the desktop modules holds the same rows with the same answers:
 * change one and the other changes with it. A clear removes the one entry, the machine's or one workspace's; a
 * workspace left with none is dropped; nothing else moves, and clearing what is not set changes nothing.
 */
const DEFAULT_EDITS: [why: string, before: Pick<HarnessSettings, 'defaults' | 'workspaces'>, owner: string,
  profile: string | null, workspace: string | null, after: Pick<HarnessSettings, 'defaults' | 'workspaces'>][] = [
  ['a machine default set', { defaults: {}, workspaces: {} }, 'claude-code', 'work', null,
    { defaults: { 'claude-code': 'work' }, workspaces: {} }],
  ['a machine default cleared, another agent\'s kept', { defaults: { 'claude-code': 'work', codex: 'play' }, workspaces: {} },
    'claude-code', null, null, { defaults: { codex: 'play' }, workspaces: {} }],
  ['a workspace default set, the machine\'s kept', { defaults: { 'claude-code': 'play' }, workspaces: {} },
    'claude-code', 'work', 'aurora', { defaults: { 'claude-code': 'play' }, workspaces: { aurora: { 'claude-code': 'work' } } }],
  ['a workspace default cleared, another agent\'s there kept',
    { defaults: {}, workspaces: { aurora: { 'claude-code': 'work', codex: 'play' } } }, 'claude-code', null, 'aurora',
    { defaults: {}, workspaces: { aurora: { codex: 'play' } } }],
  ['a workspace left with none is dropped, the machine\'s default kept',
    { defaults: { 'claude-code': 'play' }, workspaces: { aurora: { 'claude-code': 'work' }, lab: { codex: 'play' } } },
    'claude-code', null, 'aurora', { defaults: { 'claude-code': 'play' }, workspaces: { lab: { codex: 'play' } } }],
  ['clearing what is not set changes nothing', { defaults: { codex: 'play' }, workspaces: {} }, 'claude-code', null, 'aurora',
    { defaults: { codex: 'play' }, workspaces: {} }],
  // CASEFOLD1c: a workspace is edited in any case, under the spelling first written, as the driver's dictionaries hold one.
  ['a workspace default set in another case replaces the one there, as first written',
    { defaults: {}, workspaces: { aurora: { 'claude-code': 'work' } } }, 'claude-code', 'play', 'AURORA',
    { defaults: {}, workspaces: { aurora: { 'claude-code': 'play' } } }],
  ['a workspace default cleared in another case',
    { defaults: {}, workspaces: { aurora: { 'claude-code': 'work' }, lab: { codex: 'play' } } }, 'claude-code', null, 'Aurora',
    { defaults: {}, workspaces: { lab: { codex: 'play' } } }],
];

test('an account\'s default is set and cleared as the screen\'s own write sets and clears it (the twin\'s table)', () => {
  for (const [why, before, owner, profile, workspace, after] of DEFAULT_EDITS) {
    const edited = withDefault({ ...before, versions: {}, workspaceVersions: {}, rotation: {}, workspaceRotation: {}, rotationUse: {}, workspaceRotationUse: {}, rest: {} }, owner, profile, workspace);
    assert.deepEqual({ defaults: edited.defaults, workspaces: edited.workspaces }, after, why);
  }
});

/**
 * D50's two doors (LEFT3 e): the screen's *Make default* on the tool's own row clears an account's default, and until
 * now no terminal verb could. `--clear` writes what the screen writes: the machine's, or one workspace's, gone.
 */
/**
 * 🔴 Removing an account removes it (D66 §3, amending SES3's "deletes nothing"). The owner's report
 * was that Forget did not delete the account: the old rule kept any directory the harness would not
 * call signed out, so a removed account stayed listed and signed in. Now the directory goes with the
 * sign-in in it — read-only files too, which a tool that clones into its home leaves behind — and
 * every default naming it goes with it.
 */
test('a remove naming somewhere else is refused before anything is deleted', () => {
  const fx = makeFixture('harness-remove-escape');
  writeFileSync(join(fx.root, 'keep.txt'), 'mine', 'utf8');

  assert.match(captureError(() => removeProfile(fx.root, 'claude-code', '..')).message, /profile name/);
  assert.ok(existsSync(join(fx.root, 'keep.txt')));
  fx.cleanup();
});

// ——— Twin rule 6: an account that is an API key keeps its key in `keys.json` under the home
// (AGT3, D67 §1) — beside the account, never inside it — and shows it only by its last four.

// An account made of a key, and a new key into one, are the driver program's since PUSH1f2e5 (`daoris-driver agent key`): their cases
// are `agents-text.json`'s rows, and `ApiKeyAccountTests` and `KeyReplaceTests` hold `HarnessKeys`. What is left here reads a key, and
// removes one with its account; a key account is laid as the driver writes it.

/** A key account laid as the driver's `HarnessKeys.Add` writes it: its folder, and its key in `keys.json` beside it. */
function keyed(fx: { root: string }, agent: string, account: string, key: string): string {
  mkdirSync(profileHome(fx.root, agent, account), { recursive: true });
  const path = homeEntry(fx.root, 'keys');
  mkdirSync(dirname(path), { recursive: true });
  const held = existsSync(path) ? JSON.parse(readFileSync(path, 'utf8')) as Record<string, Record<string, string>> : {};
  writeFileSync(path, JSON.stringify({ ...held, [agent]: { ...held[agent], [account]: key } }), 'utf8');
  return account;
}

test('removing a key account removes its key, and another account keeps its own', () => {
  const fx = makeFixture('harness-key-remove');
  const first = keyed(fx, 'claude-code', 'acct-00000001', 'sk-first-1111');
  const second = keyed(fx, 'claude-code', 'acct-00000002', 'sk-second-2222');

  removeProfile(fx.root, 'claude-code', first);

  assert.equal(keyOf(fx.root, 'claude-code', first), null);
  assert.equal(keyOf(fx.root, 'claude-code', second), 'sk-second-2222');
  fx.cleanup();
});

test('a key account is asked with its key, and listed by its handle', () => {
  const fx = makeFixture('harness-key-probe');
  const script = join(fx.root, 'keyed.mjs');
  writeFileSync(script, [
    "if (process.argv[2] === '--version') { console.log('keyed 1.0'); process.exit(0); }",
    "console.log(JSON.stringify({ loggedIn: Boolean(process.env.ANTHROPIC_API_KEY), authMethod: 'api_key' }));",
  ].join('\n'), 'utf8');
  const account = keyed(fx, 'claude-code', 'acct-0000abcd', 'sk-probe-abcd');

  const report = probe('claude-code', {
    ...TOOLCHAINS['claude-code']!, binary: [process.execPath, script],
  }, fx.root, readHarnessSettings(at(fx)));

  assert.deepEqual(report.profiles.map((p) => [p.name, p.login, p.key]), [[account, 'in', '…abcd']]);
  fx.cleanup();
});

// ——— Twin rule 7: a door's accounts are its owner's (AGT7). `accountOf` says whose account a door
// runs as; its accounts, defaults and keys live under that name. Its pin stays its own.

// A key given for a door being its owner's is `agents-text.json`'s row since PUSH1f2e5.
test('a door lists its owner’s accounts, a key account among them', () => {
  const fx = makeFixture('harness-door-list');
  mkdirSync(profileHome(fx.root, 'claude-code', 'work'), { recursive: true });

  const account = keyed(fx, 'claude-code', 'acct-00009999', 'sk-door-9999');
  const report = probe('claude-code-acp', TOOLCHAINS['claude-code-acp']!, fx.root, readHarnessSettings(at(fx)));

  assert.deepEqual(report.profiles.map((p) => p.home).sort(),
    [profileHome(fx.root, 'claude-code', account), profileHome(fx.root, 'claude-code', 'work')].sort());
  fx.cleanup();
});

// ——— Twin rule 5: an account made by signing in, or by a key, takes a fresh id, never reused (D66 §3, ACCT2). It no
// longer takes the first free `account-N`: removing `account-2` left `account-1` and `account-3`, and the next account made
// would have taken a removed one's name, its readings and its usage.

test('a new account takes a fresh id and never a removed one\'s name', () => {
  const fx = makeFixture('harness-next');
  mkdirSync(profileHome(fx.root, 'claude-code', 'account-1'), { recursive: true });
  mkdirSync(profileHome(fx.root, 'claude-code', 'account-3'), { recursive: true });

  const made = nextAccount(fx.root, 'claude-code');

  assert.match(made, /^acct-[0-9a-f]{8}$/);
  assert.notEqual(made, 'account-2');
  assert.ok(!profiles(fx.root, 'claude-code').includes(made));
  fx.cleanup();
});

/**
 * A stand-in harness whose login question reads the home it is asked about: signed in exactly when
 * the home holds `credentials.json`, and it says WHO by that file's contents — the way `claude auth
 * status` names an email — beside an organisation, `org.txt`'s where the home has one, else `Org A`, and a plan (ACCOUNTID1).
 */
function fakeHarness(fx: { root: string }): Toolchain {
  const script = join(fx.root, 'fake-harness.mjs');
  writeFileSync(script, [
    "import { existsSync, readFileSync } from 'node:fs';",
    'const home = process.env.FAKE_HARNESS_HOME;',
    "if (process.argv[2] === '--version') { console.log('fake 1.0'); process.exit(0); }",
    "const signed = home && existsSync(home + '/credentials.json');",
    "const org = home && existsSync(home + '/org.txt') ? readFileSync(home + '/org.txt', 'utf8').trim() : 'Org A';",
    "console.log(JSON.stringify({ loggedIn: Boolean(signed), email: signed ? readFileSync(home + '/credentials.json', 'utf8').trim() : null, orgName: org, subscriptionType: 'max' }));",
  ].join('\n'), 'utf8');

  return {
    binary: [process.execPath, script],
    version: ['--version'],
    profileVariable: 'FAKE_HARNESS_HOME',
    login: ['login'],
    loginCheck: { ...TOOLCHAINS['claude-code']!.loginCheck!, args: ['status'] },
  };
}

// ——— ACCT2: an account's name, from the terminal. Its id stays, so every list, default, reading and record keeps it. How
// `agent list` says each account (ACCOUNTID1, ACCTNAME1, ACCT1, OWNSPACE1d) is `AgentsTextTests`' `list` rows since PUSH1f2e4.

test('a signed-out home names nobody, and a toolchain that does not ask keeps no name', () => {
  const fx = makeFixture('harness-who');
  const toolchain = fakeHarness(fx);
  mkdirSync(profileHome(fx.root, 'fake', 'out'), { recursive: true });
  mkdirSync(profileHome(fx.root, 'fake', 'in'), { recursive: true });
  writeFileSync(join(profileHome(fx.root, 'fake', 'in'), 'credentials.json'), 'someone@example.invalid', 'utf8');
  const settings = readHarnessSettings(at(fx));

  const asked = probe('fake', toolchain, fx.root, settings).profiles;
  assert.deepEqual(asked.map((p) => [p.name, p.login, p.account]), [
    ['in', 'in', 'someone@example.invalid'],
    ['out', 'out', null],
  ]);

  const { args, in: yes, out } = toolchain.loginCheck!;
  const silent = probe('fake', { ...toolchain, loginCheck: { args, in: yes, out } }, fx.root, settings).profiles;
  assert.deepEqual(silent.map((p) => p.account), [null, null]);
  fx.cleanup();
});

test('an unknown agent errors naming what exists, never a silent fallback', () => {
  const fx = makeFixture('harness-unknown');
  const error = captureError(() => run(['update', 'not-an-agent'], at(fx)));

  assert.match(error.message, /unknown agent 'not-an-agent'/);
  assert.match(error.message, /claude-code/);
  assert.match(error.message, /never guessed/);
  fx.cleanup();
});

/**
 * `pin` and `unpin` (TOOL2/D57). The install itself spawns npm and is not run here — what IS run is
 * every judgement around it: what gets refused, what gets written, and what a pin does to `list`.
 */
test('pin needs a version, and refuses one that would escape the toolchain directory', () => {
  const fx = makeFixture('harness-pin-args');

  assert.match(captureError(() => run(['pin', 'claude-code'], at(fx))).message, /version/);
  assert.match(captureError(() => run(['pin', 'claude-code', '../x'], at(fx))).message, /version/);
  fx.cleanup();
});

test('pin refuses a harness Daoris has no package for, rather than guessing one', () => {
  const fx = makeFixture('harness-pin-unknown');
  assert.match(captureError(() => run(['pin', 'nonesuch', '1.0.0'], at(fx))).message, /nonesuch/);
  fx.cleanup();
});

// ——— `agent list` (its pins, its next start, each account held now; TOOL2, TOOL6f, CODEXUSE1b) is the driver's program's since
// PUSH1f2e4: `AgentsTextTests`' `list` rows hold what these tests held, run once through this verb before it retired.

test('an unknown verb names the ones that exist', () => {
  const fx = makeFixture('harness-verb');
  const error = captureError(() => run(['frobnicate'], at(fx)));

  assert.match(error.message, /unknown agent verb 'frobnicate'/);
  assert.match(error.message, /list, install, update, login, key, pin, unpin, profile/);
  fx.cleanup();
});

/**
 * The declared mechanisms are claims about somebody else's program, so they are pinned here: a
 * silent edit to one of them is a command that fails in a person's terminal saying something untrue.
 * Each was verified against the real binary before it was written.
 */
test('every harness declares a real mechanism for each thing Daoris offers to do', () => {
  for (const [name, toolchain] of Object.entries(TOOLCHAINS)) {
    assert.ok(toolchain.binary.length > 0, `${name} has no binary`);
    assert.ok(toolchain.version.length > 0, `${name} cannot be asked its version`);
    // The environment seam is what a credential profile IS. A harness without one cannot have them.
    assert.ok(toolchain.profileVariable.length > 0, `${name} has no configuration-home variable`);
    // Install is a WHOLE command, because a machine without the harness cannot run the harness.
    assert.ok(toolchain.install && toolchain.install[0] !== toolchain.binary[0], `${name}'s installer needs itself`);
    // 🔴 A harness answers "is this profile logged in?" itself, NAMES the harness whose account it
    // runs as, or DECLARES that it has no account at all. Silence is none of the three: an
    // unanswerable login question is permissive (SES3), so a harness that simply omitted the check
    // would quietly widen what may spawn.
    //
    // The third case arrived with dsh (ACP3) and is deliberately a declaration rather than an
    // inference. "Has no account" and "nobody wrote the check yet" are indistinguishable from
    // outside, and they must not be: one is a fact about the harness, the other is an omission.
    assert.ok(
      toolchain.loginCheck || toolchain.accountOf || toolchain.noAccount,
      `${name} neither asks whether a profile is logged in, nor names whose account it uses, `
      + `nor declares that it has none`);
    assert.ok(
      !(toolchain.noAccount && (toolchain.loginCheck || toolchain.accountOf || toolchain.login)),
      `${name} declares it has no account and then describes one`);
    if (toolchain.accountOf) {
      assert.ok(TOOLCHAINS[toolchain.accountOf], `${name} names an account holder that does not exist`);
      assert.equal(
        toolchain.profileVariable, TOOLCHAINS[toolchain.accountOf]!.profileVariable,
        `${name} borrows an account through a different seam than the harness that owns it`);
    }
  }

  assert.equal(TOOLCHAINS['claude-code']!.profileVariable, 'CLAUDE_CONFIG_DIR');
  assert.equal(TOOLCHAINS.codex!.profileVariable, 'CODEX_HOME');
  // The protocol door borrows the pipe door's account, through the same seam (ACP2/ACP3).
  assert.equal(TOOLCHAINS['claude-code-acp']!.accountOf, 'claude-code');
  assert.equal(TOOLCHAINS['codex-acp']!.accountOf, 'codex');
  assert.equal(TOOLCHAINS.dsh!.profileVariable, 'DSH_HOME');

});

/**
 * 🔴 This table and the driver's `AdapterSet` are TWINS, and the twin risk is not membership — the
 * two sets differ on purpose, because managing a tool and spawning sessions on it are different
 * questions (D23). It is a shared NAME whose descriptors disagree: the CLI probing one binary while
 * the driver spawns another is a `harness list` reporting on a program nothing runs, and it reads as
 * correct from both sides.
 *
 * The binaries are pinned here and in `Acp3AdapterTests` rather than compared across the language
 * boundary — the FILE is the contract, the same shape the remotes map's three twins use.
 *
 * 🔴 **And the table below covers every entry**, which it did not when it was written. It pinned the
 * three ACP arrivals and left `claude-code` — the oldest entry, and what a machine actually drives
 * with — asserted on neither side. Nothing was wrong with it; nothing would have said so either.
 * Proving one row's reach proves nothing about the next one, so membership is derived.
 */
const TWINS: Record<string, { binary: string[]; profileVariable: string; product: string; maker: string }> = {
  'claude-code': { binary: ['claude'], profileVariable: 'CLAUDE_CONFIG_DIR', product: 'Claude Code', maker: 'Anthropic' },
  'claude-code-acp': { binary: ['claude-agent-acp'], profileVariable: 'CLAUDE_CONFIG_DIR', product: 'Claude Code', maker: 'Anthropic' },
  codex: { binary: ['codex'], profileVariable: 'CODEX_HOME', product: 'Codex', maker: 'OpenAI' },
  'codex-acp': { binary: ['codex-acp'], profileVariable: 'CODEX_HOME', product: 'Codex', maker: 'OpenAI' },
  // AGENTS2: the name its maker publishes it under, since `dsh` beside DeepSeek still read as no DeepSeek agent.
  dsh: { binary: ['dsh'], profileVariable: 'DSH_HOME', product: 'DeepSeek Harness', maker: 'DeepSeek' },
};

test('every declared harness is pinned by name, binary, seam, and what a person calls it', () => {
  for (const [name, toolchain] of Object.entries(TOOLCHAINS)) {
    const twin = TWINS[name];
    assert.ok(twin,
      `${name} is declared and pinned by no twin row — so the driver's AdapterSet could describe a `
      + 'different program under the same name and both sides would read as correct');
    assert.deepEqual(toolchain.binary, twin.binary, `${name}: the binary moved`);
    assert.equal(toolchain.profileVariable, twin.profileVariable, `${name}: the account seam moved`);
    // AGT1: the tool a person reads, and whose it is — the same on the screen and in a terminal.
    assert.equal(toolchain.product, twin.product, `${name}: the product moved`);
    assert.equal(toolchain.maker, twin.maker, `${name}: the maker moved`);
  }

  // The protocol door borrows the pipe door's account, through the same seam (ACP2/ACP3).
  assert.equal(TOOLCHAINS['claude-code-acp']!.accountOf, 'claude-code');
  assert.equal(TOOLCHAINS['codex-acp']!.accountOf, 'codex');

  // The trust record (DEPLOY1, D73) is the driver's alone since `agent trust` went to it (PUSH1f2d): `HarnessToolchain.TrustFile`.
  // Whose accounts say how much of each window is used is the driver's alone since `agent list` went to it (PUSH1f2e4): `Speaks`.
});

/**
 * Both supported harnesses exit 0 whether or not they are logged in, so the OUTPUT is the answer.
 * These patterns are what the real binaries actually print.
 */
test('the login questions read the answers the real harnesses give', () => {
  const claude = TOOLCHAINS['claude-code']!.loginCheck!;
  assert.ok(claude.in.test('{\n  "loggedIn": true,\n  "authMethod": "claude.ai"\n}'));
  assert.ok(claude.out.test('{\n  "loggedIn": false,\n  "authMethod": "none"\n}'));
  assert.ok(!claude.in.test('{\n  "loggedIn": false\n}'));
  // Who signed in (D66 §3) — the email, from the keys measured on the real binary; values invented.
  assert.equal(
    claude.account!.exec('{\n  "loggedIn": true,\n  "email": "someone@example.invalid",\n  "orgName": "x"\n}')?.[1],
    'someone@example.invalid');
  // ACCOUNTID1: the organisation and the plan beside who, which tell two sign-ins of one email apart; none where it says none.
  const both = '{\n  "loggedIn": true,\n  "email": "person@example.invalid",\n  "orgName": "Org A",\n  "subscriptionType": "max"\n}';
  assert.deepEqual([claude.org?.exec(both)?.[1], claude.plan?.exec(both)?.[1]], ['Org A', 'max']);
  assert.equal(claude.org?.exec('{ "loggedIn": true, "orgName": null }') ?? null, null);

  const codex = TOOLCHAINS.codex!.loginCheck!;
  assert.ok(codex.in.test('Logged in using ChatGPT'));
  assert.ok(codex.out.test('Not logged in'));
  // The one that matters. This harness answers a SENTENCE rather than a field, and "Not logged in"
  // contains "logged in" — an unanchored pattern reported every logged-out profile as logged in.
  assert.ok(!codex.in.test('Not logged in'));
  // And a warning line before the answer must not shift the verdict either.
  assert.ok(!codex.in.test('WARNING: something\nNot logged in'));
  assert.ok(codex.in.test('WARNING: something\nLogged in using ChatGPT'));
});

/**
 * 🔴 **The probe must ask about the binary a spawn would actually run.**
 *
 * Rule 4 (the binary: explicit command → managed pin → `PATH`) was implemented where a session is
 * spawned and *not* where presence is decided, so `harness list` answered "absent — not on this
 * machine's PATH" about a harness whose pin was installed, working, and printed on the very next
 * line. The driver then refused to spawn on that answer, and ACP2's driven run died on it.
 *
 * The test installs a working shim under the managed layout and puts nothing on `PATH`. A probe that
 * resolves the pin finds it; a probe that asks `PATH` cannot.
 */
test('a pinned harness probes as present, on the pin rather than on PATH', () => {
  const fx = makeFixture('harness-probe-pin');
  const home = join(fx.root, 'home');
  const bin = join(managedHome(home, 'claude-code', '1.2.3'), 'node_modules', '.bin');
  mkdirSync(bin, { recursive: true });

  // A shim that really runs: this is also the `.cmd` case, which Node will not spawn without a shell
  // (CVE-2024-27980) — the second half of the same Windows trap that broke `harness install`.
  const windows = process.platform === 'win32';
  const shim = join(bin, windows ? 'claude.cmd' : 'claude');
  writeFileSync(shim, windows ? '@echo 9.9.9-pinned\r\n' : '#!/bin/sh\necho 9.9.9-pinned\n', 'utf8');
  if (!windows) chmodSync(shim, 0o755);

  const settings = {
    defaults: {}, workspaces: {}, versions: { 'claude-code': '1.2.3' }, workspaceVersions: {}, rotation: {}, workspaceRotation: {}, rotationUse: {}, workspaceRotationUse: {}, rest: {},
  };
  const report = probe('claude-code', TOOLCHAINS['claude-code']!, home, settings);

  assert.equal(report.present, true, report.problem ?? 'probed as absent');
  assert.match(report.version ?? '', /9\.9\.9-pinned/);
  fx.cleanup();
});

/**
 * 🔴 A pin stays the version pinned (AGT2): a pinned Claude Code reported its own auto-updates
 * enabled, so the pinned binary is asked with the tool's own switch — and the machine's own binary,
 * off `PATH`, is asked exactly as before. The driver's `PinnedUpdatesTests` is the twin.
 */
test('a pinned binary is asked with its updates off, and the machine’s own is not', () => {
  const fx = makeFixture('harness-probe-pin-updates');
  const home = join(fx.root, 'home');
  const bin = join(managedHome(home, 'claude-code', '1.2.3'), 'node_modules', '.bin');
  mkdirSync(bin, { recursive: true });
  const windows = process.platform === 'win32';
  writeFileSync(
    join(bin, windows ? 'claude.cmd' : 'claude'),
    windows ? '@echo updates:%DISABLE_UPDATES%\r\n' : '#!/bin/sh\necho "updates:$DISABLE_UPDATES"\n',
    'utf8');
  if (!windows) chmodSync(join(bin, 'claude'), 0o755);

  assert.deepEqual(TOOLCHAINS['claude-code']!.pinnedEnv, { DISABLE_UPDATES: '1' });

  const settings = {
    defaults: {}, workspaces: {}, versions: { 'claude-code': '1.2.3' }, workspaceVersions: {}, rotation: {}, workspaceRotation: {}, rotationUse: {}, workspaceRotationUse: {}, rest: {},
  };
  assert.match(probe('claude-code', TOOLCHAINS['claude-code']!, home, settings).version ?? '', /updates:1/);

  // Unpinned, the same toolchain asks whatever is on PATH, and hands it nothing new.
  const script = join(fx.root, 'machine.mjs');
  writeFileSync(script, "console.log('updates:' + (process.env.DISABLE_UPDATES ?? 'on'));\n", 'utf8');
  const machine = probe('fake', {
    ...TOOLCHAINS['claude-code']!, binary: [process.execPath, script],
  }, home, { ...settings, versions: {} });
  assert.match(machine.version ?? '', /updates:on/);
  fx.cleanup();
});

/**
 * The other side of the same rule: a pin that names a version nothing is installed at must NOT quietly
 * fall back to `PATH`. The driver already refuses this by name; the probe has to agree, or the roster
 * would show the machine's own `claude` and call it the pinned one.
 */
test('a pin with nothing installed at it probes as absent rather than as PATH', () => {
  const fx = makeFixture('harness-probe-pin-missing');
  const settings = {
    defaults: {}, workspaces: {}, versions: { 'claude-code': '9.9.9' }, workspaceVersions: {}, rotation: {}, workspaceRotation: {}, rotationUse: {}, workspaceRotationUse: {}, rest: {},
  };

  const report = probe('claude-code', TOOLCHAINS['claude-code']!, join(fx.root, 'home'), settings);

  assert.equal(report.present, false);
  assert.match(report.problem ?? '', /9\.9\.9/);
  fx.cleanup();
});

// ——— CODEXACCT2, CODEXACCT1b, CODEXLOGIN1: Codex signs in by its browser first, its device code beside it, on the binary
// `agent list` asks.

/**
 * 🔴 CODEXLOGIN1 (D125's note): a ChatGPT workspace may refuse device codes, so the browser sign-in comes first, as ChatGPT's
 * desktop app signs in. Measured with 0.160.0 on 2026-10-21: `codex login` waits for the browser's answer on 127.0.0.1:1455,
 * else 1457 where 1455 is held; `codex login --device-auth` prints a link and a one-time code and needs no port (CODEXACCT2,
 * which took it alone when a reserved range covered the port, os error 10013). The driver's `CodexAccounts` reads this entry
 * and holds itself to it.
 */
test('Codex signs in by its browser first, on the ports it tries, and by its device code beside it (CODEXLOGIN1)', () => {
  assert.deepEqual(TOOLCHAINS.codex!.login, ['login']);
  assert.deepEqual(TOOLCHAINS.codex!.deviceLogin, ['login', '--device-auth']);
  assert.deepEqual(TOOLCHAINS.codex!.loginPorts, [1455, 1457]);
  // Its status question is unchanged: the sign-in's end still asks `codex login status`.
  assert.deepEqual(TOOLCHAINS.codex!.loginCheck!.args, ['login', 'status']);
  // Claude Code's own flow is its one way, and waits on no port Daoris tries.
  assert.equal(TOOLCHAINS['claude-code']!.deviceLogin, undefined);
  assert.equal(TOOLCHAINS['claude-code']!.loginPorts, undefined);
});

// ——— AGT2b: the two makers that publish their own channel are pinned from it; npm stays for the rest.

test('Claude Code and Codex pin from their makers’ own channels, and everything else from npm', () => {
  assert.equal(TOOLCHAINS['claude-code']!.channel, 'claude-code-releases');
  assert.equal(TOOLCHAINS.codex!.channel, 'codex-releases');
  // One source per pin: a harness declaring both would leave "which one did it come from" open.
  for (const [name, toolchain] of Object.entries(TOOLCHAINS)) {
    assert.ok(Boolean(toolchain.channel) !== Boolean(toolchain.package),
      `${name} must pin from exactly one source — a channel or a package`);
  }
  // The doors and dsh ship only on npm.
  for (const name of ['claude-code-acp', 'codex-acp', 'dsh']) assert.ok(TOOLCHAINS[name]!.package, name);
});

/**
 * A vendor's install lands as `<version>/bin/<binary>` — the package's own layout for Codex, and the
 * same shape for Claude Code's one file. npm's layout still resolves, because a pin made before this
 * change is still an install somebody made; the vendor's is asked first.
 */
test('a managed install in the vendor’s layout resolves, ahead of npm’s', () => {
  const fx = makeFixture('harness-managed-vendor');
  const managed = managedHome(fx.root, 'codex', '0.156.1');
  const windows = process.platform === 'win32';
  mkdirSync(join(managed, 'bin'), { recursive: true });
  mkdirSync(join(managed, 'node_modules', '.bin'), { recursive: true });
  const vendor = join(managed, 'bin', windows ? 'codex.exe' : 'codex');
  writeFileSync(vendor, '', 'utf8');
  writeFileSync(join(managed, 'node_modules', '.bin', windows ? 'codex.cmd' : 'codex'), '', 'utf8');

  assert.equal(managedBinary(fx.root, 'codex', '0.156.1', ['codex']), vendor);
  fx.cleanup();
});

test('pin refuses a Claude Code from before signed manifests, before anything is fetched', () => {
  const fx = makeFixture('harness-pin-unsigned');
  const error = captureError(() => run(['pin', 'claude-code', '2.1.87'], at(fx)));

  assert.match(error.message, /2\.1\.89/);
  assert.equal(readHarnessSettings(at(fx)).versions['claude-code'], undefined);
  fx.cleanup();
});

test("a flag before the operands is not an operand: `pin --workspace aurora claude-code 2.1.87`", () => {
  // REV3 CLI F14: the version was read as "whatever sits at index 2", which was `aurora`.
  const fx = makeFixture('harness-pin-flag-first');
  const error = captureError(() => run(['pin', '--workspace', 'aurora', 'claude-code', '2.1.87'], at(fx)));

  assert.match(error.message, /2\.1\.89/, error.message);
  assert.equal(/aurora/.test(error.message), false, error.message);
  fx.cleanup();
});

/**
 * The whole verb over a fetcher the test holds: fetch, verify, unpack, and only then pin. `npm` is the
 * package manager's stand-in, for the verbs that run one.
 */
async function runPin(
  argv: string[], path: string, fetcher: Fetcher, npm?: string[],
): Promise<{ code: number; out: string }> {
  const saved = process.env.DAORIS_HARNESS_CONFIG;
  process.env.DAORIS_HARNESS_CONFIG = path;
  const lines: string[] = [];
  try {
    const code = await commandHarness({
      root: process.cwd(), argv, write: (line) => lines.push(line), packageRoot: process.cwd(),
    }, fetcher, npm);
    return { code, out: lines.join('\n') };
  } finally {
    if (saved === undefined) delete process.env.DAORIS_HARNESS_CONFIG;
    else process.env.DAORIS_HARNESS_CONFIG = saved;
  }
}

/** A Codex release for THIS machine, in the vendor's metadata shape, served from a table. */
function codexServed(version: string, pkg: Buffer): { fetcher: Fetcher; asked: string[] } {
  const target = codexTarget();
  const name = `codex-package-${target}.tar.gz`;
  const hash = (bytes: Buffer) => createHash('sha256').update(bytes).digest('hex');
  const sums = Buffer.from(`${hash(pkg)}  ${name}\n`);
  const base = `${CODEX_RELEASES}/${version}`;
  const files: Record<string, Buffer> = {
    [`${base}/release.json`]: Buffer.from(JSON.stringify({
      tag_name: `rust-v${version}`,
      assets: [
        { name, digest: `sha256:${hash(pkg)}`, browser_download_url: `${base}/${name}` },
        { name: 'codex-package_SHA256SUMS', digest: `sha256:${hash(sums)}`, browser_download_url: `${base}/codex-package_SHA256SUMS` },
      ],
    })),
    [`${base}/${name}`]: pkg,
    [`${base}/codex-package_SHA256SUMS`]: sums,
  };
  const asked: string[] = [];
  return {
    asked,
    fetcher: {
      async bytes(url) {
        asked.push(url);
        return files[url] ?? null;
      },
      async save(url, to) {
        asked.push(url);
        const bytes = files[url];
        if (!bytes) return null;
        writeFileSync(to, bytes);
        return { sha256: hash(bytes), size: bytes.length };
      },
    },
  };
}

function codexPackage(): Buffer {
  const exe = process.platform === 'win32' ? '.exe' : '';
  return gzipSync(Buffer.concat([
    tarEntry('codex-package.json', '{}'), tarEntry(`bin/codex${exe}`, 'codex', '0', 0o755), TAR_END,
  ]));
}

test('pin fetches from the channel, verifies, installs, and only then pins', async () => {
  const fx = makeFixture('harness-pin-channel');
  const { fetcher, asked } = codexServed('0.156.1', codexPackage());

  const result = await runPin(['pin', 'codex', '0.156.1'], at(fx), fetcher);

  assert.equal(result.code, 0, result.out);
  assert.equal(readHarnessSettings(at(fx)).versions.codex, '0.156.1');
  assert.ok(managedBinary(fx.root, 'codex', '0.156.1', ['codex']), 'the pin points at nothing');
  assert.equal(asked.length, 3);
  assert.match(result.out, /OpenAI's own release channel/);
  fx.cleanup();
});

test('a channel install that does not verify pins nothing, and says so', async () => {
  const fx = makeFixture('harness-pin-channel-refused');
  const served = codexServed('0.156.1', codexPackage());
  const lying: Fetcher = {
    bytes: served.fetcher.bytes,
    // The right metadata, and the wrong bytes where the package should be.
    async save(url, to) {
      writeFileSync(to, 'not the package');
      return url ? { sha256: createHash('sha256').update('not the package').digest('hex'), size: 15 } : null;
    },
  };

  await assert.rejects(runPin(['pin', 'codex', '0.156.1'], at(fx), lying), /Nothing was pinned/);
  assert.equal(readHarnessSettings(at(fx)).versions.codex, undefined);
  assert.equal(managedBinary(fx.root, 'codex', '0.156.1', ['codex']), null);
  fx.cleanup();
});

test('re-pinning a version already installed downloads nothing', async () => {
  const fx = makeFixture('harness-pin-channel-present');
  const managed = managedHome(fx.root, 'codex', '0.156.1');
  mkdirSync(join(managed, 'bin'), { recursive: true });
  writeFileSync(join(managed, 'bin', process.platform === 'win32' ? 'codex.exe' : 'codex'), '', 'utf8');
  const { fetcher, asked } = codexServed('0.156.1', codexPackage());

  const result = await runPin(['pin', 'codex', '0.156.1'], at(fx), fetcher);

  assert.equal(result.code, 0);
  assert.deepEqual(asked, []);
  assert.match(result.out, /nothing was downloaded/);
  assert.equal(readHarnessSettings(at(fx)).versions.codex, '0.156.1');
  fx.cleanup();
});

// ——— USE1a: `agent update` does what it says. A pinned door with a package or a channel MOVES ITS PIN
// to the newest release; an unpinned door with its own updater runs it; anything else is refused. The
// desktop's `HarnessActions.UpdateAsync` is the twin, held by the same cases.

/** A fetcher that fails the test if anything asks it: an npm door's update reaches no channel. */
const NOWHERE: Fetcher = {
  async bytes(url) { throw new Error(`nothing should have fetched ${url}`); },
  async save(url) { throw new Error(`nothing should have fetched ${url}`); },
};

/**
 * npm's stand-in: `view <package> version` runs the body given, and `install --prefix <dir> <spec>`
 * lays down the adapter's shim where npm's would land. Every call is logged, in order.
 */
function standInNpm(fx: { root: string }, view: string): { npm: string[]; asked: () => string[] } {
  const script = join(fx.root, 'npm-stand-in.mjs');
  const log = join(fx.root, 'npm-asked.log');
  writeFileSync(script, [
    "import { appendFileSync, mkdirSync, writeFileSync } from 'node:fs';",
    "import { join } from 'node:path';",
    'const [verb, ...rest] = process.argv.slice(2);',
    `appendFileSync(${JSON.stringify(log)}, [verb, ...rest].join(' ') + '\\n');`,
    `if (verb === 'view') { ${view} }`,
    "if (verb === 'install') {",
    "  const bin = join(rest[rest.indexOf('--prefix') + 1], 'node_modules', '.bin');",
    '  mkdirSync(bin, { recursive: true });',
    "  writeFileSync(join(bin, 'claude-agent-acp'), '');",
    '}',
  ].join('\n'), 'utf8');
  return {
    npm: [process.execPath, script],
    asked: () => (existsSync(log) ? readFileSync(log, 'utf8').split('\n').filter(Boolean) : []),
  };
}

/** Pin a door at a version and put an install there, in npm's layout. */
function pinnedAt(fx: { root: string }, name: string, version: string, workspace?: string): void {
  const settings = readHarnessSettings(at(fx));
  writeHarnessSettings(at(fx), workspace
    ? { ...settings, workspaceVersions: { ...settings.workspaceVersions, [workspace]: { [name]: version } } }
    : { ...settings, versions: { ...settings.versions, [name]: version } });
  const bin = join(managedHome(fx.root, name, version), 'node_modules', '.bin');
  mkdirSync(bin, { recursive: true });
  writeFileSync(join(bin, TOOLCHAINS[name]!.binary[0]!), '', 'utf8');
}

const ACP_PACKAGE = '@agentclientprotocol/claude-agent-acp';

/**
 * Run with nothing on PATH. 🔴 Measured while proving these tests fail: a regression that fell back to
 * the tool's own updater ran this machine's real `claude update` and `codex update`. With no PATH it
 * fails the test instead, and touches nothing.
 */
async function pathless<T>(fx: { root: string }, fn: () => Promise<T>): Promise<T> {
  const saved = process.env.PATH;
  process.env.PATH = join(fx.root, 'nothing-on-path');
  try {
    return await fn();
  } finally {
    process.env.PATH = saved;
  }
}

test('update moves a pinned npm door to the newest release, and says from what to what', async () => {
  const fx = makeFixture('harness-update-npm');
  pinnedAt(fx, 'claude-code-acp', '0.79.0');
  const { npm, asked } = standInNpm(fx, "console.log('0.84.0');");

  const result = await runPin(['update', 'claude-code-acp'], at(fx), NOWHERE, npm);

  assert.equal(result.code, 0, result.out);
  assert.match(result.out, /0\.79\.0 → 0\.84\.0/);
  assert.equal(readHarnessSettings(at(fx)).versions['claude-code-acp'], '0.84.0');
  assert.ok(managedBinary(fx.root, 'claude-code-acp', '0.84.0', ['claude-agent-acp']), 'the pin points at nothing');
  // Resolved first, then installed at the CONCRETE version — never `@latest`.
  assert.deepEqual(asked(), [
    `view ${ACP_PACKAGE} version`,
    `install --prefix ${managedHome(fx.root, 'claude-code-acp', '0.84.0')} ${ACP_PACKAGE}@0.84.0`,
  ]);
  fx.cleanup();
});

test('update of a pin that is already the newest fetches nothing and moves nothing', async () => {
  const fx = makeFixture('harness-update-npm-newest');
  pinnedAt(fx, 'claude-code-acp', '0.79.0');
  const { npm, asked } = standInNpm(fx, "console.log('0.79.0');");

  const result = await runPin(['update', 'claude-code-acp'], at(fx), NOWHERE, npm);

  assert.equal(result.code, 0, result.out);
  assert.match(result.out, /already the newest/);
  assert.deepEqual(asked(), [`view ${ACP_PACKAGE} version`]);
  assert.equal(readHarnessSettings(at(fx)).versions['claude-code-acp'], '0.79.0');
  fx.cleanup();
});

test('update never moves a pin backwards, to a newest release older than it', async () => {
  const fx = makeFixture('harness-update-npm-ahead');
  pinnedAt(fx, 'claude-code-acp', '0.85.0');
  const { npm, asked } = standInNpm(fx, "console.log('0.84.0');");

  const result = await runPin(['update', 'claude-code-acp'], at(fx), NOWHERE, npm);

  assert.equal(result.code, 0, result.out);
  assert.match(result.out, /newer than/);
  assert.equal(asked().length, 1);
  assert.equal(readHarnessSettings(at(fx)).versions['claude-code-acp'], '0.85.0');
  fx.cleanup();
});

/** npm that fails, or answers something that is not one version, is a sentence — never a stack trace. */
test('update that cannot learn the newest version says so, and the pin stays', async () => {
  for (const view of [
    "console.error('npm error code E404'); process.exit(1);",
    "console.log('{ weird: true }');",
    "console.log('0.83.0'); console.log('0.84.0');",
  ]) {
    const fx = makeFixture('harness-update-npm-fails');
    pinnedAt(fx, 'claude-code-acp', '0.79.0');
    const { npm, asked } = standInNpm(fx, view);

    await assert.rejects(
      runPin(['update', 'claude-code-acp'], at(fx), NOWHERE, npm),
      (error: Error) => /Nothing was fetched or pinned/.test(error.message) && /0\.79\.0/.test(error.message)
        && !/\n\s+at /.test(error.message));
    assert.deepEqual(asked(), [`view ${ACP_PACKAGE} version`]);
    assert.equal(readHarnessSettings(at(fx)).versions['claude-code-acp'], '0.79.0');
    fx.cleanup();
  }
});

test('update moves the pin it names: a workspace’s, leaving the machine’s alone', async () => {
  const fx = makeFixture('harness-update-npm-workspace');
  pinnedAt(fx, 'claude-code-acp', '0.70.0');
  pinnedAt(fx, 'claude-code-acp', '0.79.0', 'aurora');
  const { npm } = standInNpm(fx, "console.log('0.84.0');");

  const result = await runPin(['update', 'claude-code-acp', '--workspace', 'aurora'], at(fx), NOWHERE, npm);

  assert.equal(result.code, 0, result.out);
  assert.equal(readHarnessSettings(at(fx)).workspaceVersions['aurora']!['claude-code-acp'], '0.84.0');
  assert.equal(readHarnessSettings(at(fx)).versions['claude-code-acp'], '0.70.0');

  // A circle that pins nothing has no pin to move, and is told how to set one.
  await assert.rejects(
    runPin(['update', 'claude-code-acp', '--workspace', 'lab'], at(fx), NOWHERE, npm),
    /pins no version[\s\S]*agent pin claude-code-acp/);
  fx.cleanup();
});

test('update moves a pinned channel door to the version its channel names newest', async () => {
  const fx = makeFixture('harness-update-channel');
  const old = managedHome(fx.root, 'codex', '0.150.0');
  mkdirSync(join(old, 'bin'), { recursive: true });
  writeFileSync(join(old, 'bin', process.platform === 'win32' ? 'codex.exe' : 'codex'), '', 'utf8');
  writeHarnessSettings(at(fx), { ...readHarnessSettings(at(fx)), versions: { codex: '0.150.0' } });
  const served = codexServed('0.156.1', codexPackage());
  const fetcher: Fetcher = {
    async bytes(url) {
      if (url === CODEX_LATEST) return Buffer.from(JSON.stringify({ tag_name: 'rust-v0.156.1' }));
      return served.fetcher.bytes(url);
    },
    save: served.fetcher.save,
  };

  const result = await pathless(fx, () => runPin(['update', 'codex'], at(fx), fetcher));

  assert.equal(result.code, 0, result.out);
  assert.match(result.out, /0\.150\.0 → 0\.156\.1/);
  assert.equal(readHarnessSettings(at(fx)).versions.codex, '0.156.1');
  assert.ok(managedBinary(fx.root, 'codex', '0.156.1', ['codex']), 'the pin points at nothing');
  fx.cleanup();
});

test('update of a channel pin already at the newest asks the pointer and nothing else', async () => {
  const fx = makeFixture('harness-update-channel-newest');
  const pinned = managedHome(fx.root, 'claude-code', '2.1.281');
  mkdirSync(join(pinned, 'bin'), { recursive: true });
  writeFileSync(join(pinned, 'bin', process.platform === 'win32' ? 'claude.exe' : 'claude'), '', 'utf8');
  writeHarnessSettings(at(fx), { ...readHarnessSettings(at(fx)), versions: { 'claude-code': '2.1.281' } });
  const asked: string[] = [];
  const fetcher: Fetcher = {
    async bytes(url) {
      asked.push(url);
      return url === CLAUDE_LATEST ? Buffer.from('2.1.281\n') : null;
    },
    async save(url) {
      asked.push(url);
      return null;
    },
  };

  const result = await pathless(fx, () => runPin(['update', 'claude-code'], at(fx), fetcher));

  assert.equal(result.code, 0, result.out);
  assert.match(result.out, /already the newest/);
  assert.deepEqual(asked, [CLAUDE_LATEST]);
  fx.cleanup();
});

/** Unpinned, a door with an updater of its own runs it — the tool's own mechanism, as before. */
test('update of an unpinned door runs the tool’s own updater', async () => {
  const fx = makeFixture('harness-update-tool');
  const script = join(fx.root, 'own-updater.mjs');
  const marker = join(fx.root, 'updated.txt');
  writeFileSync(script, `import { writeFileSync } from 'node:fs';\nwriteFileSync(${JSON.stringify(marker)}, process.argv.slice(2).join(' '));\n`, 'utf8');
  const codex = TOOLCHAINS.codex!;
  const binary = codex.binary;
  codex.binary = [process.execPath, script];
  try {
    const result = await runPin(['update', 'codex'], at(fx), NOWHERE);

    assert.equal(result.code, 0, result.out);
    assert.equal(readFileSync(marker, 'utf8'), 'update');
  } finally {
    codex.binary = binary;
  }
  fx.cleanup();
});

test('update of an unpinned door with no updater of its own is refused, as before', () => {
  const fx = makeFixture('harness-update-neither');
  assert.match(captureError(() => run(['update', 'claude-code-acp'], at(fx))).message, /declares no updater/);
  fx.cleanup();
});

/** What `npm view <package> version` answers, read defensively. The driver's `VersionFromNpm` is the twin. */
test('npm’s answer is one version or none', () => {
  assert.equal(versionFromNpm('0.84.0\n'), '0.84.0');
  assert.equal(versionFromNpm('npm warn config production Use `--omit=dev` instead.\n0.84.0\n'), '0.84.0');
  assert.equal(versionFromNpm("'0.84.0'\n"), '0.84.0');
  assert.equal(versionFromNpm('1.0.0-beta.2\n'), '1.0.0-beta.2');
  assert.equal(versionFromNpm(''), null);
  assert.equal(versionFromNpm('{ weird: true }'), null);
  assert.equal(versionFromNpm('0.83.0\n0.84.0\n'), null);
  assert.equal(versionFromNpm('latest'), null);
});
