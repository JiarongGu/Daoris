import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Icon } from '../../design/ui';
import { cn } from '../../lib/cn';
import type { Block, PlanEntry } from './conversation';
import { Markdown } from './Markdown';

/** The agent's reasoning: its first line, and the whole on a press. */
export function ThoughtRow({ text }: { text: string }) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);
  const first = text.trim().split('\n', 1)[0] ?? '';

  return (
    <div className="text-small text-ink-soft">
      <button
        type="button"
        aria-expanded={open}
        onClick={() => setOpen((was) => !was)}
        className="flex w-full min-w-0 items-center gap-1.5 border-0 bg-transparent p-0 text-left text-ink-soft hover:text-ink"
      >
        <Icon name={open ? 'chevronDown' : 'chevronRight'} size={13} className="shrink-0" />
        <Icon name="think" size={13} className="shrink-0" />
        <span className="shrink-0">{t('work.conversation.thought')}</span>
        {!open && <span className="min-w-0 truncate italic text-ink-faint">{first}</span>}
      </button>
      {open && (
        <div className="mt-1 border-l-2 border-line pl-3 italic">
          <Markdown text={text} />
        </div>
      )}
    </div>
  );
}

const PLAN_MARK: Record<string, string> = { completed: '✓', in_progress: '◐', pending: '○' };

/** The agent's plan, as it last stood. */
export function PlanView({ entries }: { entries: PlanEntry[] }) {
  const { t } = useTranslation();
  return (
    <div className="rounded-control border border-line bg-raised px-3 py-2">
      <span className="flex items-center gap-1.5 text-meta text-ink-faint">
        <Icon name="plan" size={13} />
        {t('work.conversation.plan')}
      </span>
      <ol className="m-0 mt-1 grid list-none gap-0.5 p-0">
        {entries.map((entry, index) => (
          // Done is a check, never a strike-through: struck text reads as cancelled (CONV2's look).
          <li key={index} className={cn('flex gap-2 text-small', entry.status === 'completed' ? 'text-ink-soft' : 'text-ink')}>
            <span
              aria-hidden
              className={cn(
                'w-3 shrink-0 text-center',
                entry.status === 'completed' ? 'text-st-done' : entry.status === 'in_progress' ? 'text-accent' : 'text-ink-faint',
              )}
            >
              {PLAN_MARK[entry.status ?? ''] ?? '○'}
            </span>
            {entry.content}
          </li>
        ))}
      </ol>
    </div>
  );
}

/** Something the wire said that this version has no kind for — kept, and shown as itself. */
export function RawLine({ block }: { block: Block }) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);
  return (
    <div className="text-meta text-ink-faint">
      <button
        type="button"
        aria-expanded={open}
        onClick={() => setOpen((was) => !was)}
        className="flex items-center gap-1 border-0 bg-transparent p-0 text-meta text-ink-faint hover:text-ink"
      >
        <Icon name={open ? 'chevronDown' : 'chevronRight'} size={12} />
        {t('work.conversation.raw', { title: block.title ?? '' })}
      </button>
      {open && (
        <pre className="m-0 mt-1 max-h-48 overflow-auto whitespace-pre-wrap wrap-anywhere rounded-control bg-page px-2.5 py-1.5 font-mono">
          {[block.text, block.raw].filter(Boolean).join('\n')}
        </pre>
      )}
    </div>
  );
}
