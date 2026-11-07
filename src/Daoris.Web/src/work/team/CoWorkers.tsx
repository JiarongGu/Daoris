import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Menu } from '../../design/ui';
import { type Answered, Refused } from '../InlineConfirm';
import { HelperAsks, type HelperWaits, waitsOnWord } from '../waits/HelperAsks';
import { BringInAgent, type BringInChoice } from './BringInAgent';
import type { OutsideAgent } from './outsideAgents';
import { OutsideAgentRow, type RemoveOutside } from './OutsideAgentRow';
import { type MemberRow, memberRows } from './teamRows';

type InConversation = Parameters<typeof HelperAsks>[0]['inConversation'];

/** An ask that has not reached a helper yet: still the yes or the slot's, drawn with its presses as before. */
const pending = (state: string | null) => state === null || state === 'waiting' || state === 'approved' || state === 'cooling';

/** What the frame hands the head's *Co-workers* beyond the asks: the lead's agent, the pass, and the dialog's door. */
export type CoWorkersProps = {
  session: string;
  helpers: HelperWaits;
  /** The lead's agent, as the roster names it. */
  lead: string;
  /** *Pass to the lead* on an answer kept for the person, told back how it ended. */
  onPass?: (ask: string, answered: Answered) => void;
  passing?: boolean;
  /** *Bring in another agent…*'s choices: none, and the ⋯ is not drawn. */
  choices?: readonly BringInChoice[];
  /** The agents outside Daoris on this line's channel, each beside the members with the person's *Remove*. */
  outside?: readonly OutsideAgent[];
  onRemoveOutside?: RemoveOutside;
  removingOutside?: boolean;
  inConversation?: InConversation;
  marked?: string;
};

/**
 * The head's *Co-workers*: the lead, then each agent working beside it, one row a member, in the
 * parts strip's shape. A member's row opens its thread; an answer kept for the person offers *Pass to the lead*; an ask still waiting on
 * the person's yes keeps its presses and its wait's section. Its ⋯ holds *Bring in another agent…*. Nothing where nothing is asked.
 */
export function CoWorkers({
  session, helpers, lead, onPass, passing = false, choices, outside = [], onRemoveOutside, removingOutside, inConversation, marked,
}: CoWorkersProps) {
  const { t } = useTranslation();
  const [bringing, setBringing] = useState(false);
  const onBringIn = choices && choices.length > 0 ? () => setBringing(true) : undefined;
  const waiting = helpers.asks.filter((ask) => pending(ask.state));
  const members = memberRows(t, helpers.asks.filter((ask) => !pending(ask.state)), helpers.nameOf);
  if (helpers.asks.length === 0 && outside.length === 0 && !onBringIn) return null;
  const heading = `coworkers-${session}`;
  return (
    <section
      aria-labelledby={heading}
      data-coworkers
      data-wait-section={helpers.onApprove && waiting.some(waitsOnWord) ? '' : undefined}
      className="grid gap-1"
    >
      <div className="flex items-center gap-2">
        <h3 id={heading} className="m-0 text-small font-semibold">{t('work.helper.heading')}</h3>
        {onBringIn && (
          <Menu.Root>
            <Menu.Trigger asChild>
              <button type="button" aria-label={t('work.team.more')} className="rounded-control px-1 text-ink-soft hover:bg-accent-soft/50">⋯</button>
            </Menu.Trigger>
            <Menu.Content align="start">
              <Menu.Acts acts={[{ id: 'bringIn', label: t('work.team.bringIn'), onSelect: onBringIn }]} />
            </Menu.Content>
          </Menu.Root>
        )}
      </div>
      {(helpers.asks.length > 0 || outside.length > 0) && (
        <ol className="m-0 grid list-none gap-0.5 p-0">
          <li data-team-lead className="px-2 py-1 text-small text-ink-soft">{t('work.team.lead', { agent: lead })}</li>
          {members.map((row) => (
            <Member key={row.ask.id} row={row} onOpen={helpers.onOpen} onPass={onPass} passing={passing} />
          ))}
          {outside.map((agent) => (
            <OutsideAgentRow key={agent.id} agent={agent} nameOf={helpers.nameOf} onRemove={onRemoveOutside} removing={removingOutside} />
          ))}
        </ol>
      )}
      {waiting.length > 0 && (
        <HelperAsks {...helpers} asks={waiting} inConversation={inConversation} marked={marked} />
      )}
      {bringing && choices && <BringInAgent choices={choices} onClose={() => setBringing(false)} />}
    </section>
  );
}

/** One member: its agent, role and state, where its answer went, the door to its thread, and the pass where the answer is the person's. */
function Member({ row, onOpen, onPass, passing }: {
  row: MemberRow; onOpen?: (helper: string) => void; onPass?: CoWorkersProps['onPass']; passing: boolean;
}) {
  const { t } = useTranslation();
  const [refused, setRefused] = useState<string | null>(null);
  const said = [row.agent, t('work.team.role.helper'), row.state, row.went ? t(`work.team.went.${row.went}`) : null].filter(Boolean).join(' · ');
  const helper = row.ask.helper;
  return (
    <li data-member={row.ask.id} className="flex min-w-0 flex-wrap items-center gap-x-2 gap-y-0.5">
      {helper && onOpen ? (
        <button
          type="button"
          onClick={() => onOpen(helper)}
          className="min-w-0 flex-1 rounded-control border-l-[3px] border-l-transparent px-2 py-1 text-left text-small text-ink hover:bg-accent-soft/50"
        >
          {said}
        </button>
      ) : (
        <span className="min-w-0 flex-1 px-2 py-1 text-small text-ink">{said}</span>
      )}
      {row.passable && onPass && (
        <Button
          disabled={passing}
          onClick={() => {
            setRefused(null);
            onPass(row.ask.id, { done: () => setRefused(null), refused: setRefused });
          }}
        >
          {t('work.team.pass')}
        </Button>
      )}
      {refused && <Refused sentence={refused} />}
    </li>
  );
}
