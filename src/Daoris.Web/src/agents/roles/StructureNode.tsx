import { type CSSProperties, useId } from 'react';
import { useTranslation } from 'react-i18next';
import { cn } from '../../lib/cn';
import type { Drawn } from './structureWords';

/**
 * One node of the structure (CANVAS1c; from the three columns' `NodeBox`): its title and lines, a fold's press, and in the list its edges
 * out as presses that move to their target. A band's label, a role in a band, an appointment and an agent are all this; `data-flow-node`
 * is its key (one for each occurrence), `data-node` the one identity a selection and its detail are of. The selection is an outline, which
 * takes no room.
 */
export function StructureNode({ node, identity, stop, chosen, lit, description, feet, onChoose, onOpenFold, onFollow, style, indent = 0 }: {
  node: Drawn;
  /** The node the occurrence is of: the same on every occurrence of a role. */
  identity: string;
  stop: boolean;
  chosen: boolean;
  lit: boolean;
  description: string;
  feet: { to: string; text: string }[];
  onChoose: () => void;
  onOpenFold?: () => void;
  onFollow: (to: string) => void;
  style?: CSSProperties;
  /** A band label's depth in the place tree: indented under its workspace. */
  indent?: number;
}) {
  const { t } = useTranslation();
  const titleId = useId();
  const descriptionId = useId();
  return (
    <div
      role="listitem"
      data-flow-node={node.id}
      data-node={identity}
      data-kind={node.kind}
      data-lit={lit ? '' : undefined}
      tabIndex={stop ? 0 : -1}
      aria-current={chosen ? 'true' : undefined}
      aria-labelledby={titleId}
      aria-describedby={descriptionId}
      onClick={(event) => { if (!(event.target as HTMLElement).closest('button')) onChoose(); }}
      style={{ ...style, ...(indent > 0 ? { marginLeft: `${indent}rem` } : {}) }}
      className={cn(
        'min-w-0 cursor-pointer rounded-card bg-raised px-3 py-2',
        node.dashed ? 'border border-dashed border-line-strong' : 'border border-line',
        chosen ? 'ring-2 ring-accent' : lit ? 'ring-2 ring-ink-soft' : '',
      )}
    >
      <span id={titleId} className={cn('block min-w-0 text-body font-semibold [overflow-wrap:anywhere]', node.dashed ? 'text-ink-soft' : 'text-ink')}>
        {node.title}
      </span>
      {node.lines.map((line, index) => (
        <span key={index} className="mt-0.5 block min-w-0 text-small text-ink-soft [overflow-wrap:anywhere]">{line}</span>
      ))}
      {onOpenFold && (
        <button
          type="button"
          tabIndex={-1}
          aria-expanded={false}
          onClick={onOpenFold}
          className="mt-1 cursor-pointer border-0 bg-transparent p-0 text-left text-meta text-ink-soft underline-offset-2 hover:text-ink hover:underline"
        >
          {t('agents.roles.diagram.unfold')}
        </button>
      )}
      {feet.length > 0 && (
        <div className="mt-1.5 grid min-w-0 gap-0.5">
          {feet.map((foot, index) => (
            <button
              key={index}
              type="button"
              tabIndex={-1}
              onClick={() => onFollow(foot.to)}
              className="cursor-pointer border-0 bg-transparent p-0 text-left text-meta text-ink-soft [overflow-wrap:anywhere] hover:text-ink"
            >
              <span aria-hidden className="mr-1">→</span>{foot.text}
            </button>
          ))}
        </div>
      )}
      <span id={descriptionId} hidden>{description}</span>
    </div>
  );
}
