import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import type { TFunction } from 'i18next';
import { moment } from '../locales/format';
import { cn } from '../lib/cn';
import { entryName, type MilestoneUnit } from './milestone';

/** The proofs the driver names by code (`EndBy`): any other code is said by the driver's own sentence. */
const PROOFS = ['merged', 'empty', 'on-line', 'pull-request', 'landed', 'carried', 'inside'] as const;

const what = entryName;

/** The proof of an end in the reader's language, or the driver's own sentence where the page has no word for its code. */
function proof(t: TFunction, unit: MilestoneUnit): string {
  const code = PROOFS.find((one) => one === unit.end.by);
  return code ? t(`work.milestone.by.${code}`, { on: unit.end.on ?? '' }) : unit.end.said;
}

function endSentence(t: TFunction, unit: MilestoneUnit): string {
  switch (unit.end.state) {
    case 'ended': return [
      t('work.milestone.end.ended', { proof: proof(t, unit) }),
      unit.end.at ? t('work.milestone.end.since', { when: moment(unit.end.at) }) : null,
    ].filter(Boolean).join(' ');
    case 'handed': return t('work.milestone.end.handed', { said: unit.end.said });
    case 'unknown': return t('work.milestone.end.unknown', { said: unit.end.said });
    // CLEANUP1: over without its result (the person removed its branch outside Daoris), never read as not reached.
    case 'dropped': return t('work.milestone.end.dropped', { said: unit.end.said });
    default: return t('work.milestone.end.notEnded', { said: unit.end.said });
  }
}

/** The earliest date a unit's recovery is kept until: what a person must act by. */
function keptUntil(unit: MilestoneUnit): string | null {
  const dates = unit.recovery.map((entry) => entry.until).filter((date): date is string => Boolean(date)).sort();
  return dates[0] ?? null;
}

function Row({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="grid min-w-0 grid-cols-[5.5rem_minmax(0,1fr)] gap-x-3">
      <dt className="text-ink-faint">{label}</dt>
      <dd className="m-0 grid min-w-0 gap-0.5 text-ink [overflow-wrap:anywhere]">{children}</dd>
    </div>
  );
}

/**
 * **Where this work stands** (FABRIC-MILESTONE1b, the fabric events design §D0): whether its end was reached and by what proof, the
 * result kept, the clean-up pending (what waits and why, a blocked entry apart) or completed (what went), and the recovery kept and
 * until when. The same facts `daoris-driver work state` prints, read from the driver's answer and said in the reader's language.
 *
 * @remarks
 * **The driver's clauses pass through as recorded** (`detail`, and `said` where the page has no word for a proof's code): they name
 * what git or the records found, as a note's English does. A label, a state, a kind and a proof's code are the page's own words.
 * Nothing here is judged again: an end the keeper has not proved is *not reached*, never guessed.
 *
 * A molecule: the unit arrives as props.
 */
export function WorkMilestone({ unit, className }: { unit: MilestoneUnit; className?: string }) {
  const { t } = useTranslation();
  const waits = unit.cleanup.waits;
  return (
    <section aria-label={t('work.milestone.title')} className={cn('grid min-w-0 gap-1.5 text-small', className)}>
      <h3 className="m-0 text-small font-semibold text-ink-soft">{t('work.milestone.title')}</h3>
      <dl className="m-0 grid min-w-0 gap-1">
        <Row label={t('work.milestone.end.label')}>
          <span>{endSentence(t, unit)}</span>
        </Row>
        <Row label={t('work.milestone.result.label')}>
          {unit.result.length === 0
            ? <span>{t('work.milestone.result.none')}</span>
            : <span>{t('work.milestone.result.kept', { what: unit.result.map((entry) => what(t, entry)).join('; ') })}</span>}
        </Row>
        <Row label={t('work.milestone.cleanup.label')}>
          {unit.cleanup.state === 'completed'
            ? (
              <span>
                {unit.cleanup.gone.length === 0
                  ? t('work.milestone.cleanup.completedNone')
                  : t('work.milestone.cleanup.completed', { what: unit.cleanup.gone.map((entry) => what(t, entry)).join(', ') })}
              </span>
            )
            : (
              <>
                <span>{t('work.milestone.cleanup.pending')}</span>
                {waits.map((entry, index) => (
                  <span key={`${entry.kind}-${entry.resource}-${index}`} className={entry.disposition === 'blocked' ? 'text-ink-danger' : undefined}>
                    {t(entry.disposition === 'blocked' ? 'work.milestone.cleanup.blocked' : 'work.milestone.cleanup.waits', { what: what(t, entry), detail: entry.detail })}
                  </span>
                ))}
                {unit.cleanup.gone.length > 0 && (
                  <span>{t('work.milestone.cleanup.removed', { what: unit.cleanup.gone.map((entry) => what(t, entry)).join(', ') })}</span>
                )}
              </>
            )}
        </Row>
        <Row label={t('work.milestone.recovery.label')}>
          {unit.recovery.length === 0
            ? <span>{t('work.milestone.recovery.none')}</span>
            : unit.recovery.map((entry, index) => (
              <span key={`${entry.kind}-${entry.resource}-${index}`}>
                {entry.until
                  ? t('work.milestone.recovery.until', { what: what(t, entry), when: moment(entry.until) })
                  : t('work.milestone.recovery.kept', { what: what(t, entry) })}
              </span>
            ))}
        </Row>
      </dl>
    </section>
  );
}

/**
 * The milestone in one line, for a row that names a branch (FABRIC-MILESTONE1b, *Branches*): the end, whether the clean-up is
 * pending or completed, and the recovery kept, when there is one.
 */
export function MilestoneLine({ unit, className }: { unit: MilestoneUnit; className?: string }) {
  const { t } = useTranslation();
  const until = keptUntil(unit);
  const parts = [
    t(`work.milestone.line.${({ ended: 'ended', handed: 'handed', unknown: 'unknown', dropped: 'dropped' } as Record<string, string>)[unit.end.state] ?? 'notEnded'}`),
    t(unit.cleanup.state === 'completed' ? 'work.milestone.line.cleanupCompleted' : 'work.milestone.line.cleanupPending'),
    until ? t('work.milestone.line.recoveryUntil', { when: moment(until) })
      : unit.recovery.length > 0 ? t('work.milestone.line.recovery') : null,
  ].filter(Boolean);
  return <span className={cn('text-small text-ink-soft', className)}>{parts.join(' · ')}</span>;
}
