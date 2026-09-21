import { test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import {
  TOOLCHAINS, commandHarness, harnessesPath, managedBinary, managedHome, profileHome, profiles,
  readHarnessSettings, resolveProfile, resolveVersion, writeHarnessSettings,
} from '../src/toolchain.ts';
import { makeFixture } from './_fixture.ts';
import { captureError } from './_fixture.ts';

/**
 * `daoris harness` — management parity for the toolchain (D49 §4, D50).
 *
 * The whole feature rests on one sentence: **Daoris manages directories and names, never secrets.**
 * A profile is a directory Daoris owns the location of; whatever credential ends up inside it was put
 * there by the harness's own login flow and stays in the harness's own store. These tests are written
 * to fail if that ever stops being true.
 *
 * They also hold the CLI's half of a TWIN CONTRACT. The driver's `Harnesses.cs` implements the same
 * rules in another language against the same file and the same directory layout, because the two
 * artefacts share no code. The three rules below are asserted in both, and they move together.
 */

/** Everything runs against a fixture, never the developer's real `~/.daoris`. */
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

test('the path is the override, or the conventional home beside the other wiring files', () => {
  assert.equal(harnessesPath({ DAORIS_HARNESS_CONFIG: '/x/h.json' }), '/x/h.json');
  assert.match(harnessesPath({}), /[\\/]\.daoris[\\/]harnesses\.json$/);
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
    rest: {},
  };

  assert.equal(resolveProfile(settings, 'claude-code', 'aurora', 'picked'), 'picked');
  assert.equal(resolveProfile(settings, 'claude-code', 'aurora', null), 'work');
  assert.equal(resolveProfile(settings, 'claude-code', 'tools', null), 'personal');
  assert.equal(resolveProfile(settings, 'claude-code', null, null), 'personal');
  assert.equal(resolveProfile(settings, 'codex', 'aurora', null), null);
});

// ——— Twin rule 3: none means the harness's OWN home. This is the one that must never regress.

test('no profile named anywhere means the harness’s own configuration home', () => {
  const empty = { defaults: {}, workspaces: {}, versions: {}, workspaceVersions: {}, rest: {} };

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
    defaults: {}, workspaces: {}, rest: {},
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
 * 🔴 The rule that must never regress, and the twin of "no profile means the harness's own home".
 * Nothing pinned means `PATH` — not an empty managed directory, and not a refusal.
 */
test('no version pinned anywhere means whatever the machine has on PATH', () => {
  const empty = { defaults: {}, workspaces: {}, versions: {}, workspaceVersions: {}, rest: {} };

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
  assert.match(result.out, /daoris harness login claude-code --profile work/);
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
 * The directory holds a credential the harness put there. Removing a profile clears the WIRING and
 * says, out loud, that it deleted nothing — an irreversible act is never a side effect here.
 */
test('`profile remove` un-defaults it everywhere and deletes nothing', () => {
  const fx = makeFixture('harness-remove');
  run(['profile', 'add', 'claude-code', 'work'], at(fx));
  run(['profile', 'default', 'claude-code', 'work'], at(fx));
  run(['profile', 'default', 'claude-code', 'work', '--workspace', 'aurora'], at(fx));
  writeFileSync(join(profileHome(fx.root, 'claude-code', 'work'), 'credentials.json'), '{}', 'utf8');

  const result = run(['profile', 'remove', 'claude-code', 'work'], at(fx));

  assert.equal(result.code, 0);
  assert.match(result.out, /directory is untouched/);
  assert.ok(existsSync(join(profileHome(fx.root, 'claude-code', 'work'), 'credentials.json')));
  const settings = readHarnessSettings(at(fx));
  assert.equal(resolveProfile(settings, 'claude-code', 'aurora', null), null);
  assert.equal(resolveProfile(settings, 'claude-code', null, null), null);
  fx.cleanup();
});

test('an unknown harness errors naming what exists, never a silent fallback', () => {
  const fx = makeFixture('harness-unknown');
  const error = captureError(() => run(['install', 'not-a-harness'], at(fx)));

  assert.match(error.message, /unknown harness 'not-a-harness'/);
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
  assert.match(run(['list'], at(fx)).out, /9\.9\.9/);
  assert.match(run(['list'], at(fx)).out, /not installed|missing/i);
  fx.cleanup();
});

test('an unknown verb names the ones that exist', () => {
  const fx = makeFixture('harness-verb');
  const error = captureError(() => run(['frobnicate'], at(fx)));

  assert.match(error.message, /unknown harness verb 'frobnicate'/);
  assert.match(error.message, /list, install, update, login, pin, unpin, profile/);
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
    assert.ok(toolchain.loginCheck, `${name} cannot be asked whether a profile is logged in`);
  }

  assert.equal(TOOLCHAINS['claude-code']!.profileVariable, 'CLAUDE_CONFIG_DIR');
  assert.equal(TOOLCHAINS.codex!.profileVariable, 'CODEX_HOME');
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
