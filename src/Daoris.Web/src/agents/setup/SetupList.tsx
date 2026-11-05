import type { TFunction } from 'i18next';
import { type KeyboardEvent, type ReactNode, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { PlaceNeed, PlaceRow, PlacesRead } from '../../data/wiring';
import { Button, Icon } from '../../design/ui';
import { cn } from '../../lib/cn';
import { isComposing } from '../../lib/composing';
import { LIST_ROW } from '../../work/frame/listKeys';
import { MACHINE, repositoryId, workspaceId } from '../modes';
import { landsSaid } from './placeShell';
import { keyInTree, placeIdOf, type SetupState, type TreeItem, treeItems } from './setupTreeModel';

const firstReason = (needs: readonly PlaceNeed[], t: TFunction): string | null => {
  const first = needs[0];
  return first ? `⚠ ${t(`agents.setup.need.${first.code}`, { defaultValue: first.code })}` : null;
};

/**
 * A driven repository's second line (SETUPUX1i): that its start is held, that its adoption runs, or how it lands with *n of its own*.
 * A need comes first as ⚠ and its first reason.
 */
export function placeLine(row: PlaceRow, t: TFunction): { text: string | null; need: boolean } {
  const need = firstReason(row.needs, t);
  if (need) return { text: need, need: true };
  if (row.held) return { text: t('agents.setup.tree.held'), need: false };
  if (!row.driven && row.adoption === 'running') return { text: t('agents.setup.tree.adoption'), need: false };
  return { text: row.values?.lands ? landsSaid(t, row.values.lands) : null, need: false };
}

/** The places in hand that need a look: the chip's count, from the read the tree shows. */
export const needsCount = (read: PlacesRead): number =>
  [read.machine, ...read.workspaces.map((each) => each.place), ...read.repositories].filter((row) => row.needs.length > 0).length;

type RowProps = {
  item: TreeItem;
  chosen: boolean;
  focused: boolean;
  onPress: () => void;
  onToggle?: () => void;
  children: ReactNode;
};

/** One row of the tree: the whole row chooses it, and its chevron, for the pointer, opens or closes it; the arrow keys do the same. */
function TreeRow({ item, chosen, focused, onPress, onToggle, children }: RowProps) {
  return (
    <div
      role="treeitem"
      data-tree-id={item.id}
      aria-level={item.level}
      aria-selected={item.kind === 'fold' ? undefined : chosen}
      aria-current={chosen || undefined}
      aria-expanded={item.expandable ? item.expanded : undefined}
      tabIndex={focused ? 0 : -1}
      onClick={onPress}
      className={cn(
        'flex w-full min-w-0 cursor-pointer items-start gap-1 border-l-[3px] py-1.5 pr-2.5 outline-none transition-colors duration-(--speed)',
        item.level === 1 ? 'pl-1.5' : item.level === 2 ? 'pl-5' : 'pl-9',
        'hover:bg-accent-soft/50 focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-accent',
        chosen ? 'border-l-accent bg-accent-soft' : 'border-l-transparent',
      )}
    >
      <span
        aria-hidden="true"
        onClick={onToggle ? (event) => { event.stopPropagation(); onToggle(); } : undefined}
        className="mt-0.5 flex size-4 shrink-0 items-center justify-center text-ink-faint"
      >
        {item.expandable && <Icon name={item.expanded ? 'chevronDown' : 'chevronRight'} size={12} />}
      </span>
      <span className="min-w-0 flex-1">{children}</span>
    </div>
  );
}

/**
 * **The places tree** (AGENTSPLACE1e, SETUPUX1i; the setup-UX design §2.1): *This machine*, then each workspace with its driven
 * repositories, and the rest of a workspace's imported ones folded into one row that opens a paged read. Each row has a second line, and
 * a need adds ⚠ and its first reason. The keys are the WAI tree pattern's (`keyInTree`); one row is in the tab order and the arrows move
 * it. Choosing a row and opening a workspace are separate acts.
 *
 * A molecule: the driver's answer arrives, and each press goes out.
 */
function PlacesTree({ state, chosen, onChoose }: { state: SetupState; chosen: string | null; onChoose: (id: string) => void }) {
  const { t } = useTranslation();
  const read = state.read!;
  const [collapsed, setCollapsed] = useState<ReadonlySet<string>>(new Set());
  const [focus, setFocus] = useState<string | null>(null);
  const tree = useRef<HTMLDivElement>(null);
  const items = treeItems(read, collapsed, state.fold);
  const drivenOf = new Map(read.workspaces.map((each) => [workspaceId(each.place.workspace ?? ''), each]));
  const rowOf = new Map<string, PlaceRow>([[MACHINE, read.machine], ...read.repositories.map((row): [string, PlaceRow] => [placeIdOf(row), row])]);
  const tabbable = items.some((each) => each.id === focus) ? focus : items.some((each) => each.id === chosen) ? chosen : items[0]?.id ?? null;

  const toggle = (item: TreeItem) => {
    if (item.kind === 'fold') {
      state.onFold(item.expanded ? null : item.id.slice('fold:'.length));
      return;
    }
    setCollapsed((now) => {
      const next = new Set(now);
      if (next.has(item.id)) next.delete(item.id); else next.add(item.id);
      return next;
    });
    const workspace = item.id.slice('workspace:'.length);
    if (item.expanded && state.fold === workspace) state.onFold(null);
  };
  const press = (item: TreeItem) => (item.kind === 'fold' ? toggle(item) : onChoose(item.id));
  const reach = (id: string) => {
    setFocus(id);
    tree.current?.querySelector<HTMLElement>(`[data-tree-id="${CSS.escape(id)}"]`)?.focus();
  };
  const onKey = (event: KeyboardEvent<HTMLDivElement>) => {
    if (isComposing(event) || event.altKey || event.ctrlKey || event.metaKey) return;
    const at = (event.target as HTMLElement).closest<HTMLElement>('[role="treeitem"]');
    if (!at || at.parentElement !== event.currentTarget) return;
    const id = at.dataset.treeId ?? '';
    const move = keyInTree(items, id, event.key);
    if (!move) return;
    event.preventDefault();
    const item = items.find((each) => each.id === id);
    if (move.focus) reach(move.focus);
    if (move.toggle && item) toggle(item);
    if (move.choose && item) press(item);
  };

  return (
    <div
      ref={tree}
      role="tree"
      aria-label={t('agents.setup.places')}
      {...{ [LIST_ROW]: '' }}
      onKeyDown={onKey}
      onFocus={(event) => { const id = (event.target as HTMLElement).dataset.treeId; if (id) setFocus(id); }}
    >
      {items.map((item) => {
        const common = { item, focused: tabbable === item.id, chosen: chosen === item.id };
        if (item.kind === 'machine') {
          const line = firstReason(read.machine.needs, t);
          return (
            <TreeRow key={item.id} {...common} onPress={() => press(item)}>
              <span className="block truncate text-body font-medium text-ink">{t('agents.setup.machine')}</span>
              {line && <span className="block truncate text-small text-warn">{line}</span>}
            </TreeRow>
          );
        }
        if (item.kind === 'workspace') {
          const each = drivenOf.get(item.id)!;
          const line = firstReason(each.place.needs, t);
          return (
            <TreeRow key={item.id} {...common} onPress={() => press(item)} onToggle={item.expandable ? () => toggle(item) : undefined}>
              <span className="block truncate text-body font-medium text-ink">{each.place.workspace}</span>
              <span className="block truncate text-small text-ink-soft">{t('agents.setup.drivenCount', { count: each.driven })}</span>
              {line && <span className="block truncate text-small text-warn">{line}</span>}
            </TreeRow>
          );
        }
        if (item.kind === 'fold') {
          const workspace = item.id.slice('fold:'.length);
          const notDriven = read.workspaces.find((each) => each.place.workspace === workspace)?.notDriven ?? 0;
          return (
            <TreeRow key={item.id} {...common} chosen={false} onPress={() => press(item)} onToggle={() => toggle(item)}>
              <span className="block truncate text-small text-ink-soft">{t('agents.setup.imported', { count: notDriven })}</span>
            </TreeRow>
          );
        }
        if (item.kind === 'imported') {
          const name = read.undriven?.rows.find((row) => repositoryId(row.workspace, row.repository) === item.id)?.repository;
          return (
            <TreeRow key={item.id} {...common} onPress={() => press(item)}>
              <span className="block truncate text-body text-ink-soft">{name}</span>
            </TreeRow>
          );
        }
        const row = rowOf.get(item.id)!;
        const line = placeLine(row, t);
        return (
          <TreeRow key={item.id} {...common} onPress={() => press(item)}>
            <span className="block truncate text-body text-ink">{row.repository}</span>
            {line.text && <span className={cn('block truncate text-small', line.need ? 'text-warn' : 'text-ink-soft')}>{line.text}</span>}
          </TreeRow>
        );
      })}
      {state.fold && read.undriven && read.undriven.workspace === state.fold && !collapsed.has(workspaceId(state.fold)) && (
        <div role="none" className="flex items-center gap-1.5 py-1 pl-9 pr-2.5">
          <span className="min-w-0 flex-1 truncate text-small text-ink-faint">
            {t('agents.setup.page', { shown: read.undriven.rows.length, total: read.undriven.total })}
          </span>
          {state.foldAfter && <Button variant="ghost" onClick={() => state.onFoldAfter(null)}>{t('agents.setup.first')}</Button>}
          {read.undriven.next && <Button variant="ghost" onClick={() => state.onFoldAfter(read.undriven!.next ?? null)}>{t('agents.setup.next')}</Button>}
        </div>
      )}
    </div>
  );
}

/**
 * **The Setup mode's list** (AGENTSPLACE1e, the Agents-place design §3): a search, the *Needs attention* chip with its count, and the
 * places tree. A search and the chip narrow the driven ones; the driver judges the reasons from its configuration alone.
 *
 * A molecule: the driver's answer arrives, and each press goes out.
 */
export function SetupList({ state, chosen, onChoose }: { state: SetupState; chosen: string | null; onChoose: (id: string) => void }) {
  const { t } = useTranslation();
  const { read } = state;
  const line = 'm-0 px-3 py-1.5 text-small text-ink-faint';
  return (
    <div>
      <div className="sticky top-0 z-10 grid gap-1.5 border-b border-line bg-page px-2 pb-2 pt-1.5">
        <div className="flex items-center gap-1.5 rounded-control border border-line-strong bg-raised py-0.5 pl-2 pr-0.5 text-ink-faint focus-within:border-accent">
          <Icon name="search" size={12} className="shrink-0" />
          <input
            type="search"
            value={state.search}
            aria-label={t('agents.setup.search')}
            placeholder={t('agents.setup.search')}
            onChange={(event) => state.onSearch(event.target.value)}
            className="min-h-6 min-w-0 flex-1 bg-transparent text-small text-ink outline-none placeholder:text-ink-faint"
          />
        </div>
        <div>
          <button
            type="button"
            aria-pressed={state.needsLook}
            onClick={() => state.onNeedsLook(!state.needsLook)}
            className={cn(
              'inline-flex items-center gap-1.5 rounded-full border px-2.5 py-0.5 text-small transition-colors duration-(--speed)',
              state.needsLook ? 'border-accent bg-accent-soft text-ink' : 'border-line-strong text-ink-soft hover:bg-accent-soft/50',
            )}
          >
            <span>{t('agents.setup.needsLook')}</span>
            {read && <span className="tabular-nums text-warn">{needsCount(read)}</span>}
          </button>
        </div>
      </div>
      {state.status === 'loading' && <p className={line}>{t('agents.setup.loading')}</p>}
      {state.status === 'failed' && <p className={line}>{t('agents.setup.failed')}</p>}
      {read && <PlacesTree state={state} chosen={chosen} onChoose={onChoose} />}
      {read && read.repositories.length === 0 && (state.search || state.needsLook) && <p className={line}>{t('agents.setup.nothing')}</p>}
      {read?.next && (
        <div className="px-2 py-1.5">
          <Button variant="ghost" onClick={state.onMore}>{t('agents.setup.more')}</Button>
        </div>
      )}
    </div>
  );
}
