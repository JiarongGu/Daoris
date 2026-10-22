/**
 * LIVELOGIN1: no test starts a real sign-in or runs a real agent binary (docs/development.md, "Run the full product gates").
 *
 *   node tools/live-logins.mjs        check every test source (exit 0 clean, 1 breach, 2 tool error)
 *
 * A test that ran `codex login` or `claude auth login`, or let an agent's command fall back to PATH, opened the person's browser on
 * their real account or ran the agent in a test's home. The driver refuses a sign-in of PATH's agent under a scratch home
 * (`ScratchSignIns`); this census finds the test before it runs, in two shapes:
 *
 *  - a real login command: an agent's binary followed by its sign-in words, as an argument list (`"codex", "login"`,
 *    `'claude', 'auth', 'login'`) or as one literal a spawn is handed whole (`execSync("codex login --device-auth")`);
 *  - an agent command that falls back to PATH: a `HarnessCommand` built on an agent's bare binary or on a toolchain's `.Binary`;
 *    a configured command (`"commands": { "x": ["claude"] }`, `["x"] = ["codex"]`) whose first word is an agent's bare binary; or
 *    configured commands that name a door onto another agent's accounts and not that agent (`claude-code-acp` without
 *    `claude-code`), so the account agent's reads run PATH's (RESUMEOWN1's `HelpChatTests`).
 *
 * The agents' binaries are read from the driver's toolchains (`Binary: ["…"]`), so a new agent is held without an edit here. A stub
 * is what a test runs instead: a script at a path (`["node", script]`, a pin's managed binary under the scratch home), whose own
 * code answering `login` is no command and is not matched. Prose that names a command in backticks inside a string (a refusal's
 * words) is not a command either. Comments are skipped. A guard's own test that names the real command to prove it is refused
 * marks that line `// live-logins: refused, never started`, and the clean line counts those marks. There is no .NET build.
 */
import { readdirSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { isMain } from './fsx.mjs';

const SKIPPED = new Set(['bin', 'obj', 'node_modules', 'dist', 'wwwroot', '_fixtures', '.git']);
const TOOLCHAINS = 'src/Daoris.Desktop/Daoris.Desktop.Driver';
const EXEMPT = '// live-logins: refused, never started';
// This census's own test, whose planted cases are its evidence.
const OWN_TEST = 'tools/live-logins.test.mjs';

/** The doors onto another agent's accounts, by their toolchains' `AccountOf`; the stub's twin is left out, the stub having no binary. */
export const ACCOUNT_AGENTS = { 'claude-code-acp': 'claude-code', 'codex-acp': 'codex' };

const escape = (word) => word.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');

function* files(dir, keep) {
  let entries;
  try {
    entries = readdirSync(dir, { withFileTypes: true });
  } catch {
    return;
  }
  for (const entry of entries) {
    const full = join(dir, entry.name);
    if (entry.isDirectory()) {
      if (!SKIPPED.has(entry.name)) yield* files(full, keep);
    } else if (keep(full)) {
      yield full;
    }
  }
}

const rel = (root, file) => file.slice(root.length + 1).replaceAll('\\', '/');

/** The agents' binaries the driver declares: each toolchain's `Binary: ["…"]` first word. */
export function agentBinaries(root) {
  const found = new Set();
  for (const file of files(join(root, TOOLCHAINS), (f) => f.endsWith('.cs'))) {
    for (const m of readFileSync(file, 'utf8').matchAll(/\bBinary:\s*\[\s*"([^"]+)"/g)) found.add(m[1]);
  }
  return [...found].sort();
}

/** Whether a repository-relative path is a test source: a .NET test project's C#, or a TypeScript or Node test. */
export function isTest(path) {
  if (path.endsWith('.cs')) return path.split('/').some((part) => part.endsWith('.Tests'));
  if (!/\.(ts|tsx|mts|mjs|js)$/.test(path)) return false;
  return /\.test\.[a-z]+$/.test(path) || /(^|\/)(test|tool-test|tests|e2e)\//.test(path);
}

/** The line patterns over `binaries`: each `{ kind, pattern }`. */
export function patterns(binaries) {
  const bin = `(?:${binaries.map(escape).join('|')})(?:\\.exe|\\.cmd)?`;
  const q = '["\'`]';
  const login = 'a real login command';
  const path = 'an agent command that falls back to PATH';
  return [
    { kind: login, pattern: new RegExp(`${q}${bin}${q}\\s*,\\s*(?:${q}auth${q}\\s*,\\s*)?${q}login${q}`) },
    {
      kind: login,
      pattern: new RegExp(`\\b(?:exec\\w*|spawn\\w*|execa|Start|ProcessStartInfo)\\(\\s*${q}${bin}(?: auth)? login(?: [^"'\`]*)?${q}`),
    },
    { kind: path, pattern: new RegExp(`\\bHarnessCommand\\([^,()]+,\\s*\\[\\s*"${bin}"`) },
    { kind: path, pattern: /\bHarnessCommand\([^,()]+,\s*[\w.!?]*\bBinary\b/ },
    { kind: path, pattern: new RegExp(`"commands"\\s*:\\s*\\{[^}]*?\\[\\s*\\\\?"${bin}\\\\?"`) },
    { kind: path, pattern: new RegExp(`\\["[\\w-]+"\\]\\s*=\\s*\\[\\s*"${bin}"`) },
  ];
}

const comment = (line) => /^\s*(\/\/|\*|\/\*)/.test(line);

/** Each configured command set in `text`, a JSON `"commands"` object or a C# `Commands` dictionary: its line and its keys. */
function commandSets(text) {
  const sets = [];
  for (const m of text.matchAll(/"commands"\s*:\s*\{([^}]*)\}|\bCommands\s*=\s*new\s+Dictionary<[^{]*\{([^}]*)\}/g)) {
    const body = m[1] ?? m[2];
    const keys = new Set([...body.matchAll(/\\?"([\w-]+)\\?"\s*:|\["([\w-]+)"\]\s*=/g)].map((k) => k[1] ?? k[2]));
    sets.push({ line: text.slice(0, m.index).split('\n').length, keys });
  }
  return sets;
}

/** The breaches in `text`, a file at `path`: `{ found, exempt }`, each found `path:line: kind — what`. */
export function scanText(path, text, rules) {
  const found = [];
  let exempt = 0;
  text.split(/\r?\n/).forEach((line, at) => {
    if (comment(line)) return;
    for (const { kind, pattern } of rules) {
      if (!pattern.test(line)) continue;
      if (line.trimEnd().endsWith(EXEMPT)) exempt++;
      else found.push(`${path}:${at + 1}: ${kind} — ${line.trim()}`);
      break;
    }
  });
  for (const { line, keys } of commandSets(text)) {
    for (const [door, agent] of Object.entries(ACCOUNT_AGENTS)) {
      if (keys.has(door) && !keys.has(agent)) {
        found.push(`${path}:${line}: an agent command that falls back to PATH — \`${door}\` is named and \`${agent}\`, whose accounts it runs as, is not`);
      }
    }
  }
  return { found, exempt };
}

/** The breaches over the tree at `root`: `{ problems, binaries, exempt }`. */
export function checkLiveLogins(root) {
  const binaries = agentBinaries(root);
  if (binaries.length === 0) throw new Error(`no agent binary is declared under ${TOOLCHAINS}`);
  const rules = patterns(binaries);
  const problems = [];
  let exempt = 0;
  for (const top of ['src', 'tools']) {
    for (const file of files(join(root, top), (f) => isTest(rel(root, f)) && rel(root, f) !== OWN_TEST)) {
      const scanned = scanText(rel(root, file), readFileSync(file, 'utf8'), rules);
      problems.push(...scanned.found);
      exempt += scanned.exempt;
    }
  }
  return { problems, binaries, exempt };
}

function main() {
  const root = dirname(dirname(fileURLToPath(import.meta.url)));
  try {
    const { problems, binaries, exempt } = checkLiveLogins(root);
    for (const p of problems) console.error(`live-logins: ${p}`);
    if (problems.length) {
      console.error(
        'live-logins: a test runs a stub, never the real agent: name a script for the agent (`["node", script]`) or a stub laid under '
          + "its scratch home, and take a login's words from the code (LIVELOGIN1)",
      );
      return 1;
    }
    console.log(`live-logins: clean (${binaries.join(', ')}; ${exempt} marked refused, never started)`);
    return 0;
  } catch (e) {
    console.error(`live-logins: ${e.message}`);
    return 2;
  }
}

if (isMain(import.meta.url)) process.exitCode = main();
