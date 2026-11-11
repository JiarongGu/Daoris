// The roles' judge as the page shows it: its shape as the page reads it, the
// steward's acts and desk, and the roles a terminal holds. The reader is the bridge's (`standingOf`, `bridge/person.ts`).

import { type Place } from '../help/focus';
import type { PressReceiptNow } from './pressReceipt';

/**
 * The roles' judge as `STATE`'s `standingConfirmation` says it (the modules' `StandingNow`): the terminals that hold a role this run,
 * the holders living, the steward's acts and its desk.
 */
export type StandingConfirmationNow = {
  /** The roles a terminal holds this run: a role the catalogue staffs with the terminal and not here is not started. */
  holds: RoleHoldNow[];
  /** The holders living this run, each a terminal Daoris started, which a later start may add a role to. */
  holders: HolderNow[];
  /** This run's steward acts, newest first, from Daoris's memory: what *Notifications*' line lists. */
  stewardActs: StewardActNow[];
  /** The steward's desk this run, newest first, from Daoris's memory: what the line shows under its acts. */
  stewardDesk: DeskRequestNow[];
  /** What Ask Daoris pressed this run, newest first, from Daoris's memory: the second group of *Notifications*' line. */
  helpPresses: HelpPressNow[];
};

/** How an Ask Daoris press stands: one run with no card under way, done or not; one asked on a card, its card's state. */
export const HELP_PRESS_OUTCOMES = ['running', 'done', 'failed', 'waiting', 'allowed', 'refused', 'changed', 'unanswered'] as const;
export type HelpPressOutcome = (typeof HELP_PRESS_OUTCOMES)[number];

/** One press Ask Daoris made this run, as `STATE`'s `standingConfirmation.helpPresses` says it (the modules' `HelpPressNow`). */
export type HelpPressNow = {
  id: string;
  module: string;
  route: string;
  session: string;
  scope: string | null;
  target: string | null;
  at: string;
  outcome: HelpPressOutcome;
  /** The workspace or repository the press named; none where it named neither, or until the driver placed it. */
  place?: Place | null;
  /** Whether that place lay beyond its conversation's focus at the press. */
  beyond?: boolean;
  /** That it asked the person on a card in restricted mode rather than ran: Notifications' and the receipt's *Asked you*. */
  asked?: boolean;
  receipt?: PressReceiptNow;
  /** The words a rule refused the press in, before any run or card: the growth switch's among them. */
  why?: string;
};

/** The owner's own press that takes back an Ask Daoris press the window already sends: the work's resume, for a pause. */
export type HelpReverse = 'resume';

/** The reverse press of a press Ask Daoris made: only a done pause, naming the quest or ask its resume carries. */
export function helpReverse(press: HelpPressNow): HelpReverse | null {
  return press.route === 'WORK_PAUSE' && press.outcome === 'done' && (press.scope === 'quest' || press.scope === 'ask') && press.target ? 'resume' : null;
}

/**
 * One request at the steward's desk, as `STATE`'s `standingConfirmation.stewardDesk` says it:
 * the act any caller asked the steward to make, on what, and why, all three the caller's own words, which grant nothing; when it is
 * filed; whether it still waits, is answered, is declined with a reason, or is left open when Daoris stops. Never the caller.
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
 * One act the steward made this run, as `STATE`'s `standingConfirmation.stewardActs` says it: its id, the act, its target (a repository, or a quest and its session), when, whether it still stands (the owner has
 * not acted on that target since, so the owner's reverse press is offered), and, for a stop, whether its quest still waits on a
 * *Try again*; and whether a supervisor allowed it outside the steward's grants.
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
  allowed: boolean;
};

/** The acts a steward's line and ask have words for (and a repository's workflow); a later build's is said by its name. */
export const STEWARD_ACTS = ['drive', 'undrive', 'hold', 'resume', 'stop', 'finish', 'retry', 'workflow', 'pause', 'unpause', 'done', 'delete', 'updatenow', 'updatecancel', 'clean', 'handover'] as const;

/** Whether the page has words for this act. */
export const stewardActKnown = (act: string) => (STEWARD_ACTS as readonly string[]).includes(act);

/** The acts that set a repository's driving, which Setup → Driving says the steward set (§5.1's last rows). */
const SETS_DRIVING = new Set(['drive', 'undrive', 'hold', 'resume']);

/**
 * When the steward last set this repository's driving, from this run's memory: the newest act among drive, undrive, hold and
 * resume naming it, matched without case as the driver matches a repository, that still stands; null where none does.
 */
export function stewardSetAt(acts: readonly StewardActNow[], repository: string): string | null {
  const lower = repository.toLowerCase();
  const set = acts.filter((each) => each.standing && SETS_DRIVING.has(each.act) && each.repository?.toLowerCase() === lower);
  if (set.length === 0) return null;
  return set.reduce((newest, each) => (Date.parse(each.at) > Date.parse(newest.at) ? each : newest)).at;
}

/** The owner's own press that reverses a steward's act, each a press the window already sends. */
export type StewardReverse = 'stopDriving' | 'drive' | 'resume' | 'hold' | 'retry';

const REVERSES: Readonly<Record<string, StewardReverse>> = {
  drive: 'stopDriving', undrive: 'drive', hold: 'resume', resume: 'hold', stop: 'retry',
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

/** A living holder as the shell says it (the modules' `HolderNow`): its id, when its tab is opened, its roles. Never a key. */
export type HolderNow = { id: string; since: string | null; roles: string[] };

/**
 * A role a terminal holds this run, as `STATE`'s `standingConfirmation.holds` says it (the modules' `RoleHoldNow`): the
 * role's catalogue id, its kind (`supervisor` or `steward`), the living holder, since when, and how many acts it made. Never a key.
 */
export type RoleHoldNow = { role: string; kind: string; holder: string; since: string | null; answers: number };

/** The supervisor's overrule window, in minutes: 1 to 60, five where its role keeps none. */
export const WINDOW = { shortest: 1, longest: 60, standing: 5 } as const;

/** Whether `minutes` is a window the supervisor's role may keep: a whole number from 1 to 60. */
export const inWindow = (minutes: number) => Number.isInteger(minutes) && minutes >= WINDOW.shortest && minutes <= WINDOW.longest;

/** A start of a role's terminal (`START_ROLE_TERMINAL`): done, or refused as data with its code. Never a key. */
export type RoleTerminalAnswer = { done: boolean; why: string | null; message: string };
