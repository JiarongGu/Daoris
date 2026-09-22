import { test } from 'node:test';
import assert from 'node:assert/strict';
import { chmodSync, existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import {
  TOOLCHAINS, commandHarness, harnessesPath, managedBinary, managedHome, profileHome, profiles,
  probe, readHarnessSettings, resolveProfile, resolveVersion, writeHarnessSettings,
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
 * The directory holds what the harness put there. Removing a profile clears the WIRING and, unless
 * the harness itself says the profile is signed out, says out loud that it deleted nothing — an
 * irreversible act is never a side effect here. `dsh` declares no login question, so its answer is
 * always "could not say", on every machine: the case that must keep the directory.
 */
test('`profile remove` un-defaults it everywhere and keeps a directory it cannot vouch for', () => {
  const fx = makeFixture('harness-remove');
  run(['profile', 'add', 'dsh', 'work'], at(fx));
  run(['profile', 'default', 'dsh', 'work'], at(fx));
  run(['profile', 'default', 'dsh', 'work', '--workspace', 'aurora'], at(fx));
  writeFileSync(join(profileHome(fx.root, 'dsh', 'work'), 'credentials.json'), '{}', 'utf8');

  const result = run(['profile', 'remove', 'dsh', 'work'], at(fx));

  assert.equal(result.code, 0);
  assert.match(result.out, /directory is untouched/);
  assert.match(result.out, /could not say/);
  assert.ok(existsSync(join(profileHome(fx.root, 'dsh', 'work'), 'credentials.json')));
  const settings = readHarnessSettings(at(fx));
  assert.equal(resolveProfile(settings, 'dsh', 'aurora', null), null);
  assert.equal(resolveProfile(settings, 'dsh', null, null), null);
  fx.cleanup();
});

/**
 * 🔴 The other half of the same rule. A profile the harness never wrote into is an empty directory
 * Daoris made; removing THAT destroys nothing, and leaving it was a "remove" nobody could see —
 * the directory is the account, so the account stayed listed after being forgotten. (A signed-out
 * but scaffolded profile goes the same way on the harness's own word; the family rehearsal's stub
 * harness is where that answer can be given with no account on any machine.)
 */
test('`profile remove` takes an empty directory with it', () => {
  const fx = makeFixture('harness-remove-empty');
  run(['profile', 'add', 'claude-code', 'fresh'], at(fx));

  const result = run(['profile', 'remove', 'claude-code', 'fresh'], at(fx));

  assert.equal(result.code, 0);
  assert.match(result.out, /empty directory/);
  assert.ok(!existsSync(profileHome(fx.root, 'claude-code', 'fresh')));
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
const TWINS: Record<string, { binary: string[]; profileVariable: string }> = {
  'claude-code': { binary: ['claude'], profileVariable: 'CLAUDE_CONFIG_DIR' },
  'claude-code-acp': { binary: ['claude-agent-acp'], profileVariable: 'CLAUDE_CONFIG_DIR' },
  codex: { binary: ['codex'], profileVariable: 'CODEX_HOME' },
  'codex-acp': { binary: ['codex-acp'], profileVariable: 'CODEX_HOME' },
  dsh: { binary: ['dsh'], profileVariable: 'DSH_HOME' },
};

test('every declared harness is pinned by name, binary and seam', () => {
  for (const [name, toolchain] of Object.entries(TOOLCHAINS)) {
    const twin = TWINS[name];
    assert.ok(twin,
      `${name} is declared and pinned by no twin row — so the driver's AdapterSet could describe a `
      + 'different program under the same name and both sides would read as correct');
    assert.deepEqual(toolchain.binary, twin.binary, `${name}: the binary moved`);
    assert.equal(toolchain.profileVariable, twin.profileVariable, `${name}: the account seam moved`);
  }

  // The protocol door borrows the pipe door's account, through the same seam (ACP2/ACP3).
  assert.equal(TOOLCHAINS['claude-code-acp']!.accountOf, 'claude-code');
  assert.equal(TOOLCHAINS['codex-acp']!.accountOf, 'codex');
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
    defaults: {}, workspaces: {}, versions: { 'claude-code': '1.2.3' }, workspaceVersions: {}, rest: {},
  };
  const report = probe('claude-code', TOOLCHAINS['claude-code']!, home, settings);

  assert.equal(report.present, true, report.problem ?? 'probed as absent');
  assert.match(report.version ?? '', /9\.9\.9-pinned/);
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
    defaults: {}, workspaces: {}, versions: { 'claude-code': '9.9.9' }, workspaceVersions: {}, rest: {},
  };

  const report = probe('claude-code', TOOLCHAINS['claude-code']!, join(fx.root, 'home'), settings);

  assert.equal(report.present, false);
  assert.match(report.problem ?? '', /9\.9\.9/);
  fx.cleanup();
});
