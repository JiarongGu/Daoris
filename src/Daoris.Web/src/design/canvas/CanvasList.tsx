import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { cn } from '../../lib/cn';

/** A list expands whole to this many items; past it only the chosen group opens, and the rest open on a press. */
const OPEN_ALL = 200;

export type ListGroup = { id: string; title: string; items: readonly { id: string; title: string }[] };

/**
 * **The graph as a list** (CANVAS1d; the canvas design §2.6, trap 11): every item, grouped as the graph is, each group's count said and
 * each item's place in it (`aria-setsize`, `aria-posinset`). It is what a reader gets of a world no viewport draws, what stands below
 * the 36rem floor, and what *Show as list* chooses. A group opens on a press, and only an open group's items are drawn, so ten thousand
 * items cost the groups' count, never theirs. A molecule: the groups and the selection are given.
 */
export function CanvasList({ label, groups, selected, onPick }: {
  label: string;
  groups: readonly ListGroup[];
  selected: string | null;
  onPick: (id: string) => void;
}) {
  const { t } = useTranslation();
  const total = groups.reduce((sum, group) => sum + group.items.length, 0);
  const holding = groups.find((group) => group.items.some((item) => item.id === selected))?.id ?? groups[0]?.id;
  const [open, setOpen] = useState<ReadonlySet<string>>(() => new Set(total <= OPEN_ALL ? groups.map((group) => group.id) : holding ? [holding] : []));
  const toggle = (id: string) => setOpen((now) => { const next = new Set(now); if (!next.delete(id)) next.add(id); return next; });
  const flat = groups.length === 1 && groups[0]!.id === '';
  return (
    <div role="group" aria-label={label} data-canvas-list="" className="min-w-0 rounded-control outline -outline-offset-1 outline-line">
      {groups.map((group) => {
        const shown = flat || open.has(group.id);
        return (
          <section key={group.id} aria-label={group.title || label}>
            {!flat && (
              <h3 className="m-0">
                <button
                  type="button"
                  aria-expanded={shown}
                  onClick={() => toggle(group.id)}
                  className="flex w-full items-center justify-between gap-2 px-3 py-1.5 text-left text-small font-medium text-ink hover:bg-raised focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-accent"
                >
                  <span className="truncate">{group.title}</span>
                  <span className="shrink-0 font-mono text-meta text-ink-soft">{t('canvas.items', { count: group.items.length })}</span>
                </button>
              </h3>
            )}
            {shown && (
              <ul className="m-0 list-none p-0">
                {group.items.map((item, index) => (
                  <li key={item.id} aria-setsize={group.items.length} aria-posinset={index + 1}>
                    <button
                      type="button"
                      aria-pressed={item.id === selected}
                      onClick={() => onPick(item.id)}
                      className={cn(
                        'block w-full truncate px-6 py-1 text-left text-small text-ink hover:bg-raised',
                        'focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-accent',
                        item.id === selected && 'bg-raised font-medium',
                      )}
                    >
                      {item.title}
                    </button>
                  </li>
                ))}
              </ul>
            )}
          </section>
        );
      })}
    </div>
  );
}
