import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { cn } from '../../lib/cn';
import { Menu, Tip } from '../ui';
import type { ZoomBounds } from './viewMath';

/** The sizes the sizing menu offers by name, as a map tool's zoom menu does. */
const PRESETS = [0.5, 1, 2] as const;

/** A way to change the size: an act, its name and its key, its tick column kept empty so it lines up with the sizes. */
function SizeItem({ label, keys, disabled, onSelect }: { label: string; keys?: string; disabled?: boolean; onSelect: () => void }) {
  return (
    <Menu.Item tick={false} disabled={disabled} onSelect={onSelect}>
      <span className="flex-1 truncate">{label}</span>
      {keys && <kbd className="font-mono text-meta text-ink-faint">{keys}</kbd>}
    </Menu.Item>
  );
}

export type SizingActs = {
  zoomIn: () => void;
  zoomOut: () => void;
  fit: () => void;
  toSelection: (() => void) | null;
  actual: () => void;
  zoomTo: (k: number) => void;
  /** The minimap's switch, where the world is large enough to offer one (`minimap.ts`). */
  minimap?: { on: boolean; toggle: () => void } | null;
};

/**
 * **The canvas's toolbar** (CANVAS1a; the canvas design §2.1): above the viewport, never over the drawing, as the map's. The feature's own
 * tools, then the size, which opens the sizing menu: zoom in and out, the feature's fit, to the selection and 100% with their keys, and
 * the sizes by name within the feature's bounds. It never scales with the drawing.
 *
 * A molecule: every press goes out.
 */
export function CanvasToolbar({ tools, k, bounds, fit, acts }: {
  tools: ReactNode;
  k: number;
  bounds: ZoomBounds;
  /** The feature's words for showing the whole. */
  fit: string;
  acts: SizingActs;
}) {
  const { t } = useTranslation();
  const percent = Math.round(k * 100);
  return (
    <div className="mb-2 flex flex-wrap items-center justify-end gap-x-1 gap-y-1">
      {tools}
      <Menu.Root>
        <Tip content={t('canvas.sizing')}>
          <Menu.Trigger asChild>
            <button
              type="button"
              aria-label={t('canvas.size', { percent })}
              className={cn(
                'h-7 min-w-12 shrink-0 rounded-control px-2 font-mono text-meta tabular-nums text-ink-soft',
                'transition-colors duration-(--speed) hover:bg-raised hover:text-ink data-[state=open]:bg-raised data-[state=open]:text-ink',
                'focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-accent',
              )}
            >
              {percent}%
            </button>
          </Menu.Trigger>
        </Tip>
        <Menu.Content align="end" className="min-w-56">
          <SizeItem label={t('canvas.zoomIn')} onSelect={acts.zoomIn} />
          <SizeItem label={t('canvas.zoomOut')} onSelect={acts.zoomOut} />
          <Menu.Separator />
          <SizeItem label={fit} keys="Shift+1" onSelect={acts.fit} />
          <SizeItem label={t('canvas.toSelection')} keys="Shift+2" disabled={acts.toSelection === null} onSelect={() => acts.toSelection?.()} />
          <SizeItem label={t('canvas.actual')} keys="Shift+0" onSelect={acts.actual} />
          <Menu.RadioGroup aria-label={t('canvas.sizes')} value={String(percent)}>
            {PRESETS.filter((scale) => scale >= bounds.min && scale <= bounds.max).map((scale) => (
              <Menu.RadioItem key={scale} value={String(scale * 100)} onSelect={() => acts.zoomTo(scale)}>
                <span className="flex-1 truncate">{`${scale * 100}%`}</span>
              </Menu.RadioItem>
            ))}
          </Menu.RadioGroup>
          {acts.minimap && (
            <>
              <Menu.Separator />
              <Menu.CheckboxItem checked={acts.minimap.on} onCheckedChange={acts.minimap.toggle}>
                <span className="flex-1 truncate">{t('canvas.minimap')}</span>
              </Menu.CheckboxItem>
            </>
          )}
        </Menu.Content>
      </Menu.Root>
    </div>
  );
}
