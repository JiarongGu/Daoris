import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '../../design/ui';
import { type Answered, Refused } from '../InlineConfirm';
import { type OutsideAgent, outsideRowLine } from './outsideAgents';

/** *Remove* on an outside agent: told back how it ended, which the row words where it did not. */
export type RemoveOutside = (member: string, answered: Answered) => void;

/**
 * One agent outside Daoris on the line's channel, beside its co-workers: the agent and the
 * repository it claimed, that it is outside Daoris, when it joined and last read or posted, and the person's *Remove*. It is shown as
 * itself, never as the person or a session; it reads and posts as itself and starts, stops and steers nothing.
 */
export function OutsideAgentRow({ agent, nameOf, onRemove, removing }: {
  agent: OutsideAgent; nameOf: (agent: string) => string; onRemove?: RemoveOutside; removing?: boolean;
}) {
  const { t, i18n } = useTranslation();
  const [refused, setRefused] = useState<string | null>(null);
  return (
    <li data-outside-agent={agent.id} className="flex min-w-0 flex-wrap items-center gap-x-2 gap-y-0.5">
      <span className="min-w-0 flex-1 px-2 py-1 text-small text-ink">{outsideRowLine(t, agent, nameOf, i18n.language)}</span>
      {onRemove && (
        <Button
          disabled={removing}
          onClick={() => {
            setRefused(null);
            onRemove(agent.id, { done: () => setRefused(null), refused: setRefused });
          }}
        >
          {t('work.team.outside.remove')}
        </Button>
      )}
      {refused && <Refused sentence={refused} />}
    </li>
  );
}
