import { type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { Icon } from '../../design/ui';
import type { AgentSection } from '../AgentPage';

/**
 * A section folded to its line: its name, the values it holds in a phrase, and a chevron that opens it,
 * since it opens and closes something. The section's id is what a door's part scrolls to.
 */
export function Fold({ section, title, summary, open, onToggle, children }: {
  section: AgentSection;
  title: string;
  summary: string;
  open: boolean;
  onToggle: (section: AgentSection) => void;
  children: ReactNode;
}) {
  const { t } = useTranslation();
  return (
    <section id={`agents-${section}`} aria-label={title} className="mt-3 scroll-mt-3 border-t border-line pt-2">
      <button
        type="button"
        aria-expanded={open}
        aria-label={t(open ? 'agents.section.hide' : 'agents.section.show', { section: title })}
        onClick={() => onToggle(section)}
        className="flex w-full min-w-0 flex-wrap items-baseline gap-x-3 gap-y-0.5 rounded-control py-1 text-left hover:bg-accent-soft/40"
      >
        <span className="min-w-40 text-small font-semibold text-ink-soft">{title}</span>
        {!open && <span className="min-w-0 flex-1 text-small text-ink-faint [overflow-wrap:anywhere]">{summary}</span>}
        <Icon name={open ? 'chevronDown' : 'chevronRight'} size={13} className="ml-auto shrink-0 self-center text-ink-faint" />
      </button>
      {open && <div className="mt-2">{children}</div>}
    </section>
  );
}
