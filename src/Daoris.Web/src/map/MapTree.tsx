import { Fragment, type KeyboardEvent, type ReactNode, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Icon } from '../design/ui';
import { cn } from '../lib/cn';
import { keyInTree, openedAtStart, type TreeGroup, type TreeRow, treeRows } from './mapTreeModel';

/** One row: the whole row presses it, and a group's chevron, for the pointer, opens or closes it; the arrow keys do the same. */
function Row({ row, chosen, focused, onPress }: { row: TreeRow; chosen: boolean; focused: boolean; onPress: () => void }) {
  const { t } = useTranslation();
  return (
    <div
      role="treeitem"
      data-tree-id={row.id}
      aria-level={row.level}
      aria-selected={row.kind === 'item' ? chosen : undefined}
      aria-current={chosen || undefined}
      aria-expanded={row.expandable ? row.expanded : undefined}
      tabIndex={focused ? 0 : -1}
      onClick={onPress}
      className={cn(
        'flex w-full min-w-0 cursor-pointer items-center gap-1 border-l-[3px] py-1.5 pr-2.5 outline-none transition-colors duration-(--speed)',
        row.level === 1 ? 'pl-1.5' : row.level === 2 ? 'pl-5' : 'pl-9',
        'hover:bg-accent-soft/50 focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-accent',
        chosen ? 'border-l-accent bg-accent-soft' : 'border-l-transparent',
      )}
    >
      <span aria-hidden="true" className="flex size-4 shrink-0 items-center justify-center text-ink-faint">
        {row.expandable && <Icon name={row.expanded ? 'chevronDown' : 'chevronRight'} size={12} />}
      </span>
      <span className={cn('min-w-0 flex-1 truncate', row.kind === 'group' ? 'text-small font-medium text-ink' : cn('text-small text-ink', chosen && 'font-medium'))}>{row.title}</span>
      {row.kind === 'group' && <span className="shrink-0 font-mono text-meta text-ink-soft">{t('canvas.items', { count: row.count })}</span>}
    </div>
  );
}

/**
 * **The Map's list** (MAPUX1e; the map UX design §6): one tree on both levels, the WAI pattern, with one row in the tab order. The arrows
 * walk it, → and ← open, close and climb, Enter chooses an item or opens a group, and Escape releases the choice. Below the 36rem floor
 * `detail` is given, and opens in place under the chosen row, so a long list is never scrolled away from to read a choice.
 *
 * A molecule: the groups and the choice are given, and each press goes out.
 */
export function MapTree({ label, groups, selected, onPick, onRelease, detail }: {
  label: string;
  groups: readonly TreeGroup[];
  selected: string | null;
  onPick: (id: string) => void;
  onRelease: () => void;
  detail?: ReactNode;
}) {
  const [open, setOpen] = useState<ReadonlySet<string>>(() => openedAtStart(groups, selected));
  const [focus, setFocus] = useState<string | null>(null);
  const tree = useRef<HTMLDivElement>(null);
  const rows = treeRows(groups, open);
  const tabbable = rows.some((each) => each.id === focus) ? focus : rows.some((each) => each.id === selected) ? selected : rows[0]?.id ?? null;

  const toggle = (id: string) => setOpen((now) => { const next = new Set(now); if (!next.delete(id)) next.add(id); return next; });
  const press = (row: TreeRow) => (row.kind === 'group' ? toggle(row.id) : onPick(row.id));
  const reach = (id: string) => {
    setFocus(id);
    tree.current?.querySelector<HTMLElement>(`[data-tree-id="${CSS.escape(id)}"]`)?.focus();
  };
  const onKey = (event: KeyboardEvent<HTMLDivElement>) => {
    if (event.altKey || event.ctrlKey || event.metaKey) return;
    const at = (event.target as HTMLElement).closest<HTMLElement>('[role="treeitem"]');
    if (!at || at.parentElement !== event.currentTarget) return;
    const id = at.dataset.treeId ?? '';
    const move = keyInTree(rows, id, event, selected);
    if (!move) return;
    event.preventDefault();
    if (move.focus) reach(move.focus);
    if (move.toggle) toggle(move.toggle);
    if (move.choose) onPick(move.choose);
    if (move.release) onRelease();
  };

  return (
    <div
      ref={tree}
      role="tree"
      aria-label={label}
      data-canvas-list=""
      onKeyDown={onKey}
      onFocus={(event) => { const id = (event.target as HTMLElement).dataset.treeId; if (id) setFocus(id); }}
      className="min-w-0 rounded-control outline -outline-offset-1 outline-line"
    >
      {rows.map((row) => (
        <Fragment key={row.id}>
          <Row row={row} chosen={row.kind === 'item' && row.id === selected} focused={tabbable === row.id} onPress={() => press(row)} />
          {detail !== undefined && row.kind === 'item' && row.id === selected && (
            <div role="none" data-tree-detail="" className="border-l-[3px] border-l-accent bg-page px-3 py-2">{detail}</div>
          )}
        </Fragment>
      ))}
    </div>
  );
}
