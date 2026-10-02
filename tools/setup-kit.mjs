/**
 * The family rehearsal's set-up phase, in the parts a test can hold without a host or a driver (LAYOUT7a):
 * the doctrine tool's launcher a child of Daoris finds by its bare name, the PATH that puts it first, and a
 * reading of what `daoris-driver setup` prints; and, for the registration that follows the landing (WSSETUP5a),
 * a reading of what `daoris-driver register` prints and of the machine log's `registry.followed` lines.
 *
 * ## Why the workspace's CLI, and a launcher of the rehearsal's own
 *
 * A set-up's session runs `daoris` by its bare name (D124 §1, §2.3), and the press refuses where a child of
 * Daoris would find none. The install's `app/bin/` launchers are the deployment rehearsal's to test (WSSETUP2),
 * so this phase puts the workspace's own CLI there instead: a launcher in scratch, first on the PATH the
 * driver is started with, which the driver hands every session it starts (TOOLS5's one environment).
 *
 * ## A reading of another program's words
 *
 * `readSetup` reads `SetupCommand`'s output by its shape: the head, the rows of what was read, the quest's
 * words, the rules, the refusals, what a press published. It shares no code with it. `setup-kit.test.mjs`
 * holds the reading against output spelled as `SetupCommand` writes it, so a change to those words is a
 * change to that test's fixtures too, and the family rehearsal is what runs the two against each other.
 * `readRegister` and `readFollowed` read `RegisterCommand` and `MachineLog` the same way.
 */
import { chmodSync, mkdirSync, writeFileSync } from 'node:fs';
import { delimiter, join } from 'node:path';

/** A set-up quest's title, the whole set-up's (`SetupQuests.SetUp`), with the day the press composed it on. */
export const SETUP_TITLE = /^Set up this repository for every agent \(\d{4}-\d{2}-\d{2}\)$/;

/**
 * The rule a press adds, verb for verb (D124 §2.4; `SetupPress.Verbs`): the doctrine tool's exact verbs as
 * `Bash` allows, never a runner, never `upstream` or a management verb.
 */
export const SETUP_RULES = [
  'daoris --version', 'daoris init --harness agents', 'daoris analyze --json', 'daoris sync --dry-run',
  'daoris sync', 'daoris sync --force', 'daoris check', 'daoris status --json', 'daoris doctor',
].map((verb) => `Bash(${verb})`);

/**
 * The launcher a platform's shell starts for `daoris`: a batch file where Windows finds a command by PATHEXT,
 * and an executable script elsewhere. Either runs the CLI at `cliBin` on the `node` the same PATH finds, as the
 * install's launchers do, and keeps its exit code.
 */
export function doctrineLauncher(cliBin, platform = process.platform) {
  if (platform === 'win32') {
    // A `%` in a batch file is a variable's mark, so a path's own is doubled; CRLF, because cmd reads batch lines by it.
    return {
      name: 'daoris.cmd',
      text: ['@echo off', `node "${cliBin.replaceAll('%', '%%')}" %*`, 'exit /b %ERRORLEVEL%', ''].join('\r\n'),
    };
  }
  return { name: 'daoris', text: `#!/bin/sh\nexec node '${cliBin.replaceAll("'", "'\\''")}' "$@"\n` };
}

/** Write the launcher into `folder`, executable where that is a mode; the file it wrote. */
export function writeDoctrineLauncher(folder, cliBin, platform = process.platform) {
  const { name, text } = doctrineLauncher(cliBin, platform);
  mkdirSync(folder, { recursive: true });
  const file = join(folder, name);
  writeFileSync(file, text);
  if (platform !== 'win32') chmodSync(file, 0o755);
  return file;
}

/**
 * The variable that puts `folder` first on the PATH, under the name `env` already spells it with: Windows keeps
 * `Path`, and a child handed both `Path` and `PATH` keeps whichever sorts first.
 */
export function withFirstOnPath(folder, env = process.env) {
  const key = Object.keys(env).find((name) => name.toUpperCase() === 'PATH') ?? 'PATH';
  return { [key]: env[key] ? `${folder}${delimiter}${env[key]}` : folder };
}

/** A row of what was read (`  line     …`): its label padded to nine, after two spaces. */
const row = (lines, label) => {
  const lead = `  ${label.padEnd(9)}`;
  const found = lines.find((line) => line.startsWith(lead));
  return found === undefined ? null : found.slice(lead.length);
};

/** The lines after `from` that start with `lead`, up to the first that does not. */
const following = (lines, from, lead) => {
  const taken = [];
  for (let at = from + 1; at < lines.length && lines[at].startsWith(lead); at++) taken.push(lines[at].slice(lead.length));
  return taken;
};

/**
 * What `daoris-driver setup <repository> [--plan]` printed (`SetupCommand`), read back: null for a field it did
 * not print. `title` and `body` are the ask's words a plan shows; `rules` are the ones it would add or added;
 * `refusals` each sentence after `refused:`; `ask` and `quest` the ids a press published; `nothingPublished`
 * whether it said a plan published nothing and added no rule.
 */
export function readSetup(out) {
  const lines = out.split(/\r?\n/);
  const head = /^setup: `([^`]+)`(?:, in workspace `([^`]+)`)?$/.exec(lines.find((line) => line.startsWith('setup: `')) ?? '');
  const line = /^`([^`]+)` at `([0-9a-f]+)`$/.exec(row(lines, 'line') ?? '');
  const tools = /^node (.+) · daoris (.+)$/.exec(row(lines, 'tools') ?? '');

  // The words: a blank line after the heading that names where they go, then the title, a blank line and the
  // body, and a blank line before the rules.
  const opens = lines.findIndex((each) => /^a press publishes this to `[^`]+`, as your ask, in `[^`]+`:$/.test(each));
  const closes = lines.findIndex((each, at) => at > opens && /^and adds to `[^`]+`'s rules/.test(each));
  const words = opens >= 0 && closes > opens ? lines.slice(opens + 2, closes) : null;

  const rulesAt = lines.findIndex((each) => /^(and adds|added) to `[^`]+`'s rules, so its session may run the doctrine tool:$/.test(each));
  const refusedAt = lines.indexOf('refused:');
  const id = (label) => lines.map((each) => new RegExp(`^  ${label} +#([0-9a-f]+)$`).exec(each)).find(Boolean)?.[1] ?? null;
  return {
    repository: head?.[1] ?? null,
    workspace: head?.[2] ?? null,
    line: line?.[1] ?? null,
    commit: line?.[2] ?? null,
    layout: row(lines, 'layout'),
    agent: row(lines, 'agent'),
    lands: row(lines, 'lands'),
    node: tools?.[1] ?? null,
    daoris: tools?.[2] ?? null,
    title: words?.[0] ?? null,
    body: words ? words.slice(2).join('\n').trim() : null,
    rules: rulesAt < 0 ? [] : following(lines, rulesAt, '  ').filter((each) => /^Bash\(.+\)$/.test(each)),
    refusals: refusedAt < 0 ? [] : following(lines, refusedAt, '  - '),
    ask: id('ask'),
    quest: id('quest'),
    nothingPublished: lines.includes('--plan: nothing was published, and no rule was added.'),
  };
}

/**
 * What `daoris-driver register [--repository <name>]` printed (`RegisterCommand`, WSSETUP5), read back: each
 * repository's line as its outcome's word, its name and the sentence its row says, and the counts the last line gives
 * (null where it gave none). A line per repository starts with two spaces, the outcome padded to sixteen, a space, the
 * name and two spaces; every other line starts `register: `.
 */
export function readRegister(out) {
  const lines = out.split(/\r?\n/);
  const followed = lines
    .map((line) => /^ {2}(\S+) +(\S+) {2}(.+)$/.exec(line))
    .filter(Boolean)
    .map(([, outcome, repository, said]) => ({ outcome, repository, said }));
  const counts = lines
    .map((line) => /^register: (\d+) registered, (\d+) already as their lines say, (\d+) not registered\b/.exec(line))
    .find(Boolean);
  const count = (at) => (counts ? Number(counts[at]) : null);
  return { followed, registered: count(1), unchanged: count(2), refused: count(3) };
}

/**
 * The machine log's `registry.followed` lines (WSSETUP5, D124 §3.4), from what `daoris-driver logs --json` printed: each
 * as its repository's name, its outcome's word and the names of every field the line carried, in order, so a reader can
 * hold that it carried nothing else. Other events, and lines that are not the log's, are skipped.
 */
export function readFollowed(out) {
  return out.split(/\r?\n/).flatMap((line) => {
    let parsed;
    try {
      parsed = JSON.parse(line);
    } catch {
      return [];
    }
    if (parsed?.event !== 'registry.followed' || typeof parsed.data !== 'object' || parsed.data === null) return [];
    return [{ repository: parsed.data.repository, outcome: parsed.data.outcome, fields: Object.keys(parsed.data) }];
  });
}
