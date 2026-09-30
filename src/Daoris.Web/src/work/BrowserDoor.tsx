import { useTranslation } from 'react-i18next';
import { Button, Icon, Tip } from '../ui';

/**
 * Daoris's browser's door on the app strip (BRW7): one press from every view, where View → *Browser*
 * and the palette were the only ways in.
 *
 * @remarks
 * **On the strip, not the activity bar.** The bar is the one navigation (D66), a place per icon: every
 * item there changes what this window's centre shows and wears the current-place mark, and its foot
 * holds actions on this window. The browser is neither — it is another window, and opening it changes
 * no place. The strip is where the application's acts are, the View menu that already opens the
 * browser among them, and its right end holds what changes the work's surroundings rather than the
 * view: the region toggles, beside which this sits.
 *
 * **A molecule**: the press goes out, and the shell opens whichever browser the machine uses, on its own
 * start page, or brings it forward. A browser is given no door, since it has no window to open.
 */
export function BrowserDoor({ onOpen }: { onOpen: () => void }) {
  const { t } = useTranslation();
  const label = t('browser.door.open');

  return (
    <Tip content={label}>
      <Button variant="ghost" aria-label={label} onClick={onOpen} className="h-7 w-7 justify-center px-0">
        <Icon name="browser" size={15} />
      </Button>
    </Tip>
  );
}
