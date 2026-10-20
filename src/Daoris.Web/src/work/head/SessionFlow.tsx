import { Fragment, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Flow } from '../../design/Flow';
import { Icon, SESSION_TONE, type ShownState, shownKey, shownState } from '../../design/ui';
import { cn } from '../../lib/cn';
import { store, stored } from '../../lib/stored';
import { type OutcomeMember, type SessionOfLife, sessionFlow } from './sessionLife';

/** The fold's choice, remembered on this machine: the Timeline view is opened and closed often, and its flow is tall at the side bar's floor. */
const FOLDED_KEY = 'daoris.timeline.states';

/**
 * **A session's states drawn as a flowchart** (FLOWCHART1b; D186, the flowchart design §2.2): at the head of the side bar's
 * *Timeline* view, above the observed events, folded to its line (*States · working*) when the person folds it, else the four
 * live states and one node of outcomes, the state the record is in lit, the moves out of each said by who makes them, and the
 * return from waiting on you to working on its own lane.
 *
 * @remarks
 * - **The record's state, not its path.** The hint says the record keeps where a session stands and not how it got there, so
 *   the drawing has no earlier state lit and no time (trap 19).
 * - **The word on the lit node** is `shown`'s, the page's reading over the record (*idle*, *parked*, *awaiting reply*,
 *   *answered*, *going on*), where the caller holds it, and the record's own reading otherwise.
 */
export function SessionFlow({ session, shown, className }: { session: SessionOfLife; shown?: ShownState; className?: string }) {
  const { t } = useTranslation();
  const [folded, setFolded] = useState(() => stored(FOLDED_KEY) === 'folded');
  const title = t('work.life.title');
  const word = shown ?? shownState(session, undefined);
  return (
    <section aria-label={title} className={cn('min-w-0', className)}>
      <button
        type="button"
        aria-expanded={!folded}
        onClick={() => {
          store(FOLDED_KEY, folded ? null : 'folded');
          setFolded(!folded);
        }}
        className="flex w-full min-w-0 cursor-pointer items-start gap-1.5 border-0 bg-transparent p-0 text-left text-small text-ink-faint hover:text-ink"
      >
        <Icon name={folded ? 'chevronRight' : 'chevronDown'} size={12} className="mt-1 shrink-0" />
        <span className="min-w-0 [overflow-wrap:anywhere]">
          <span className="font-semibold">{title}</span>
          {/* A word this page does not name is said as recorded, never as a missing catalogue key (trap 10). */}
          <span> · {word in SESSION_TONE ? t(shownKey(word)) : String(word)}</span>
        </span>
      </button>
      {!folded && (
        <div className="mt-2 grid min-w-0 gap-2 pl-[18px]">
          <p className="m-0 text-small text-ink-faint">{t('work.life.hint')}</p>
          <SessionLifeFlow session={session} shown={word} />
        </div>
      )}
    </section>
  );
}

/** The flow a session's section draws: its states, and on the outcomes node the members, the stop and the one that never goes on. */
export function SessionLifeFlow({ session, shown }: { session: SessionOfLife; shown?: ShownState }) {
  const { t } = useTranslation();
  const life = sessionFlow(t, session, shown);
  const nodes = life.nodes.map((node) => (node.id === 'ended'
    ? { ...node, lines: <Outcomes members={life.members} stop={life.stop} never={life.neverGoesOn} /> }
    : node));
  return <Flow label={life.label} nodes={nodes} edges={life.edges} entries={life.entries} />;
}

/**
 * The outcomes node's words (§2.2): its members in a line with the reached one marked (by a dot and weight, and `aria-current`,
 * never by hue alone), the person's stop said once for every live state, and the outcome going on never leaves.
 */
function Outcomes({ members, stop, never }: { members: OutcomeMember[]; stop: string; never: string }) {
  return (
    <div className="grid min-w-0 gap-0.5">
      <p className="m-0 min-w-0 [overflow-wrap:anywhere]">
        {members.map((member, index) => (
          <Fragment key={member.state}>
            {index > 0 && <span aria-hidden> · </span>}
            <span aria-current={member.current ? 'true' : undefined} className={member.current ? 'font-semibold text-ink' : undefined}>
              {member.current && <span aria-hidden className="mr-0.5">●</span>}
              {member.word}
            </span>
          </Fragment>
        ))}
      </p>
      <p className="m-0 min-w-0 [overflow-wrap:anywhere]">{stop}</p>
      <p className="m-0 min-w-0 [overflow-wrap:anywhere]">{never}</p>
    </div>
  );
}
