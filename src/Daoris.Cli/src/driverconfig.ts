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
//
// A name or an id the file holds (a repository, a workspace, a quest, an ask, a plugin) is matched "in any case" as the
// driver's `OrdinalIgnoreCase` matches it, through `casefold.ts` (CASEFOLD1): `straße` is not `STRASSE`, and `İzmir` is not
// an `i` with a dot above, which a lowered `İzmir` would have met.

import { dirname } from 'node:path';
import { requireHomeFile } from './home.ts';
import { DaorisError } from './errors.ts';
import { flagValue, operands } from './args.ts';
import { readJsonObject, writeJsonAtomic } from './fsx.ts';
import { findName, sameName } from './casefold.ts';
import { isoMoment } from './cooling.ts';
import { readPlugins, type PluginCatalog } from './plugins.ts';
import { normalizeWorkspace } from './remotemap.ts';
import { TOOLCHAINS } from './toolchain.ts';
import { failuresOf, type RecordsReader } from './strikes.ts';
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
  /**
   * Quests the person released from their stop (SESSUX1b, D126 §3.4), each against the session they stopped: a later stop
   * holds the quest again. The driver's `DriverConfig.Released` is the twin, read by the same table.
   */
  released: Record<string, string>;
  /**
   * The asks paused on this machine (PAUSE1a, D132 point 5, design §2.5), each with when and the stops its pause made, read
   * for `list` by the driver's table (`DriverConfig.PausedAsks`). 🔴 Never written from here: pausing and resuming are
   * `daoris-driver`'s, so the file's own value stays in `rest` and goes back exactly as it was written.
   */
  pausedAsks: Record<string, WorkPause>;
  /** The quests paused on this machine on their own: read, and kept as written, as `pausedAsks` is. */
  pausedQuests: Record<string, WorkPause>;
  /** The harness an ask's intake session runs on (INT4b, D65 §1b) — null, and no intake runs. */
  intakeAdapter: string | null;
  /** The agent Ask Daoris runs on (HELP1, D89) — null, and it offers only its starters. */
  helperAdapter: string | null;
  /** How long a session may run before the driver kills it — null is the driver's own default. */
  timeoutMinutes: number | null;
  /**
   * How long an account cools, in minutes, when its agent's limit names no time this reads (TOOL4e, D125 §2.2) — null is
   * the default hour. The driver's `DriverConfig.CoolOffMinutes` is the twin, read by the same table.
   */
  cooloff: number | null;
  /** The line each repository's work grows from and lands on, as the person set it (WSR2). */
  lines: Record<string, string>;
  /** A workspace's line, for every repository in it that sets none of its own. */
  workspaceLines: Record<string, string>;
  /** How each repository's work lands, as the person set it (WSR1, D87). */
  landings: Record<string, LandingRule>;
  /** A workspace's landing rule, for every repository in it that sets none of its own. */
  workspaceLandings: Record<string, LandingRule>;
  /**
   * Whether each repository's checkout is read by agents outside it (READ1, D107) — absent takes its
   * workspace's, then on. Only the driver resolves it (`Across.cs`); this editor sets and lists.
   */
  readAcross: Record<string, boolean>;
  /** A workspace's reading across, for every repository in it that sets none of its own. */
  workspaceReadAcross: Record<string, boolean>;
  /** The declared relationships (D107): the repositories each repository's sessions may also write into. */
  writeAcross: Record<string, string[]>;
  /**
   * What the person has told this machine holds for every session in a repository (KNOWUSE1b, D135 §3), in their words, with
   * when it was set. The driver's `DriverConfig.Standing` is the twin, read by the same table.
   */
  standing: Record<string, StandingAnswer>;
  /**
   * The language each repository's sessions are asked to write to the person in (LANG1c, D142 point 7), a code of
   * `SESSION_LANGUAGES`; it wins over its workspace's. The driver's `DriverConfig.Languages` is the twin, read by the same table.
   */
  languages: Record<string, string>;
  /** A workspace's session language, for every repository in it that sets none, and for its intake. */
  workspaceLanguages: Record<string, string>;
  rest: Record<string, unknown>;
}

/**
 * The closed table of session languages (LANG1c, D142 point 7): each code and the name the line gives the agent — the driver's
 * `SessionLanguages.Table`, a deliberate copy, held row for row by both sides' tests. Adding a language is a row in both, and
 * needs no window catalogue: a session may write in a language the window does not speak.
 */
export const SESSION_LANGUAGES: Readonly<Record<string, string>> = {
  en: 'English',
  zh: 'Simplified Chinese (简体中文)',
};

/** A session language as it resolves (LANG1c): the table's code, the name the line gives it, and where it was set. */
export interface SessionLanguage {
  code: string;
  name: string;
  source: 'repository' | 'workspace';
}

/** The table's code a person's spelling names, in any case and without the spaces around it, or null — `SessionLanguages.Code`. */
export function languageCode(spelled: unknown): string | null {
  if (typeof spelled !== 'string') return null;
  const code = spelled.trim().toLowerCase();
  return Object.hasOwn(SESSION_LANGUAGES, code) ? code : null;
}

/** The refusal both doors say for a code the table does not hold — `SessionLanguages.Refusal`, in the same words. */
export function languageRefusal(spelled: string): string {
  return `\`${spelled.trim()}\` is not a language a session can be asked to write in here — one of `
    + `${Object.keys(SESSION_LANGUAGES).map((code) => `\`${code}\``).join(', ')}.`;
}

/**
 * What a session in this repository is asked to write in (LANG1c): its own, else its workspace's (one in none is in the default
 * one), else null — the driver's `SessionLanguages.Resolve`, names matched without case.
 */
export function languageFor(choices: DriverChoices, repository: string, workspace: string | null): SessionLanguage | null {
  const own = entryIn(choices.languages, repository.trim());
  if (own !== null) return { code: own, name: SESSION_LANGUAGES[own]!, source: 'repository' };
  const shared = entryIn(choices.workspaceLanguages, normalizeWorkspace(workspace));
  return shared === null ? null : { code: shared, name: SESSION_LANGUAGES[shared]!, source: 'workspace' };
}

function entryIn(map: Record<string, string>, name: string): string | null {
  const key = findName(Object.keys(map), name);
  return key === null ? null : map[key]!;
}

/** A standing answer (KNOWUSE1b): the person's words, and when they set them, or null where the file does not say. */
export interface StandingAnswer {
  says: string;
  at: Date | null;
}

/** The most characters a standing answer holds — the driver's `DriverConfig.StandingLimit`, a deliberate copy. */
export const STANDING_LIMIT = 2_000;

/**
 * One pause of an ask's work or a quest's (PAUSE1a, design §2.5): when the person made it, or null where the file does not
 * say in a form ISO 8601 writes, and each quest it stopped against the session it stopped. The driver's `WorkPause`.
 */
export interface WorkPause {
  at: Date | null;
  stopped: Record<string, string>;
}

/** The driver's own default for `timeoutMinutes` (`DriverConfig.Empty`) — the twin says 30 too. */
export const DEFAULT_TIMEOUT_MINUTES = 30;

/**
 * How long an account cools when its agent's limit names no time (TOOL4e, D125 §2.2), when `cooloff` sets none — a
 * deliberate copy of the driver's `AccountLimits.DefaultCoolOff`, which `CoolOffTests` holds beside this one's table.
 */
export const DEFAULT_COOLOFF_MINUTES = 60;

/** Drives nothing, holds nothing — the safe shape silence takes, matching the driver's own default. */
const EMPTY: DriverChoices = {
  drivable: [], holds: [], trees: [], cap: 2, adapter: 'claude-code', notify: true,
  strikes: 3, forgiven: {}, released: {}, pausedAsks: {}, pausedQuests: {}, intakeAdapter: null, helperAdapter: null, timeoutMinutes: null, cooloff: null, lines: {}, workspaceLines: {},
  landings: {}, workspaceLandings: {}, readAcross: {}, workspaceReadAcross: {}, writeAcross: {}, standing: {}, languages: {},
  workspaceLanguages: {}, rest: {},
};

/**
 * Why a relationship from one repository to another cannot be declared, in a sentence, or null when it
 * can (D107). `AcrossRules.Problem` in the driver says the same two sentences.
 */
export function writeAcrossProblem(repository: string, to: string): string | null {
  if (to.trim().length === 0) return 'a relationship names the repository it may write into.';
  return sameName(repository.trim(), to.trim())
    ? `\`${repository.trim()}\` writes in its own tree already — a relationship names another repository.`
    : null;
}

/**
 * How a session's work lands (WSR1, D87): merged into the line, or put on a branch the pattern names,
 * for the person to push — or, where a branch rule names one, for a plugin to push and open the pull
 * request from (WSR4, D100). Daoris itself never pushes.
 */
export interface LandingRule {
  form: string;
  pattern?: string;
  /** Once a press lands the work, its tree and branch go — behind the driver's proof (D88). Absent is off. */
  tidy?: boolean;
  /** The plugin a branch rule hands its new branch to, spoken to on `work/land` (D100). Absent is the person. */
  plugin?: string;
  /**
   * *Accept automatically* (LAND2a, D145): a quest's done lands its work with no press, and the rule's plugin pushes it
   * and opens the pull request. Only a branch rule takes it; absent is the person's press. The driver's
   * `LandingRule.AutoAccept` is the twin; the driver lands a released done under it at a look (LAND2b).
   */
  autoAccept?: boolean;
}

/**
 * What a door says as *Accept automatically* is set (D145 point 5, design §6): the person's standing say-so for a push
 * with no press, and with no plugin the warning that nothing leaves the machine — the driver's
 * `LandingRules.AutoAcceptSays`, word for word.
 */
export function autoAcceptSays(plugin: string | undefined): string {
  return plugin !== undefined
    ? `when a quest here is done, its work is put on its branch, and \`${plugin}\` pushes it and opens a pull request without `
      + 'asking you each time. The pull request is where it is judged. Switch it off to accept each one yourself.'
    : 'when a quest here is done, its work is put on its branch, and nothing leaves this machine: no plugin opens a pull '
      + 'request, so each done\'s branch waits here for you to push it. Switch it off to accept each one yourself.';
}

/** The point a plugin lands work on — the driver's `HookPoints.Land`. */
export const LAND_POINT = 'work/land';

/** What a plugin's id may be — the catalogue's own shape, so a rule never names a path. */
const PLUGIN_ID = /^[a-z0-9][a-z0-9.-]*$/;

/** What a pattern may say — the driver's `LandingRules.Placeholders`. One of the first two is required. */
const PLACEHOLDERS = ['quest', 'session', 'slug', 'repository'];

/** What a pattern is tried against before it is kept — the driver tries the same names. */
const SAMPLE: Record<string, string> = { quest: '0fda18', session: 's1a2b3c4', slug: 'sample-work', repository: 'engine' };

/**
 * What is wrong with a landing rule's shape, in a sentence, or null when it can land work.
 * `LandingRules.Problem` in the driver is the twin, and both carry the same tables. Whether its plugin is
 * on this machine is `landingPluginProblem`'s question.
 */
export function landingProblem(rule: LandingRule): string | null {
  if (rule.form === 'merge') {
    // A merge makes no branch, and the plugin starts from the branch Daoris made (D100).
    if (rule.plugin !== undefined) {
      return 'only a branch rule hands its work to a plugin — the plugin pushes the branch Daoris made, and a merge '
        + 'makes none. `branch <pattern> --plugin <id>` is the form that does.';
    }
    // A merge writes into the person's checkout, and with no press nothing would stand between the work and the line
    // (D145 point 1, D51 rule 6).
    return rule.autoAccept
      ? 'only a branch rule accepts automatically — a merge writes into your checkout, and with no press nothing would '
        + 'stand between the work and the line. `branch <pattern> --auto-accept` is the form that does.'
      : null;
  }
  if (rule.form !== 'branch') {
    return `\`${rule.form}\` is not a way work lands here — \`merge\` or \`branch\`. `
      + 'Pushing and opening a pull request is a plugin\'s to do, named on the branch form: '
      + '`branch <pattern> --plugin <id>`.';
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
  if (sample.includes('{') || sample.includes('}') || !isBranchName(sample)) {
    return `\`${pattern}\` does not make a branch name git would take — it gives \`${sample}\`.`;
  }

  return rule.plugin !== undefined && !PLUGIN_ID.test(rule.plugin)
    ? `\`${rule.plugin}\` is not a plugin id — one is lowercase letters, digits, dots and dashes, like \`example.github-pull-request\`.`
    : null;
}

/**
 * Why the plugin a rule names cannot land work on this machine, in a sentence, or null when it can
 * (D100): not installed, switched off, refused by the catalogue, or speaking on no `work/land` point.
 * The driver's `LandingRules.PluginProblem` is the twin, and asks again at the press.
 */
export function landingPluginProblem(plugin: string, catalog: PluginCatalog): string | null {
  const entry = catalog.plugins.find((p) => sameName(p.manifest.id, plugin));
  if (!entry) {
    return `the landing rule names plugin \`${plugin}\`, which is not installed on this machine — `
      + '`daoris plugin add <folder>` installs one, and `daoris plugin list` shows what there is.';
  }
  if (!entry.enabled) {
    return `plugin \`${plugin}\` is switched off on this machine — \`daoris plugin enable ${entry.manifest.id}\` switches it on.`;
  }
  if (entry.problem !== null) return `plugin \`${plugin}\` contributes nothing: ${entry.problem}`;
  return entry.manifest.hooks?.points.includes(LAND_POINT)
    ? null
    : `plugin \`${plugin}\` does not land work — its manifest speaks on no \`${LAND_POINT}\` point.`;
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
  if (parsed === null) {
    return {
      ...EMPTY, forgiven: {}, released: {}, pausedAsks: {}, pausedQuests: {}, lines: {}, workspaceLines: {}, landings: {},
      workspaceLandings: {}, readAcross: {}, workspaceReadAcross: {}, writeAcross: {}, standing: {}, languages: {},
      workspaceLanguages: {}, rest: {},
    };
  }

  const {
    drivable, holds, trees, cap, adapter, notify, strikes, forgiven, released, intakeAdapter, helperAdapter, timeoutMinutes,
    cooloff, lines, workspaceLines, landings, workspaceLandings, readAcross, workspaceReadAcross, writeAcross, standing,
    languages, workspaceLanguages, ...rest
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
    // Each quest against a session, as the driver reads it (SESSUX1b): a release the driver would not read is not listed.
    released: releases(released),
    // Read as the driver reads them (PAUSE1a), and left in `rest` as written: this editor never writes a pause.
    pausedAsks: pauses(parsed['pausedAsks']),
    pausedQuests: pauses(parsed['pausedQuests']),
    // 🔴 Absent means ON, the same reading the driver makes (SURF5b): every machine that already
    // has this file predates the field, and taking silence for "off" would ship the feature
    // switched off on exactly the machines that have been driving longest.
    notify: typeof notify === 'boolean' ? notify : EMPTY.notify,
    // 🔴 Absent means OFF — the opposite of `notify`, the driver's own reading (INT4b): an intake
    // spends a real login on every ask, so it runs only where a person named the harness for it.
    intakeAdapter: typeof intakeAdapter === 'string' && intakeAdapter.trim().length > 0
      ? intakeAdapter.trim() : null,
    // Absent means OFF, as the intake's does — the driver's reading (D89).
    helperAdapter: typeof helperAdapter === 'string' && helperAdapter.trim().length > 0
      ? helperAdapter.trim() : null,
    // Read as the driver reads it (`DriverConfig`: a whole number, lifted to at least one), so the
    // listing never names a number the driver would not use.
    timeoutMinutes: typeof timeoutMinutes === 'number' && Number.isInteger(timeoutMinutes)
      ? Math.max(1, timeoutMinutes) : null,
    // A whole number of at least one, within what the driver reads as a number, or the default (TOOL4e): never a spin.
    cooloff: typeof cooloff === 'number' && Number.isInteger(cooloff) && cooloff >= 1 && cooloff <= 2_147_483_647
      ? cooloff : null,
    // An entry git would refuse is not read, as the driver does not read it: a line the driver would
    // ignore must not be listed as if it held.
    lines: branchMap(lines),
    workspaceLines: branchMap(workspaceLines),
    // A rule that could not land work is not read, as the driver does not read it.
    landings: ruleMap(landings),
    workspaceLandings: ruleMap(workspaceLandings),
    // Only a boolean is read, and only other names as a relationship, as the driver reads them (D107).
    readAcross: flagMap(readAcross),
    workspaceReadAcross: flagMap(workspaceReadAcross),
    writeAcross: targetMap(writeAcross),
    // The person's words by repository, as the driver reads them (KNOWUSE1b): an answer the driver would not read is not listed.
    standing: standings(standing),
    // A code of the table by name, as the driver reads them (LANG1c): a language the driver would not hand is not listed.
    languages: languageMap(languages),
    workspaceLanguages: languageMap(workspaceLanguages),
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
    // Written only when set (SESSUX1b), as the driver writes it: absent is no release.
    ...(Object.keys(choices.released).length > 0 ? { released: choices.released } : {}),
    // Written only when named — absent IS off, and the driver writes it the same way.
    ...(choices.intakeAdapter ? { intakeAdapter: choices.intakeAdapter } : {}),
    ...(choices.helperAdapter ? { helperAdapter: choices.helperAdapter } : {}),
    // Written only when set: absent is the driver's own default, and an edit elsewhere must not
    // pin today's default into a file that never chose it.
    ...(choices.timeoutMinutes !== null ? { timeoutMinutes: choices.timeoutMinutes } : {}),
    // Written only when set (TOOL4e), as the driver writes it: absent is the default hour.
    ...(choices.cooloff !== null ? { cooloff: choices.cooloff } : {}),
    // Written only when set (WSR2): absent is the checkout's guess, and the driver writes them the same way.
    ...(Object.keys(choices.lines).length > 0 ? { lines: choices.lines } : {}),
    ...(Object.keys(choices.workspaceLines).length > 0 ? { workspaceLines: choices.workspaceLines } : {}),
    // Written only when set (WSR1): absent is the merge it always was.
    ...(Object.keys(choices.landings).length > 0 ? { landings: choices.landings } : {}),
    ...(Object.keys(choices.workspaceLandings).length > 0 ? { workspaceLandings: choices.workspaceLandings } : {}),
    // Written only when set (D107): absent is reading on and no relationship.
    ...(Object.keys(choices.readAcross).length > 0 ? { readAcross: choices.readAcross } : {}),
    ...(Object.keys(choices.workspaceReadAcross).length > 0 ? { workspaceReadAcross: choices.workspaceReadAcross } : {}),
    ...(Object.keys(choices.writeAcross).length > 0 ? { writeAcross: choices.writeAcross } : {}),
    // Written only when set (KNOWUSE1b), each moment in UTC to the second, as the driver writes it: absent is none.
    ...(Object.keys(choices.standing).length > 0
      ? {
          standing: Object.fromEntries(Object.entries(choices.standing).map(([repository, answer]) => [repository, {
            says: answer.says,
            ...(answer.at ? { at: answer.at.toISOString().replace(/\.\d{3}Z$/, 'Z') } : {}),
          }])),
        }
      : {}),
    // Written only when set (LANG1c), as the driver writes them: absent is no language, and no line handed.
    ...(Object.keys(choices.languages).length > 0 ? { languages: choices.languages } : {}),
    ...(Object.keys(choices.workspaceLanguages).length > 0 ? { workspaceLanguages: choices.workspaceLanguages } : {}),
  });
}

/** No way to read the records was handed in, so a retry that needs them is refused (RETRY1b). */
const unhanded: RecordsReader = async () => ({ unread: 'this command was handed no way to read them' });

/**
 * Read or change what this machine drives. Every verb edits `driver.json` and answers at once, but `retry <quest>` without
 * `--at`, which counts the quest's failures from the records `records` reads (RETRY1b) and answers when they are read.
 *
 * @remarks
 * `drive`/`undrive` and `hold`/`resume` are pairs of verbs rather than one verb with a boolean,
 * because they mean different things: opting a repository IN is a standing decision, and holding one
 * is a pause on something already opted in. A surface that collapsed them would lose that a held
 * repository is still drivable.
 *
 * @param records How this machine's session records are read: the `driver` row hands in the service client's, so this
 * module reaches no network. Absent, they cannot be read, and such a retry is refused.
 */
export function commandDriver(
  { argv, write }: CommandArgs, records: RecordsReader = unhanded,
): ExitCode | Promise<ExitCode> {
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
      const kept = choices.trees.filter((name) => !sameName(name, repository));
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
    // SESSUX1b (D126 §3.4): with `--session`, it releases the person's stop of that session instead. This command reads no
    // verdict (only the records, for a mark, RETRY1b), so it cannot see which hold a quest is under: the stop's sentence
    // names the session, and a stop is not a strike, so a release moves no mark.
    case 'retry': {
      const quest = named(argv, 'retry').replace(/^#/, '');
      const sessionAt = argv.indexOf('--session');
      if (sessionAt !== -1) {
        const session = argv[sessionAt + 1]?.trim();
        if (!session || session.startsWith('--')) {
          throw new DaorisError('`--session` needs the session your stop ended — the quest\'s *Sitting* names it: '
            + '`daoris driver retry <quest> --session <id>`.');
        }
        if (argv.includes('--at')) {
          throw new DaorisError('a retry either releases your stop (`--session <id>`) or counts the strikes from a mark '
            + '(`--at <n>`) — a stop is not a strike, so the two never go together.');
        }

        const key = findName(Object.keys(choices.released), quest) ?? quest;
        writeDriverChoices(path, { ...choices, released: { ...choices.released, [key]: session } });
        write(`daoris: quest \`#${quest}\` is released from your stop of session \`${session}\`.`);
        write('  The driver takes it up again at its next look: a taken quest is carried on in the tree that session');
        write('  worked in, an open one is planned again. A later stop holds it again.');
        write(`  Written to ${path} — the driver re-reads it every tick, so nothing restarts.`);
        return 0;
      }

      // RETRY1b: the mark is the quest's failures as the records count them, which the person may give with `--at`. Without
      // it they are read from this machine's records, as the driver counts them (`strikes.ts`): the strike limit, which this
      // marked before, was right only on a first park. A count it cannot read is refused, never guessed.
      const marked = (mark: number, counted: boolean): ExitCode => {
        // Read again: the records were read over the network, and the driver may have written the file meanwhile.
        const now = counted ? readDriverChoices(path) : choices;
        writeDriverChoices(path, { ...now, forgiven: { ...now.forgiven, [quest]: mark } });
        write(`daoris: quest \`#${quest}\` may be started again.`);
        write(`  Counting from ${mark} failure(s)${counted ? ', as this machine\'s records count them' : ''} — what already`);
        write(`  happened is still in the records, and ${now.strikes || 'no'} more failure(s) will park it again.`);
        write(`  Written to ${path} — the driver re-reads it every tick, so nothing restarts.`);
        return 0;
      };

      const at = argv.indexOf('--at');
      if (at !== -1) {
        const mark = argv[at + 1] ? Number(argv[at + 1]) : Number.NaN;
        if (!Number.isInteger(mark) || mark < 0) {
          throw new DaorisError('`driver retry --at` needs a whole number of failures to count from.');
        }
        return marked(mark, false);
      }

      return records().then((read) => {
        const failures = 'records' in read ? failuresOf(read.records, quest) : null;
        if (failures === null) {
          const why = 'unread' in read ? read.unread : 'the records answered were not a list';
          const before = choices.forgiven[quest] ?? 0;
          throw new DaorisError(`cannot count the failures of \`#${quest}\`: this machine's session records could not be read — `
            + `${why}.\n  Nothing was written. Give the count yourself: \`daoris driver retry ${quest} --at <n>\`, with n the `
            + `failures its *Sitting* names${before > 0 ? ` plus ${before}, the mark its last retry left` : ''}.\n`
            + '  The quest\'s page\'s *Try again* counts them for you.');
        }
        return marked(failures, true);
      });
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

    // How long an account cools when its agent hits a limit and names no time this reads (TOOL4e, D125 §2.2). A time the
    // agent names is read, whatever this says; this is the guess for a sentence that names none.
    case 'cooloff': {
      const value = Number(named(argv, 'cooloff'));
      if (!Number.isInteger(value) || value < 1 || value > 2_147_483_647) {
        throw new DaorisError(
          '`driver cooloff` needs a whole number of minutes, at least 1 — e.g. `daoris driver cooloff 60`; a zero '
          + 'cool-off would start a spent account again at every look.');
      }

      writeDriverChoices(path, { ...choices, cooloff: value });
      write(`daoris: an account whose agent names no time for its limit cools for ${value} minutes on this machine.`);
      write('  When the agent names when its limit resets, that time is the cool-off, whatever this says.');
      write(`  Written to ${path} — read at the next limit, so nothing restarts.`);
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
    // The agent Ask Daoris runs on (HELP1, D89), or `off`: its own name, since answering asks and helping
    // a person are two jobs. The desktop's `SET_HELPER` is the other door (D50).
    case 'helper': {
      const agent = argv.slice(1).find((token) => !token.startsWith('--'));
      if (!agent) {
        throw new DaorisError('`driver helper` needs <adapter>|off — e.g. `daoris driver helper claude-code-acp`.');
      }

      const off = agent === 'off';
      writeDriverChoices(path, { ...choices, helperAdapter: off ? null : agent });
      write(off
        ? 'daoris: Ask Daoris runs no agent here — it offers its starters, each a door to the screen that fixes it.'
        : `daoris: Ask Daoris runs on \`${agent}\`. It reads, and proposes; every change is yours to apply (D89).`);
      if (!off && !(agent in TOOLCHAINS)) {
        write('  Daoris manages no toolchain for that name — `daoris agent list` shows the ones it does.');
      }

      write(`  Written to ${path}.`);
      return 0;
    }

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
    // it that sets none of its own: `merge` into the line, or `branch <pattern>` for the person to push,
    // or for the plugin `--plugin` names to push and open the pull request from (WSR4, D100), and on a branch
    // `--auto-accept` for a quest's done to land it with no press (LAND2a, D145). Unset, it is the merge it
    // always was. Daoris itself never pushes.
    case 'landing': {
      const workspace = flagValue(argv, '--workspace');
      const clear = argv.includes('--clear');
      const words = operands(argv, new Set(['--workspace', '--plugin'])).slice(1);
      const name = workspace ?? words.shift();
      const [form, pattern] = words;
      if (!name || (!clear && !form)) {
        throw new DaorisError(
          '`driver landing` needs <repository>|--workspace <name>, then merge|branch <pattern>|--clear — '
          + 'e.g. `daoris driver landing --workspace aurora branch "feature/{quest}-{slug}"`, and on a branch '
          + '`--plugin <id>` for an installed plugin that pushes it and opens the pull request, and `--auto-accept` '
          + 'for a quest\'s done to land it with no press.');
      }

      const pluginAt = argv.indexOf('--plugin');
      const plugin = pluginAt === -1 ? undefined : argv[pluginAt + 1];
      if (pluginAt !== -1 && (!plugin || plugin.startsWith('--'))) {
        throw new DaorisError('`--plugin` needs the id of an installed plugin — `--plugin <id>`; '
          + '`daoris plugin list` shows what there is.');
      }

      // *Accept automatically* (LAND2a, D145): on with `--auto-accept`; `--no-auto-accept` says off out loud, which is a
      // repository's own rule overriding a workspace that accepts automatically, since a rule replaces the one above whole.
      const autoAccept = argv.includes('--auto-accept');
      if (autoAccept && argv.includes('--no-auto-accept')) {
        throw new DaorisError('a rule accepts automatically or waits for your Accept — say `--auto-accept` or `--no-auto-accept`, not both.');
      }

      const rule: LandingRule | null = clear ? null : {
        form: form!,
        ...(pattern !== undefined ? { pattern } : {}),
        ...(argv.includes('--tidy') ? { tidy: true } : {}),
        ...(plugin !== undefined ? { plugin } : {}),
        ...(autoAccept ? { autoAccept: true } : {}),
      };
      const problem = rule === null ? null : landingProblem(rule);
      if (problem !== null) throw new DaorisError(problem);
      // The plugin must land work on THIS machine — its plugins sit beside the file, as the driver reads them.
      const unready = rule?.plugin === undefined ? null : landingPluginProblem(rule.plugin, readPlugins(dirname(path)));
      if (unready !== null) throw new DaorisError(`${unready} Nothing was written.`);

      writeDriverChoices(path, workspace
        ? { ...choices, workspaceLandings: withEntry(choices.workspaceLandings, workspace, rule && kept(rule)) }
        : { ...choices, landings: withEntry(choices.landings, name, rule && kept(rule)) });

      const whose = workspace ? `repositories in the workspace \`${workspace}\`` : `\`${name}\``;
      if (rule === null) {
        write(`daoris: work in ${whose} lands as ${workspace ? 'each one\'s own rule, else ' : 'its workspace\'s rule, else '}`
          + 'the merge into its line.');
      } else if (rule.form === 'merge') {
        write(`daoris: work in ${whose} is merged into the line when you accept it, in the repository's own checkout.`);
      } else if (rule.plugin !== undefined) {
        write(`daoris: work in ${whose} is put on a branch named \`${rule.pattern}\` when you accept it, and then`);
        write(`  plugin \`${rule.plugin}\` pushes it and opens the pull request, over its wire. Daoris itself never pushes`);
        write('  (D87, D100): the plugin\'s own process does, signed in as its platform\'s tools are. A plugin that fails');
        write('  leaves the branch standing, and the review says how to push it yourself.');
      } else {
        write(`daoris: work in ${whose} is put on a branch named \`${rule.pattern}\` when you accept it —`);
        write('  from the session\'s branch, with nothing merged and no checkout touched. You push it and open');
        write('  the pull request: Daoris never pushes (D87).');
      }

      if (rule?.tidy) {
        write('  Once a press lands the work, its tree and its branch go — only where git proves the work is on a');
        write('  branch of yours. Without --tidy the tree stays until you discard it.');
      }

      // The person's standing say-so for a push with no press, said as it is given (D145 point 5, design §6).
      if (rule?.autoAccept) {
        write(`  Accept automatically: ${autoAcceptSays(rule.plugin)}`);
      } else if (rule?.form === 'branch' && argv.includes('--no-auto-accept')) {
        write('  Done work there waits for your Accept — a repository\'s own rule replaces its workspace\'s whole, the switch with it.');
      }

      if (rule !== null && workspace) {
        write('  A repository with a rule of its own keeps it — `daoris driver landing <repository> --clear` hands it back.');
      }

      write(`  Written to ${path} — the review reads it at each press, so nothing restarts.`);
      return 0;
    }

    // Reading and writing across repositories (READ1, D107). `read` is whether a repository's checkout —
    // or, with `--workspace`, each checkout there that sets none of its own — is read by agents outside
    // it; on unless switched off. `write-to` declares that one repository's sessions may also write into
    // another: the person's standing say-so, one direction per declaration.
    case 'across': {
      const workspace = flagValue(argv, '--workspace');
      const clear = argv.includes('--clear');
      const words = operands(argv, new Set(['--workspace'])).slice(1);
      const name = workspace ?? words.shift();
      const [what, value] = words;
      if (!name) {
        throw new DaorisError(
          '`driver across` needs <repository>|--workspace <name>, then read on|off|--clear or write-to <other> '
          + '[--clear] — e.g. `daoris driver across engine read off`.');
      }

      if (what === 'read') {
        if (!clear && value !== 'on' && value !== 'off') {
          throw new DaorisError('`driver across … read` needs on|off|--clear — e.g. `daoris driver across engine read off`.');
        }

        const set = clear ? null : value === 'on';
        writeDriverChoices(path, workspace
          ? { ...choices, workspaceReadAcross: withEntry(choices.workspaceReadAcross, workspace, set) }
          : { ...choices, readAcross: withEntry(choices.readAcross, name, set) });

        const whose = workspace ? `each checkout in the workspace \`${workspace}\` that sets none of its own is`
          : `\`${name}\`'s checkout is`;
        if (set === null) {
          write(workspace
            ? `daoris: checkouts in the workspace \`${workspace}\` are read across again, unless one says otherwise.`
            : `daoris: \`${name}\` takes its workspace's reading again, else on.`);
        } else if (set) {
          write(`daoris: ${whose} read by sessions in its workspace's other repositories and by Ask Daoris —`);
          write('  its files and `git status` and the branch list, never a write.');
        } else {
          write(`daoris: ${whose} read by no agent outside it: no session in another repository, and not Ask Daoris.`);
        }

        if (set !== null && workspace) {
          write('  A repository with a setting of its own keeps it — `daoris driver across <repository> read --clear` hands it back.');
        }

        write('  A session already running keeps what it started with.');
        write(`  Written to ${path} — the driver reads it at every start, so nothing restarts.`);
        return 0;
      }

      if (what === 'write-to') {
        if (workspace) {
          throw new DaorisError('a relationship is declared from one repository — `daoris driver across <repository> write-to <other>`.');
        }

        const problem = writeAcrossProblem(name, value ?? '');
        if (problem !== null) throw new DaorisError(problem);

        writeDriverChoices(path, { ...choices, writeAcross: withTarget(choices.writeAcross, name, value!, !clear) });
        if (clear) {
          write(`daoris: sessions in \`${name}\` no longer write into \`${value}\`; a change needed there is a quest again.`);
        } else {
          write(`daoris: sessions in \`${name}\` may also write into \`${value}\` — its files, and a commit there —`);
          write('  where both are in one workspace with a checkout here. This is your standing say-so for writing');
          write(`  across; it has one direction, so \`${value}\` writes nothing into \`${name}\` unless you declare that too.`);
          write(`  \`daoris driver across ${name} write-to ${value} --clear\` takes it back.`);
        }

        write(`  Written to ${path} — the driver reads it at every start, so nothing restarts.`);
        return 0;
      }

      throw new DaorisError(
        `\`driver across\` sets read or write-to, not \`${what ?? ''}\` — e.g. \`daoris driver across engine read off\`, `
        + '`daoris driver across plugins write-to engine`.');
    }

    // A standing answer (KNOWUSE1b, D135 §3): what the person says holds for every session in a repository, in their words,
    // handed to each one beneath its quest. Kept on this machine and never written into the repository (D32); the
    // repository's page and Ask Daoris's `setting` kind are its other doors (D50).
    case 'standing': {
      const clear = argv.includes('--clear');
      const [, repository, ...words] = operands(argv, new Set());
      const says = words.join(' ').trim();
      if (!repository || (!clear && says.length === 0)) {
        throw new DaorisError(
          '`driver standing` needs <repository>, then the answer in your words, or --clear — e.g. '
          + '`daoris driver standing work-app "dev writes allowed; test locally against dev; prod only on a yes"`.');
      }

      if (!clear && says.length > STANDING_LIMIT) {
        throw new DaorisError(`a standing answer is your words, at most 2,000 characters — these are ${says.length}.`);
      }

      // The repository's spelling first written, when it has one in another case: one entry, never two.
      const key = findName(Object.keys(choices.standing), repository) ?? repository;
      const rest = Object.fromEntries(Object.entries(choices.standing).filter(([name]) => name !== key));
      writeDriverChoices(path, { ...choices, standing: clear ? rest : { ...rest, [key]: { says, at: new Date() } } });

      if (clear) {
        write(`daoris: \`${key}\` keeps no standing answer; its sessions are handed none.`);
      } else {
        write(`daoris: every session in \`${key}\` is handed your standing answer, beneath its quest:`);
        write(`  > ${says}`);
        write('  A quest\'s own words, and yours on its ask, are newer and win where they differ. It is kept on this');
        write('  machine and never written into the repository.');
      }

      write(`  Written to ${path} — the driver reads it at every start, so nothing restarts.`);
      return 0;
    }

    // The session language (LANG1c, D142 point 7): what a repository's sessions — or, with `--workspace`, those of each
    // repository there that sets none of its own — are asked to write to the person in. The work's, set apart from the
    // window's (Settings → Appearance); unset, no line is handed. A repository's page, Settings → Workspace and Ask Daoris's
    // `setting` kind are its other doors (D50).
    case 'language': {
      const workspace = flagValue(argv, '--workspace');
      const clear = argv.includes('--clear');
      const [, first, second] = operands(argv, new Set(['--workspace']));
      const name = workspace ?? first;
      const spelled = workspace ? first : second;
      if (!name || (!clear && !spelled)) {
        throw new DaorisError(
          '`driver language` needs <repository>|--workspace <name>, then en|zh|--clear — '
          + 'e.g. `daoris driver language aurora-engine zh`.');
      }

      const code = clear ? null : languageCode(spelled);
      if (!clear && code === null) throw new DaorisError(languageRefusal(spelled!));

      const map = workspace ? choices.workspaceLanguages : choices.languages;
      // The name's spelling first written, when it has one in another case: one entry, never two.
      const key = findName(Object.keys(map), name) ?? name;
      const rest = Object.fromEntries(Object.entries(map).filter(([each]) => each !== key));
      const next = code === null ? rest : { ...rest, [key]: code };
      writeDriverChoices(path, workspace ? { ...choices, workspaceLanguages: next } : { ...choices, languages: next });

      if (code === null) {
        write(workspace
          ? `daoris: repositories in the workspace \`${key}\` keep their own session language, else none.`
          : `daoris: \`${key}\` takes its workspace's session language again, else none.`);
      } else {
        write(workspace
          ? `daoris: sessions in each repository in the workspace \`${key}\` that sets none of its own write to you in `
            + `${SESSION_LANGUAGES[code]}: their questions, closing notes, decline reasons and last words.`
          : `daoris: sessions in \`${key}\` write to you in ${SESSION_LANGUAGES[code]}: their questions, closing notes, `
            + 'decline reasons and last words.');
        write('  One line in each instruction asks it; code, commands and anything quoted stay as written. The window\'s own');
        write('  language is Settings → Appearance, and neither sets the other.');
        if (workspace) {
          write('  A repository with a language of its own keeps it — `daoris driver language <repository> --clear` hands it back.');
        }
      }

      write('  A session already running keeps what it was handed.');
      write(`  Written to ${path} — the driver reads it at every start, so nothing restarts.`);
      return 0;
    }

    default:
      throw new DaorisError(
        `unknown driver verb '${verb}' — one of: list, drive, undrive, hold, resume, trees, line, landing, across, standing, `
        + 'language, notify, strikes, retry, timeout, cooloff, cap, adapter, intake, helper');
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

    for (const [quest, session] of Object.entries(choices.released)) {
      write(`  released   #${quest}  (your stop of session ${session})`);
    }

    // PAUSE1a (D132 §2.5): each pause this machine keeps, when it was made and the stops it made. Its words say paused,
    // never held (design §8.1): a hold stops nothing that runs, and a pause does. PAUSE1b: and the door that resumes it,
    // daoris-driver's, which reads the work and reaches the running loop (§7.2), since this command talks to nothing (D50).
    for (const [scope, pauses] of [['ask', choices.pausedAsks], ['quest', choices.pausedQuests]] as const) {
      for (const [id, pause] of Object.entries(pauses)) {
        const when = pause.at === null ? 'when is not recorded' : `since ${pause.at.toISOString().replace(/\.\d{3}Z$/, 'Z')}`;
        const stops = Object.keys(pause.stopped).sort().map((quest) => `#${quest}'s session ${pause.stopped[quest]}`);
        const resume = scope === 'ask' ? `daoris-driver ask --resume ${id}` : `daoris-driver quest resume ${id}`;
        write(`  paused     ${scope} #${id}  (${when}${stops.length > 0 ? `; its pause stopped ${stops.join(', ')}` : ''}) — resume: ${resume}`);
      }
    }

    write(choices.timeoutMinutes === null
      ? `  timeout    ${DEFAULT_TIMEOUT_MINUTES} minutes a session may run  (the default — \`daoris driver timeout <minutes>\` changes it)`
      : `  timeout    ${choices.timeoutMinutes} minutes a session may run`);
    write(choices.cooloff === null
      ? `  cooloff    ${DEFAULT_COOLOFF_MINUTES} minutes an account cools when its agent names no time for its limit  (the default — `
        + '`daoris driver cooloff <minutes>` changes it)'
      : `  cooloff    ${choices.cooloff} minutes an account cools when its agent names no time for its limit`);

    write(`  notify     ${choices.notify ? 'on' : 'off'}`
      + `  (a session parking, or ending without you asking${choices.notify ? '' : ' — not said'})`);
    write(choices.helperAdapter
      ? `  helper     ${choices.helperAdapter}  (Ask Daoris — it proposes, and you apply)`
      : '  helper     off — Ask Daoris offers its starters; `daoris driver helper <adapter>` names an agent');
    write(choices.intakeAdapter
      ? `  intake     ${choices.intakeAdapter}  (an ask the declarations do not settle opens a session — a login each)`
      : '  intake     off — asks are answered by declarations only; `daoris driver intake <adapter>` names a harness');

    if (choices.drivable.length === 0) {
      write('  drivable   nothing — this machine drives no repository, which is the default (D46 §2).');
      write('             `daoris driver drive <repository>` opts one in.');
    }

    for (const repository of choices.drivable) {
      const held = findName(choices.holds, repository) !== null;
      const trees = findName(choices.trees, repository) !== null;
      write(`  drivable   ${repository}`
        + `${held ? '  (held by you — `daoris driver resume` releases it)' : ''}`
        + `${trees ? '  (sessions open their own tree — D51)' : ''}`);
    }

    // Trees on something not drivable is standing configuration, not an error — the desktop's chat
    // door reads it too — but naming it keeps the list the whole truth.
    for (const repository of choices.trees) {
      if (findName(choices.drivable, repository) === null) {
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
      (rule.form === 'branch' ? `branch ${rule.pattern}` : rule.form)
      + (rule.plugin ? `, plugin ${rule.plugin}` : '') + (rule.tidy ? ', tidy' : '') + (rule.autoAccept ? ', accept automatically' : '');
    for (const [repository, rule] of Object.entries(choices.landings)) {
      write(`  landing    ${repository}  ${spelled(rule)}`);
    }

    for (const [workspace, rule] of Object.entries(choices.workspaceLandings)) {
      write(`  landing    workspace ${workspace}  ${spelled(rule)}  (for each repository there that sets none)`);
    }

    // Reading across (D107): on unless switched off, the repository's own over its workspace's.
    for (const [repository, read] of Object.entries(choices.readAcross)) {
      write(`  across     ${repository}  read ${read ? 'on' : 'off'}`);
    }

    for (const [workspace, read] of Object.entries(choices.workspaceReadAcross)) {
      write(`  across     workspace ${workspace}  read ${read ? 'on' : 'off'}  (for each repository there that sets none)`);
    }

    for (const [repository, targets] of Object.entries(choices.writeAcross)) {
      write(`  across     ${repository}  writes into ${targets.join(', ')}  (declared by you)`);
    }

    // KNOWUSE1b: each repository's standing answer, in the person's words, handed to every session there.
    for (const [repository, answer] of Object.entries(choices.standing)) {
      const when = answer.at === null ? '' : `  (set ${answer.at.toISOString().replace(/\.\d{3}Z$/, 'Z')})`;
      write(`  standing   ${repository}  "${answer.says}"${when}`);
    }

    // LANG1c: each session language and where it was set. This command reads no registry (D50), so a workspace's is said for
    // the repositories there that set none; the screen names each repository's resolution.
    for (const [repository, code] of Object.entries(choices.languages)) {
      write(`  language   ${repository}  ${code} (${SESSION_LANGUAGES[code]})  (set for it)`);
    }

    for (const [workspace, code] of Object.entries(choices.workspaceLanguages)) {
      write(`  language   workspace ${workspace}  ${code} (${SESSION_LANGUAGES[code]})  (for each repository there that sets none)`);
    }

    if (Object.keys(choices.languages).length === 0 && Object.keys(choices.workspaceLanguages).length === 0) {
      write('  language   none set — sessions are asked for no language; `daoris driver language <repository> en|zh` sets one');
    }

    // A hold on something not opted in is inert, and saying so is the point: it reads as protection
    // and is not. Reported even when NOTHING is drivable — which is exactly the machine where a
    // person is most likely to believe a hold is what is stopping things.
    for (const held of choices.holds) {
      if (findName(choices.drivable, held) === null) {
        write(`  held       ${held}  (not drivable anyway — the hold changes nothing)`);
      }
    }

    return 0;
  }

  function toggle(field: 'drivable' | 'holds', repository: string, present: boolean): ExitCode {
    const kept = choices[field].filter((name) => !sameName(name, repository));
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
    // A flag's value is never the name (REV3): `retry --at 2 42` read `2` as the quest. `--session`'s neither (SESSUX1b).
    const name = operands(args, new Set(['--at', '--session']))[1];
    if (name !== undefined) return name;

    throw new DaorisError(
      `\`driver ${verb}\` needs a name — e.g. \`daoris driver ${verb} aurora-engine\`.`);
  }
}

/**
 * The session whose stop of this quest the person released (SESSUX1b), or null: the quest matched without case, as the
 * driver's `DriverConfig.ReleasedFor` matches it.
 */
export function releasedFor(choices: DriverChoices, quest: string): string | null {
  const key = findName(Object.keys(choices.released), quest);
  return key === null ? null : choices.released[key]!;
}

/**
 * This repository's standing answer on this machine (KNOWUSE1b), or null: the name matched without case, as the driver's
 * `DriverConfig.StandingFor` matches it.
 */
export function standingFor(choices: DriverChoices, repository: string): StandingAnswer | null {
  const key = findName(Object.keys(choices.standing), repository.trim());
  return key === null ? null : choices.standing[key]!;
}

/**
 * The standing answers, as the driver reads them (KNOWUSE1b): each repository an object whose `says` is text, read without the
 * spaces around it, and read where first written in any case. Its `at` is read only as ISO 8601 writes a moment, and a time
 * that does not read leaves the answer standing with its time unknown. Blank words, an entry that is not an object, and a map
 * that is not one are none.
 */
function standings(value: unknown): Record<string, StandingAnswer> {
  if (!value || typeof value !== 'object' || Array.isArray(value)) return {};
  const held: Record<string, StandingAnswer> = {};
  for (const [repository, entry] of Object.entries(value as Record<string, unknown>)) {
    if (!entry || typeof entry !== 'object' || Array.isArray(entry) || repository.trim().length === 0) continue;
    const { says, at } = entry as Record<string, unknown>;
    if (typeof says !== 'string' || says.trim().length === 0) continue;
    if (findName(Object.keys(held), repository) !== null) continue;
    held[repository] = { says: says.trim(), at: isoMoment(at) };
  }

  return held;
}

/**
 * The session languages, as the driver reads them (LANG1c): each name a code of the table, read in any case without the spaces
 * around it. A code the table does not hold, a value that is not text, a blank name and a map that is not one are not read; a
 * name written twice in any case is read where first written.
 */
function languageMap(value: unknown): Record<string, string> {
  if (!value || typeof value !== 'object' || Array.isArray(value)) return {};
  const held: Record<string, string> = {};
  for (const [name, spelled] of Object.entries(value as Record<string, unknown>)) {
    const code = languageCode(spelled);
    const named = name.trim();
    if (code === null || named.length === 0) continue;
    if (findName(Object.keys(held), named) !== null) continue;
    held[named] = code;
  }

  return held;
}

/** This ask's pause on this machine (PAUSE1a), or null: the id as a person writes it, matched without case, as the driver's `PausedAsk` matches it. */
export function pausedAsk(choices: DriverChoices, ask: string): WorkPause | null {
  return pauseOf(choices.pausedAsks, ask);
}

/** This quest's own pause on this machine (PAUSE1a), or null, as the driver's `PausedQuest` reads it. */
export function pausedQuest(choices: DriverChoices, quest: string): WorkPause | null {
  return pauseOf(choices.pausedQuests, quest);
}

function pauseOf(pauses: Record<string, WorkPause>, id: string): WorkPause | null {
  const key = findName(Object.keys(pauses), id.trim().replace(/^#+/, '').trim());
  return key === null ? null : pauses[key]!;
}

/**
 * The pauses, as the driver reads them (PAUSE1a): each id an object, read where first written in any case. Its `at` is read
 * only as ISO 8601 writes a moment, and a time that does not read leaves the pause standing with its time unknown: the pause
 * is the person's, and the time only says when. Its `stopped` is read as `released` is. A map that is not one is none.
 */
function pauses(value: unknown): Record<string, WorkPause> {
  if (!value || typeof value !== 'object' || Array.isArray(value)) return {};
  const held: Record<string, WorkPause> = {};
  for (const [id, entry] of Object.entries(value as Record<string, unknown>)) {
    if (!entry || typeof entry !== 'object' || Array.isArray(entry) || id.length === 0) continue;
    if (findName(Object.keys(held), id) !== null) continue;
    const { at, stopped } = entry as Record<string, unknown>;
    held[id] = { at: isoMoment(at), stopped: releases(stopped) };
  }

  return held;
}

/**
 * The releases, as the driver reads them (SESSUX1b), and a pause's stops (PAUSE1a): each quest against a session's id, read
 * without the spaces around it. A blank session, one that is not text, a map that is not one, and a quest written again in
 * another case are not read.
 */
function releases(value: unknown): Record<string, string> {
  if (!value || typeof value !== 'object' || Array.isArray(value)) return {};
  const held: Record<string, string> = {};
  for (const [quest, session] of Object.entries(value as Record<string, unknown>)) {
    if (typeof session !== 'string' || session.trim().length === 0 || quest.length === 0) continue;
    if (findName(Object.keys(held), quest) !== null) continue;
    held[quest] = session.trim();
  }

  return held;
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
  const kept = Object.fromEntries(Object.entries(map).filter(([name]) => !sameName(name, key)));
  return value === null ? kept : { ...kept, [key]: value };
}

/**
 * The relationships with `to` declared from `repository`, or taken back — matched without case, as the
 * driver matches it, keeping the repository's existing spelling. The last one taken back leaves no entry.
 */
function withTarget(map: Record<string, string[]>, repository: string, to: string, allow: boolean): Record<string, string[]> {
  const key = findName(Object.keys(map), repository.trim()) ?? repository.trim();
  const held = map[key] ?? [];
  const same = (name: string) => sameName(name, to.trim());
  const kept = allow ? (held.some(same) ? held : [...held, to.trim()]) : held.filter((name) => !same(name));
  const rest = Object.fromEntries(Object.entries(map).filter(([name]) => name !== key));
  return kept.length > 0 ? { ...rest, [key]: kept } : rest;
}

/** A map of names to booleans; an entry of any other type is not read, as the driver does not read it (D107). */
function flagMap(value: unknown): Record<string, boolean> {
  if (!value || typeof value !== 'object' || Array.isArray(value)) return {};
  const held: Record<string, boolean> = {};
  for (const [name, flag] of Object.entries(value as Record<string, unknown>)) {
    if (typeof flag === 'boolean' && name.trim().length > 0) held[name.trim()] = flag;
  }

  return held;
}

/**
 * The declared relationships (D107): each repository's list of other names, once each in any case, in the
 * order first written. A list that is not one, an entry that is not a name, and the repository itself are
 * not read, as the driver does not read them; a repository left with none has no entry.
 */
function targetMap(value: unknown): Record<string, string[]> {
  if (!value || typeof value !== 'object' || Array.isArray(value)) return {};
  const held: Record<string, string[]> = {};
  for (const [name, list] of Object.entries(value as Record<string, unknown>)) {
    if (!Array.isArray(list) || name.trim().length === 0) continue;
    const targets: string[] = [];
    for (const item of list) {
      if (typeof item !== 'string' || writeAcrossProblem(name, item) !== null) continue;
      if (findName(targets, item.trim()) === null) targets.push(item.trim());
    }

    if (targets.length > 0) held[name.trim()] = targets;
  }

  return held;
}

/** A map of names to landing rules; one that could not land work is skipped, as the driver skips it. */
function ruleMap(value: unknown): Record<string, LandingRule> {
  if (!value || typeof value !== 'object' || Array.isArray(value)) return {};
  const held: Record<string, LandingRule> = {};
  for (const [name, rule] of Object.entries(value as Record<string, unknown>)) {
    if (!rule || typeof rule !== 'object') continue;
    const { form, pattern, tidy, plugin, autoAccept } = rule as Record<string, unknown>;
    const read: LandingRule = {
      form: typeof form === 'string' ? form : '',
      ...(typeof pattern === 'string' ? { pattern } : {}),
      ...(tidy === true ? { tidy: true } : {}),
      // An empty name is no name, as the driver reads it.
      ...(typeof plugin === 'string' && plugin.length > 0 ? { plugin } : {}),
      // Only JSON `true` (LAND2a): `"true"` or 1 is not the person's say-so for a push with no press.
      ...(autoAccept === true ? { autoAccept: true } : {}),
    };
    if (landingProblem(read) === null) held[name] = kept(read);
  }

  return held;
}

/**
 * A rule as it is kept: a merge carries no pattern, a plugin only on a branch, and the tidy and the automatic
 * acceptance only when on, in that order — the driver keeps it the same way.
 */
function kept(rule: LandingRule): LandingRule {
  return {
    ...(rule.form === 'merge' ? { form: 'merge' } : { form: rule.form, pattern: rule.pattern! }),
    ...(rule.form !== 'merge' && rule.plugin ? { plugin: rule.plugin } : {}),
    ...(rule.tidy ? { tidy: true } : {}),
    ...(rule.form !== 'merge' && rule.autoAccept ? { autoAccept: true } : {}),
  };
}

function names(value: unknown): string[] {
  return Array.isArray(value) ? value.filter((name): name is string => typeof name === 'string' && name.length > 0) : [];
}
