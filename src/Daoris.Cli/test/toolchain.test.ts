import { test } from 'node:test';
import assert from 'node:assert/strict';
import { chmodSync, existsSync, mkdirSync, readdirSync, readFileSync, writeFileSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { join } from 'node:path';
import { gzipSync } from 'node:zlib';
import {
  TOOLCHAINS, accountLines, addKeyAccount, commandHarness, harnessesPath, joinsOf, keyOf, managedBinary, managedHome,
  nextAccount, profileHome, profiles, probe, readHarnessSettings, removeProfile, resolveProfile,
  resolveVersion, signInNew, signInTo, versionFromNpm, withDefault, writeHarnessSettings,
} from '../src/toolchain.ts';
import type { HarnessSettings, Toolchain } from '../src/toolchain.ts';
import { CLAUDE_LATEST, CODEX_LATEST, CODEX_RELEASES, codexTarget } from '../src/channels.ts';
import type { Fetcher } from '../src/channels.ts';
import { COOLING_FILE, coolingWhen, machineZone } from '../src/cooling.ts';
import { WINDOWS_FILE } from '../src/windows.ts';
import { makeFixture } from './_fixture.ts';
import { captureError } from './_fixture.ts';
import { TAR_END, tarEntry } from './_tar.ts';

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
  return join(fx.root, 'harnesses.json');
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
  assert.equal(harnessesPath({ DAORIS_HOME: '/x/data' }), join('/x/data', 'harnesses.json'));
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
    join('/home', 'toolchain', 'claude-code', '1.2.3'));

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

  run(['unpin', 'claude-code', '--workspace', 'AURORA'], at(fx));
  assert.deepEqual(readHarnessSettings(at(fx)).workspaceVersions, {});

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

  run(['profile', 'add', 'claude-code', 'work'], at(fx));
  run(['profile', 'default', 'claude-code', 'work'], at(fx));

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

test('`profile add` creates the directory and says it is empty until you log in', () => {
  const fx = makeFixture('harness-add');
  const result = run(['profile', 'add', 'claude-code', 'work'], at(fx));

  assert.equal(result.code, 0);
  assert.ok(existsSync(profileHome(fx.root, 'claude-code', 'work')));
  assert.match(result.out, /empty until you log into it/);
  assert.match(result.out, /daoris agent login claude-code --profile work/);
  fx.cleanup();
});

test('`profile default` refuses a profile that does not exist, and names the ones that do', () => {
  const fx = makeFixture('harness-default-unknown');
  run(['profile', 'add', 'claude-code', 'work'], at(fx));

  const error = captureError(() => run(['profile', 'default', 'claude-code', 'typo'], at(fx)));

  // A default nobody can resolve would make every spawn in that circle refuse with a message about
  // logging in — which is the wrong sentence for a typo.
  assert.match(error.message, /has no profile `typo`/);
  assert.match(error.message, /work/);
  fx.cleanup();
});

test('`profile default` sets the machine’s, and `--workspace` sets one circle’s', () => {
  const fx = makeFixture('harness-default');
  run(['profile', 'add', 'claude-code', 'personal'], at(fx));
  run(['profile', 'add', 'claude-code', 'work'], at(fx));

  run(['profile', 'default', 'claude-code', 'personal'], at(fx));
  const scoped = run(['profile', 'default', 'claude-code', 'work', '--workspace', 'aurora'], at(fx));

  assert.match(scoped.out, /sessions in `aurora` run `claude-code` as `work`/);
  const settings = readHarnessSettings(at(fx));
  assert.equal(resolveProfile(settings, 'claude-code', 'aurora', null), 'work');
  assert.equal(resolveProfile(settings, 'claude-code', 'tools', null), 'personal');
  fx.cleanup();
});

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
test('`profile default --clear` clears the machine’s default, or one workspace’s, and says what sessions run as now', () => {
  const fx = makeFixture('harness-default-clear');
  run(['profile', 'add', 'claude-code', 'personal'], at(fx));
  run(['profile', 'add', 'claude-code', 'work'], at(fx));
  run(['profile', 'default', 'claude-code', 'personal'], at(fx));
  run(['profile', 'default', 'claude-code', 'work', '--workspace', 'aurora'], at(fx));

  const scoped = run(['profile', 'default', 'claude-code', '--clear', '--workspace', 'aurora'], at(fx));

  assert.equal(scoped.code, 0);
  assert.match(scoped.out, /`aurora` names no account for `claude-code` now: its sessions run as the machine's default, `personal`/);
  assert.equal(resolveProfile(readHarnessSettings(at(fx)), 'claude-code', 'aurora', null), 'personal');
  assert.deepEqual(readHarnessSettings(at(fx)).workspaces, {});

  const machine = run(['profile', 'default', 'claude-code', '--clear'], at(fx));

  assert.match(machine.out, /this machine runs `claude-code` in its own configuration home again/);
  assert.equal(resolveProfile(readHarnessSettings(at(fx)), 'claude-code', 'aurora', null), null);
  // The accounts stay: a default cleared deletes nothing.
  assert.deepEqual(profiles(fx.root, 'claude-code'), ['personal', 'work']);
  fx.cleanup();
});

test('`profile default --clear` on a door clears its owner’s, and refuses a profile named beside it', () => {
  const fx = makeFixture('harness-default-clear-door');
  run(['profile', 'add', 'claude-code', 'work'], at(fx));
  run(['profile', 'default', 'claude-code', 'work'], at(fx));

  const cleared = run(['profile', 'default', 'claude-code-acp', '--clear'], at(fx));

  assert.match(cleared.out, /runs as `claude-code`/);
  assert.equal(readHarnessSettings(at(fx)).defaults['claude-code'], undefined);
  const error = captureError(() => run(['profile', 'default', 'claude-code', 'work', '--clear'], at(fx)));
  assert.match(error.message, /`--clear` names no account/);
  fx.cleanup();
});

/**
 * 🔴 Removing an account removes it (D66 §3, amending SES3's "deletes nothing"). The owner's report
 * was that Forget did not delete the account: the old rule kept any directory the harness would not
 * call signed out, so a removed account stayed listed and signed in. Now the directory goes with the
 * sign-in in it — read-only files too, which a tool that clones into its home leaves behind — and
 * every default naming it goes with it.
 */
test('`profile remove` deletes the account, sign-in and all, and un-defaults it everywhere', () => {
  const fx = makeFixture('harness-remove');
  run(['profile', 'add', 'dsh', 'work'], at(fx));
  run(['profile', 'default', 'dsh', 'work'], at(fx));
  run(['profile', 'default', 'dsh', 'work', '--workspace', 'aurora'], at(fx));
  writeFileSync(join(profileHome(fx.root, 'dsh', 'work'), 'credentials.json'), '{}', 'utf8');
  const cloned = join(profileHome(fx.root, 'dsh', 'work'), 'plugins', '.git');
  mkdirSync(cloned, { recursive: true });
  writeFileSync(join(cloned, 'pack.idx'), 'x', 'utf8');
  chmodSync(join(cloned, 'pack.idx'), 0o444);

  const result = run(['profile', 'remove', 'dsh', 'work'], at(fx));

  assert.equal(result.code, 0);
  assert.match(result.out, /the sign-in in it are gone/);
  assert.ok(!existsSync(profileHome(fx.root, 'dsh', 'work')));
  const settings = readHarnessSettings(at(fx));
  assert.equal(resolveProfile(settings, 'dsh', 'aurora', null), null);
  assert.equal(resolveProfile(settings, 'dsh', null, null), null);
  fx.cleanup();
});

test('`profile remove` of an account that is not there is an answer, not a failure', () => {
  const fx = makeFixture('harness-remove-absent');

  const result = run(['profile', 'remove', 'claude-code', 'nobody'], at(fx));

  assert.equal(result.code, 0);
  assert.match(result.out, /nothing to remove/);
  fx.cleanup();
});

test('a remove naming somewhere else is refused before anything is deleted', () => {
  const fx = makeFixture('harness-remove-escape');
  writeFileSync(join(fx.root, 'keep.txt'), 'mine', 'utf8');

  assert.match(captureError(() => removeProfile(fx.root, 'claude-code', '..')).message, /profile name/);
  assert.ok(existsSync(join(fx.root, 'keep.txt')));
  fx.cleanup();
});

// ——— Twin rule 6: an account that is an API key keeps its key in `keys.json` under the home
// (AGT3, D67 §1) — beside the account, never inside it — and shows it only by its last four.

test('an API key makes an account whose key is kept beside it, in the file both twins read', () => {
  const fx = makeFixture('harness-key');
  const lines: string[] = [];

  const account = addKeyAccount(fx.root, 'claude-code', 'sk-ant-api03-cli-test-wxyz\n', (l) => lines.push(l));

  assert.match(account, /^acct-[0-9a-f]{8}$/);
  assert.deepEqual(readdirSync(profileHome(fx.root, 'claude-code', account)), []);
  // The file's shape is the twin contract: the driver's HarnessKeys reads exactly this.
  assert.deepEqual(JSON.parse(readFileSync(join(fx.root, 'keys.json'), 'utf8')),
    { 'claude-code': { [account]: 'sk-ant-api03-cli-test-wxyz' } });
  assert.equal(keyOf(fx.root, 'claude-code', account), 'sk-ant-api03-cli-test-wxyz');
  // ACCT2: its id is never what a person has to read; the end offers a name of theirs.
  assert.match(lines.join('\n'), new RegExp(`daoris agent profile rename claude-code ${account} <name>`));
  // Said back by its handle only.
  assert.match(lines.join('\n'), /…wxyz/);
  assert.doesNotMatch(lines.join('\n'), /sk-ant-api03-cli-test/);
  fx.cleanup();
});

/** 🔴 A key typed as an argument is in the shell's history: refused, saying so, and nothing made. */
test('`agent key` refuses a key on the command line', () => {
  const fx = makeFixture('harness-key-argv');

  const error = captureError(() => run(['key', 'claude-code', 'sk-ant-api03-in-history-0000'], at(fx)));

  assert.match(error.message, /stdin, not on the command line/);
  assert.doesNotMatch(error.message, /in-history/);
  assert.deepEqual(profiles(fx.root, 'claude-code'), []);
  fx.cleanup();
});

test('a blank key is refused and makes nothing; an agent with no key variable takes none', () => {
  const fx = makeFixture('harness-key-refused');

  assert.match(captureError(() => addKeyAccount(fx.root, 'claude-code', '  \n', () => {})).message, /not an API key/);
  assert.match(captureError(() => addKeyAccount(fx.root, 'dsh', 'sk-x-1234', () => {})).message, /takes no API key/);
  assert.deepEqual(profiles(fx.root, 'claude-code'), []);
  assert.deepEqual(profiles(fx.root, 'dsh'), []);
  fx.cleanup();
});

test('removing a key account removes its key, and another account keeps its own', () => {
  const fx = makeFixture('harness-key-remove');
  const first = addKeyAccount(fx.root, 'claude-code', 'sk-first-1111', () => {});
  const second = addKeyAccount(fx.root, 'claude-code', 'sk-second-2222', () => {});

  run(['profile', 'remove', 'claude-code', first], at(fx));

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
  const account = addKeyAccount(fx.root, 'claude-code', 'sk-probe-abcd', () => {});

  const report = probe('claude-code', {
    ...TOOLCHAINS['claude-code']!, binary: [process.execPath, script],
  }, fx.root, readHarnessSettings(at(fx)));

  assert.deepEqual(report.profiles.map((p) => [p.name, p.login, p.key]), [[account, 'in', '…abcd']]);
  fx.cleanup();
});

// ——— Twin rule 7: a door's accounts are its owner's (AGT7). `accountOf` says whose account a door
// runs as; its accounts, defaults and keys live under that name. Its pin stays its own.

test('an account verb on a door acts on its owner’s accounts, and says so', () => {
  const fx = makeFixture('harness-door');

  const added = run(['profile', 'add', 'claude-code-acp', 'work'], at(fx));
  run(['profile', 'default', 'claude-code-acp', 'work'], at(fx));

  assert.ok(existsSync(profileHome(fx.root, 'claude-code', 'work')));
  assert.ok(!existsSync(join(fx.root, 'harnesses', 'claude-code-acp')));
  assert.equal(readHarnessSettings(at(fx)).defaults['claude-code'], 'work');
  assert.equal(readHarnessSettings(at(fx)).defaults['claude-code-acp'], undefined);
  assert.match(added.out, /runs as `claude-code`/);
  fx.cleanup();
});

test('a door lists its owner’s accounts, and a key given for it is the owner’s', () => {
  const fx = makeFixture('harness-door-list');
  mkdirSync(profileHome(fx.root, 'claude-code', 'work'), { recursive: true });

  const account = addKeyAccount(fx.root, 'claude-code-acp', 'sk-door-9999', () => {});
  const report = probe('claude-code-acp', TOOLCHAINS['claude-code-acp']!, fx.root, readHarnessSettings(at(fx)));

  assert.equal(keyOf(fx.root, 'claude-code', account), 'sk-door-9999');
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
 * status` names an email beside an organisation Daoris must not keep.
 */
function fakeHarness(fx: { root: string }): Toolchain {
  const script = join(fx.root, 'fake-harness.mjs');
  writeFileSync(script, [
    "import { existsSync, readFileSync } from 'node:fs';",
    'const home = process.env.FAKE_HARNESS_HOME;',
    "if (process.argv[2] === '--version') { console.log('fake 1.0'); process.exit(0); }",
    "const signed = home && existsSync(home + '/credentials.json');",
    "console.log(JSON.stringify({ loggedIn: Boolean(signed), email: signed ? readFileSync(home + '/credentials.json', 'utf8').trim() : null, orgName: 'Secretive' }));",
  ].join('\n'), 'utf8');

  return {
    binary: [process.execPath, script],
    version: ['--version'],
    profileVariable: 'FAKE_HARNESS_HOME',
    login: ['login'],
    loginCheck: { ...TOOLCHAINS['claude-code']!.loginCheck!, args: ['status'] },
  };
}

/**
 * 🔴 An account is made by signing in (D66 §3) — the terminal's half of the desktop's *Add an account…* (D50). The
 * sign-in runs into a fresh id (ACCT2), and the end names who signed in by the tool's own answer, keeping neither the
 * organisation nor anything else it said — and offers it as the account's name, written only by the person's rename.
 */
test('`login --new` keeps a finished sign-in, names who signed in, and offers it as the name, writing none', () => {
  const fx = makeFixture('harness-sign-in');
  const toolchain = fakeHarness(fx);
  const lines: string[] = [];

  const code = signInNew('fake', toolchain, fx.root, (where) => {
    writeFileSync(join(where, 'credentials.json'), 'someone@example.invalid', 'utf8');
    return 0;
  }, (line) => lines.push(line));

  assert.equal(code, 0);
  const [made] = profiles(fx.root, 'fake');
  assert.match(made!, /^acct-[0-9a-f]{8}$/);
  assert.ok(existsSync(join(profileHome(fx.root, 'fake', made!), 'credentials.json')));
  assert.match(lines.join('\n'), /signed in as someone@example\.invalid/);
  assert.match(lines.join('\n'), new RegExp(`daoris agent profile rename fake ${made} someone@example\\.invalid`));
  assert.doesNotMatch(lines.join('\n'), /Secretive/);
  // D66 §3: who signed in is offered, never written without the person.
  assert.ok(!existsSync(join(fx.root, 'accounts.json')));

  const listed = probe('fake', toolchain, fx.root, readHarnessSettings(at(fx)));
  assert.equal(listed.profiles[0]!.account, 'someone@example.invalid');
  fx.cleanup();
});

/**
 * ACCTQUOTE1: who signed in is offered as a command the person pastes, so it is spelled for a shell — and where no spelling
 * holds in every shell, the command names a placeholder and the sentence says who.
 */
test('`login --new` offers who signed in as a command every shell reads as written', () => {
  const fx = makeFixture('harness-sign-in-spelled');
  const toolchain = fakeHarness(fx);
  const said = (who: string): string => {
    const lines: string[] = [];
    signInNew('fake', toolchain, fx.root, (where) => {
      writeFileSync(join(where, 'credentials.json'), who, 'utf8');
      return 0;
    }, (line) => lines.push(line));
    return lines.join('\n');
  };

  assert.match(said("o'brien@example.invalid"), /`daoris agent profile rename fake acct-[0-9a-f]{8} "o'brien@example\.invalid"` names it o'brien@/);
  assert.match(said('r&d@example.invalid'), /`daoris agent profile rename fake acct-[0-9a-f]{8} <name>` names it r&d@example\.invalid;/);
  fx.cleanup();
});

/**
 * ACCT1, ACCT2: a new account named as it is made, and put into the lists the person named in the same step; the end says
 * where it runs. A list that cannot be joined is refused before anything starts, so it costs nothing.
 */
test('`login --new --name --join` names the new account, joins the lists named, and says where it runs', () => {
  const fx = makeFixture('harness-sign-in-join');
  const toolchain = fakeHarness(fx);
  mkdirSync(profileHome(fx.root, 'fake', 'account-1'), { recursive: true });
  writeFileSync(at(fx), JSON.stringify({ rotation: { fake: ['account-1'] }, workspaceRotation: { work: { fake: ['account-1'] } } }), 'utf8');
  const lines: string[] = [];

  const code = signInNew('fake', toolchain, fx.root, (where) => {
    writeFileSync(join(where, 'credentials.json'), 'spare@example.invalid', 'utf8');
    return 0;
  }, (line) => lines.push(line), { name: 'spare', joins: ['work', null], path: at(fx) });

  assert.equal(code, 0);
  const made = profiles(fx.root, 'fake').find((name) => name !== 'account-1')!;
  const settings = readHarnessSettings(at(fx));
  assert.deepEqual(settings.workspaceRotation.work!.fake, ['account-1', made]);
  assert.deepEqual(settings.rotation.fake, ['account-1', made]);
  assert.deepEqual(JSON.parse(readFileSync(join(fx.root, 'accounts.json'), 'utf8')), { fake: { [made]: { name: 'spare' } } });
  assert.match(lines.join('\n'), /this machine lists it as `spare`/);
  assert.match(lines.join('\n'), /`spare` runs work in this machine's list, `work`'s list/);
  assert.doesNotMatch(lines.join('\n'), /profile rename/);

  // A workspace that takes this machine's list cannot be joined: refused before anything starts, nothing made.
  const before = profiles(fx.root, 'fake');
  const refused = captureError(() => signInNew('fake', toolchain, fx.root, () => 0, () => {}, { joins: ['forge'], path: at(fx) }));
  assert.match(refused.message, /`forge` names no `fake` account or list of its own/);
  assert.deepEqual(profiles(fx.root, 'fake'), before);
  // So is a name another account has.
  const taken = captureError(() => signInNew('fake', toolchain, fx.root, () => 0, () => {}, { name: 'SPARE', path: at(fx) }));
  assert.match(taken.message, /already names/);
  assert.deepEqual(profiles(fx.root, 'fake'), before);
  fx.cleanup();
});

/** ACCT1: a new account that joins nothing says it runs nowhere, the lists there are, and the command that joins one. */
test('a new account in no list says no start runs on it, and names the lists and the join', () => {
  const fx = makeFixture('harness-sign-in-nowhere');
  const toolchain = fakeHarness(fx);
  mkdirSync(profileHome(fx.root, 'fake', 'account-1'), { recursive: true });
  writeFileSync(at(fx), JSON.stringify({ workspaceRotation: { work: { fake: ['account-1'] } } }), 'utf8');
  const lines: string[] = [];

  signInNew('fake', toolchain, fx.root, (where) => {
    writeFileSync(join(where, 'credentials.json'), 'spare@example.invalid', 'utf8');
    return 0;
  }, (line) => lines.push(line), { path: at(fx) });

  const said = lines.join('\n');
  assert.match(said, /no list and no default holds `acct-[0-9a-f]{8}`, so no start runs on it/);
  assert.match(said, /The lists here: `work`'s own \(account-1\)/);
  assert.match(said, /daoris agent profile join fake acct-[0-9a-f]{8} <workspace>…\|--machine/);
  fx.cleanup();
});

/**
 * 🔴 ACCT1: signing back in to an account that is here signs in there — by its id or its name, else the machine's default
 * — never a new folder. The install's owner signed in to bring a signed-out account back and got a new account no list
 * held.
 */
test('`login --profile` signs in to the account that is here, by its id or its name, never a new folder', () => {
  const fx = makeFixture('harness-sign-in-existing');
  const toolchain = fakeHarness(fx);
  mkdirSync(profileHome(fx.root, 'fake', 'account-1'), { recursive: true });
  mkdirSync(profileHome(fx.root, 'fake', 'account-2'), { recursive: true });
  writeFileSync(join(fx.root, 'accounts.json'), JSON.stringify({ fake: { 'account-2': { name: 'seat' } } }), 'utf8');
  writeFileSync(at(fx), JSON.stringify({ defaults: { fake: 'account-1' }, rotation: { fake: ['account-1', 'account-2'] } }), 'utf8');
  const reached: string[] = [];
  const signIn = (where: string) => {
    reached.push(where);
    writeFileSync(join(where, 'credentials.json'), 'back@example.invalid', 'utf8');
    return 0 as const;
  };

  const lines: string[] = [];
  assert.equal(signInTo('fake', toolchain, fx.root, 'seat', signIn, (line) => lines.push(line), { path: at(fx) }), 0);
  assert.equal(signInTo('fake', toolchain, fx.root, 'account-2', signIn, () => {}, { path: at(fx) }), 0);
  assert.equal(signInTo('fake', toolchain, fx.root, null, signIn, () => {}, { path: at(fx) }), 0);

  assert.deepEqual(reached, [
    profileHome(fx.root, 'fake', 'account-2'), profileHome(fx.root, 'fake', 'account-2'), profileHome(fx.root, 'fake', 'account-1'),
  ]);
  assert.deepEqual(profiles(fx.root, 'fake'), ['account-1', 'account-2']);
  assert.match(lines.join('\n'), /into the account `seat`/);
  assert.match(lines.join('\n'), /`seat` runs work in this machine's list/);

  // An account that is not here is refused before the sign-in runs, and no folder is made.
  const refused = captureError(() => signInTo('fake', toolchain, fx.root, 'account-3', signIn, () => {}, { path: at(fx) }));
  assert.match(refused.message, /no account `account-3` on this machine, so nothing was signed in and no account was made/);
  assert.match(refused.message, /account-1, seat \(account-2\)/);
  assert.equal(reached.length, 3);
  assert.ok(!existsSync(profileHome(fx.root, 'fake', 'account-3')));

  // None named and no default is refused too, naming the new account's door.
  writeFileSync(at(fx), '{}', 'utf8');
  const none = captureError(() => signInTo('fake', toolchain, fx.root, null, signIn, () => {}, { path: at(fx) }));
  assert.match(none.message, /name the `fake` account to sign in to/);
  assert.match(none.message, /daoris agent login fake --new/);
  fx.cleanup();
});

/** ACCT1, ACCT2: an account with no name signed back in is offered who signed in as one; one with a name is not. */
test('signing back in to an account with no name offers who signed in as its name, writing none', () => {
  const fx = makeFixture('harness-sign-in-offer');
  const toolchain = fakeHarness(fx);
  mkdirSync(profileHome(fx.root, 'fake', 'account-1'), { recursive: true });
  const lines: string[] = [];

  signInTo('fake', toolchain, fx.root, 'account-1', (where) => {
    writeFileSync(join(where, 'credentials.json'), 'back@example.invalid', 'utf8');
    return 0;
  }, (line) => lines.push(line), { path: at(fx) });

  assert.match(lines.join('\n'), /daoris agent profile rename fake account-1 back@example\.invalid/);
  assert.match(lines.join('\n'), /no list and no default holds `account-1`/);
  assert.ok(!existsSync(join(fx.root, 'accounts.json')));
  fx.cleanup();
});

test('`--join` names workspaces, repeated or comma-separated, and `--join-machine` this machine\'s list', () => {
  assert.deepEqual(joinsOf(['login', 'claude-code', '--new', '--join', 'work,lab', '--join', ' tools ', '--join-machine']),
    ['work', 'lab', 'tools', null]);
  assert.deepEqual(joinsOf(['login', 'claude-code', '--join', 'work', '--join', 'work']), ['work']);
  assert.deepEqual(joinsOf(['login', 'claude-code']), []);
  assert.match(captureError(() => joinsOf(['login', 'claude-code', '--join'])).message, /`--join` needs a workspace/);
  assert.match(captureError(() => joinsOf(['login', 'claude-code', '--join', '--new'])).message, /`--join` needs a workspace/);
});

test('`login` refuses `--new` beside `--profile`, and `--name` without `--new`', () => {
  const fx = makeFixture('harness-login-args');

  assert.match(captureError(() => run(['login', 'claude-code', '--new', '--profile', 'work'], at(fx))).message,
    /`--new` signs in to a new account and `--profile` to one that is here/);
  assert.match(captureError(() => run(['login', 'claude-code', '--name', 'work'], at(fx))).message,
    /`--name` names a new account as it is made/);
  // An account that is not here is refused before any login flow runs.
  assert.match(captureError(() => run(['login', 'claude-code', '--profile', 'nobody'], at(fx))).message,
    /no account `nobody` on this machine/);
  assert.deepEqual(profiles(fx.root, 'claude-code'), []);
  fx.cleanup();
});

// ——— ACCT2: an account's name, from the terminal. Its id stays, so every list, default, reading and record keeps it.

test('`profile rename` names an account, and its lists, defaults, readings and records keep it by its id', () => {
  const fx = makeFixture('harness-rename');
  run(['profile', 'add', 'claude-code', 'account-1'], at(fx));
  run(['profile', 'add', 'claude-code', 'account-2'], at(fx));
  run(['profile', 'order', 'claude-code', 'account-1', 'account-2'], at(fx));
  run(['profile', 'default', 'claude-code', 'account-1'], at(fx));
  run(['profile', 'order', 'claude-code', 'account-2', '--workspace', 'work'], at(fx));
  writeFileSync(join(fx.root, COOLING_FILE), JSON.stringify({ 'claude-code': { 'account-1': { until: '2999-01-01T00:00:00Z' } } }), 'utf8');
  const wiring = readFileSync(at(fx), 'utf8');

  const renamed = run(['profile', 'rename', 'claude-code', 'account-1', 'you@work.example'], at(fx));

  assert.equal(renamed.code, 0);
  assert.match(renamed.out, /`claude-code`'s account `account-1` is called `you@work.example` now/);
  // The wiring names the id, and is not touched: the rotation, the default and the workspace's list keep working.
  assert.equal(readFileSync(at(fx), 'utf8'), wiring);
  // A verb takes the account by its new name, and writes its id.
  run(['profile', 'order', 'claude-code', 'account-2', 'YOU@work.example', '--workspace', 'work'], at(fx));
  assert.deepEqual(readHarnessSettings(at(fx)).workspaceRotation.work!['claude-code'], ['account-2', 'account-1']);
  const ready = run(['profile', 'ready', 'claude-code', 'you@work.example'], at(fx));
  assert.match(ready.out, /is offered again/);
  // `agent list` says the name, the id beside it, and the list by names.
  const lines = accountLines('claude-code', TOOLCHAINS['claude-code']!, {
    harness: 'claude-code', present: true, version: '1', problem: null, machineDefault: 'account-1',
    profiles: [
      { name: 'account-1', home: '', login: 'in', account: null, key: null },
      { name: 'account-2', home: '', login: 'out', account: null, key: null },
    ],
  }, readHarnessSettings(at(fx)), fx.root, new Date(), 'UTC').join('\n');
  assert.match(lines, /you@work\.example +in +\(id account-1, machine default\)/);
  assert.match(lines, /rotation +you@work\.example, then account-2/);

  // Its own id gives it none again; another account's id or name is refused.
  assert.match(captureError(() => run(['profile', 'rename', 'claude-code', 'account-2', 'Account-1'], at(fx))).message,
    /already names `account-1`/);
  const cleared = run(['profile', 'rename', 'claude-code', 'you@work.example', 'account-1'], at(fx));
  assert.match(cleared.out, /has no name of yours now/);
  assert.deepEqual(JSON.parse(readFileSync(join(fx.root, 'accounts.json'), 'utf8')), {});
  fx.cleanup();
});

test('`profile remove` by an account\'s name removes it and forgets its name', () => {
  const fx = makeFixture('harness-rename-remove');
  run(['profile', 'add', 'claude-code', 'account-1'], at(fx));
  run(['profile', 'rename', 'claude-code', 'account-1', 'seat'], at(fx));

  const removed = run(['profile', 'remove', 'claude-code', 'seat'], at(fx));

  assert.match(removed.out, /the sign-in in it are gone/);
  assert.deepEqual(profiles(fx.root, 'claude-code'), []);
  assert.deepEqual(JSON.parse(readFileSync(join(fx.root, 'accounts.json'), 'utf8')), {});
  fx.cleanup();
});

// ——— ACCT1: an account put into lists from the terminal, and one in none said where it is listed.

test('`profile join` puts an account into the lists named and says where it runs; a borrowed workspace is refused', () => {
  const fx = makeFixture('harness-join');
  run(['profile', 'add', 'claude-code', 'account-1'], at(fx));
  run(['profile', 'add', 'claude-code', 'account-2'], at(fx));
  run(['profile', 'default', 'claude-code', 'account-1', '--workspace', 'work'], at(fx));

  const joined = run(['profile', 'join', 'claude-code', 'account-2', 'work', '--machine'], at(fx));

  assert.equal(joined.code, 0);
  const settings = readHarnessSettings(at(fx));
  assert.deepEqual(settings.rotation['claude-code'], ['account-2']);
  assert.deepEqual(settings.workspaceRotation.work!['claude-code'], ['account-1', 'account-2']);
  assert.match(joined.out, /runs work in this machine's list, `work`'s list/);

  assert.match(captureError(() => run(['profile', 'join', 'claude-code', 'account-2', 'forge'], at(fx))).message,
    /`forge` names no `claude-code` account or list of its own/);
  assert.match(captureError(() => run(['profile', 'join', 'claude-code', 'account-9', 'work'], at(fx))).message,
    /no account `account-9`/);
  assert.match(captureError(() => run(['profile', 'join', 'claude-code', 'account-2'], at(fx))).message,
    /needs the lists/);
  fx.cleanup();
});

test('`agent list` says an account no list and no default holds runs nowhere, and how to put it in one', () => {
  const fx = makeFixture('harness-list-nowhere');
  run(['profile', 'add', 'claude-code', 'account-1'], at(fx));
  run(['profile', 'add', 'claude-code', 'account-2'], at(fx));
  run(['profile', 'order', 'claude-code', 'account-1'], at(fx));

  const lines = accountLines('claude-code', TOOLCHAINS['claude-code']!, {
    harness: 'claude-code', present: true, version: '1', problem: null, machineDefault: null,
    profiles: [
      { name: 'account-1', home: '', login: 'in', account: null, key: null },
      { name: 'account-2', home: '', login: 'in', account: null, key: null },
    ],
  }, readHarnessSettings(at(fx)), fx.root, new Date(), 'UTC');

  const nowhere = lines.filter((line) => /no workspace: no list and no default holds it/.test(line));
  assert.equal(nowhere.length, 1);
  assert.match(nowhere[0]!, /daoris agent profile join claude-code account-2 <workspace>…\|--machine/);
  fx.cleanup();
});

/** A sign-in that did not finish — failed, or ended signed out — leaves nothing behind. */
test('`login --new` that does not finish leaves nothing behind', () => {
  const fx = makeFixture('harness-sign-in-unfinished');
  const toolchain = fakeHarness(fx);

  for (const outcome of [2, 0] as const) {
    const lines: string[] = [];
    const code = signInNew('fake', toolchain, fx.root, () => outcome, (line) => lines.push(line));

    assert.equal(code, outcome);
    assert.deepEqual(profiles(fx.root, 'fake'), []);
    assert.match(lines.join('\n'), /nothing was kept/);
  }
  fx.cleanup();
});

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
  const error = captureError(() => run(['install', 'not-an-agent'], at(fx)));

  assert.match(error.message, /unknown agent 'not-an-agent'/);
  assert.match(error.message, /claude-code/);
  assert.match(error.message, /never guessed/);
  fx.cleanup();
});

/**
 * `pin` and `unpin` (TOOL2/D57). The install itself spawns npm and is not run here — what IS run is
 * every judgement around it: what gets refused, what gets written, and what a pin does to `list`.
 */
test('unpin takes the pin off and leaves the installed directory alone', () => {
  const fx = makeFixture('harness-unpin');
  const settings = readHarnessSettings(at(fx));
  writeHarnessSettings(at(fx), {
    ...settings,
    versions: { 'claude-code': '1.2.3' },
    workspaceVersions: { aurora: { 'claude-code': '2.0.0' } },
  });
  const installed = managedHome(fx.root, 'claude-code', '1.2.3');
  mkdirSync(installed, { recursive: true });

  const machine = run(['unpin', 'claude-code'], at(fx));
  assert.equal(readHarnessSettings(at(fx)).versions['claude-code'], undefined);
  // The circle's pin is its own choice and is not collateral.
  assert.equal(readHarnessSettings(at(fx)).workspaceVersions['aurora']!['claude-code'], '2.0.0');
  // Nothing is deleted — the same rule `profile remove` follows, for the same reason.
  assert.ok(existsSync(installed));
  assert.match(machine.out, /PATH/);

  run(['unpin', 'claude-code', '--workspace', 'aurora'], at(fx));
  assert.deepEqual(readHarnessSettings(at(fx)).workspaceVersions, {});
  fx.cleanup();
});

test('unpinning what was never pinned is an answer, not a failure', () => {
  const fx = makeFixture('harness-unpin-absent');
  assert.equal(run(['unpin', 'claude-code'], at(fx)).code, 0);
  fx.cleanup();
});

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

test('list says which version is pinned, and whether it is actually installed', () => {
  const fx = makeFixture('harness-pin-list');
  const settings = readHarnessSettings(at(fx));
  writeHarnessSettings(at(fx), { ...settings, versions: { 'claude-code': '9.9.9' } });

  // Pinned but never installed: the list must say so rather than imply the pin is in force.
  const out = run(['list'], at(fx)).out;
  assert.match(out, /9\.9\.9/);
  assert.match(out, /not installed|missing/i);
  // And say what the driver does about it, which is refuse — never run whatever PATH has (REV3 CLI F13).
  assert.equal(/fall back to PATH/i.test(out), false, out);
  assert.match(out, /refuse/i, out);
  fx.cleanup();
});

// ——— `agent list`'s next start (TOOL6f; D130's TOOL6e note, design §16.4): beneath each list, the walk's steps as
// `profile use` names them, each account of it held now and until when, and where the account it takes is named. This
// command reads no session record, so it never names that account where a step would choose it (D57, D143). Every agent
// is pinned to a version nothing installed, so the listing asks no tool anything.

function listedWith(fx: { root: string }, wiring: Record<string, unknown>, held: Record<string, unknown> = {}): string {
  const nowhere = { 'claude-code': '0.0.0-none', 'claude-code-acp': '0.0.0-none', codex: '0.0.0-none', 'codex-acp': '0.0.0-none', dsh: '0.0.0-none' };
  writeFileSync(join(fx.root, COOLING_FILE), JSON.stringify(held), 'utf8');
  writeFileSync(at(fx), JSON.stringify({ versions: nowhere, ...wiring }), 'utf8');
  return run(['list'], at(fx)).out;
}

function claudeAccounts(fx: { root: string }, ...names: string[]): void {
  for (const name of names) mkdirSync(profileHome(fx.root, 'claude-code', name), { recursive: true });
}

/** A moment `hours` from now, as the driver writes `until`, and as `agent list` says it in this machine's zone. */
function moment(hours: number): { until: string; said: string } {
  const until = new Date(Math.floor((Date.now() + hours * 3_600_000) / 60_000) * 60_000).toISOString().replace(/\.\d+Z$/, 'Z');
  return { until, said: coolingWhen(new Date(until), machineZone()).replace(/[.*+?^${}()|[\]\\]/g, '\\$&') };
}

// UX6e2: the screen that names it is the agent's page in the Agents place since UX6e (D150 §5.2).
const POINTER = 'Agents → the agent\'s page → How accounts are used names the account it takes, from the sessions Daoris '
  + 'runs and its last starts, which this terminal does not read';

/**
 * TOOL6g: one account list per tool. A door onto another agent's accounts (`claude-code-acp`) listed the same accounts
 * again, with states its owner had already said (`unknown` beside `out`). It now names whose accounts it runs on, once.
 */
test('`agent list` lists a tool\'s accounts once, under the tool, and a door names whose it runs on', () => {
  const fx = makeFixture('harness-list-one');
  claudeAccounts(fx, 'account-1', 'account-2');

  const out = listedWith(fx, { rotation: { 'claude-code': ['account-1', 'account-2'] } });

  assert.equal(out.match(/^\s+account-1\s/gm)?.length, 1, out);
  assert.equal(out.match(/rotation\s+account-1, then account-2/g)?.length, 1, out);
  assert.match(out, /claude-code-acp[^\n]*\n(?:\s{17}[^\n]*\n)*?\s{17}its accounts are Claude Code's, listed under `claude-code`: this is another way Claude Code runs on them\n/);
  fx.cleanup();
});

test('`agent list` says the next start\'s steps beneath each list, and where the account it takes is named (TOOL6f)', () => {
  const fx = makeFixture('harness-list-next');
  claudeAccounts(fx, 'account-1', 'account-2', 'account-3');

  const out = listedWith(fx, {
    rotation: { 'claude-code': ['account-1', 'account-2', 'account-3'] },
    workspaceRotation: { work: { 'claude-code': ['account-2', 'account-1'] } },
    workspaceRotationUse: { work: { 'claude-code': { use: 'order' } } },
  });

  assert.match(out, new RegExp('rotation\\s+account-1, then account-2, then account-3\\n(?:.*\\n)*?'
    + '\\s+next start: the ready account its agent did not say is near; then the one running the fewest of Daoris\'s sessions; '
    + 'then one whose week resets within a day; then the one furthest behind its week\'s pace; then the one Daoris started on '
    + 'least recently; then this list\'s order, from `account-1`\\n'
    + '\\s+no account has said what it has left yet: Daoris spreads starts across them by its own sessions, and learns each '
    + 'account\'s weekly reset from the limits it meets\\n'
    + `\\s+${POINTER}\\n`));
  assert.match(out, new RegExp('rotation in work account-2, then account-1\\n(?:.*\\n)*?'
    + '\\s+next start: the first ready account of this list, from `account-2`; one its agent said is near goes last\\n'
    + `\\s+${POINTER}\\n`));
  // Never a guessed account: nothing is held, and no line names the account a step would choose.
  assert.doesNotMatch(out, /held now|waits until|next start takes|next start runs on/);
  fx.cleanup();
});

test('`agent list` names each account of a list held now and until when, and the wait when none is ready (TOOL6f)', () => {
  const fx = makeFixture('harness-list-held');
  claudeAccounts(fx, 'account-1', 'account-2');
  const wiring = { rotation: { 'claude-code': ['account-1', 'account-2'] } };
  const later = moment(5);
  const sooner = moment(2);

  const one = listedWith(fx, wiring, { 'claude-code': { 'account-2': { until: later.until, stated: true } } });
  const both = listedWith(fx, wiring, {
    'claude-code': { 'account-1': { until: later.until, stated: true }, 'account-2': { until: sooner.until, stated: false } },
  });

  assert.match(one, new RegExp(`next start: [^\\n]+\\n(?:.*\\n)*?\\s+held now: \`account-2\` is cooling until ${later.said}\\n\\s+${POINTER}\\n`));
  assert.doesNotMatch(one, /held now: `account-1`|waits until/);
  assert.match(both, new RegExp(`held now: \`account-1\` is cooling until ${later.said}; \`account-2\` is cooling until ${sooner.said}\\n`
    + `\\s+every account of this list is cooling, so the next start waits until ${sooner.said}\\n`));

  // A kept account ready while every other cools: driven work waits, and a conversation is not said to.
  claudeAccounts(fx, 'account-3');
  const kept = listedWith(fx, {
    rotation: { 'claude-code': ['account-1', 'account-2', 'account-3'] },
    rotationUse: { 'claude-code': { keep: 'account-3' } },
  }, { 'claude-code': { 'account-1': { until: later.until, stated: true }, 'account-2': { until: sooner.until, stated: true } } });
  assert.match(kept, new RegExp(`held now: \`account-1\` is cooling until ${later.said}; \`account-2\` is cooling until ${sooner.said}\\n`
    + `\\s+every account but \`account-3\`, kept for conversations, is cooling, so driven work waits until ${sooner.said}\\n`));
  assert.doesNotMatch(kept, /every account of this list is cooling/);
  fx.cleanup();
});

test('`agent list` says the machine\'s next start where it has no list: its default alone, or nothing more (TOOL6f)', () => {
  const fx = makeFixture('harness-list-nolist');
  claudeAccounts(fx, 'account-1', 'account-2');
  const held = moment(3);

  const named = listedWith(fx, { defaults: { 'claude-code': 'account-1' } });
  const cooling = listedWith(fx, { defaults: { 'claude-code': 'account-1' } }, {
    'claude-code': { 'account-1': { until: held.until, stated: true } },
  });
  const none = listedWith(fx, {});

  // Its one account is the settings', not a step's choice, so it is named; no walk, so no pointer.
  assert.match(named, /\n\s+next start\s+`account-1`, this machine's default — with no list, the one account its starts run on\n/);
  assert.doesNotMatch(named, /names the account it takes|held now|waits until/);
  assert.match(cooling, new RegExp(`next start\\s+\`account-1\`[^\\n]+\\n\\s+held now: \`account-1\` is cooling until ${held.said}, `
    + 'so the next start waits until then\\n'));
  // No default and no list: the tool's own sign-in, which its own lines say; nothing is guessed here.
  assert.doesNotMatch(none, /next start/);
  fx.cleanup();
});

/**
 * CODEXUSE1b: the driver reads a Codex account's windows from Codex's own app server and keeps them as a door's frame is
 * kept (CODEXUSE1, `docs/2026-10-07-codex-usage-evidence.md`), so `agent list` printed the reading beneath the account and
 * still said, beneath its list, that Codex's sessions do not say how near their limits are, and walked without near and pace.
 */
test('`agent list` reads a Codex account\'s kept reading as one that speaks: its use and next-start lines weigh its week (CODEXUSE1b)', () => {
  const fx = makeFixture('harness-list-codex');
  for (const name of ['account-1', 'account-2']) mkdirSync(profileHome(fx.root, 'codex', name), { recursive: true });
  const stamp = (offset: number) => new Date(Date.now() + offset).toISOString().replace(/\.\d+Z$/, 'Z');
  // As the driver keeps the server's answer (evidence §2): a use per window, its reset, when; no standing and no session.
  writeFileSync(join(fx.root, WINDOWS_FILE), JSON.stringify({
    codex: {
      'account-1': {
        session: { reset: stamp(3 * 3_600_000), used: 0.01, seen: stamp(-20 * 60_000) },
        weekly: { reset: stamp(100 * 3_600_000), used: 0.15, seen: stamp(-20 * 60_000) },
      },
    },
  }), 'utf8');

  const out = listedWith(fx, { rotation: { codex: ['account-1', 'account-2'] } });
  const codex = out.slice(out.search(/^\s*codex\s/m));

  assert.match(codex, /account-1[^\n]*\n\s+said 20 min ago: 1% of its session limit used, resetting [^;]+; 15% of its weekly limit used, resetting [^\n]+\n/);
  assert.match(codex, new RegExp('rotation\\s+account-1, then account-2\\n(?:.*\\n)*?'
    + '\\s+switch before the limit: on, at 90% — a start passes an account Codex says is near its limit, or that has used 90% of a window\\n'
    + '\\s+next start: the ready account its agent did not say is near; then the one running the fewest of Daoris\'s sessions; '
    + 'then one whose week resets within a day; then the one furthest behind its week\'s pace; then the one Daoris started on '
    + 'least recently; then this list\'s order, from `account-1`\\n'
    + `\\s+${POINTER}\\n`));
  assert.doesNotMatch(codex, /do not say how near their limits are|no account has said what it has left yet/);
  fx.cleanup();
});

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

  // The trust record (DEPLOY1, D73) — the driver's `TrustFile` is its twin, on the same two entries,
  // because both doors onto Claude Code ignore an untrusted folder's allow-list (measured).
  const trusting = Object.entries(TOOLCHAINS).filter(([, toolchain]) => toolchain.trustFile).map(([name]) => name);
  assert.deepEqual(trusting, ['claude-code', 'claude-code-acp']);
  assert.equal(TOOLCHAINS['claude-code']!.trustFile, '.claude.json');

  // Whose accounts say how much of each window is used (TOOL6c, CODEXUSE1b) — the driver's `Speaks` is its twin: Claude Code's
  // `Windows`, its frame on the door (and the stub mirroring it, which the CLI does not know), and Codex's `Usage`, its own app
  // server asked, declared on `codex-acp` since no `codex` adapter exists. Both are owners' here, and a door reads its owner's.
  const saying = Object.entries(TOOLCHAINS).filter(([, toolchain]) => toolchain.windows).map(([name]) => name);
  assert.deepEqual(saying, ['claude-code', 'codex']);
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

// ——— CODEXACCT2, CODEXACCT1b: Codex signs in by its device code, on the binary `agent list` asks.

/**
 * 🔴 Measured with 0.160.0 (D125's CODEXACCT2 note): `codex login` waits for its browser callback on a local port, and
 * Windows reserves that port on the owner's machine, so it exits 1 there (os error 10013). `codex login --device-auth`
 * prints a link and a one-time code and needs no port. The driver's `CodexAccounts` reads this entry and holds itself to it.
 */
test('Codex signs in by its device code, which listens on no port of this machine (CODEXACCT2)', () => {
  assert.deepEqual(TOOLCHAINS.codex!.login, ['login', '--device-auth']);
  // Its status question is unchanged: the sign-in's end still asks `codex login status`.
  assert.deepEqual(TOOLCHAINS.codex!.loginCheck!.args, ['login', 'status']);
});

/**
 * A stand-in `codex`, pinned in npm's layout at `version` and nowhere on PATH: each call is logged with the folder it was
 * handed as `CODEX_HOME`, its device-code sign-in leaves `auth.json` there, and its status question answers in the words
 * `codex login status` printed (0.160.0).
 */
function pinnedCodex(fx: { root: string }, version: string): { binary: string; asked: () => string[] } {
  const script = join(fx.root, 'codex-stand-in.mjs');
  const log = join(fx.root, 'codex-asked.log');
  writeFileSync(script, [
    "import { appendFileSync, existsSync, writeFileSync } from 'node:fs';",
    "import { basename, join } from 'node:path';",
    "const home = process.env.CODEX_HOME ?? '';",
    'const args = process.argv.slice(2);',
    `appendFileSync(${JSON.stringify(log)}, basename(home) + ' ' + args.join(' ') + '\\n');`,
    "if (args[0] === 'login' && args[1] === 'status') {",
    "  console.log(existsSync(join(home, 'auth.json')) ? 'Logged in using ChatGPT' : 'Not logged in');",
    '  process.exit(0);',
    '}',
    "if (args[0] === 'login' && args[1] === '--device-auth' && args.length === 2 && home) {",
    "  writeFileSync(join(home, 'auth.json'), '{}');",
    '  process.exit(0);',
    '}',
    'process.exit(1);',
  ].join('\n'), 'utf8');

  const settings = readHarnessSettings(at(fx));
  writeHarnessSettings(at(fx), { ...settings, versions: { ...settings.versions, codex: version } });
  const bin = join(managedHome(fx.root, 'codex', version), 'node_modules', '.bin');
  mkdirSync(bin, { recursive: true });
  const windows = process.platform === 'win32';
  const binary = join(bin, windows ? 'codex.cmd' : 'codex');
  writeFileSync(binary, windows
    ? `@"${process.execPath}" "${script}" %*\r\n`
    : `#!/bin/sh\nexec "${process.execPath}" "${script}" "$@"\n`, 'utf8');
  if (!windows) chmodSync(binary, 0o755);
  return { binary, asked: () => (existsSync(log) ? readFileSync(log, 'utf8').split('\n').filter(Boolean) : []) };
}

/**
 * 🔴 CODEXACCT1b: the owner's machine has `codex` only as its pin, and the terminal's sign-in ran `PATH`'s, so it could not
 * start. It runs the binary `agent list` asks now: the pin, its device-code sign-in into a new folder, and the end's
 * status question on the same pin. Nothing is on PATH, so a regression fails here and runs no real `codex`.
 */
test('`agent login codex --new` with only a pin signs in on the pinned codex, by its device code (CODEXACCT1b)', async () => {
  const fx = makeFixture('harness-sign-in-codex-pin');
  const codex = pinnedCodex(fx, '0.160.0');

  const made = await pathless(fx, async () => run(['login', 'codex', '--new'], at(fx)));

  assert.equal(made.code, 0, made.out);
  const [account] = profiles(fx.root, 'codex');
  assert.match(account!, /^acct-[0-9a-f]{8}$/);
  assert.ok(existsSync(join(profileHome(fx.root, 'codex', account!), 'auth.json')));
  assert.ok(made.out.includes(`$ ${codex.binary} login --device-auth`), made.out);
  assert.deepEqual(codex.asked(), [`${account} login --device-auth`, `${account} login status`]);
  assert.match(made.out, /`codex` did not say who/);

  // Signing back in to it runs the same pin, into its own folder (ACCT1).
  const again = await pathless(fx, async () => run(['login', 'codex', '--profile', account!], at(fx)));
  assert.equal(again.code, 0, again.out);
  assert.deepEqual(codex.asked().slice(2), [`${account} login --device-auth`, `${account} login status`]);
  fx.cleanup();
});

/** Pinned with nothing installed at the pin: refused naming the pin, with nothing made, and never `PATH`'s instead (D57). */
test('`agent login codex --new` on a pin with nothing installed is refused naming the pin, and makes nothing', async () => {
  const fx = makeFixture('harness-sign-in-codex-pin-missing');
  writeHarnessSettings(at(fx), { ...readHarnessSettings(at(fx)), versions: { codex: '0.160.0' } });

  for (const argv of [['login', 'codex', '--new'], ['login', 'codex', '--profile', 'acct-0a1b2c3d']]) {
    if (argv.includes('--profile')) mkdirSync(profileHome(fx.root, 'codex', 'acct-0a1b2c3d'), { recursive: true });
    const refused = await pathless(fx, async () => captureError(() => run(argv, at(fx))));

    assert.match(refused.message, /`codex` is pinned to 0\.160\.0 on this machine, and nothing is installed at that version/);
    assert.match(refused.message, /`daoris agent pin codex 0\.160\.0` installs it/);
  }
  assert.deepEqual(profiles(fx.root, 'codex'), ['acct-0a1b2c3d']);
  fx.cleanup();
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
