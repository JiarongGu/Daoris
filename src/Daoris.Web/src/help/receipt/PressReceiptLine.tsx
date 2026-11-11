import { useTranslation } from 'react-i18next';
import { Button } from '../../design/ui';
import type { PressReceiptNow } from '../../person/pressReceipt';

/** What a receipt's go names: the repository's own page, or one quest. */
export type ReceiptScope = 'repository' | 'quest';

type Say = (key: string, options?: Record<string, unknown>) => string;

/** The receipt's one sentence, by what it made: a department opened or stopped, a room's quest alone, or no quest at all. */
function sentenceOf(t: Say, receipt: PressReceiptNow): string {
  const quest = receipt.quests[0] ?? '';
  const values = {
    name: receipt.repository, repository: receipt.repository, workspace: receipt.workspace ?? '', folder: receipt.folder ?? '', line: receipt.line ?? '',
    room: receipt.room ?? '', quest,
  };
  if (receipt.kind === 'room') return t(quest ? 'help.receipt.room' : 'help.receipt.roomNone', values);
  if (!receipt.done) return t('help.receipt.stopped', values);
  return t(receipt.line && quest ? 'help.receipt.opened' : 'help.receipt.openedFolder', values);
}

/**
 * What a press that opened a place says it made, once: *Opened ledger in aurora: folder, line, set-up quest*, or a room's quest alone. Each place
 * it made is a press that goes there (the repository's own page, the quest on Quests), never an act: with nothing to open a place the line is
 * words alone. Drawn under the press's row in *Notifications* and under its turn in the conversation.
 *
 * A molecule: drawn from props; the one that holds the window opens the place.
 */
export function PressReceiptLine({ receipt, onOpen }: { receipt: PressReceiptNow; onOpen?: (scope: ReceiptScope, id: string) => void }) {
  const { t } = useTranslation();
  const quest = receipt.quests[0] ?? null;
  return (
    <p data-press-receipt={receipt.kind} className="m-0 flex min-w-0 flex-wrap items-baseline gap-x-2 text-small text-ink">
      <span className="min-w-0 wrap-anywhere">{sentenceOf(t, receipt)}</span>
      {onOpen && receipt.kind === 'opened' && (
        <Button variant="ghost" onClick={() => onOpen('repository', receipt.repository)}>{t('help.receipt.openRepository', { name: receipt.repository })}</Button>
      )}
      {onOpen && quest !== null && (
        <Button variant="ghost" onClick={() => onOpen('quest', quest)}>{t('help.receipt.openQuest', { quest })}</Button>
      )}
    </p>
  );
}
