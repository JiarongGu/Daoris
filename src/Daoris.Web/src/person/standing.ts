// The roles' judge as the page shows it: its shape as the page reads it, the
// steward's acts and desk, and the roles a terminal holds. The reader is the bridge's (`standingOf`, `bridge/person.ts`).

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
  /** The steward's lands a supervisor allowed that wait their window, soonest first: the line says when each lands and offers *Overrule*. */
  stewardLands: StewardLandNow[];
  /** What Ask Daoris pressed this run in full access, newest first, from Daoris's memory: the second group of *Notifications*' line. */
  helpPresses: HelpPressNow[];
};

/**
 * One press Ask Daoris made in full access with no card, as `STATE`'s `standingConfirmation.helpPresses` says it (the modules'
 * `HelpPressNow`): the route as the window words it, the conversation that pressed it, what it named, when and how it ended.
 */
export type HelpPressNow = {
  id: string;
  module: string;
  route: string;
  session: string;
  scope: string | null;
  target: string | null;
  at: string;
  outcome: 'running' | 'done' | 'failed';
};

/** The owner's own press that takes back an Ask Daoris press the window already sends: the work's resume, for a pause. */
export type HelpReverse = 'resume';

/** The reverse press of a press Ask Daoris made: only a pause that was done, naming the quest or ask its resume carries. */
export function helpReverse(press: HelpPressNow): HelpReverse | null {
  return press.route === 'WORK_PAUSE' && press.outcome === 'done' && (press.scope === 'quest' || press.scope === 'ask') && press.target ? 'resume' : null;
}

/**
 * A `land` of the steward's that a supervisor allowed and that waits its window, as `STATE`'s `standingConfirmation.stewardLands` says
 * it: the request's id, what it acts on and when it lands. *Overrule* names the request and its session.
 */
export type StewardLandNow = {
  ask: string;
  act: string;
  repository: string | null;
  quest: string | null;
  session: string | null;
  landsAt: string;
};

/**
 * One request at the steward's desk, as `STATE`'s `standingConfirmation.stewardDesk` says it:
 * the act any caller asked the steward to make, on what, and why, all three the caller's own words, which grant nothing; when it is
 * filed; whether it still waits, is answered by the steward's act, is declined by the steward with a reason, or is left open when
 * Daoris stops (the desk is kept across a restart, and nothing answers a request from before it). Never the caller.
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
export const STEWARD_ACTS = ['drive', 'undrive', 'trees', 'hold', 'resume', 'stop', 'finish', 'retry', 'workflow', 'pause', 'unpause', 'done', 'delete', 'updatenow', 'updatecancel', 'land', 'clean', 'handover'] as const;

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

/** The owner's own press that reverses a steward's act, each a press the window already sends. */
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
