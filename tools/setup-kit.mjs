/**
 * The family rehearsal's set-up phase, in the parts a test can hold without a host or a driver:
 * the doctrine tool's launcher a child of Daoris finds by its bare name, the PATH that puts it first, and a
 * reading of what `daoris place <repository> --preset keep --adoption` prints (the one adoption door);
 * for the registration that follows the landing, a reading of what `daoris repository register` prints and of
 * the machine log's lines; and, for a workspace set up one repository at a time, a reading of what
 * `daoris place --workspace` prints.
 *
 * ## Why the workspace's CLI, and a launcher of the rehearsal's own
 *
 * A set-up's session runs `daoris` by its bare name, and the press refuses where a child of
 * Daoris would find none. The install's `app/bin/` launchers are the deployment rehearsal's to test,
 * so this phase puts the workspace's own CLI there instead: a launcher in scratch, first on the PATH the
 * driver is started with, which the driver hands every session it starts.
 *
 * ## A reading of another program's words
 *
 * `readPlace` reads `PlaceCommand`'s output by its shape: the head, each repository's row, the changes, the refusals,
 * the apply's sentence and the quests it asked. It shares no code with it. The CLI's `tool-test/setup-kit.test.ts`
 * holds the reading against output spelled as `PlaceCommand` writes it, so a change to those words is a change to
 * that test's fixtures too, and the family rehearsal is what runs the two against each other.
 * `readRegister`, `readFollowed`, `readEvents` and `readWorkspaceSetup` read `RegisterCommand`, `MachineLog` and
 * `WorkspaceSetupCommand` the same way.
 */
import { chmodSync, mkdirSync, writeFileSync } from 'node:fs';
import { delimiter, join } from 'node:path';

/** A set-up quest's title, the whole set-up's (`SetupQuests.SetUp`), with the day the press composed it on. */
export const SETUP_TITLE = /^Set up this repository for every agent \(\d{4}-\d{2}-\d{2}\)$/;

/**
 * The rule a press adds, verb for verb (`SetupPress.Verbs`): the doctrine tool's exact verbs as
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

/** A row of what is read (`  at once  …`): its label padded to `width` after two spaces. */
const row = (lines, label, width = 9) => {
  const lead = `  ${label.padEnd(width)}`;
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
 * What `daoris place <repository> --preset keep --adoption [--plan]` printed (`PlaceCommand`, the one adoption door), read
 * back: null for a field it did not print. `repositories` and `adoption` are the head's; `rows` each repository's line (now,
 * after, suggested); `changes` the sentences under `changes, each said once:`; `refusals` each sentence after `refused:`;
 * `nothingWritten` whether it said a plan wrote and asked nothing; `message` the apply's outcome sentence; `quests` each
 * adoption quest an apply asked, by repository.
 */
export function readPlace(out) {
  const lines = out.split(/\r?\n/);
  const headAt = lines.findIndex((line) => /^place: `[^`]+`(?:, `[^`]+`)* — /.test(line));
  const head = headAt < 0 ? null : lines[headAt];
  const changesAt = lines.indexOf('changes, each said once:');
  const refusedAt = lines.indexOf('refused:');
  return {
    repositories: head === null ? [] : [...head.slice(0, head.indexOf(' — ')).matchAll(/`([^`]+)`/g)].map((match) => match[1]),
    adoption: head !== null && /; adoption\b/.test(head),
    rows: lines.map((line) => /^ {2}`([^`]+)`: now (.*) · after (.*) · suggested (.*)$/.exec(line)).filter(Boolean)
      .map(([, repository, now, after, suggested]) => ({ repository, now, after, suggested })),
    changes: changesAt < 0 ? [] : following(lines, changesAt, '  '),
    refusals: refusedAt < 0 ? [] : following(lines, refusedAt, '  - '),
    nothingWritten: lines.includes('--plan: nothing was written, and nothing was asked.'),
    message: lines.find((line, at) => at !== headAt && line.startsWith('place: '))?.slice(7) ?? null,
    quests: lines.map((line) => /^ {2}quest {2}#([0-9a-f]+) for `([^`]+)`$/.exec(line)).filter(Boolean)
      .map(([, quest, repository]) => ({ quest, repository })),
  };
}

/**
 * What `daoris repository register [--repository <name>]` printed (`RegisterCommand`), read back: each
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
 * The machine log's `registry.followed` lines, from what `daoris logs --json` printed: each
 * as its repository's name, its outcome's word and the names of every field the line carried, in order, so a reader can
 * hold that it carried nothing else. Other events, and lines that are not the log's, are skipped.
 */
export function readFollowed(out) {
  return readEvents(out, 'registry.followed')
    .map((data) => ({ repository: data.repository, outcome: data.outcome, fields: Object.keys(data) }));
}

/**
 * The data of every machine log line of `event`, from what `daoris logs --json` printed, in the order the
 * lines are written. Other events, and lines that are not the log's, are skipped.
 */
export function readEvents(out, event) {
  return out.split(/\r?\n/).flatMap((line) => {
    let parsed;
    try {
      parsed = JSON.parse(line);
    } catch {
      return [];
    }
    return parsed?.event === event && typeof parsed.data === 'object' && parsed.data !== null ? [parsed.data] : [];
  });
}

/** The words a workspace plan says a repository's state in (`WorkspaceSetup.Word`). */
const SETUP_STATES = ['set up', 'waiting for your review', 'setting up', 'parked', 'to go', 'skipped', 'declined', 'deleted'];

/**
 * What `daoris place --workspace <name> --adoption …` printed (`WorkspaceSetupCommand`), read back. `rows` is the
 * list a press works from (what touched each, its state now, and each refusal under it); `atOnce`, `pilot` and `agent`
 * the pacing rows; `rules` the ones a press would add or added; `refusals` the press's own, after `refused:`; `messages`
 * each `place: …` sentence that is not a head (a press's, a steer's); `standing` where each repository of a plan stands,
 * with `summary` its head line; `nothingWritten` whether a plan said it wrote, published and added nothing.
 */
export function readWorkspaceSetup(out) {
  const lines = out.split(/\r?\n/);
  const head = lines.map((line) => /^place: workspace `([^`]+)`, /.exec(line)).find(Boolean);
  const rows = [];
  const standing = [];
  for (const line of lines) {
    const listed = /^ {0,2}\d+ {2}(\S+) +(\d+) {2,}(\d+) {2,}(\d+) {2}(.+)$/.exec(line);
    if (listed) {
      rows.push({
        repository: listed[1], asked: Number(listed[2]), read: Number(listed[3]), sessions: Number(listed[4]), now: listed[5],
        refusals: [],
      });
      continue;
    }
    const refused = /^ {7}refused: (.+)$/.exec(line);
    if (refused && rows.length > 0) {
      rows.at(-1).refusals.push(refused[1]);
      continue;
    }
    const stands = /^ {0,2}\d+ {2}(\S+) +(.+)$/.exec(line);
    const state = stands && SETUP_STATES.find((word) => stands[2] === word || stands[2].startsWith(`${word} — `));
    if (state) {
      standing.push({ repository: stands[1], state, said: stands[2] === state ? null : stands[2].slice(state.length + 3) });
    }
  }

  const rulesAt = lines.findIndex((line) => /^(a press adds|added) to workspace `[^`]+`'s rules, .*doctrine tool:$/.test(line));
  const refusedAt = lines.indexOf('refused:');
  return {
    workspace: head?.[1] ?? null,
    rows,
    atOnce: row(lines, 'at once', 10),
    pilot: row(lines, 'pilot', 10),
    agent: row(lines, 'agent', 10),
    rules: rulesAt < 0 ? [] : following(lines, rulesAt, '  ').filter((each) => /^Bash\(.+\)$/.test(each)),
    refusals: refusedAt < 0 ? [] : following(lines, refusedAt, '  - '),
    messages: lines.filter((line) => line.startsWith('place: ') && !/^place: workspace `/.test(line)).map((line) => line.slice(7)),
    standing,
    summary: lines.find((line) => line.startsWith('Setting up — ')) ?? null,
    nothingWritten: lines.includes('--plan: nothing was written, published or added.'),
  };
}
