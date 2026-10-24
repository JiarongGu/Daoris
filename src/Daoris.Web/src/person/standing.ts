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
  /** Whether this build offers any role (STEWARDROLE1a-i, D209 §2.4): derived from the two role flags below, gating nothing by itself. */
  appointmentsOffered: boolean;
  /** Whether this build offers the supervisor, by its own flag: false until SUPERVISOR1a-iii, whatever D201's grant is. */
  supervisorOffered: boolean;
  /** Whether this build offers the steward, by its own flag: false until STEWARDROLE1d. */
  stewardOffered: boolean;
  /** The owner's appointments the record holds, dormant where found as Daoris started. */
  appointments: AppointmentNow[];
  /** The holders living this run, each a terminal Daoris started, which a later *Appoint…* may add a role to (D209 §2.2). */
  holders: HolderNow[];
  /**
   * The acts this build offers a steward's appointment, in its order (STEWARDROLE1b, D209 §2.3): what *Appoint…* and *Appoint again*
   * name, never an appointment's own `acts`.
   */
  stewardActsOffered: string[];
  /** This run's steward acts, newest first, from Daoris's memory (D209 §5.1): what *Notifications*' line lists. */
  stewardActs: StewardActNow[];
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

/** The acts a steward's line and ask have words for (the steward design §3.1 rows 1 to 8); a later build's is said by its name. */
export const STEWARD_ACTS = ['drive', 'undrive', 'trees', 'hold', 'resume', 'stop', 'finish', 'retry'] as const;

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
 * One of the owner's appointments as `STATE`'s `standingConfirmation.appointments` says it (SUPERVISOR1a-ii, D208; the modules'
 * `AppointmentNow`): the role, the repository, the holder's kind, since when, whether it was found as Daoris started and so admits no
 * holder until *Appoint again* (`dormant`), and how many acts its holder made under it in this run. Never its generation nor any key.
 */
export type AppointmentNow = {
  role: string;
  /** The repository its scope names; empty for the steward's `machine`. */
  repository: string;
  holder: string;
  since: string | null;
  dormant: boolean;
  answers: number;
  /** Its scope (STEWARDROLE1a-i, D209 §2.3): `machine`, `repository:<name>`, or the supervisor's `workspace:<name>` (SUPERVISOR1e). */
  scope: string;
  /** The living holder that holds it this run; null where it is dormant. */
  holderId: string | null;
  /** The acts it grants (STEWARDROLE1b, D209 §2.3), as the ask named them; empty for the supervisor's. Never read into an ask. */
  acts: string[];
  /** The owner's set-up tick at the appointment (§2.3, §3.1 row 12); never read into an ask, which shows it off. */
  setup: boolean;
  /** The workspace its scope names (SUPERVISOR1e); empty for any other scope. */
  workspace: string;
  /** How many minutes the supervisor's answers are held for the owner's overrule, 1 to 60 (SUPERVISOR1e); five where none was set. */
  window: number;
};

/** The overrule window an appointment may set, in minutes (SUPERVISOR1e, the supervisor design §6.3): 1 to 60, five where none is set. */
export const WINDOW = { shortest: 1, longest: 60, standing: 5 } as const;

/** Whether `minutes` is a window an appointment may set: a whole number from 1 to 60. */
export const inWindow = (minutes: number) => Number.isInteger(minutes) && minutes >= WINDOW.shortest && minutes <= WINDOW.longest;

/** An appointment's or a dismissal's answer (`SET_APPOINTMENT`): done, or refused as data with a code the page words. Never a key. */
export type AppointmentAnswer = { done: boolean; why: string | null; message: string };

/** The codes an appointment is refused by (the modules' `PersonRoutes`, `StandingConfirmation`), each said in the catalogue's words. */
const APPOINT_REFUSALS = new Set([
  'window-only', 'not-offered', 'unknown-role', 'unknown-repository', 'newer', 'unwritten', 'unstarted', 'no-pipe',
  'scope', 'choose-terminal', 'no-holder', 'revoked-holder', 'holder-harness', 'unknown-workspace', 'window',
]);

/** A refusal of an appointment in words, by its code, else the driver's own sentence. */
export const appointmentSaid = (t: TFunction, why: string | null, message: string) =>
  (why && APPOINT_REFUSALS.has(why) ? t(`projects.supervisor.refused.${why}`) : message || why || '');

/** The refusals whose supervisor's sentence names the supervisor, said for the steward in its own words. */
const STEWARD_REFUSALS = new Set(['not-offered', 'unstarted']);

/** A refusal of the steward's appointment in words: its own where the supervisor's names the supervisor, else the shared one. */
export const stewardAppointmentSaid = (t: TFunction, why: string | null, message: string) =>
  (why && STEWARD_REFUSALS.has(why) ? t(`settings.daoris.steward.refused.${why}`) : appointmentSaid(t, why, message));
