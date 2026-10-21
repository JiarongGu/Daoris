import { useTranslation } from 'react-i18next';
import { Button, CountBadge, Icon, Tip } from '../design/ui';

/**
 * The title bar's *Notifications* (NOTIFY2b, D196 point 3, the notification design §2.1): a bell on the strip's trailing group,
 * before the browser's door, counting what needs the person (each press of either source once, and every other row of *What needs
 * you*) in open's hue, the badge Overview's place wore before it.
 *
 * @remarks
 * **While any press waits unshown it wears the warning hue with the presses' own count** (§3.3, Fable's F1.1): a toast closes and
 * a count looks like any other row's, so this is what says a press waits for as long as it waits to be seen, in the window in front;
 * it goes back to open's hue and the whole count once every press waiting is shown or has ended. Its name for a reader says which.
 *
 * **It stays on a narrow window** (UX7a): the region toggles leave the strip first, and this is how anything is found. Pressed, it
 * shows the view where it stands, or gives its region back (`WorkFrame`); never the focus moved or the view opened by itself.
 *
 * A molecule: the counts come in and the press goes out.
 */
export function NotificationsBell({ needs, presses, warn, onPress }: {
  /** What needs the person in all: each press once, and every other row of *What needs you*. */
  needs: number;
  /** How many presses wait, of either source. */
  presses: number;
  /** Whether any press waits unshown: the warning hue, with the presses' count. */
  warn: boolean;
  onPress: () => void;
}) {
  const { t } = useTranslation();
  const count = warn ? presses : needs;
  const label = count > 0
    ? t('notifications.labelled', { what: t(warn ? 'notifications.tipConfirm' : 'notifications.tip', { count }) })
    : t('notifications.title');
  return (
    <Tip content={label}>
      <Button variant="ghost" aria-label={label} onClick={onPress} className="relative h-7 w-7 justify-center px-0" data-bell="">
        <Icon name="bell" size={15} className={warn ? 'text-warn' : undefined} />
        <CountBadge count={count} tone={warn ? 'warn' : 'open'} />
      </Button>
    </Tip>
  );
}
