import type { TFunction } from 'i18next';
import { ago } from '../../locales/format';
import type { DetailRow } from './DetailsFold';

/** One count of a line as `SESSION_SUPERVISOR` answers it: what it stands at, and the rule that acts on it, where one does. */
export type LineCountRead = {
  count: string;
  n: number | null;
  known: 'exact' | 'at-least' | 'unknown';
  why?: string | null;
  from: string;
  /** What Daoris does at `at`, by the person's rule of `place`; no place is Daoris's own number. */
  watch?: { at: number; do: string; place?: string | null } | null;
};

/** One act a person's rule made on the line, as it was kept. */
export type RuleActRead = {
  count: string;
  n: number;
  known: boolean;
  rule: { at: number; do: string; place: string };
  point: string;
  when: string;
};

/** What the head and *Details* say of the supervisor on one line. */
export type LineSupervision = { counts: LineCountRead[]; acts: RuleActRead[] };

export const NO_SUPERVISION: LineSupervision = { counts: [], acts: [] };

/** The answer read defensively: an older driver answers neither field. */
export function readSupervision(answer: unknown): LineSupervision {
  const read = (answer ?? {}) as Partial<LineSupervision>;
  return {
    counts: Array.isArray(read.counts) ? read.counts : [],
    acts: Array.isArray(read.acts) ? read.acts : [],
  };
}

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
    const rule = watch
      ? t(watch.place ? 'work.supervision.ruleAt' : 'work.supervision.ownAt', {
        act: actWord(t, watch.do), at: watch.at, place: watch.place ? placeWord(t, watch.place) : '',
      })
      : null;
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
