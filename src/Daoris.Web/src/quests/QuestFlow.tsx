import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { Quest } from '../data/api';
import { Flow } from '../design/Flow';
import { Icon } from '../design/ui';
import { cn } from '../lib/cn';
import { questFlow } from './questLife';
import { shownStatus } from './records';

/**
 * **A quest's statuses drawn as a flowchart** (FLOWCHART1a1; D186, the flowchart design §2.1): a section of its page after
 * the chain strip and before *How this came to be*, folded to its line (its name and the status it stands at) until it is
 * opened, then the four statuses with the one it stands at lit and the moves out of each said. Folded, like the trace,
 * since it is looked up rather than read; its moves, once a row reads them (FLOWCHART1a2), are read only when it opens.
 *
 * A molecule: its one state is whether it is open, and the page's quest is all it reads.
 */
export function QuestFlow({ quest, className }: { quest: Pick<Quest, 'id' | 'status' | 'submission'>; className?: string }) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);
  const title = t('quests.flow.title');
  return (
    <section aria-label={title} className={cn('min-w-0', className)}>
      <button
        type="button"
        aria-expanded={open}
        onClick={() => setOpen((was) => !was)}
        className="flex w-full min-w-0 cursor-pointer items-start gap-1.5 border-0 bg-transparent p-0 text-left text-small text-ink-faint hover:text-ink"
      >
        <Icon name={open ? 'chevronDown' : 'chevronRight'} size={12} className="mt-1 shrink-0" />
        <span className="min-w-0 [overflow-wrap:anywhere]">
          <span className="font-semibold">{title}</span>
          <span> · {t(`status.${shownStatus(quest)}`)}</span>
        </span>
      </button>
      {open && (
        <div className="mt-2 grid min-w-0 gap-2 pl-[18px]">
          <p className="m-0 text-small text-ink-faint">{t('quests.flow.hint')}</p>
          <Flow {...questFlow(t, quest)} />
        </div>
      )}
    </section>
  );
}
