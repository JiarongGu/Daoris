import { useTranslation } from 'react-i18next';
import { Button, Inline } from '../../design/ui';
import type { RunJoint, RunPartReceipt } from '../run';

const STATES: Record<string, true> = { reached: true, waiting: true, failed: true };

const ROW = 'm-0 flex min-w-0 flex-wrap items-baseline gap-x-2 gap-y-0.5 text-small text-ink-soft';

/** The other part's quest as a door, where the plan gave it one and the page opens quests. */
function QuestDoor({ quest, onQuest }: { quest?: string | null; onQuest?: (id: string) => void }) {
  const { t } = useTranslation();
  if (!quest || !onQuest) return null;
  return (
    <Button variant="ghost" className="min-h-0 px-1.5 py-0.5 text-small" onClick={() => onQuest(quest)}>
      {t('workflow.run.door.quest', { id: quest })}
    </Button>
  );
}

/** A receipt's state and words, as its part's run journaled the newest; nothing where it journaled none. */
function ReceiptSaid({ receipt }: { receipt: RunPartReceipt }) {
  const { t } = useTranslation();
  return (
    <>
      {receipt.state && (
        <span className={receipt.state === 'failed' ? 'text-ink' : 'text-ink-faint'}>
          {STATES[receipt.state] ? t(`workflow.run.acts.state.${receipt.state}`) : receipt.state}
        </span>
      )}
      {receipt.message && <span className="text-ink">{receipt.message}</span>}
    </>
  );
}

/**
 * What a run's part of its ask's plan waits on before any of its steps is handed: *waits on `<part>`'s `<fact>`* for each other part's
 * receipt not reached yet, its newest state and words, and that part's quest. Nothing where it waits on none. Presentational.
 */
export function PartWaitRows({ waits, onQuest }: { waits?: readonly RunPartReceipt[] | null; onQuest?: (id: string) => void }) {
  const { t } = useTranslation();
  if (!waits || waits.length === 0) return null;
  return (
    <ul aria-label={t('workflow.run.parts.title')} className="m-0 grid list-none gap-1 p-0">
      {waits.map((wait) => (
        <li key={`${wait.part}:${wait.fact}`} data-part={wait.part} className={ROW}>
          <Inline text={t('workflow.run.parts.waitsOn', { part: wait.part, fact: wait.fact })} />
          <ReceiptSaid receipt={wait} />
          <QuestDoor quest={wait.quest} onQuest={onQuest} />
        </li>
      ))}
    </ul>
  );
}

/**
 * A joint check over its parts' receipts: *holds `<part>`'s `<fact>`* for each reached, *waits for* each not, and where its newest test
 * failed and went back, *failed on `<part>`, sent back to it* with the test's words and that part's quest. Nothing on any other step.
 * Presentational.
 */
export function JointCheckRows({ joint, onQuest }: { joint?: RunJoint | null; onQuest?: (id: string) => void }) {
  const { t } = useTranslation();
  if (!joint) return null;
  return (
    <ul aria-label={t('workflow.run.joint.title')} className="m-0 grid list-none gap-1 p-0">
      {joint.parts.map((part) => (
        <li key={part.part} data-part={part.part} className={ROW}>
          <Inline text={t(part.state === 'reached' ? 'workflow.run.joint.holds' : 'workflow.run.joint.waitsFor', { part: part.part, fact: part.fact })} />
          {part.state !== 'reached' && <QuestDoor quest={part.quest} onQuest={onQuest} />}
        </li>
      ))}
      {joint.returned && (
        <li data-returned={joint.returned.part} className={ROW}>
          <span className="text-ink"><Inline text={t('workflow.run.joint.returned', { part: joint.returned.part })} /></span>
          {joint.returned.message && <span className="text-ink">{joint.returned.message}</span>}
          <QuestDoor quest={joint.returned.quest} onQuest={onQuest} />
        </li>
      )}
    </ul>
  );
}
