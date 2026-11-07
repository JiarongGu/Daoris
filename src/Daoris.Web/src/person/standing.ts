import type { TFunction } from 'i18next';
import { list } from '../locales/format';

// The person's standing confirmation as the page shows it (TERMTRUST1a, D201; the terminal-trust design §6): its shape as the page
// reads it, its presses in words, and a grant's or a revoke's refusal in words. The reader is the bridge's (`standingOf`,
// `bridge/person.ts`); the row is This Daoris's (`WiringStanding`), the line Notifications' (`StandingLine`).

/** Why a standing confirmation's record is not honoured, as the shell's read says it (the terminal-trust design §4.2, §6.1). */
export type StandingUnread = 'unread' | 'newer' | 'other-machine';

/**
 * The person's standing confirmation as `STATE`'s `standingConfirmation` says it (the modules' `StandingNow`): whether this build
 * offers the grant, whether it is on, the presses granted and those this build names (`built`), in its order; when it was granted;
 * why a record found is not honoured; that the last revoke could not write its record; how many presses it covered since Daoris
 * started; and the repositories a covered hold held in this run.
 */
export type StandingConfirmationNow = {
  offered: boolean;
  on: boolean;
  presses: string[];
  built: string[];
  since: string | null;
  unread: StandingUnread | null;
  unwritten: boolean;
  used: number;
  held: string[];
  /** The roles a terminal holds this run (D233 point 2): a role the catalogue staffs with the terminal and not here is not started. */
  holds: RoleHoldNow[];
  /** The holders living this run, each a terminal Daoris started, which a later start may add a role to (D209 §2.2). */
  holders: HolderNow[];
  /** This run's steward acts, newest first, from Daoris's memory (D209 §5.1): what *Notifications*' line lists. */
  stewardActs: StewardActNow[];
  /** The steward's desk this run, newest first, from Daoris's memory (STEWARDROLE1c, D209 §6.2): what the line shows under its acts. */
  stewardDesk: DeskRequestNow[];
};

/**
 * One request at the steward's desk, as `STATE`'s `standingConfirmation.stewardDesk` says it (STEWARDROLE1c; the steward design §6.2):
 * the act any caller asked the steward to make, on what, and why, all three the caller's own words, which grant nothing; when it was
 * filed; whether it still waits, was answered by the steward's act, was declined by the steward with a reason, or was left open when
 * Daoris stopped (COMMS1l: the desk is kept across a restart, and nothing answers a request from before it). Never the caller.
 */
export type DeskRequestNow = {
  id: string;
  act: string;
  target: string;
  why: string;
  filed: string;
  state: 'open' | 'answered' | 'declined' | 'interrupted';
  closed: string | null;
  answeredBy: string | null;
  reason: string | null;
};

/**
 * One act the steward made this run, as `STATE`'s `standingConfirmation.stewardActs` says it (STEWARDROLE1b; the steward design
 * §5.1, §5.4): its id, the act, its target (a repository, or a quest and its session), when, whether it still stands (the owner has
 * not acted on that target since, so the owner's reverse press is offered), and, for a stop, whether its quest still waits on a
 * *Try again*.
 */
export type StewardActNow = {
  id: string;
  act: string;
  repository: string | null;
  quest: string | null;
  session: string | null;
  at: string;
  standing: boolean;
  waiting: boolean;
};

/** The acts a steward's line and ask have words for (the steward design §3.1 rows 1 to 8, and a repository's workflow); a later build's is said by its name. */
export const STEWARD_ACTS = ['drive', 'undrive', 'trees', 'hold', 'resume', 'stop', 'finish', 'retry', 'workflow'] as const;

/** Whether the page has words for this act. */
export const stewardActKnown = (act: string) => (STEWARD_ACTS as readonly string[]).includes(act);

/** The acts that set a repository's driving, which Setup → Driving says the steward set (§5.1's last rows). */
const SETS_DRIVING = new Set(['drive', 'undrive', 'trees', 'hold', 'resume']);

/**
 * When the steward last set this repository's driving, from this run's memory: the newest act among drive, undrive, trees, hold and
 * resume naming it, matched without case as the driver matches a repository, that still stands; null where none does.
 */
export function stewardSetAt(acts: readonly StewardActNow[], repository: string): string | null {
  const lower = repository.toLowerCase();
  const set = acts.filter((each) => each.standing && SETS_DRIVING.has(each.act) && each.repository?.toLowerCase() === lower);
  if (set.length === 0) return null;
  return set.reduce((newest, each) => (Date.parse(each.at) > Date.parse(newest.at) ? each : newest)).at;
}

/** The owner's own press that reverses a steward's act (the steward design §5.4), each a press the window already sends. */
export type StewardReverse = 'stopDriving' | 'drive' | 'treesOff' | 'resume' | 'hold' | 'retry';

const REVERSES: Readonly<Record<string, StewardReverse>> = {
  drive: 'stopDriving', undrive: 'drive', trees: 'treesOff', hold: 'resume', resume: 'hold', stop: 'retry',
};

/**
 * The owner's reverse press for a steward's act, only while it stands and only where it names what the press carries: a repository
 * for a setting, a quest for a stop. A finish and a Try again have none (§5.4: a finish neither accepts nor closes its quest).
 */
export function stewardReverse(act: StewardActNow): StewardReverse | null {
  const press = REVERSES[act.act];
  if (!press || !act.standing) return null;
  if (press === 'retry') return act.quest ? press : null;
  return act.repository ? press : null;
}

/** A living holder as the shell says it (the modules' `HolderNow`): its id, when its tab was opened, its roles. Never a key. */
export type HolderNow = { id: string; since: string | null; roles: string[] };

/** A grant's or a revoke's answer (`SET_STANDING_CONFIRMATION`): done, or refused as data with a code the page words. */
export type StandingAnswer = { done: boolean; why: string | null; message: string };

/** The presses this page has words for (design §6.1); a later build's press is said by its own name. */
const PRESSES = new Set(['stop', 'finish', 'hold', 'resume']);

/** One press in words: *stop a session*. */
export const standingPressWord = (t: TFunction, press: string) =>
  (PRESSES.has(press) ? t(`settings.daoris.standing.press.${press}`) : press);

/** The presses named, in words and in the order given, joined as the reader's language joins a list. */
export const standingPressList = (t: TFunction, presses: readonly string[], language?: string) =>
  list(presses.map((press) => standingPressWord(t, press)), language);

/**
 * The codes a grant or a revoke is refused by (the modules' `PersonRoutes`, `StandingConfirmation`), each said in the catalogue's
 * words. A code a later driver adds is said in the driver's own sentence.
 */
const REFUSALS = new Set(['window-only', 'not-offered', 'unknown-press', 'newer', 'unwritten', 'presses', 'no-pipe']);

/** A refusal of the standing confirmation's press in words, by its code, else the driver's own sentence. */
export const standingSaid = (t: TFunction, why: string | null, message: string) =>
  (why && REFUSALS.has(why) ? t(`settings.daoris.standing.refused.${why}`) : message || why || '');

/**
 * A role a terminal holds this run, as `STATE`'s `standingConfirmation.holds` says it (D233 point 2; the modules' `RoleHoldNow`): the
 * role's catalogue id, its kind (`supervisor` or `steward`), the living holder, since when, and how many acts it made. Never a key.
 */
export type RoleHoldNow = { role: string; kind: string; holder: string; since: string | null; answers: number };

/** The supervisor's overrule window, in minutes (the supervisor design §6.3): 1 to 60, five where its role keeps none. */
export const WINDOW = { shortest: 1, longest: 60, standing: 5 } as const;

/** Whether `minutes` is a window the supervisor's role may keep: a whole number from 1 to 60. */
export const inWindow = (minutes: number) => Number.isInteger(minutes) && minutes >= WINDOW.shortest && minutes <= WINDOW.longest;

/** A start of a role's terminal (`START_ROLE_TERMINAL`): done, or refused as data with its code. Never a key. */
export type RoleTerminalAnswer = { done: boolean; why: string | null; message: string };
