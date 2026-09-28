// `daoris driver` — this machine's standing driving choices, from a terminal (D50).
//
// The file is the home's `driver.json` (`$DAORIS_HOME`, D63) and the driver re-reads it every tick (D46 §6), which is what
// makes management parity cheap here: the desktop's checkboxes and these verbs edit the same file,
// and hand-editing keeps working because the FILE — not the surface — is the truth. A headless
// machine running `daoris-driver` has no checkbox and still has to be told what it may drive.
//
// It is a MANAGEMENT command and it is entirely OFFLINE: it reads and writes one file under the
// home and talks to nothing. It does not even spawn, unlike its sibling `toolchain.ts`.
//
// EVERY EDIT PRESERVES WHAT IT DID NOT TOUCH. The driver writes fields this build has no verb for
// (`pollSeconds`, the per-adapter `commands` map), and an editor that rewrote the file from its own
// idea of the shape would silently delete the command that makes the stub run.

import { requireHomeFile } from './home.ts';
import { DaorisError } from './errors.ts';
import { flagValue, operands } from './args.ts';
import { readJsonObject, writeJsonAtomic } from './fsx.ts';
import { TOOLCHAINS } from './toolchain.ts';
import type { CommandArgs } from './types.ts';
import type { ExitCode } from './errors.ts';

export const PATH_VARIABLE = 'DAORIS_DRIVER_CONFIG';

/** The file's path: the override, or the conventional home beside the remotes map. */
export function driverConfigPath(env: Record<string, string | undefined> = process.env): string {
  return env[PATH_VARIABLE] ?? requireHomeFile(env, 'driver.json');
}

/** The choices, plus everything else the file held — this is an editor, not the file's owner. */
export interface DriverChoices {
  drivable: string[];
  holds: string[];
  /** Repositories whose sessions open their own worktree instead of the registered root (D51). */
  trees: string[];
  cap: number;
  adapter: string;
  /** Whether this machine says so when a session parks or ends unasked (SURF5b). */
  notify: boolean;
  /** How many failed sessions park a quest (DRV6). `0` never parks — the behaviour before it existed. */
  strikes: number;
  /** Quests the person restarted, and the failure count each was restarted at. */
  forgiven: Record<string, number>;
  /** The harness an ask's intake session runs on (INT4b, D65 §1b) — null, and no intake runs. */
  intakeAdapter: string | null;
  /** How long a session may run before the driver kills it — null is the driver's own default. */
  timeoutMinutes: number | null;
  /** The line each repository's work grows from and lands on, as the person set it (WSR2). */
  lines: Record<string, string>;
  /** A workspace's line, for every repository in it that sets none of its own. */
  workspaceLines: Record<string, string>;
  /** How each repository's work lands, as the person set it (WSR1, D87). */
  landings: Record<string, LandingRule>;
  /** A workspace's landing rule, for every repository in it that sets none of its own. */
  workspaceLandings: Record<string, LandingRule>;
  rest: Record<string, unknown>;
}

/** The driver's own default for `timeoutMinutes` (`DriverConfig.Empty`) — the twin says 30 too. */
export const DEFAULT_TIMEOUT_MINUTES = 30;

/** Drives nothing, holds nothing — the safe shape silence takes, matching the driver's own default. */
const EMPTY: DriverChoices = {
  drivable: [], holds: [], trees: [], cap: 2, adapter: 'claude-code', notify: true,
  strikes: 3, forgiven: {}, intakeAdapter: null, timeoutMinutes: null, lines: {}, workspaceLines: {},
  landings: {}, workspaceLandings: {}, rest: {},
};

/**
 * How a session's work lands (WSR1, D87): merged into the line, or put on a branch the pattern names,
 * for the person to push. Pushing and opening a pull request is a plugin's form, not yet built (WSR4).
 */
export interface LandingRule {
  form: string;
  pattern?: string;
  /** Once a press lands the work, its tree and branch go — behind the driver's proof (D88). Absent is off. */
  tidy?: boolean;
}

/** What a pattern may say — the driver's `LandingRules.Placeholders`. One of the first two is required. */
const PLACEHOLDERS = ['quest', 'session', 'slug', 'repository'];

/** What a pattern is tried against before it is kept — the driver tries the same names. */
const SAMPLE: Record<string, string> = { quest: '0fda18', session: 's1a2b3c4', slug: 'sample-work', repository: 'engine' };

/**
 * What is wrong with a landing rule, in a sentence, or null when it can land work. `LandingRules.Problem`
 * in the driver is the twin, and both carry the same pattern table.
 */
export function landingProblem(rule: LandingRule): string | null {
  if (rule.form === 'merge') return null;
  if (rule.form !== 'branch') {
    return `\`${rule.form}\` is not a way work lands here — \`merge\` or \`branch\`. `
      + 'Pushing and opening a pull request is a plugin\'s to do, and none can yet.';
  }

  const pattern = rule.pattern ?? '';
  if (pattern.trim().length === 0) return 'a branch pattern needs a name — e.g. `feature/{quest}-{slug}`.';

  for (const [used, name] of pattern.matchAll(/\{([^{}]*)\}/g)) {
    if (!PLACEHOLDERS.includes(name!)) {
      return `\`${used}\` is not something a pattern can say — ${PLACEHOLDERS.map((p) => `\`{${p}}\``).join(', ')}.`;
    }
  }

  if (!pattern.includes('{quest}') && !pattern.includes('{session}')) {
    return 'a pattern needs `{quest}` or `{session}`, or every session\'s work would be put on one branch.';
  }

  const sample = PLACEHOLDERS.reduce((text, name) => text.replaceAll(`{${name}}`, SAMPLE[name]!), pattern);
  return sample.includes('{') || sample.includes('}') || !isBranchName(sample)
    ? `\`${pattern}\` does not make a branch name git would take — it gives \`${sample}\`.`
    : null;
}

/**
 * A name the line may be set to (WSR2): git's own rules for a ref name, the ones a person could break
 * by typing. `BranchName.IsValid` in the driver is the twin, and both carry the same test table.
 */
export function isBranchName(name: string): boolean {
  return name.length > 0 && name.length <= 200
    && name !== '@'
    && !name.startsWith('-') && !name.startsWith('/') && !name.endsWith('/') && !name.endsWith('.')
    && !name.includes('..') && !name.includes('//') && !name.includes('@{')
    && ![...name].some(refused)
    && name.split('/').every((part) => !part.startsWith('.') && !part.endsWith('.lock'));

  // The driver's `char.IsWhiteSpace || char.IsControl`, spelled for JavaScript: C0 and C1 controls,
  // any space, and the characters git gives a meaning to.
  function refused(c: string): boolean {
    const code = c.codePointAt(0)!;
    return code < 0x20 || (code >= 0x7f && code <= 0x9f) || /\s/u.test(c) || '~^:?*[\\'.includes(c);
  }
}

/**
 * The choices as they stand.
 *
 * @remarks
 * A missing file is a machine that has opted nothing in — the driver's own reading of it, held here
 * too because two artefacts disagreeing about what an absent file means is exactly how a machine ends
 * up driving something nobody opted in.
 */
export function readDriverChoices(path = driverConfigPath()): DriverChoices {
  const { value: parsed, problem } = readJsonObject(path);
  if (problem !== null) {
    // A torn or hand-mangled file is reported, never silently replaced: rewriting it would destroy
    // whatever the person was in the middle of typing, and the driver reads the same file.
    throw new DaorisError(`${problem}. Fix it, or delete it to start from nothing — `
      + 'this command will not overwrite a file it could not understand.');
  }
  if (parsed === null) return { ...EMPTY, lines: {}, workspaceLines: {}, landings: {}, workspaceLandings: {}, rest: {} };

  const {
    drivable, holds, trees, cap, adapter, notify, strikes, forgiven, intakeAdapter, timeoutMinutes,
    lines, workspaceLines, landings, workspaceLandings, ...rest
  } = parsed;
  return {
    drivable: names(drivable),
    holds: names(holds),
    trees: names(trees),
    cap: typeof cap === 'number' && cap >= 1 ? Math.floor(cap) : EMPTY.cap,
    adapter: typeof adapter === 'string' && adapter.length > 0 ? adapter : EMPTY.adapter,
    // 🔴 Absent means the DEFAULT, and this is the OPPOSITE reading from `notify` directly above.
    // A machine whose file predates this field is the one that has been driving unattended longest,
    // so silence there must not mean "never park". Zero is settable and means exactly that.
    strikes: typeof strikes === 'number' && strikes >= 0 ? Math.floor(strikes) : EMPTY.strikes,
    forgiven: marks(forgiven),
    // 🔴 Absent means ON, the same reading the driver makes (SURF5b): every machine that already
    // has this file predates the field, and taking silence for "off" would ship the feature
    // switched off on exactly the machines that have been driving longest.
    notify: typeof notify === 'boolean' ? notify : EMPTY.notify,
    // 🔴 Absent means OFF — the opposite of `notify`, the driver's own reading (INT4b): an intake
    // spends a real login on every ask, so it runs only where a person named the harness for it.
    intakeAdapter: typeof intakeAdapter === 'string' && intakeAdapter.trim().length > 0
      ? intakeAdapter.trim() : null,
    // Read as the driver reads it (`DriverConfig`: a whole number, lifted to at least one), so the
    // listing never names a number the driver would not use.
    timeoutMinutes: typeof timeoutMinutes === 'number' && Number.isInteger(timeoutMinutes)
      ? Math.max(1, timeoutMinutes) : null,
    // An entry git would refuse is not read, as the driver does not read it: a line the driver would
    // ignore must not be listed as if it held.
    lines: branchMap(lines),
    workspaceLines: branchMap(workspaceLines),
    // A rule that could not land work is not read, as the driver does not read it.
    landings: ruleMap(landings),
    workspaceLandings: ruleMap(workspaceLandings),
    rest,
  };
}

/** Write them back, preserving anything this build did not put there. */
export function writeDriverChoices(path: string, choices: DriverChoices): void {
  writeJsonAtomic(path, {
    ...choices.rest,
    drivable: choices.drivable,
    holds: choices.holds,
    trees: choices.trees,
    cap: choices.cap,
    adapter: choices.adapter,
    notify: choices.notify,
    strikes: choices.strikes,
    forgiven: choices.forgiven,
    // Written only when named — absent IS off, and the driver writes it the same way.
    ...(choices.intakeAdapter ? { intakeAdapter: choices.intakeAdapter } : {}),
    // Written only when set: absent is the driver's own default, and an edit elsewhere must not
    // pin today's default into a file that never chose it.
    ...(choices.timeoutMinutes !== null ? { timeoutMinutes: choices.timeoutMinutes } : {}),
    // Written only when set (WSR2): absent is the checkout's guess, and the driver writes them the same way.
    ...(Object.keys(choices.lines).length > 0 ? { lines: choices.lines } : {}),
    ...(Object.keys(choices.workspaceLines).length > 0 ? { workspaceLines: choices.workspaceLines } : {}),
    // Written only when set (WSR1): absent is the merge it always was.
    ...(Object.keys(choices.landings).length > 0 ? { landings: choices.landings } : {}),
    ...(Object.keys(choices.workspaceLandings).length > 0 ? { workspaceLandings: choices.workspaceLandings } : {}),
  });
}

/**
 * Read or change what this machine drives.
 *
 * @remarks
 * `drive`/`undrive` and `hold`/`resume` are pairs of verbs rather than one verb with a boolean,
 * because they mean different things: opting a repository IN is a standing decision, and holding one
 * is a pause on something already opted in. A surface that collapsed them would lose that a held
 * repository is still drivable.
 */
export function commandDriver({ argv, write }: CommandArgs): ExitCode {
  const verb = argv[0] ?? 'list';
  const path = driverConfigPath();
  const choices = readDriverChoices(path);

  switch (verb) {
    case 'list':
      return list();

    case 'drive':
      return toggle('drivable', named(argv, 'drive'), true);

    case 'undrive':
      return toggle('drivable', named(argv, 'undrive'), false);

    case 'hold':
      return toggle('holds', named(argv, 'hold'), true);

    case 'resume':
      return toggle('holds', named(argv, 'resume'), false);

    // One flag with two directions rather than a verb pair, unlike drive/undrive and hold/resume:
    // those pairs mean different things (a standing decision vs a pause on one), where this is a
    // single standing switch and inventing two words for it would imply a difference that is not there.
    case 'trees': {
      const repository = named(argv, 'trees');
      const direction = argv.slice(1).find((token) => token === 'on' || token === 'off');
      if (!direction) {
        throw new DaorisError(
          '`driver trees` needs on|off — e.g. `daoris driver trees aurora-engine on`.');
      }

      const on = direction === 'on';
      const kept = choices.trees.filter((name) => name.toLowerCase() !== repository.toLowerCase());
      writeDriverChoices(path, { ...choices, trees: on ? [...kept, repository] : kept });

      write(on
        ? `daoris: sessions in \`${repository}\` open their own worktree (D51).`
        : `daoris: sessions in \`${repository}\` run in its registered root again.`);
      if (on) {
        write('  A fresh tree holds nothing git does not track — no installed dependencies, no build');
        write('  outputs — so a session pays that repository\'s own setup cost per tree. In exchange,');
        write('  your uncommitted work in the root no longer holds the driver.');
      }

      write(`  Written to ${path} — the driver re-reads it every tick, so nothing restarts.`);
      return 0;
    }

    // One flag with two directions, like `trees` and for the same reason. The desktop has a
    // checkbox for this; a machine with no screen has this verb (D50's two doors, SURF5b).
    case 'notify': {
      const direction = argv.slice(1).find((token) => token === 'on' || token === 'off');
      if (!direction) {
        throw new DaorisError('`driver notify` needs on|off — e.g. `daoris driver notify off`.');
      }

      const on = direction === 'on';
      writeDriverChoices(path, { ...choices, notify: on });

      write(on
        ? 'daoris: this machine says so when a session parks or ends without you asking.'
        : 'daoris: this machine will not interrupt you about sessions.');
      if (!on) {
        write('  The records still say everything they said — this is about being TOLD, not about');
        write('  what is recorded. A parked session waits for you either way.');
      }

      write(`  Written to ${path} — the driver re-reads it every tick, so nothing restarts.`);
      return 0;
    }

    case 'cap': {
      const value = Number(named(argv, 'cap'));
      if (!Number.isInteger(value) || value < 1) {
        throw new DaorisError(
          '`driver cap` needs a whole number of concurrent sessions, at least 1 — e.g. `daoris driver cap 3`.');
      }

      writeDriverChoices(path, { ...choices, cap: value });
      write(`daoris: this machine runs at most ${value} session(s) at once.`);
      return 0;
    }

    // DRV6. A number rather than on|off, because the useful question is not "should it give up" but
    // "after how many" — and 0 is a real answer to it, not the absence of one.
    case 'strikes': {
      const value = Number(named(argv, 'strikes'));
      if (!Number.isInteger(value) || value < 0) {
        throw new DaorisError(
          '`driver strikes` needs a whole number of failed sessions, 0 or more — '
          + 'e.g. `daoris driver strikes 3`, or `0` to never park a quest.');
      }

      writeDriverChoices(path, { ...choices, strikes: value });
      write(value === 0
        ? 'daoris: this machine never parks a quest — it keeps trying, as it did before this setting.'
        : `daoris: a quest is parked after ${value} failed session(s), and waits for you.`);
      if (value === 0) {
        write('  🔴 A quest that fails for a reason no retry can fix will be started again every tick,');
        write('     and every start spends a real login. That is the behaviour this setting exists for.');
      } else {
        write('  A failure is a session that ended without landing anything. A stand-down, a decline,');
        write('  a stop and a held repository are not failures and never count toward this.');
      }

      write(`  Written to ${path} — the driver re-reads it every tick, so nothing restarts.`);
      return 0;
    }

    // Not `unpark` or `forgive`: the person is saying "try this again", and the mark records where
    // to count from rather than erasing what happened — the records still say it.
    case 'retry': {
      const quest = named(argv, 'retry').replace(/^#/, '');
      const at = argv.indexOf('--at');
      const mark = at !== -1 && argv[at + 1] ? Number(argv[at + 1]) : choices.strikes;
      if (!Number.isInteger(mark) || mark < 0) {
        throw new DaorisError('`driver retry --at` needs a whole number of failures to count from.');
      }

      writeDriverChoices(path, { ...choices, forgiven: { ...choices.forgiven, [quest]: mark } });
      write(`daoris: quest \`#${quest}\` may be started again.`);
      write(`  Counting from ${mark} failure(s) — what already happened is still in the records, and`);
      write(`  ${choices.strikes || 'no'} more failure(s) will park it again.`);
      write(`  Written to ${path} — the driver re-reads it every tick, so nothing restarts.`);
      return 0;
    }

    // How long one session may run before the driver kills it. A real development turn — the work,
    // then the repository's install, build and tests — outlasted the default on the first real run,
    // and the killed session left its quest to be carried on (D80). Minutes, because that is the
    // unit a person thinks about a turn in.
    case 'timeout': {
      const value = Number(named(argv, 'timeout'));
      if (!Number.isInteger(value) || value < 1) {
        throw new DaorisError(
          '`driver timeout` needs a whole number of minutes, at least 1 — e.g. `daoris driver timeout 120`.');
      }

      writeDriverChoices(path, { ...choices, timeoutMinutes: value });
      write(`daoris: a session on this machine may run ${value} minutes before the driver ends it.`);
      write('  One that runs out is recorded as failed, and its quest is carried on in the same tree at a');
      write('  later tick, until the strikes park it.');
      write(`  Written to ${path} — the driver re-reads it every tick; a session already running keeps its own.`);
      return 0;
    }

    case 'adapter': {
      const adapter = named(argv, 'adapter');
      // Not refused against a list: the driver's adapter set is the driver's, and the CLI naming a
      // shorter list than it has would refuse a harness that actually works. The driver errors
      // naming what exists (D23), which is the answer that comes from the side that knows.
      writeDriverChoices(path, { ...choices, adapter });
      write(`daoris: sessions on this machine spawn via \`${adapter}\`.`);
      if (!(adapter in TOOLCHAINS)) {
        write('  Daoris manages no toolchain for that name — `daoris agent list` shows the ones it does.');
      }

      return 0;
    }

    // Which harness answers an ask with an intake session (INT4b, D65 §1b), or `off`. A NAME rather
    // than on|off: which harness answers asks and which does the work are two choices, and changing
    // the second must not quietly move the first. The desktop's `SET_INTAKE` is the other door (D50).
    case 'intake': {
      const named = argv.slice(1).find((token) => !token.startsWith('--'));
      if (!named) {
        throw new DaorisError(
          '`driver intake` needs <adapter>|off — e.g. `daoris driver intake claude-code-acp`, or `off` '
          + 'to answer asks by declarations only.');
      }

      const off = named === 'off';
      writeDriverChoices(path, { ...choices, intakeAdapter: off ? null : named });
      if (off) {
        write('daoris: asks on this machine are answered by declarations only — proposed, never published unasked.');
        write('  An intake already waiting on you still waits; answering its ask ends it.');
      } else {
        write(`daoris: an ask the declarations do not settle opens an intake session via \`${named}\`.`);
        write('  It reads the workspace\'s declarations, publishes the quests itself, and asks you where they do not');
        write('  settle it. 🔴 Every intake spends a real login on that harness\'s account.');
        if (!(named in TOOLCHAINS)) {
          write('  Daoris manages no toolchain for that name — `daoris agent list` shows the ones it does.');
        }
      }

      write(`  Written to ${path} — the driver re-reads it every tick, so nothing restarts.`);
      return 0;
    }

    // The line a repository's work grows from and lands on (WSR2) — for one repository, or with
    // `--workspace` for every repository in it that sets none of its own. Unset, the checkout's own
    // guess stands (`origin/HEAD`, else main, else master), which is what it always was.
    case 'line': {
      const workspace = flagValue(argv, '--workspace');
      const clear = argv.includes('--clear');
      const [, first, second] = operands(argv, new Set(['--workspace']));
      const name = workspace ?? first;
      const branch = workspace ? first : second;
      if (!name || (!clear && !branch)) {
        throw new DaorisError(
          '`driver line` needs <repository>|--workspace <name>, then <branch>|--clear — '
          + 'e.g. `daoris driver line aurora-engine develop`.');
      }

      if (!clear && !isBranchName(branch!)) {
        throw new DaorisError(`\`${branch}\` is not a branch name git would take.`);
      }

      const set = clear ? null : branch!;
      writeDriverChoices(path, workspace
        ? { ...choices, workspaceLines: withEntry(choices.workspaceLines, workspace, set) }
        : { ...choices, lines: withEntry(choices.lines, name, set) });

      const whose = workspace ? `repositories in the workspace \`${workspace}\`` : `\`${name}\``;
      write(set === null
        ? `daoris: ${whose} take${workspace ? '' : 's'} ${workspace ? 'their' : 'its'} line from `
          + `${workspace ? 'their own setting or ' : 'its workspace, else '}the checkout again.`
        : `daoris: work in ${whose} grows from \`${set}\` and lands on it.`);
      if (set !== null && workspace) {
        write('  A repository with a line of its own keeps it — `daoris driver line <repository> --clear` hands it back.');
      }

      write('  A session already running keeps the line it started from.');
      write(`  Written to ${path} — the driver re-reads it every tick, so nothing restarts.`);
      return 0;
    }

    // How work lands (WSR1, D87) — for one repository, or with `--workspace` for every repository in
    // it that sets none of its own: `merge` into the line, or `branch <pattern>` for the person to push.
    // Unset, it is the merge it always was. Daoris never pushes; that form is a plugin's (WSR4).
    case 'landing': {
      const workspace = flagValue(argv, '--workspace');
      const clear = argv.includes('--clear');
      const words = operands(argv, new Set(['--workspace'])).slice(1);
      const name = workspace ?? words.shift();
      const [form, pattern] = words;
      if (!name || (!clear && !form)) {
        throw new DaorisError(
          '`driver landing` needs <repository>|--workspace <name>, then merge|branch <pattern>|--clear — '
          + 'e.g. `daoris driver landing --workspace aurora branch "feature/{quest}-{slug}"`.');
      }

      const rule: LandingRule | null = clear ? null : {
        form: form!,
        ...(pattern !== undefined ? { pattern } : {}),
        ...(argv.includes('--tidy') ? { tidy: true } : {}),
      };
      const problem = rule === null ? null : landingProblem(rule);
      if (problem !== null) throw new DaorisError(problem);

      writeDriverChoices(path, workspace
        ? { ...choices, workspaceLandings: withEntry(choices.workspaceLandings, workspace, rule && kept(rule)) }
        : { ...choices, landings: withEntry(choices.landings, name, rule && kept(rule)) });

      const whose = workspace ? `repositories in the workspace \`${workspace}\`` : `\`${name}\``;
      if (rule === null) {
        write(`daoris: work in ${whose} lands as ${workspace ? 'each one\'s own rule, else ' : 'its workspace\'s rule, else '}`
          + 'the merge into its line.');
      } else if (rule.form === 'merge') {
        write(`daoris: work in ${whose} is merged into the line when you accept it, in the repository's own checkout.`);
      } else {
        write(`daoris: work in ${whose} is put on a branch named \`${rule.pattern}\` when you accept it —`);
        write('  from the session\'s branch, with nothing merged and no checkout touched. You push it and open');
        write('  the pull request: Daoris never pushes (D87).');
      }

      if (rule?.tidy) {
        write('  Once a press lands the work, its tree and its branch go — only where git proves the work is on a');
        write('  branch of yours. Without --tidy the tree stays until you discard it.');
      }

      if (rule !== null && workspace) {
        write('  A repository with a rule of its own keeps it — `daoris driver landing <repository> --clear` hands it back.');
      }

      write(`  Written to ${path} — the review reads it at each press, so nothing restarts.`);
      return 0;
    }

    default:
      throw new DaorisError(
        `unknown driver verb '${verb}' — one of: list, drive, undrive, hold, resume, trees, line, landing, notify, `
        + 'strikes, retry, timeout, cap, adapter, intake');
  }

  function list(): ExitCode {
    write(`daoris: ${path}`);
    write(`  adapter    ${choices.adapter}`);
    write(`  cap        ${choices.cap} concurrent session(s)`);
    write(`  strikes    ${choices.strikes === 0
      ? 'off — a quest is never parked, and it keeps trying at the cost of a login each time'
      : `${choices.strikes} failed session(s) park a quest`}`);
    for (const [quest, mark] of Object.entries(choices.forgiven)) {
      write(`  retried    #${quest}  (counting from ${mark} failure(s))`);
    }

    write(choices.timeoutMinutes === null
      ? `  timeout    ${DEFAULT_TIMEOUT_MINUTES} minutes a session may run  (the default — \`daoris driver timeout <minutes>\` changes it)`
      : `  timeout    ${choices.timeoutMinutes} minutes a session may run`);

    write(`  notify     ${choices.notify ? 'on' : 'off'}`
      + `  (a session parking, or ending without you asking${choices.notify ? '' : ' — not said'})`);
    write(choices.intakeAdapter
      ? `  intake     ${choices.intakeAdapter}  (an ask the declarations do not settle opens a session — a login each)`
      : '  intake     off — asks are answered by declarations only; `daoris driver intake <adapter>` names a harness');

    if (choices.drivable.length === 0) {
      write('  drivable   nothing — this machine drives no repository, which is the default (D46 §2).');
      write('             `daoris driver drive <repository>` opts one in.');
    }

    for (const repository of choices.drivable) {
      const held = choices.holds.some((name) => name.toLowerCase() === repository.toLowerCase());
      const trees = choices.trees.some((name) => name.toLowerCase() === repository.toLowerCase());
      write(`  drivable   ${repository}`
        + `${held ? '  (held by you — `daoris driver resume` releases it)' : ''}`
        + `${trees ? '  (sessions open their own tree — D51)' : ''}`);
    }

    // Trees on something not drivable is standing configuration, not an error — the desktop's chat
    // door reads it too — but naming it keeps the list the whole truth.
    for (const repository of choices.trees) {
      if (!choices.drivable.some((name) => name.toLowerCase() === repository.toLowerCase())) {
        write(`  trees      ${repository}  (sessions there open their own tree when anything spawns one)`);
      }
    }

    for (const [repository, branch] of Object.entries(choices.lines)) {
      write(`  line       ${repository}  ${branch}`);
    }

    for (const [workspace, branch] of Object.entries(choices.workspaceLines)) {
      write(`  line       workspace ${workspace}  ${branch}  (for each repository there that sets none)`);
    }

    const spelled = (rule: LandingRule) =>
      (rule.form === 'branch' ? `branch ${rule.pattern}` : rule.form) + (rule.tidy ? ', tidy' : '');
    for (const [repository, rule] of Object.entries(choices.landings)) {
      write(`  landing    ${repository}  ${spelled(rule)}`);
    }

    for (const [workspace, rule] of Object.entries(choices.workspaceLandings)) {
      write(`  landing    workspace ${workspace}  ${spelled(rule)}  (for each repository there that sets none)`);
    }

    // A hold on something not opted in is inert, and saying so is the point: it reads as protection
    // and is not. Reported even when NOTHING is drivable — which is exactly the machine where a
    // person is most likely to believe a hold is what is stopping things.
    for (const held of choices.holds) {
      if (!choices.drivable.some((name) => name.toLowerCase() === held.toLowerCase())) {
        write(`  held       ${held}  (not drivable anyway — the hold changes nothing)`);
      }
    }

    return 0;
  }

  function toggle(field: 'drivable' | 'holds', repository: string, present: boolean): ExitCode {
    const kept = choices[field].filter((name) => name.toLowerCase() !== repository.toLowerCase());
    const next = present ? [...kept, repository] : kept;
    writeDriverChoices(path, { ...choices, [field]: next });

    if (field === 'drivable') {
      write(present
        ? `daoris: this machine may drive \`${repository}\`.`
        : `daoris: this machine no longer drives \`${repository}\`.`);
      if (present) {
        write('  Driving is additive (D46 §2): outside sessions and hand work in that repository are');
        write('  untouched, and a quest somebody else already took is never the driver\'s to start.');
      }
    } else {
      write(present
        ? `daoris: \`${repository}\` is held — still drivable, not now.`
        : `daoris: \`${repository}\` is released; the driver may start it again.`);
    }

    write(`  Written to ${path} — the driver re-reads it every tick, so nothing restarts.`);
    return 0;
  }

  function named(args: string[], verb: string): string {
    // A flag's value is never the name (REV3): `retry --at 2 42` read `2` as the quest.
    const name = operands(args, new Set(['--at']))[1];
    if (name !== undefined) return name;

    throw new DaorisError(
      `\`driver ${verb}\` needs a name — e.g. \`daoris driver ${verb} aurora-engine\`.`);
  }
}

/** The forgiveness marks, as a map of quest id to the failure count it was restarted at. */
function marks(value: unknown): Record<string, number> {
  if (!value || typeof value !== 'object' || Array.isArray(value)) return {};
  const held: Record<string, number> = {};
  for (const [quest, mark] of Object.entries(value as Record<string, unknown>)) {
    if (typeof mark === 'number' && Number.isInteger(mark) && mark > 0) held[quest] = mark;
  }

  return held;
}

/** A map of names to branch names; an entry git would not take is skipped, as the driver skips it. */
function branchMap(value: unknown): Record<string, string> {
  if (!value || typeof value !== 'object' || Array.isArray(value)) return {};
  const held: Record<string, string> = {};
  for (const [name, branch] of Object.entries(value as Record<string, unknown>)) {
    if (typeof branch === 'string' && isBranchName(branch)) held[name] = branch;
  }

  return held;
}

/** The map with `key` set to `value`, or removed — matched without case, as the driver matches it. */
function withEntry<T>(map: Record<string, T>, key: string, value: T | null): Record<string, T> {
  const kept = Object.fromEntries(Object.entries(map).filter(([name]) => name.toLowerCase() !== key.toLowerCase()));
  return value === null ? kept : { ...kept, [key]: value };
}

/** A map of names to landing rules; one that could not land work is skipped, as the driver skips it. */
function ruleMap(value: unknown): Record<string, LandingRule> {
  if (!value || typeof value !== 'object' || Array.isArray(value)) return {};
  const held: Record<string, LandingRule> = {};
  for (const [name, rule] of Object.entries(value as Record<string, unknown>)) {
    if (!rule || typeof rule !== 'object') continue;
    const { form, pattern, tidy } = rule as Record<string, unknown>;
    const read: LandingRule = {
      form: typeof form === 'string' ? form : '',
      ...(typeof pattern === 'string' ? { pattern } : {}),
      ...(tidy === true ? { tidy: true } : {}),
    };
    if (landingProblem(read) === null) held[name] = kept(read);
  }

  return held;
}

/** A rule as it is kept: a merge carries no pattern, and the tidy only when on — the driver keeps it the same way. */
function kept(rule: LandingRule): LandingRule {
  return {
    ...(rule.form === 'merge' ? { form: 'merge' } : { form: rule.form, pattern: rule.pattern! }),
    ...(rule.tidy ? { tidy: true } : {}),
  };
}

function names(value: unknown): string[] {
  return Array.isArray(value) ? value.filter((name): name is string => typeof name === 'string' && name.length > 0) : [];
}
