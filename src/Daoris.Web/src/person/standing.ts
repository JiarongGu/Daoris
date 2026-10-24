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
  /** Whether this build offers the owner's appointments (SUPERVISOR1a-ii): false until the activation's proof, and never without the grant's. */
  appointmentsOffered: boolean;
  /** The owner's appointments the record holds, dormant where found as Daoris started. */
  appointments: AppointmentNow[];
};

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
  repository: string;
  holder: string;
  since: string | null;
  dormant: boolean;
  answers: number;
};

/** An appointment's or a dismissal's answer (`SET_APPOINTMENT`): done, or refused as data with a code the page words. Never a key. */
export type AppointmentAnswer = { done: boolean; why: string | null; message: string };

/** The codes an appointment is refused by (the modules' `PersonRoutes`, `StandingConfirmation`), each said in the catalogue's words. */
const APPOINT_REFUSALS = new Set([
  'window-only', 'not-offered', 'unknown-role', 'unknown-repository', 'newer', 'unwritten', 'unstarted', 'no-pipe',
]);

/** A refusal of an appointment in words, by its code, else the driver's own sentence. */
export const appointmentSaid = (t: TFunction, why: string | null, message: string) =>
  (why && APPOINT_REFUSALS.has(why) ? t(`projects.supervisor.refused.${why}`) : message || why || '');
