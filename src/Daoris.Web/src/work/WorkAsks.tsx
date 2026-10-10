import { type ReactNode, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Inline, Prose } from '../ui';
import { InlineConfirm, type Answered } from './InlineConfirm';
import {
  abandonList, type AbandonOutcome, type GoingRow, type Line, say, type StayingRow, type WorkPlan, type WorkTarget,
} from './pausing';
import { PageSection } from './ViewMain';

/**
 * **A pause's ask** (PAUSE1e, D132 §2.6): a pause that ends work in flight asks once, under the header, as *Stop…* does
 * (D126 §3.3; platform language §4, a destructive edit asks once). It says what follows: how many running sessions stop,
 * that their trees keep what they wrote, and where they apply, that another machine may still take an open quest and that
 * a running intake keeps reading. Then the move, *Pause ask* or *Pause quest*, and *Never mind*.
 *
 * @remarks
 * **A molecule**: every state is reached by its props. While the plan it says is on its way it says so and offers no move,
 * since a pause pressed before it is read would stop what the person was not told about.
 */
export function PauseAsk({ target, lines, meanIt, busy = false, onPause, onClose, className }: {
  target: WorkTarget;
  /** What the pause stops and keeps, as `pauseAsk` says it; null while the plan is read. */
  lines: Line[] | null;
  /** The move's name: *Pause ask* or *Pause quest*. */
  meanIt: string;
  /** Another act on the record is on its way: the presses wait for it. */
  busy?: boolean;
  /** The second press, and how to tell the ask how it ended (UXFIX2b2a). */
  onPause: (answered: Answered) => void;
  /** Put down: by *Never mind*, Escape, or once the pause landed. */
  onClose: () => void;
  className?: string;
}) {
  const { t } = useTranslation();
  return (
    <InlineConfirm
      label={t('work.pause.title')}
      // Two sentences join the catalogue's way: a space after an English full stop, none after a Chinese one.
      says={lines === null
        ? t('work.pause.reading')
        : <Inline text={lines.map((line) => say(t, line, target)).reduce((first, second) => t('work.pause.join', { first, second }))} />}
      meanIt={lines === null ? undefined : meanIt}
      busy={busy}
      onConfirm={onPause}
      onClose={onClose}
      className={className}
    />
  );
}

/** One piece the abandon takes: its name, what it does with it, and what a tree holds. */
function Going({ row, target }: { row: GoingRow; target: WorkTarget }) {
  const { t } = useTranslation();
  return (
    <li className="grid min-w-0 gap-0.5">
      <span className="min-w-0 wrap-anywhere text-body text-ink">{say(t, row.name, target)}</span>
      <span className="text-small text-ink-soft">{say(t, row.act, target)}</span>
      {row.holds?.map((line) => (
        <span key={line.key} className="wrap-anywhere text-small text-ink-faint"><Inline text={say(t, line, target)} /></span>
      ))}
    </li>
  );
}

/** One piece kept: its name, and why, in the words of design §3.2. */
function Staying({ row, target }: { row: StayingRow; target: WorkTarget }) {
  const { t } = useTranslation();
  return (
    <li className="grid min-w-0 gap-0.5">
      <span className="min-w-0 wrap-anywhere text-body text-ink">{say(t, row.name, target)}</span>
      <span className="wrap-anywhere text-small text-ink-soft"><Inline text={say(t, row.why, target)} /></span>
    </li>
  );
}

/** A named list of the abandon's: its heading, and the list it names. */
function Listed({ title, children }: { title: string; children: ReactNode }) {
  return (
    <div className="min-w-0">
      <h3 className="m-0 mb-1.5 text-small font-semibold text-ink-faint">{title}</h3>
      <ul aria-label={title} className="m-0 grid list-none gap-2 p-0">{children}</ul>
    </div>
  );
}

/**
 * **An abandon's first press** (PAUSE1e, D132 §3.1, §3.2): it lists, under the header, everything the abandon would take
 * and everything it would keep, each kept piece with its reason, and asks for the person's reason, which each declined
 * quest keeps. The move (*Abandon ask*, *Abandon quest*) waits for a reason; *Never mind* puts it down.
 *
 * @remarks
 * **A molecule**: the plan it lists is the one held from when the list opened, handed in, since the reader answers again on
 * every look and the second press sends exactly what the first listed. A list that finds nothing left to take says so and
 * offers only *Close*, never a press that abandons nothing. Each tree is named by its repository and branch, with how many
 * commits it holds only here and up to five uncommitted files, never a path on this machine (platform language §4).
 */
export function AbandonAsk({ target, plan, meanIt, placeholder, busy = false, onAbandon, onClose }: {
  target: WorkTarget;
  /** The plan as it stood when the list opened. */
  plan: WorkPlan;
  /** The move's name: *Abandon ask* or *Abandon quest*. */
  meanIt: string;
  /** The reason field's hint. */
  placeholder: string;
  busy?: boolean;
  /** The second press, with the reason as the person wrote it, trimmed, and how to tell the ask how it ended (UXFIX2b2a). */
  onAbandon: (reason: string, answered: Answered) => void;
  /** Put down: by *Never mind*, Escape, *Close*, or once the abandon landed. */
  onClose: () => void;
}) {
  const { t } = useTranslation();
  const [reason, setReason] = useState('');
  const { goes, stays } = abandonList(plan);
  // Closing the ask alone is *Close ask*'s, never an abandon's: the reader's own word decides whether anything is left.
  const nothing = !plan.abandon.abandonable || goes.length === 0;
  const what = t(`work.scope.${target.scope}`, { id: target.id });

  return (
    <InlineConfirm
      block
      label={t('work.abandon.title')}
      className="mb-4"
      says={nothing
        ? <Prose className="text-small"><Inline text={t('work.abandon.nothingLeft', { what })} /></Prose>
        : (
          <>
            <Prose className="text-small">{t('work.abandon.lead', { what })}</Prose>
            <div className="grid gap-4 @min-[44rem]/main:grid-cols-2">
              <Listed title={t('work.abandon.goes')}>
                {goes.map((row) => <Going key={row.piece} row={row} target={target} />)}
              </Listed>
              {stays.length > 0 && (
                <Listed title={t('work.abandon.stays')}>
                  {stays.map((row) => <Staying key={row.piece} row={row} target={target} />)}
                </Listed>
              )}
            </div>
          </>
        )}
      meanIt={nothing ? undefined : meanIt}
      busy={busy}
      // Abandoning without a reason is refused (`WORK_REASON`); the form does not offer the mistake.
      ready={!!reason.trim()}
      onConfirm={(answered) => onAbandon(reason.trim(), answered)}
      onClose={onClose}
    >
      {!nothing && (
        <textarea
          aria-label={placeholder}
          placeholder={placeholder}
          rows={2}
          value={reason}
          onChange={(e) => setReason(e.target.value)}
          className="resize-y rounded-control border border-line-strong bg-raised px-2.5 py-1.5 text-body text-ink"
        />
      )}
    </InlineConfirm>
  );
}

/**
 * **What went and what stayed** (PAUSE1e, D132 §4.2): after an abandon, the page says what went (the quests declined, the ask
 * closed, the sessions stopped, each tree discarded with the `git branch` line that brings it back while git keeps its
 * commits, the sessions archived, each shared decline's answer) and what stayed, with why; and, after a step that could
 * not finish, that the work stays paused. From the second press's answer at once, and from `abandoned.json` on a later look.
 */
export function AbandonedWork({ target, outcome, went, stayed }: {
  target: WorkTarget;
  outcome: AbandonOutcome;
  /** The two sections' names, the page's own (*What went*, *What stayed*). */
  went: string;
  stayed: string;
}) {
  const { t } = useTranslation();
  return (
    <>
      {outcome.went.length > 0 && (
        <PageSection title={went}>
          <ul className="m-0 grid list-none gap-1.5 p-0">
            {outcome.went.map((line, i) => (
              <li key={`${line.key}-${i}`} className="wrap-anywhere text-body text-ink-soft"><Inline text={say(t, line, target)} /></li>
            ))}
          </ul>
        </PageSection>
      )}
      {(outcome.stayed.length > 0 || outcome.stillPaused) && (
        <PageSection title={stayed}>
          {outcome.stayed.length > 0 && (
            <ul className="m-0 grid list-none gap-2 p-0">
              {outcome.stayed.map((row, i) => <Staying key={`${row.piece}-${i}`} row={row} target={target} />)}
            </ul>
          )}
          {outcome.stillPaused && <Prose className="mt-2 text-small">{t('work.abandon.stillPaused')}</Prose>}
        </PageSection>
      )}
    </>
  );
}
