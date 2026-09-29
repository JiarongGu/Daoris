import { useTranslation } from 'react-i18next';
import { Button, Icon, type IconName, Tip } from '../ui';

/** A region the strip toggles: the session rail, the output panel, the right side bar. */
export type LayoutRegion = 'rail' | 'panel' | 'right';

/** Each region's picture and key — VS Code's, so a hand that knows one knows the other. */
export const LAYOUT_KEYS: Record<LayoutRegion, { icon: IconName; keys: string }> = {
  rail: { icon: 'layoutRail', keys: 'Ctrl+B' },
  panel: { icon: 'layoutPanel', keys: 'Ctrl+J' },
  right: { icon: 'layoutRight', keys: 'Ctrl+Alt+B' },
};

/**
 * The region toggles (DOCK1c, SURF11): on the strip's right, beside the window controls, as VS Code's
 * layout toggles sit in its title bar — and in the View menu, with the same keys.
 *
 * @remarks
 * **Pressed is shown.** A toggle's state is the region's, so a person can read the layout off the strip.
 * Only the regions the view has are offered: the rail and the panel are Sessions', and a button that
 * could only do nothing is worse than none. A molecule: the state arrives, a press goes out.
 */
export function LayoutToggles({ regions, closed, names = {}, onToggle }: {
  regions: readonly LayoutRegion[];
  closed: Record<LayoutRegion, boolean>;
  /** What a region is called where it holds one thing: away from Sessions the right side bar is Ask Daoris. */
  names?: Partial<Record<LayoutRegion, string>>;
  onToggle: (region: LayoutRegion) => void;
}) {
  const { t } = useTranslation();

  return (
    <div className="flex items-center">
      {regions.map((region) => {
        const label = t('layout.toggle', { region: names[region] ?? t(`layout.${region}`), keys: LAYOUT_KEYS[region].keys });
        return (
          <Tip key={region} content={label}>
            <Button
              variant="ghost"
              aria-label={label}
              aria-pressed={!closed[region]}
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
