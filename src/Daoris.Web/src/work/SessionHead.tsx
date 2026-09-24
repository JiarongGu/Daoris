import { useTranslation } from 'react-i18next';
import type { Quest, Session } from '../api';
import { ago, elapsed, sessionTool } from '../format';
import { MetaLine, Pill, SESSION_ACTIVE, SESSION_TONE } from '../ui';
import { AwaitingIntake } from './AwaitingIntake';
import { AwaitingPerson, type Resolution } from './AwaitingPerson';
import { isIntake, sessionOrigin, sessionTitle } from './identity';

/**
 * The attended session's record (design §3): what it is, where it runs, what it ran as, and — when
 * it is parked — the analysis that is waiting on a person.
 *
 * @remarks
 * **The rail's row and this head must not disagree**, so both derive their identity from
 * `sessionTitle` and neither invents one. What the head adds is the room the rail does not have:
 * the whole tree path rather than its last segment, the tool with its version and account, and
 * both ends of the clock.
 *
 * **`AwaitingPerson` gets a surface at last** (design §4). It has meant "only the person can clear
 * this" since D46 and had never been rendered anywhere. It sits **above** the record, because it is
 * the reason the person is looking, and it carries the three moves — which is why `onResolve` is a
 * prop: the head knows nothing about the bridge, and the frame that does hands it down.
 *
 * **It does not repeat the driver's note.** The timeline below carries it with the time it was
 * observed, which is strictly more than a bare sentence here — and one screen saying the same
 * thing twice teaches a reader to skim both. The one exception is a park, where the analysis IS
 * the reason the person is here.
 *
 * **An absence is never a dash.** No tree is the registered root, no profile is the harness's own
 * configuration home, no machine is this deployment's own — and a browser over a keyed remote is
 * told none of them (D47 §4). `MetaLine` drops a pair it has no value for, which is why all four
 * can be passed unconditionally.
 */
export function SessionHead({ session, quest, resolving = false, onResolve, onAnswerAsk }: {
  session: Session;
  /** The quest it serves, where the caller has it — absent is a state, not a gap (D49 §3). */
  quest?: Quest | null;
  resolving?: boolean;
  /** How a parked session is cleared. Absent where nothing can act — a browser, or a story. */
  onResolve?: (state: Resolution, note: string | null) => void;
  /**
   * Open the ask a parked intake is waiting on (INT4g) — its answer is there, not on the session.
   * Absent where nothing can open it.
   */
  onAnswerAsk?: (ask: string) => void;
}) {
  const { t } = useTranslation();
  const running = SESSION_ACTIVE.has(session.state);
  const parked = session.state === 'awaiting-person';
  const intake = isIntake(session);

  return (
    <header className="grid gap-2.5">
      {parked && intake && (onResolve || onAnswerAsk) && (
        <AwaitingIntake
          ask={session.ask!}
          note={session.note}
          pending={resolving}
          onAnswer={onAnswerAsk}
          onStop={onResolve ? () => onResolve('stopped', null) : undefined}
        />
      )}
      {parked && !intake && onResolve && (
        <AwaitingPerson note={session.note} pending={resolving} onResolve={onResolve} />
      )}
      {/* Nothing here can act — a browser, or a mirrored record from another machine — so the
          analysis is shown and the moves are not. Half a control is worse than none. */}
      {parked && !onResolve && !(intake && onAnswerAsk) && session.note && (
        <div className="rounded-card border border-line border-l-[3px] border-l-st-open bg-raised px-[1.15rem] py-3.5">
          <p className="m-0 text-small font-semibold text-st-open">{t('work.head.waiting')}</p>
          <p className="m-0 mt-1.5 whitespace-pre-wrap text-body leading-relaxed">{session.note}</p>
        </div>
      )}

      <div className="flex flex-wrap items-baseline justify-between gap-x-4 gap-y-1.5">
        <h2 className="m-0 text-title font-[650] leading-[1.35]">{sessionTitle(session, quest)}</h2>
        <span className="flex shrink-0 items-baseline gap-2">
          <Pill tone={SESSION_TONE[session.state]}>{t(`sessionState.${session.state}`)}</Pill>
          <span className="font-mono text-meta text-ink-faint">{session.id}</span>
        </span>
      </div>

      <MetaLine
        items={[
          // An intake serves an ask and runs in Daoris's own room, never a repository's tree
          // (INT4b) — its record says so rather than `repository: ask #…` (INT4g).
          intake
            ? { label: t('work.intake.ask'), value: `#${session.ask}`, mono: true }
            : { label: t('work.head.repository'), value: session.repository },
          { label: t(intake ? 'work.intake.room' : 'work.head.tree'), value: session.tree ?? null, mono: true },
          { label: t('work.head.quest'), value: session.quest ? `#${session.quest}` : null, mono: true },
          { label: t('work.head.tool'), value: sessionTool(session) },
          { label: t('work.head.machine'), value: sessionOrigin(session) },
          { label: t('work.head.started'), value: ago(session.created) },
          // A running session has an age and a finished one has a lifetime. Same number, different
          // question, so the label says which rather than leaving the reader to guess.
          {
            label: running ? t('work.head.elapsed') : t('work.head.ran'),
            value: elapsed(session.created, running ? null : session.updated),
          },
          { label: t('work.head.moved'), value: ago(session.updated) },
        ]}
      />

    </header>
  );
}
