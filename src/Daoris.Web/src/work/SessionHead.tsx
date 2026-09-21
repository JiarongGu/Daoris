import { useTranslation } from 'react-i18next';
import type { Quest, Session } from '../api';
import { ago, elapsed, sessionTool } from '../format';
import { MetaLine, Pill, SESSION_ACTIVE, SESSION_TONE } from '../ui';
import { sessionOrigin, sessionTitle } from './identity';

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
 * this" since D46 and has never been rendered anywhere. The analysis sits **above** the record,
 * because it is the reason the person is looking — and it renders verbatim, like every driver
 * observation. The person's three moves are SURF5's; this is where they will attach.
 *
 * **An absence is never a dash.** No tree is the registered root, no profile is the harness's own
 * configuration home, no machine is this deployment's own — and a browser over a keyed remote is
 * told none of them (D47 §4). `MetaLine` drops a pair it has no value for, which is why all four
 * can be passed unconditionally.
 */
export function SessionHead({ session, quest }: {
  session: Session;
  /** The quest it serves, where the caller has it — absent is a state, not a gap (D49 §3). */
  quest?: Quest | null;
}) {
  const { t } = useTranslation();
  const running = SESSION_ACTIVE.has(session.state);
  const parked = session.state === 'awaiting-person';

  return (
    <header className="grid gap-2.5">
      {parked && session.note && (
        <div className="rounded-card border border-line border-l-[3px] border-l-st-open bg-raised px-[1.15rem] py-3.5">
          <p className="m-0 text-[0.8rem] font-semibold text-st-open">{t('work.head.waiting')}</p>
          {/* Verbatim: the analysis is what the session said, and `autonomous-development` asks it
              to carry options, a recommendation and a reason — none of which survive rewording. */}
          <p className="m-0 mt-1.5 whitespace-pre-wrap text-[0.875rem] leading-relaxed">{session.note}</p>
        </div>
      )}

      <div className="flex flex-wrap items-baseline justify-between gap-x-4 gap-y-1.5">
        <h2 className="m-0 text-[1.05rem] font-[650] leading-[1.35]">{sessionTitle(session, quest)}</h2>
        <span className="flex shrink-0 items-baseline gap-2">
          <Pill tone={SESSION_TONE[session.state]}>{t(`sessionState.${session.state}`)}</Pill>
          <span className="font-mono text-[0.72rem] text-ink-faint">{session.id}</span>
        </span>
      </div>

      <MetaLine
        items={[
          { label: t('work.head.repository'), value: session.repository },
          { label: t('work.head.tree'), value: session.tree ?? null, mono: true },
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

      {!parked && session.note && (
        <p className="m-0 whitespace-pre-wrap text-[0.85rem] italic text-ink-soft">{session.note}</p>
      )}
    </header>
  );
}
