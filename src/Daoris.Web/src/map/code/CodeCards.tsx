import { useTranslation } from 'react-i18next';
import { cn } from '../../lib/cn';
import { CardLabel } from '../../design/canvas/CardLabel';
import type { NodeState } from '../../design/canvas/SuppliedCanvas';

/** Characters of an id a card can hold at the meta size before it would run past its edge. */
export const FITS = 17;

/** An id cut to what a card holds, with an ellipsis; whole in the detail and in the name a reader hears. */
export const cutId = (id: string) => (id.length > FITS ? `${id.slice(0, FITS - 1)}…` : id);

/** An id's last segment, which is what a chip says of a module: the id, if it has no separator. */
export const lastSegment = (id: string) => cutId(id.split(/[/\\.:]/).filter(Boolean).pop() ?? id);

/** A closed folder or a cycle: what its card says is named once, by the caller, in the viewer's language. */
export type GroupCard = { kind: 'folder'; name: string; count: number } | { kind: 'cycle'; title: string; count: number };

/**
 * A module, a closed folder or a cycle, each tier its own (MAPUX1a; the map UX design §3): at the full tier a module's id cut at `FITS`,
 * a folder's name and ×n, a cycle's title with the folders it crosses; at the chip tier the id's last segment, a folder's name and ×n;
 * at the dot tier nothing inside. The chosen and the focused keep their name beside the card at every tier but the full.
 */
export function CodeCard({ id, group, state, back = false }: { id: string; group?: GroupCard; state: NodeState; back?: boolean }) {
  const { t } = useTranslation();
  const { tier } = state;
  if (group) {
    const name = group.kind === 'folder' ? group.name : group.title;
    return (
      <span
        className={cn(
          'relative flex h-full w-full items-center justify-between gap-2 rounded-card border-[1.5px] border-dashed bg-raised px-2.5',
          state.selected ? 'border-accent' : 'border-line-strong', back && 'opacity-40',
        )}
      >
        {tier !== 'dot' && (
          <>
            <span className="min-w-0 truncate text-small font-semibold text-ink">{name}</span>
            {(group.kind === 'folder' || tier === 'chip') && (
              <span className="shrink-0 font-mono text-meta tabular-nums text-ink-soft">{t('code.times', { count: group.count })}</span>
            )}
          </>
        )}
        <CardLabel state={state}>{name}</CardLabel>
      </span>
    );
  }
  return (
    <span
      className={cn(
        'relative flex h-full w-full items-center justify-center rounded-card border bg-raised px-2',
        state.selected ? 'border-[3px] border-accent' : 'border-[1.5px] border-line-strong', back && 'opacity-40',
      )}
    >
      {tier !== 'dot' && <span className="truncate font-mono text-meta text-ink">{tier === 'full' ? cutId(id) : lastSegment(id)}</span>}
      <CardLabel state={state}>{id}</CardLabel>
    </span>
  );
}
