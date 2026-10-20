import { Fragment, type ReactNode, useId, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { TFunction } from 'i18next';
import { moment } from '../../locales/format';
import { cn } from '../../lib/cn';
import { DotMark, Icon } from '../../design/ui';
import { entryName, heldSentence, type MilestoneUnit } from './milestone';

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

/** `Dot`'s tones, by the value a fact has (the UI/UX design §3.7); a fact with no tone wears no mark. */
type FactTone = 'done' | 'waits' | 'failed' | 'ended' | 'idle';

/** The end's own mark: reached is done's, an end not reached or handed waits, an unknown one is neutral, never read as reached. */
const END_TONE: Record<string, FactTone> = { ended: 'done', handed: 'waits', unknown: 'idle', dropped: 'ended' };

/**
 * One fact and the mark of its own value. The words beside it say the value, so the mark is never hue alone; a fact with
 * nothing to mark (a recovery with nothing kept) keeps its place in the column and wears none, since no fact is marked
 * because another is (R4). The label column stacks over its words under 24rem of the list's own width.
 */
function Row({ label, tone, children }: { label: string; tone?: FactTone; children: ReactNode }) {
  return (
    <div className="grid min-w-0 grid-cols-[5.5rem_minmax(0,1fr)] gap-x-3 @max-[24rem]:grid-cols-1">
      <dt className="text-ink-faint">{label}</dt>
      <dd className="m-0 flex min-w-0 gap-2 text-ink">
        <span className="flex h-lh w-1.5 shrink-0 items-center">{tone && <DotMark tone={tone} />}</span>
        <span className="grid min-w-0 gap-0.5 [overflow-wrap:anywhere]">{children}</span>
      </dd>
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
 * **Four facts, never a sequence** (MILESTONEUX1, the UI/UX design R4 and §3.7): the end, the result, the clean-up and the
 * recovery are separate collections, each marked by its own value with `Dot`'s tone, so a clean-up completed with nothing kept
 * for recovery wears no recovery mark. **Folded to its line** (`MilestoneLine`, which says *blocked* and *held* too, so nothing
 * that waits on the person hides behind it) and opened in place; a held attempt stays its own row under the facts.
 *
 * A molecule: the unit arrives as props, and its one state is whether it is open. The page mounts it under the record's id,
 * so another record starts folded again.
 */
export function WorkMilestone({ unit, className, defaultOpen = false }: {
  unit: MilestoneUnit;
  className?: string;
  /** Open on first draw: a story's or a test's, since the page's own default is folded. */
  defaultOpen?: boolean;
}) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(defaultOpen);
  const facts = useId();
  return (
    <section aria-label={t('work.milestone.title')} className={cn('min-w-0', className)}>
      <h3 className="m-0 text-small font-normal">
        <button
          type="button"
          aria-expanded={open}
          aria-controls={open ? facts : undefined}
          onClick={() => setOpen((was) => !was)}
          className="flex w-full min-w-0 cursor-pointer items-start gap-1.5 border-0 bg-transparent p-0 text-left text-small text-ink-faint hover:text-ink"
        >
          <Icon name={open ? 'chevronDown' : 'chevronRight'} size={12} className="mt-1 shrink-0" />
          <span className="min-w-0 [overflow-wrap:anywhere]">
            <span className="font-semibold">{t('work.milestone.title')}</span>
            <MilestoneLine unit={unit} className="ml-2" />
          </span>
        </button>
      </h3>
      {open && <Facts id={facts} unit={unit} />}
    </section>
  );
}

/** The four facts, each marked by its own value, and a held attempt's row under them. */
function Facts({ id, unit }: { id: string; unit: MilestoneUnit }) {
  const { t } = useTranslation();
  const waits = unit.cleanup.waits;
  const blocked = waits.some((entry) => entry.disposition === 'blocked');
  const unworded = (unit.held ?? []).some((held) => !held.resolution);
  return (
    <dl id={id} className="m-0 mt-2 grid min-w-0 gap-1 pl-[18px] @container">
      <Row label={t('work.milestone.end.label')} tone={END_TONE[unit.end.state] ?? 'waits'}>
        <span>{endSentence(t, unit)}</span>
      </Row>
      <Row label={t('work.milestone.result.label')} tone={unit.result.length === 0 ? 'idle' : 'done'}>
        {unit.result.length === 0
          ? <span>{t('work.milestone.result.none')}</span>
          : <span>{t('work.milestone.result.kept', { what: unit.result.map((entry) => what(t, entry)).join('; ') })}</span>}
      </Row>
      <Row label={t('work.milestone.cleanup.label')} tone={unit.cleanup.state === 'completed' ? 'done' : blocked ? 'failed' : 'waits'}>
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
      {/* FABRIC-PERFORM1g: a plugin step's attempt held for the person, its node's place here; their word is What needs you's. */}
      {(unit.held?.length ?? 0) > 0 && (
        <Row label={t('work.milestone.held.label')} tone={unworded ? 'failed' : 'waits'}>
          {unit.held!.map((held) => (
            <span key={`${held.node}-${held.attempt}`} className={held.resolution ? undefined : 'text-ink-danger'}>{heldSentence(t, held)}</span>
          ))}
        </Row>
      )}
      {/* Nothing kept wears no mark: a clean-up completed with nothing kept for recovery is ordinary, no recovery done (R4). */}
      <Row label={t('work.milestone.recovery.label')} tone={unit.recovery.length === 0 ? undefined : 'done'}>
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
  );
}

/**
 * The milestone in one line, for a row that names a branch (FABRIC-MILESTONE1b, *Branches*) and for the record's fold head
 * (MILESTONEUX1): the end, whether the clean-up is pending or completed, *blocked* where a clean-up entry is, the recovery kept,
 * when there is one, and *held for you* where an attempt is held, so nothing that waits on the person hides behind a fold. What
 * waits on them wears the danger ink (a held attempt the person has already worded, only the plain one).
 */
export function MilestoneLine({ unit, className }: { unit: MilestoneUnit; className?: string }) {
  const { t } = useTranslation();
  const until = keptUntil(unit);
  const parts: ReactNode[] = [
    t(`work.milestone.line.${({ ended: 'ended', handed: 'handed', unknown: 'unknown', dropped: 'dropped' } as Record<string, string>)[unit.end.state] ?? 'notEnded'}`),
    t(unit.cleanup.state === 'completed' ? 'work.milestone.line.cleanupCompleted' : 'work.milestone.line.cleanupPending'),
    unit.cleanup.waits.some((entry) => entry.disposition === 'blocked')
      ? <span className="text-ink-danger">{t('work.milestone.line.blocked')}</span> : null,
    until ? t('work.milestone.line.recoveryUntil', { when: moment(until) })
      : unit.recovery.length > 0 ? t('work.milestone.line.recovery') : null,
    (unit.held?.length ?? 0) > 0
      ? <span className={unit.held!.some((held) => !held.resolution) ? 'text-ink-danger' : undefined}>{t('work.milestone.line.held')}</span> : null,
  ].filter(Boolean);
  return (
    <span className={cn('text-small text-ink-soft', className)}>
      {parts.map((part, index) => <Fragment key={index}>{index > 0 && ' · '}{part}</Fragment>)}
    </span>
  );
}
