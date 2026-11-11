import type { TFunction } from 'i18next';
import { ago, stamp } from '../../locales/format';
import type { DetailRow } from './DetailsFold';

/** One count of a line as `SESSION_SUPERVISOR` answers it: what it stands at, and the rule that acts on it, where one does. */
export type LineCountRead = {
  count: string;
  n: number | null;
  known: 'exact' | 'at-least' | 'unknown';
  why?: string | null;
  from: string;
  /** What Daoris does at `at`, by the person's rule of `place`. */
  watch?: { at: number; do: string; place: string } | null;
};

/** One act a person's rule made on the line, as kept. */
export type RuleActRead = {
  count: string;
  n: number;
  known: boolean;
  rule: { at: number; do: string; place: string };
  point: string;
  when: string;
};

/**
 * The supervisor's decision on a point a person's `refer` rule held the line at, as `SESSION_SUPERVISOR` answers it: the count the rule
 * read and its place, the supervisor's act (`go-on` or `hold`), when it is kept and when it goes on, and where it stands.
 */
export type CourseRead = {
  id: string;
  kind?: string;
  point: string;
  count: string;
  n: number;
  rule: string;
  place: string;
  do: string;
  state: string;
  why?: string | null;
  holder?: { kind: string; parent?: string | null } | null;
  keptAt: string;
  goesOnAt: string;
  stateAt: string;
  earlier: boolean;
};

/** The supervisor's decision on a step of the line's run waiting for the person's press, as `SESSION_SUPERVISOR` answers it. */
export type LandingRead = {
  id: string;
  kind?: string;
  run: string;
  node: string;
  attempt: number;
  do: string;
  /** An Accept's kind of work, as its attempt's review record holds it (`branch`, `checkout`, `folder`, `copy`, `plan`, `answer`). */
  work?: string | null;
  state: string;
  why?: string | null;
  holder?: { kind: string; parent?: string | null } | null;
  keptAt: string;
  goesOnAt: string;
  stateAt: string;
  earlier: boolean;
};

/** The supervisor's allow, as `SESSION_SUPERVISOR` answers it: the subject it answered (`request`, `widening` or `steward`), its act and state. */
export type AllowRead = {
  id: string;
  kind?: string;
  shape: string;
  subject: string;
  repository: string;
  do: string;
  role?: string | null;
  act?: string | null;
  state: string;
  why?: string | null;
  bound?: string | null;
  reasons?: { says: string; source: string }[];
  holder?: { kind: string; parent?: string | null } | null;
  keptAt: string;
  goesOnAt: string;
  stateAt: string;
};

/** What the head and *Details* say of the supervisor on one line. */
export type LineSupervision = {
  counts: LineCountRead[]; acts: RuleActRead[]; courses: CourseRead[]; landings?: LandingRead[]; allows?: AllowRead[];
};

export const NO_SUPERVISION: LineSupervision = { counts: [], acts: [], courses: [], landings: [], allows: [] };

/** The answer read defensively: an older driver answers none of the fields. */
export function readSupervision(answer: unknown): LineSupervision {
  const read = (answer ?? {}) as Partial<LineSupervision>;
  return {
    counts: Array.isArray(read.counts) ? read.counts : [],
    acts: Array.isArray(read.acts) ? read.acts : [],
    courses: Array.isArray(read.courses) ? read.courses : [],
    landings: Array.isArray(read.landings) ? read.landings : [],
    allows: Array.isArray(read.allows) ? read.allows : [],
  };
}

export const allowSays = (t: TFunction, allow: AllowRead): string =>
  t(`work.supervision.allow.${allow.do}`, { when: ago(allow.keptAt), defaultValue: allow.do });

/** What the supervisor did with the step and when it takes effect, in one sentence: an Accept says the kind of work it took. */
export const landingSays = (t: TFunction, landing: LandingRead): string =>
  t(landing.do === 'accept' && landing.work ? `work.supervision.landing.accept.${landing.work}` : `work.supervision.landing.${landing.do}`,
    { at: stamp(landing.goesOnAt), defaultValue: landing.do });

/** A rule's place in the person's words: this Daoris, a line, or the repository or workspace by its name. */
export function placeWord(t: TFunction, place: string): string {
  if (place === 'machine') return t('settings.supervisor.machine');
  const at = place.indexOf(':');
  if (at < 0) return place;
  return place.slice(0, at) === 'line' ? t('work.supervision.thisLine') : place.slice(at + 1);
}

const actWord = (t: TFunction, act: string) => t(`settings.supervisor.act.${act}`, { defaultValue: act });
const countWord = (t: TFunction, count: string) => t(`settings.supervisor.count.${count}`, { defaultValue: count });

/** A count's figure as it is known: exact, a floor, or not known. */
function figure(t: TFunction, count: LineCountRead): string {
  if (count.n === null || count.known === 'unknown') return t('work.supervision.unknown');
  return count.known === 'at-least' ? t('work.supervision.atLeast', { n: count.n }) : String(count.n);
}

/** *Details*' course: each count of the line with what it stands at and what the rule does at its number. */
export function countRows(t: TFunction, { counts }: LineSupervision): DetailRow[] {
  return counts.map((count) => {
    const watch = count.watch;
    const rule = watch ? t('work.supervision.ruleAt', { act: actWord(t, watch.do), at: watch.at, place: placeWord(t, watch.place) }) : null;
    return { label: countWord(t, count.count), value: rule ? `${figure(t, count)} · ${rule}` : figure(t, count) };
  });
}

/** *Details*' Supervisor line: how many times the person's rules acted on the line and the last act; no row where none did. */
export function supervisorRow(t: TFunction, { acts }: LineSupervision): DetailRow {
  const label = t('work.supervision.supervisor');
  const last = acts.at(-1);
  if (!last) return { label, value: null };
  return {
    label,
    value: t('work.supervision.acted', {
      count: acts.length, when: ago(last.when), counted: countWord(t, last.count), n: last.n, act: actWord(t, last.rule.do),
    }),
  };
}

/** What the supervisor chose on the point, which rule referred it, and when, in one sentence. */
export function courseSays(t: TFunction, course: CourseRead): string {
  return t('work.supervision.referred', {
    counted: countWord(t, course.count).toLowerCase(), place: placeWord(t, course.place), n: course.n,
    choice: t(`work.supervision.choice.${course.do}`, { defaultValue: course.do }), when: ago(course.keptAt),
  });
}

/** Where the decision stands: when it goes on, that it did, that the person overruled it, why it did not, or that it waits for them. */
export function courseStands(t: TFunction, course: CourseRead): string {
  const why = course.why ? t(`work.supervision.why.${course.why}`, { defaultValue: course.why }) : '';
  return t(`work.supervision.course.${course.state}`, { at: stamp(course.goesOnAt), why, defaultValue: course.state });
}

/** The decision said whole: what the supervisor chose, then where it stands, joined the language's way. */
export const courseWhole = (t: TFunction, course: CourseRead): string =>
  t('work.supervision.whole', { says: courseSays(t, course), stands: courseStands(t, course) });

/** The kind a decision decided, in the words the card and *Details* name it by; a driver that names none decided a course. */
export const decisionKind = (t: TFunction, course: CourseRead): string => {
  const kind = course.kind ?? 'course';
  return t(`work.supervision.kind.${kind}`, { defaultValue: kind });
};

/** *Details*' Supervisor's decision: the newest course decision on the line, its kind named, said whole; no value where there is none. */
export function courseRow(t: TFunction, { courses }: LineSupervision): DetailRow {
  const last = courses.at(-1);
  return last
    ? { label: t('work.supervision.decisionOn', { kind: decisionKind(t, last) }), value: courseWhole(t, last) }
    : { label: t('work.supervision.decision'), value: null };
}

/** The count the head's chip names and its words: one a rule holds the line at, or one short of it. */
export type SupervisionChip = { count: string; n: number; at: number; held: boolean; text: string };

/**
 * The head's chip (the context counters design §7.4): *answers alone 2 of 2* where a rule holds the line, or the count one short of the
 * rule that would. Only a count Daoris acts on by a hold or an ask, at a number known exactly; none otherwise.
 */
export function supervisionChip(t: TFunction, { counts }: LineSupervision): SupervisionChip | null {
  let near: SupervisionChip | null = null;
  for (const count of counts) {
    const watch = count.watch;
    if (!watch || count.known !== 'exact' || count.n === null || (watch.do !== 'hold' && watch.do !== 'ask')) continue;
    const held = count.n >= watch.at;
    if (!held && count.n !== watch.at - 1) continue;
    const text = t('work.supervision.chip', { counted: countWord(t, count.count).toLowerCase(), n: count.n, at: watch.at });
    if (held) return { count: count.count, n: count.n, at: watch.at, held, text };
    near ??= { count: count.count, n: count.n, at: watch.at, held, text };
  }
  return near;
}
