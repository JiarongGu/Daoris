import * as Menu from '@radix-ui/react-dropdown-menu';
import { useTranslation } from 'react-i18next';
import { cn } from '../lib/cn';
import { Button, Icon, Tip } from '../ui';
import type { BrowserDriver } from './browserDrivers';

/**
 * Daoris's browser's door on the app strip (BRW7), and who is driving it (BRW8): one press from every
 * view, where View → *Browser* and the palette were the only ways in, and, beside it, whose hands are on
 * the page before the person types into it.
 *
 * @remarks
 * **On the strip, not the activity bar.** The bar is the one navigation (D66), a place per icon: every
 * item there changes what this window's centre shows and wears the current-place mark, and its foot
 * holds actions on this window. The browser is neither — it is another window, and opening it changes
 * no place. The strip is where the application's acts are, the View menu that already opens the
 * browser among them, and its right end holds what changes the work's surroundings rather than the
 * view: the region toggles, beside which this sits.
 *
 * **Who is driving is said here, in Daoris** (BRW8): neither the engine's window nor Edge's is Daoris's
 * to draw in, and an agent's current tab is the first page it found, not the one in front. So the strip
 * names the session in words — never a mark alone (D41 §6) — and is a door to it: one session is a
 * chip that opens it, two or more a count whose menu lists each. Nothing is said while nobody drives.
 *
 * **A molecule**: the presses go out, and the shell opens whichever browser the machine uses, on its
 * own start page, or brings it forward. A browser is given no door, since it has no window to open.
 */
export function BrowserDoor({ onOpen, drivers = [], onAttend }: {
  onOpen: () => void;
  /** The sessions driving it, already named (`browserDrivers`); none says nothing. */
  drivers?: readonly BrowserDriver[];
  /** Open a session in Sessions. Absent leaves the names as words, since a door must open something. */
  onAttend?: (session: string) => void;
}) {
  const { t } = useTranslation();
  const label = t('browser.door.open');

  return (
    <div className="flex min-w-0 items-center gap-1">
      <Tip content={label}>
        <Button variant="ghost" aria-label={label} onClick={onOpen} className="h-7 w-7 justify-center px-0">
          <Icon name="browser" size={15} />
        </Button>
      </Tip>
      {drivers.length > 0 && <Driving drivers={drivers} onAttend={onAttend} />}
    </div>
  );
}

/** A chip's frame: a bordered token a size down from the strip's text, and a box to press where it opens something. */
const CHIP = cn(
  'flex h-6 min-w-0 max-w-64 items-center gap-1.5 rounded-control border border-line px-2 text-meta text-ink-soft',
);
const PRESSABLE = cn(
  'cursor-pointer bg-transparent transition-colors duration-(--speed)',
  'hover:bg-accent-soft hover:text-ink focus-visible:bg-accent-soft focus-visible:outline-none',
  'focus-visible:ring-1 focus-visible:ring-accent',
);

function Driving({ drivers, onAttend }: { drivers: readonly BrowserDriver[]; onAttend?: (session: string) => void }) {
  const { t } = useTranslation();
  const tip = t('browser.driving.tip');
  const face = (text: string) => (
    <>
      <Icon name="frameWork" size={11} className="shrink-0" />
      <span className="min-w-0 truncate">{text}</span>
    </>
  );

  if (drivers.length === 1) {
    const [only] = drivers as [BrowserDriver];
    const text = t('browser.driving.one', { name: only.name });
    return (
      <Tip content={`${text} — ${tip}`}>
        {onAttend
          ? (
            <button
              type="button"
              aria-label={t('browser.driving.oneLabel', { name: only.name })}
              onClick={() => onAttend(only.id)}
              className={cn(CHIP, PRESSABLE)}
            >
              {face(text)}
            </button>
          )
          : <span className={CHIP}>{face(text)}</span>}
      </Tip>
    );
  }

  const count = drivers.length;
  const text = t('browser.driving.several', { count });
  if (!onAttend) return <Tip content={`${text} — ${tip}`}><span className={CHIP}>{face(text)}</span></Tip>;

  return (
    // `modal={false}`: a menu on the strip has no business making the page inert (AppMenu's rule).
    <Menu.Root modal={false}>
      <Tip content={`${text} — ${tip}`}>
        <Menu.Trigger asChild>
          <button type="button" aria-label={t('browser.driving.severalLabel', { count })} className={cn(CHIP, PRESSABLE)}>
            {face(text)}
          </button>
        </Menu.Trigger>
      </Tip>
      <Menu.Portal>
        <Menu.Content
          align="end"
          sideOffset={4}
          collisionPadding={8}
          className="z-30 w-72 max-w-[calc(100vw-2rem)] rounded-control border border-line bg-overlay p-1 text-small shadow-lg"
        >
          <Menu.Label className="px-2 py-1 text-meta text-ink-faint">{t('browser.driving.menu')}</Menu.Label>
          {drivers.map((driver) => (
            <Menu.Item key={driver.id} onSelect={() => onAttend(driver.id)} className={ROW}>
              <Icon name="frameWork" size={12} className="shrink-0" aria-hidden />
              <span className="min-w-0 truncate">{driver.name}</span>
            </Menu.Item>
          ))}
        </Menu.Content>
      </Menu.Portal>
    </Menu.Root>
  );
}

/** One row of the menu — AppMenu's row, so the menus in this window read as one kind of thing. */
const ROW = cn(
  'flex cursor-pointer items-center gap-2 rounded-[4px] px-2 py-1.5 text-small text-ink-soft outline-none',
  'data-[highlighted]:bg-raised data-[highlighted]:text-ink',
);
