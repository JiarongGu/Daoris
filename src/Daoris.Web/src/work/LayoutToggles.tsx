import { useTranslation } from 'react-i18next';
import { Button, Icon, type IconName, Tip } from '../ui';
import { LIST_DOOR } from './listKeys';

/** A region the strip toggles: the view's list, the output panel, the right side bar. */
export type LayoutRegion = 'list' | 'panel' | 'right';

/** Each region's picture and key — VS Code's, so a hand that knows one knows the other. */
export const LAYOUT_KEYS: Record<LayoutRegion, { icon: IconName; keys: string }> = {
  list: { icon: 'layoutRail', keys: 'Ctrl+B' },
  panel: { icon: 'layoutPanel', keys: 'Ctrl+J' },
  right: { icon: 'layoutRight', keys: 'Ctrl+Alt+B' },
};

/**
 * The region toggles (DOCK1c, SURF11): on the strip's right, beside the window controls, as VS Code's
 * layout toggles sit in its title bar — and in the View menu, with the same keys.
 *
 * @remarks
 * **Pressed is shown.** A toggle's state is the region's, so a person can read the layout off the strip.
 * The panel and the side bar are the frame's, on every view (DOCK1a); the list is the view's own and is
 * named for the view (D118 §3a), so the view hands its name in, and a view with no list hands none and
 * has no list toggle: a button that could only do nothing is worse than none. A molecule: the state
 * arrives, a press goes out.
 */
export function LayoutToggles({ regions, list, closed, onToggle }: {
  regions: readonly LayoutRegion[];
  /** The view's list, as its toggle names it (*the session list*); absent where the view has no list. */
  list?: string;
  closed: Record<LayoutRegion, boolean>;
  onToggle: (region: LayoutRegion) => void;
}) {
  const { t } = useTranslation();
  const shown = regions.filter((region) => region !== 'list' || list !== undefined);

  return (
    <div className="flex items-center">
      {shown.map((region) => {
        const label = t('layout.toggle', { region: region === 'list' ? list : t(`layout.${region}`), keys: LAYOUT_KEYS[region].keys });
        return (
          <Tip key={region} content={label}>
            <Button
              variant="ghost"
              aria-label={label}
              aria-pressed={!closed[region]}
              {...(region === 'list' ? { [LIST_DOOR]: '' } : {})}
              onClick={() => onToggle(region)}
              className="h-7 w-7 justify-center px-0"
            >
              <Icon name={LAYOUT_KEYS[region].icon} size={15} className={closed[region] ? 'text-ink-faint' : 'text-ink'} />
            </Button>
          </Tip>
        );
      })}
    </div>
  );
}
